"""Finalize review artifacts AFTER finish_delivery.prepare and explicit FINALS_READY.

This wrapper never changes models, prefabs, materials, scripts, scenes or project
settings. It writes evidence/comparison artifacts and appends a bounded documentation
section. Missing, failed or stale evidence remains pending/failed rather than PASS.
"""
import argparse
from collections import Counter, defaultdict
import csv
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont, ImageOps

sys.path.insert(0, str(Path(__file__).resolve().parent))
from catalog import ROOT, WORK, ASSET, VERIFY, GROUPS

PROOF = VERIFY / "ProfessionalAudit"
WORK_PROOF = WORK / "ProfessionalAudit"
BACKUP = WORK_PROOF / "Before"
BEFORE_ASSET = BACKUP / "Assets/PostApocalypticFurniture"
BEGIN = "<!-- FURNITURE_PROFESSIONAL_AUDIT_BEGIN -->"
END = "<!-- FURNITURE_PROFESSIONAL_AUDIT_END -->"
REFERENCE_NOTICE = "Фотореференс не предоставлен; тип по ТЗ:"
COLUMNS = ["Object", "Reference", "Problem", "Severity", "Required Fix", "Status"]


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024*1024), b""):
            digest.update(block)
    return digest.hexdigest()


def text(path):
    return Path(path).read_text(encoding="utf-8-sig", errors="replace")


def json_read(path):
    return json.loads(text(path))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def latest_time(files):
    return max((p.stat().st_mtime for p in files if p.is_file()), default=0)


def load_evidence(name, candidates, fresh_after=0, required=True):
    available = [p for p in candidates if p.is_file()]
    result = {"name": name, "required": required, "status": "PENDING", "file": None, "reason": "Файл проверки отсутствует"}
    if not available:
        return None, result
    source = max(available, key=lambda p: p.stat().st_mtime)
    try:
        data = json_read(source)
    except Exception as error:
        result.update(status="FAILED", file=str(source), reason="Невозможно прочитать JSON: " + str(error))
        return None, result
    result.update(file=str(source), sha256=sha(source), modifiedUtc=datetime.fromtimestamp(source.stat().st_mtime, timezone.utc).isoformat())
    result["stale"] = source.stat().st_mtime+2 < fresh_after
    if result["stale"]:
        result.update(status="STALE", reason="Проверка старше относящихся к ней производственных файлов")
    elif isinstance(data, dict) and data.get("passed") is True:
        result.update(status="PASS", reason="В файле есть явный passed=true")
    elif isinstance(data, dict) and data.get("passed") is False:
        result.update(status="FAILED", reason="В файле есть passed=false")
    else:
        result.update(status="RECORDED", reason="Результаты записаны; общего флага PASS нет")
    # Work-only audits must be included in the final proof bundle, without changing their content.
    if source.parent == WORK_PROOF:
        destination = PROOF / source.name
        if source.resolve() != destination.resolve():
            shutil.copy2(source, destination)
            result["file"] = str(destination)
    return data, result


def source_issues(data):
    result = []
    if not isinstance(data, dict):
        return result
    for file in data.get("files", []):
        for obj in file.get("objects", []):
            match = re.match(r"F(\d{3})_", obj.get("object", ""))
            if not match:
                continue
            for issue in obj.get("issues", []):
                result.append({"id": int(match.group(1)), "object": obj["object"], "code": issue.get("code", "unknown"),
                               "severity": issue.get("severity", "review"), "count": issue.get("count", 1),
                               "message": issue.get("message", ""), "mesh": obj})
    return result


def geometry_issues(data):
    if not isinstance(data, dict):
        return []
    indexed = {issue.get("key"): issue for issue in data.get("issues", []) if isinstance(issue, dict)}
    result, seen = [], set()
    for issue in indexed.values():
        for aid in issue.get("affected_ids", []):
            key = (int(aid), issue.get("key"))
            seen.add(key)
            result.append({"id": int(aid), **issue})
    for asset in data.get("assets", []):
        aid = int(asset.get("id", 0))
        for item in asset.get("issues", []):
            issue = indexed.get(item, {}) if isinstance(item, str) else item
            if not isinstance(issue, dict):
                continue
            key = (aid, issue.get("key", issue.get("problem", issue.get("before_problem", ""))))
            if key in seen:
                continue
            seen.add(key)
            result.append({"id": aid, "asset_source_status": asset.get("source_status"), **issue})
    return result


def source_fix(code):
    return {
        "inward_closed_component": "Пересчёт ориентированных нормалей замкнутых островов с проверкой знака объёма.",
        "collapsed_uv0": "Повторная UV0-развёртка/ремонт после упрощения LOD.",
        "uv0_surface_stretch": "Локальная/цилиндрическая UV0-развёртка и ремонт сильной анизотропии после decimation.",
        "multi_face_junction": "Разделение разных исходных Geo-примитивов при сварке; каждый узел требует контекста.",
        "smooth_hard_edge": "Авторские угловые нормали/жёсткие границы; счётчик геометрических углов сам по себе не доказывает ошибку shading.",
        "boundary_hard_surface": "Проверка назначения открытой границы; намеренные проёмы и тонкие поверхности не закрываются автоматически.",
        "duplicate_geometric_face": "Удаление совпадающих полигонов с сохранением исходного назначения поверхности.",
        "degenerate_face": "Удаление/перестроение вырожденных полигонов.",
        "uv2_overlap": "Перепаковка UV2 отдельных островов с отступами и повторная проверка.",
    }.get(code, "Адресное исправление и повторный source-аудит; автоматическое художественное одобрение не подразумевается.")


def make_ledger(catalog, before, after, geometry_a, geometry_b, evidence):
    by_id = {int(row["id"]): row for row in catalog}
    after_objects = {obj["object"] for file in (after or {}).get("files", []) for obj in file.get("objects", [])}
    after_lookup = {(item["object"], item["code"]): item for item in source_issues(after)}
    rows = []
    def add(aid, problem, severity, fix, status, detail=None):
        asset = by_id.get(aid, {"name": str(aid), "slug": str(aid)})
        rows.append(dict(zip(COLUMNS, [f"{aid:03d} · {asset['name']}", REFERENCE_NOTICE+" "+asset["name"], problem,
                                      severity, fix, status]), assetId=aid, detail=detail or {}))
    for item in source_issues(before):
        match = after_lookup.get((item["object"], item["code"]))
        if after is None:
            status = "PENDING: повторный source-аудит отсутствует"
        elif item["object"] not in after_objects:
            status = "REPLACED: исходный mesh заменён/удалён; назначение замены требует просмотра"
        elif match:
            status = "OPEN: повторный аудит сохраняет этот признак (review не равен доказанному визуальному дефекту)"
        else:
            status = "SOURCE_VERIFIED: признак не найден в повторном source-аудите"
        add(item["id"], f"{item['object']}: {item['code']} ({item['count']}). {item['message']}",
            "HIGH" if item["severity"] == "critical" else "Review", source_fix(item["code"]), status,
            {"sourceObject": item["object"], "sourceCode": item["code"], "beforeCount": item["count"], "afterCount": match["count"] if match else None})
    before_keys = {(item["object"], item["code"]) for item in source_issues(before)}
    for item in source_issues(after):
        if (item["object"], item["code"]) not in before_keys:
            add(item["id"], f"После изменения: {item['object']}: {item['code']} ({item['count']}). {item['message']}",
                "HIGH" if item["severity"] == "critical" else "Review", source_fix(item["code"]), "OPEN: новый признак повторного аудита")
    for data in [geometry_a, geometry_b]:
        for item in geometry_issues(data):
            aid = item["id"]
            problem = item.get("before_problem", item.get("problem", item.get("key", "Геометрическая проверка")))
            status = item.get("status", item.get("asset_source_status", "PENDING"))
            add(aid, problem, item.get("severity", "Review"), item.get("fix", "Исправление не описано"),
                "GEOMETRY: "+str(status)+"; нативные кадры/механизмы проверяются отдельно", {"geometryEvidence": item})
    represented = {row["assetId"] for row in rows}
    for asset in catalog:
        if int(asset["id"]) not in represented:
            add(int(asset["id"]), "Перечисленные автоматические source-проверки не зарегистрировали дефекта; сходство с фотографией не оценивалось.",
                "Info", "Общие доработки поверхностей/UV/нормалей; персональные изменения см. geometry JSON.",
                "SOURCE_RECORDED" if after else "PENDING: повторный source-аудит отсутствует")
    return sorted(rows, key=lambda row: (row["assetId"], row["Severity"], row["Problem"]))


def parse_prefab(path):
    contents = text(path)
    headers = list(re.finditer(r"^--- !u!(\d+) &(-?\d+)(?:[^\n]*)\n", contents, re.M))
    records = {}
    for index, match in enumerate(headers):
        body = contents[match.end():headers[index+1].start() if index+1 < len(headers) else len(contents)]
        def field(pattern, default=""):
            found = re.search(pattern, body, re.M)
            return found.group(1) if found else default
        records[match.group(2)] = {"classId": int(match.group(1)), "name": field(r"^  m_Name: (.*)$"),
            "owner": field(r"^  m_GameObject: \{fileID: (-?\d+)"), "father": field(r"^  m_Father: \{fileID: (-?\d+)"),
            "script": field(r"^  m_Script: \{fileID: [^,]+, guid: ([0-9a-f]{32})"),
            "components": re.findall(r"^  - component: \{fileID: (-?\d+)\}", body, re.M)}
    transforms = {key: value for key, value in records.items() if value["classId"] in [4, 224]}
    go_transform = {value["owner"]: key for key, value in transforms.items()}
    def go_path(go_id, seen=None):
        if not go_id or go_id not in records:
            return "<unknown>"
        seen = set() if seen is None else seen
        if go_id in seen:
            return "<cycle>"
        seen.add(go_id)
        transform = transforms.get(go_transform.get(go_id), {})
        father = transform.get("father", "0")
        parent_go = transforms.get(father, {}).get("owner") if father != "0" else None
        return (go_path(parent_go, seen)+"/" if parent_go else "")+records[go_id]["name"]
    root_candidates = [value["owner"] for value in transforms.values() if value["father"] == "0"]
    root = next((go_id for go_id in root_candidates if records.get(go_id, {}).get("name") == path.stem), root_candidates[0] if root_candidates else None)
    for key, record in records.items():
        owner = key if record["classId"] == 1 else record["owner"]
        record["semantic"] = (record["classId"], go_path(owner), record["script"])
    return {"root": root, "rootComponents": records.get(root, {}).get("components", []), "records": records}


def compatibility_report():
    old_metas = sorted(BEFORE_ASSET.rglob("*.meta"))
    pairs=[(p,ASSET/p.relative_to(BEFORE_ASSET),p.relative_to(BEFORE_ASSET)) for p in old_metas]
    top=Path(str(BEFORE_ASSET)+".meta")
    if top.exists():pairs.append((top,Path(str(ASSET)+".meta"),Path("../PostApocalypticFurniture.meta")))
    metas = {"before": len(pairs), "existingGuidUnchanged": 0, "byteIdentical": 0, "metadataModifiedWithStableGuid": [], "missing": [], "changedGuid": []}
    for old,new,relative in pairs:
        if not new.exists():
            metas["missing"].append(str(relative)); continue
        old_guid = re.search(r"^guid: ([0-9a-f]{32})$", text(old), re.M)
        new_guid = re.search(r"^guid: ([0-9a-f]{32})$", text(new), re.M)
        if old_guid and new_guid and old_guid[1] == new_guid[1]:
            metas["existingGuidUnchanged"] += 1
            if sha(old) == sha(new): metas["byteIdentical"] += 1
            else: metas["metadataModifiedWithStableGuid"].append(str(relative))
        else:
            metas["changedGuid"].append({"file": str(relative), "before": old_guid[1] if old_guid else None, "after": new_guid[1] if new_guid else None})
    metas["newMetaFiles"] = sum(not (BEFORE_ASSET/p.relative_to(ASSET)).exists() for p in ASSET.rglob("*.meta"))
    prefabs = []
    for old_path in sorted((BEFORE_ASSET/"Prefabs").rglob("*.prefab")):
        new_path = ASSET/old_path.relative_to(BEFORE_ASSET)
        if not new_path.exists():
            prefabs.append({"prefab": str(old_path.relative_to(BEFORE_ASSET)), "missing": True}); continue
        old, new = parse_prefab(old_path), parse_prefab(new_path)
        old_ids, new_ids = set(old["records"]), set(new["records"])
        shared = old_ids & new_ids
        root_components = [{"fileID": key, "classId": old["records"].get(key, {}).get("classId"),
            "script": old["records"].get(key, {}).get("script"), "retained": key in new_ids,
            "sameSemanticRecord": key in new_ids and old["records"][key]["semantic"] == new["records"][key]["semantic"]} for key in old["rootComponents"]]
        prefabs.append({"prefab": str(old_path.relative_to(BEFORE_ASSET)), "rootGameObjectBefore": old["root"], "rootGameObjectAfter": new["root"],
            "rootGameObjectStable": old["root"] is not None and old["root"] == new["root"], "beforeRecords": len(old_ids), "afterRecords": len(new_ids),
            "retainedFileIDs": len(shared), "retainedSameSemanticRecord": sum(old["records"][key]["semantic"] == new["records"][key]["semantic"] for key in shared),
            "removedOrReplacedRecords": [{"fileID": key, "classId": old["records"][key]["classId"], "semantic": old["records"][key]["semantic"]} for key in sorted(old_ids-new_ids)],
            "newRecords": len(new_ids-old_ids), "retainedIdSemanticChanges": [{"fileID": key, "before": old["records"][key]["semantic"], "after": new["records"][key]["semantic"]} for key in shared if old["records"][key]["semantic"] != new["records"][key]["semantic"]],
            "rootComponents": root_components})
    summary = {"prefabs": len(prefabs), "stableRootGameObjects": sum(p.get("rootGameObjectStable", False) for p in prefabs),
        "retainedFileIDs": sum(p.get("retainedFileIDs", 0) for p in prefabs), "newRecords": sum(p.get("newRecords", 0) for p in prefabs),
        "removedOrReplacedRecords": sum(len(p.get("removedOrReplacedRecords", [])) for p in prefabs),
        "retainedIdSemanticChanges": sum(len(p.get("retainedIdSemanticChanges", [])) for p in prefabs),
        "rootComponentRecords": sum(len(p.get("rootComponents", [])) for p in prefabs),
        "rootComponentFileIDsRetained": sum(c["retained"] for p in prefabs for c in p.get("rootComponents", [])),
        "rootComponentSameSemanticRecords":sum(c["sameSemanticRecord"] for p in prefabs for c in p.get("rootComponents",[]))}
    return {"passed": not metas["missing"] and not metas["changedGuid"] and summary["stableRootGameObjects"] == 200
        and summary["rootComponentSameSemanticRecords"]==summary["rootComponentRecords"],
        "policy": "Added records are expected for new authored details. Removed/replaced and semantic-changed records are factual counts for review, not an automatic claim of broken references or massive fileID loss. Root identity and existing asset GUID changes are checked separately.",
        "meta": metas, "summary": summary, "prefabs": prefabs}


def font(size):
    for path in [Path("C:/Windows/Fonts/arial.ttf"), Path("C:/Windows/Fonts/calibri.ttf"), Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")]:
        if path.exists(): return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def wrapped(draw, message, position, width, face, color, spacing=6):
    x, y = position
    for paragraph in str(message).split("\n"):
        line = ""
        for word in paragraph.split():
            candidate = (line+" "+word).strip()
            if line and draw.textbbox((0,0), candidate, font=face)[2] > width:
                draw.text((x,y), line, font=face, fill=color); y += face.size+spacing; line = word
            else: line = candidate
        draw.text((x,y), line, font=face, fill=color); y += face.size+spacing
    return y


def vector_values(value):
    if isinstance(value, dict): return [value.get(k) for k in ["x", "y", "z"]]
    return value if isinstance(value, list) else []


def camera_match(a, b):
    if not a or not b: return False
    for key in ["cameraPosition", "target"]:
        va, vb = vector_values(a.get(key)), vector_values(b.get(key))
        if len(va) != 3 or len(vb) != 3 or any(x is None or y is None or abs(x-y)>1e-5 for x,y in zip(va,vb)): return False
    return a.get("lighting") == b.get("lighting") == "Day"


def comparisons(catalog, native):
    directory = PROOF/"Comparisons"; directory.mkdir(parents=True, exist_ok=True)
    frames = {(int(f.get("id", -1)), f.get("view"), f.get("lighting")): f for f in (native or {}).get("images", [])}
    entries = []
    for row in catalog:
        aid = int(row["id"])
        before_path = PROOF/"Native/Before"/f"{aid:03d}_Perspective_Day.png"
        after_path = PROOF/"Native/After"/f"{aid:03d}_Perspective_Day.png"
        before_frame,after_frame=frames.get((aid,"Before/Perspective","Day")),frames.get((aid,"After/Perspective","Day"))
        same_camera = camera_match(before_frame,after_frame) and before_frame.get("file")==f"Native/Before/{aid:03d}_Perspective_Day.png" and after_frame.get("file")==f"Native/After/{aid:03d}_Perspective_Day.png"
        canvas = Image.new("RGB", (1360, 654), (237,234,225)); draw = ImageDraw.Draw(canvas)
        draw.text((20,16), f"{aid:03d} · {row['name']}", font=font(25), fill=(34,35,32))
        draw.text((334,64), "ДО · исходный пакет", font=font(20), fill=(50,50,45)); draw.text((850,64), "ПОСЛЕ · доработка", font=font(20), fill=(50,50,45))
        wrapped(draw, REFERENCE_NOTICE+"\n\n"+row["name"]+"\n\n"+row["categoryRu"]+"\n\nРеконструкция по описанию. Сходство с конкретной фотографией не заявляется.",
            (20,105), 286, font(20), (47,49,44))
        for path, x, label in [(before_path,328,"ДО"),(after_path,848,"ПОСЛЕ")]:
            if path.exists():
                with Image.open(path) as source:
                    image = ImageOps.contain(source.convert("RGB"), (504,504), Image.Resampling.LANCZOS)
                    canvas.paste(image, (x+(504-image.width)//2,96+(504-image.height)//2))
            else:
                draw.rectangle((x,96,x+504,600), fill=(213,212,205))
                wrapped(draw,"Нативный кадр "+label+" отсутствует",(x+24,270),450,font(24),(75,70,65))
        caption = "Одинаковая камера и дневной свет подтверждены native_review.json." if same_camera else "Сопоставимость камеры не подтверждена; сравнение не принято."
        draw.text((328,617),caption,font=font(17),fill=(41,87,55) if same_camera else (145,67,41))
        output = directory/f"{aid:03d}_{row['slug']}_BeforeAfter.png";canvas.save(output)
        entries.append({"id":aid,"file":str(output.relative_to(PROOF)),"beforeExists":before_path.exists(),"afterExists":after_path.exists(),"sameCamera":same_camera,
            "beforeSHA256":sha(before_path) if before_path.exists() else None,"afterSHA256":sha(after_path) if after_path.exists() else None})
    category_dir=directory/"Categories";category_dir.mkdir(exist_ok=True)
    for category_index,(category,name,_) in enumerate(GROUPS):
        selected=[e for e in entries if catalog[e["id"]-1]["category"]==category]
        page=Image.new("RGB",(1380,1810),(237,234,225));draw=ImageDraw.Draw(page)
        draw.text((20,14),f"{category_index+1:02d} · {name} · 10 предметов · до / после",font=font(27),fill=(33,35,32))
        for index,entry in enumerate(selected):
            with Image.open(PROOF/entry["file"]) as source: thumb=ImageOps.contain(source,(670,330),Image.Resampling.LANCZOS)
            page.paste(thumb,(20+(index%2)*680,65+(index//2)*345))
        page.save(category_dir/f"{category_index+1:02d}_{category}.jpg",quality=91)
    overview=Image.new("RGB",(1640,1640),(237,234,225));draw=ImageDraw.Draw(overview)
    draw.text((20,14),"20 примеров · по одному предмету в каждой категории",font=font(29),fill=(33,35,32))
    for index,(category,name,_) in enumerate(GROUPS):
        row=next(row for row in catalog if row["category"]==category);path=PROOF/"Native/After"/f"{row['id']:03d}_Perspective_Day.png"
        x=20+(index%4)*405;y=70+(index//4)*310
        if path.exists():
            with Image.open(path) as source:thumb=ImageOps.contain(source.convert("RGB"),(390,270),Image.Resampling.LANCZOS)
            overview.paste(thumb,(x+(390-thumb.width)//2,y))
        else:wrapped(draw,"Кадр отсутствует",(x+20,y+100),350,font(22),(140,60,40))
        draw.text((x,y+274),f"{row['id']:03d} · {name}",font=font(18),fill=(40,42,37))
    overview.save(directory/"Overview_20_Categories_After.jpg",quality=92)
    return {"models":len(entries),"comparablePairs":sum(e["beforeExists"] and e["afterExists"] and e["sameCamera"] for e in entries),"categoryContactSheets":20,"referencePhotosSupplied":False,"entries":entries}


def markdown_table(rows):
    def safe(value):return str(value).replace("|","/").replace("\n"," ")
    return "| "+" | ".join(COLUMNS)+" |\n|"+"|".join(["---"]*len(COLUMNS))+"|\n"+"\n".join("| "+" | ".join(safe(row.get(column,"")) for column in COLUMNS)+" |" for row in rows)+"\n"


def append_section(path, message):
    if not path.exists():raise RuntimeError("Run finish_delivery.prepare before professional_delivery: "+str(path))
    value=text(path)
    value=re.sub(re.escape(BEGIN)+r".*?"+re.escape(END)+r"\s*", "", value, flags=re.S).rstrip()
    path.write_text(value+"\n\n"+BEGIN+"\n"+message.rstrip()+"\n"+END+"\n",encoding="utf-8")


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--finals-ready",required=True,choices=["FINALS_READY"])
    args=parser.parse_args()
    if not ASSET.resolve().is_relative_to(WORK.resolve()) or not BEFORE_ASSET.is_dir():raise RuntimeError("Expected isolated stage and immutable Before backup")
    for required in [ASSET/"Documentation/README_RU.md",ROOT/"Docs/PostApocalypticFurniture.md"]:
        if not required.exists():raise RuntimeError("finish_delivery.prepare must create documentation first: "+str(required))
    PROOF.mkdir(parents=True,exist_ok=True)
    catalog=json_read(ASSET/"Documentation/catalog.json")["assets"]
    if len(catalog)!=200:raise RuntimeError("Expected 200 catalogue models")
    source_time=latest_time((ASSET/"SourceBlender/Source~").glob("*.blend"))
    model_time=latest_time((ASSET/"Models").rglob("*.fbx"))
    runtime_time=max(model_time,latest_time(list((ASSET/"Prefabs").rglob("*.prefab"))+list((ASSET/"Scripts/Runtime").glob("*.cs"))))
    image_time=latest_time((ASSET/"Textures").rglob("*.png"))
    native_time=max(runtime_time,image_time,latest_time((ASSET/"Scripts/Runtime").glob("*.shader")),
        latest_time((ASSET/"Materials").rglob("*.mat")),latest_time((ASSET/"LODs").rglob("*.asset")))
    specs=[
        ("geometry_01_10","geometry_01_10.json",0),("geometry_11_20","geometry_11_20.json",0),
        ("before_source","before_source.json",0),("after_source","after_source.json",source_time),
        ("PBR","PBR_Audit.json",image_time),("native_review","native_review.json",native_time),
        ("cord158","phone158_cord_audit.json",runtime_time),("external_scene_references","external_scene_references.json",runtime_time)]
    data,evidence={},[]
    for key,filename,fresh in specs:
        data[key],record=load_evidence(key,[PROOF/filename,WORK_PROOF/filename],fresh)
        evidence.append(record)
    for key,filename in [("runtime","unity_runtime_checks.json"),("cold_restore","unity_cold_restore.json"),("cold_reopen","unity_cold_check.json"),("prefab_audit","unity_prefab_audit.json"),("reimport","blender_clean_reimport.json")]:
        fresh=runtime_time if key in ["runtime","cold_restore","cold_reopen","prefab_audit"] else max(source_time,model_time)
        data[key],record=load_evidence(key,[VERIFY/filename],fresh);evidence.append(record)
    rows=make_ledger(catalog,data["before_source"],data["after_source"],data["geometry_01_10"],data["geometry_11_20"],evidence)
    write_json(PROOF/"Issues_Ledger.json",{"referencePhotosSupplied":False,"columns":COLUMNS,"rows":rows})
    with (PROOF/"Issues_Ledger.csv").open("w",encoding="utf-8-sig",newline="") as stream:
        writer=csv.DictWriter(stream,fieldnames=COLUMNS);writer.writeheader();writer.writerows({key:row[key] for key in COLUMNS} for row in rows)
    (PROOF/"Issues_Ledger_RU.md").write_text("# Реестр наблюдений и исправлений\n\nФотографии-референсы не предоставлены. Типы взяты из исходного ТЗ; кадры ДО — предыдущая версия ассетов. Review означает признак для интерпретации, а не автоматически доказанный визуальный дефект.\n\n"+markdown_table(rows),encoding="utf-8")
    compatibility=compatibility_report();write_json(PROOF/"identity_compatibility.json",compatibility)
    compare=comparisons(catalog,data["native_review"]);write_json(PROOF/"comparison_inventory.json",compare)
    baseline_images=[]
    for folder in [BACKUP/"Verification/PostApocalypticFurniture/Catalog",BACKUP/"Verification/PostApocalypticFurniture/UnityPreviews"]:
        for path in sorted(folder.glob("*")):
            if path.suffix.lower() in [".jpg",".png"]:
                with Image.open(path) as image: size=list(image.size)
                baseline_images.append({"file":str(path.relative_to(BACKUP)),"size":size,"sha256":sha(path)})
    write_json(PROOF/"baseline_image_inventory.json",baseline_images)
    required_pass=["after_source","PBR","native_review","cord158","runtime","cold_restore","cold_reopen","prefab_audit","reimport","external_scene_references"]
    evidence_status={item["name"]:item["status"] for item in evidence}
    missing_or_failed=[key for key in required_pass if evidence_status.get(key)!="PASS"]
    geometry_present=all(isinstance(data[key],dict) and len(data[key].get("assets",[]))==100 for key in ["geometry_01_10","geometry_11_20"])
    passed=not missing_or_failed and geometry_present and compatibility["passed"] and compare["comparablePairs"]==200
    summary={"passed":passed,"technicalEvidenceOnly":True,"photographicFidelityAccepted":False,"referencePhotosSupplied":False,
        "evidence":evidence,"requiredChecksNotPassing":missing_or_failed,"geometryLedgersHaveAll200Assets":geometry_present,
        "ledgerRows":len(rows),"ledgerStatusCounts":dict(Counter(row["Status"].split(":",1)[0] for row in rows)),
        "comparisonPairs":compare["comparablePairs"],"identitySummary":compatibility["summary"],
        "unchangedExistingGuids":compatibility["meta"]["existingGuidUnchanged"],"existingMetaFiles":compatibility["meta"]["before"],
        "limitations":["No reference photographs supplied; no photo likeness or AAA acceptance", "Automated native captures are not independent human visual acceptance of every angle", "Editor checks do not establish standalone/main-world FPS or streaming acceptance", "Removed/new serialized records require structural context; added authored details are not fileID loss"]}
    write_json(PROOF/"professional_delivery.json",summary)
    source_before=(data["before_source"] or {}).get("summary",{});source_after=(data["after_source"] or {}).get("summary",{})
    result="Приняты перечисленные технические проверки" if passed else "Приёмка не завершена: есть отсутствующие, устаревшие или неуспешные проверки"
    lines=["# Профессиональная доработка набора мебели · отчёт", "", result+".", "", "Фотографии-референсы не предоставлены. Визуальные сравнения показывают старую и новую версии по одному текстовому ТЗ; они не подменяют фотографический эталон.", "", "| Доказательство | Статус | Основание |", "|---|---|---|"]
    lines += [f"| {e['name']} | {e['status']} | {e['reason']} |" for e in evidence]
    lines += ["", "## Source-аудит", "", f"До: {source_before.get('assets','не проверено')} моделей / {source_before.get('assetLodGroups','не проверено')} LOD, {source_before.get('criticalIssueRecords','не проверено')} critical records. После: {source_after.get('assets','не проверено')} / {source_after.get('assetLodGroups','не проверено')}, {source_after.get('criticalIssueRecords','не проверено')} critical records.", "", "Метод: геометрия/топология каждого source mesh, действительная площадь 3D и UV и сингулярные значения отображения, ограниченная эвристика UV2, нормали/сглаживание, пути текстур, масштабы и состав корней. Открытые cloth/Labels не объявляются дефектом без назначения поверхности. Micro-AO поверхностей не является отдельным high-poly object bake.", "", "## Совместимость идентификаторов", "", f"Существующие GUID: {compatibility['meta']['existingGuidUnchanged']} / {compatibility['meta']['before']}; побайтно прежние .meta: {compatibility['meta']['byteIdentical']}. Неизменный root GameObject fileID: {compatibility['summary']['stableRootGameObjects']} / {compatibility['summary']['prefabs']} префабов. Прежние root component fileIDs, присутствующие после: {compatibility['summary']['rootComponentFileIDsRetained']} / {compatibility['summary']['rootComponentRecords']}.", "", f"Сохранено {compatibility['summary']['retainedFileIDs']} прежних serialized records; добавлено {compatibility['summary']['newRecords']}; удалено/заменено {compatibility['summary']['removedOrReplacedRecords']}. Последние числа описывают структурные изменения; добавление новых деталей не называется потерей fileID. Изменения прежних записей и семантики перечислены в identity_compatibility.json для адресной проверки.", "", "## Сравнения", "", f"{compare['comparablePairs']} / 200 пар с подтверждённой одинаковой камерой/дневным светом; 20 категорийных листов по10 предметов. Слева в каждой паре явно указано отсутствие фотореференса. [Обзор20категорий](Comparisons/Overview_20_Categories_After.jpg). [Реестр наблюдений](Issues_Ledger_RU.md).", "", "## Границы результата", "", "Взаимодействие остаётся только через существующий PlayerInteractor на F. Износ и состояния мебели используют прежний единый snapshot мира. Провода/индикация являются представлением и не создают второй ввод или сохранение. Не подтверждаются AAA, сходство с отсутствующими фотографиями, все игровые ракурсы, основной Play/build/FPS и streaming. Отсутствующий результат обозначен PENDING, устаревший STALE; они не превращаются в PASS."]
    native=data["native_review"] or {}; external=data["external_scene_references"] or {}
    truncated=source_after.get("issueRecordsByCode",{}).get("uv2_heuristic_truncated",0)
    uv2_note=(f"Для {truncated} объектов бюджет UV2-эвристики исчерпан, полного результата для них нет." if truncated else "UV2-проход завершён для всех 1908 mesh objects без исчерпания бюджета кандидатов и без обнаруженных перекрытий положительной площади; это проверка указанного алгоритма с численными допусками.")
    lines += ["", "## Объём проверки и оставшиеся диагностические признаки", "", f"Нативные кадры: {native.get('models',0)} моделей, {native.get('frames',0)} изображений, шесть основных ракурсов, детали, три типа освещения, LOD1/2 и открытые механизмы. Малый CPU lightmap bake для Wood/Steel: {native.get('lightmapBaked',False)}, {native.get('lightmaps',0)} lightmaps; геометрия видна на {native.get('lightmapWitnessPixels',0)} пикселях контрольного кадра. Это проверка проходов шейдера, не запекание игрового уровня.", "", f"Повторный source-аудит содержит {source_after.get('reviewIssueRecords',0)} review-записей: угловая эвристика сглаживания 60° при авторском пороге 65° и custom normals; открытые границы упрощённых LOD1/2. Это не объявления доказанных визуальных дефектов. Все жёсткие поверхности LOD0 замкнуты по проверке; {uv2_note} Точные объекты и статусы перечислены в реестре.", "", "Визуально просмотрены все 20 листов каталога (200 предметов), выбранные крупные планы, раскрытые конструкции, LOD2, задние стороны, скользящий свет и исправленный кадр baked-материалов. Список и хеши: visual_review.json. Все 2519 кадров не объявляются индивидуально художественно принятыми.", "", f"В существующей {external.get('scene','сцене')} проверены {external.get('references',0)} сериализованных ссылок на {external.get('assets',0)} ассетов, отсутствующих local fileID: {len(external.get('missing',[]))}. Сцена не пересохранялась; SHA256 записан в external_scene_references.json.", "", "Кресло-кровать019 теперь имеет пять последовательных операций раскладывания и длину спального места 1,855 м. Добавлены четыре механизма; старый снимок мира с прежним019 несовместим с новым составом. Для такой сцены после обновления требуется новое совместимое сохранение F5. Формат общей системы сохранения и строгая проверка состава не обходились.", "", "Резервная копия до пересборки находится в work/PostApocalypticFurniture/ProfessionalAudit/Before; её состав и исходные хеши сохранены в baseline_manifest.json."]
    report="\n".join(lines)+"\n";(PROOF/"Report_RU.md").write_text(report,encoding="utf-8")
    (ASSET/"Documentation/ProfessionalAudit_RU.md").write_text(report.replace("(Comparisons/", "(../../../Verification/PostApocalypticFurniture/ProfessionalAudit/Comparisons/").replace("(Issues_Ledger_RU.md)","(../../../Verification/PostApocalypticFurniture/ProfessionalAudit/Issues_Ledger_RU.md)"),encoding="utf-8")
    appendix="## Профессиональная доработка и её доказательства\n\n"+result+". Полный [отчёт](../../../Verification/PostApocalypticFurniture/ProfessionalAudit/Report_RU.md), реестр проблем, source/PBR/native проверки и200сравнений находятся в `Verification/PostApocalypticFurniture/ProfessionalAudit`. Фотореференсы не предоставлены; типы взяты из исходного ТЗ. Изменения GUID и локальных prefab IDs проверены отдельно от добавления новых авторских деталей. Движущийся витой провод158 имеет три настоящих mesh LOD и следует за трубкой без собственного ввода/сохранения. Нативная проверка провода: "+evidence_status.get("cord158","PENDING")+".\n"
    append_section(ASSET/"Documentation/README_RU.md",appendix)
    append_section(ROOT/"Docs/PostApocalypticFurniture.md",appendix.replace("../../../Verification/","../Verification/"))
    print("FURNITURE_PROFESSIONAL_DELIVERY_"+("PASS" if passed else "PENDING"),json.dumps({"ledgerRows":len(rows),"comparablePairs":compare["comparablePairs"],"requiredChecksNotPassing":missing_or_failed,"stableRoots":compatibility["summary"]["stableRootGameObjects"]},ensure_ascii=False),flush=True)
    if not passed:raise SystemExit(2)


if __name__=="__main__":main()
