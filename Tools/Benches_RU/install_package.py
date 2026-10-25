from pathlib import Path
from datetime import datetime
import hashlib
import json
import re
import shutil
import zipfile

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/"work/Benches_RU"
SOURCE=WORK/"UnityProject/Assets/Environment/Props/Benches_RU"
VERIFY=WORK/"Verification"
TARGET=ROOT/"Assets/Environment/Props/Benches_RU"
EVIDENCE=("BlenderReimport.json","UnityValidation.json","ColdReopen.json","TextureAudit.json","LodUvAudit.json","ImageAudit.json")

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    reports={name:json.loads((VERIFY/name).read_text(encoding="utf-8-sig")) for name in EVIDENCE}
    for name,report in reports.items():
        if report["result"]!="PASS": raise RuntimeError(name+" has not passed")
    unity=reports["UnityValidation.json"]
    if "UniversalRenderPipelineAsset" not in unity["renderPipeline"]: raise RuntimeError("Target URP was not verified")
    if unity["prefabCount"]!=15 or unity["textureCount"]!=45: raise RuntimeError("Incomplete package")
    existing={}
    for path in (ROOT/"Assets").rglob("*.meta"):
        if TARGET in path.parents or path==TARGET.with_suffix(".meta"): continue
        found=re.search(r"^guid: ([a-f0-9]{32})$",path.read_text(encoding="utf-8",errors="ignore"),re.M)
        if found: existing.setdefault(found[1],[]).append(str(path.relative_to(ROOT)))
    new={}
    for path in list(SOURCE.rglob("*.meta"))+[SOURCE.with_suffix(".meta")]:
        if not path.exists(): continue
        found=re.search(r"^guid: ([a-f0-9]{32})$",path.read_text(encoding="utf-8",errors="ignore"),re.M)
        if found: new.setdefault(found[1],[]).append(str(path.relative_to(SOURCE.parent)))
    collisions=[guid for guid,paths in new.items() if guid in existing or len(paths)>1]
    if collisions: raise RuntimeError("Asset GUID collision: "+str(collisions))
    if TARGET.exists():
        backup=WORK/("BeforeInstall_"+datetime.now().strftime("%Y%m%d_%H%M%S"))
        shutil.copytree(TARGET,backup)
    shutil.copytree(SOURCE,TARGET,dirs_exist_ok=True)
    shutil.copy2(SOURCE.with_suffix(".meta"),TARGET.with_suffix(".meta"))
    files=[p for p in SOURCE.rglob("*") if p.is_file()]
    mismatches=[str(p.relative_to(SOURCE)) for p in files if sha(p)!=sha(TARGET/p.relative_to(SOURCE))]
    if mismatches: raise RuntimeError("Installed file mismatch: "+str(mismatches))
    audit=dict(result="PASS",guidCollisions=0,filesCopied=len(files),hashMismatches=0,
               sourceBlendBytes=(TARGET/"Source~/Benches_RU_Master.blend").stat().st_size)
    (VERIFY/"InstallAudit.json").write_text(json.dumps(audit,indent=2),encoding="utf-8")
    report=["# Российские уличные лавочки — Benches_RU", "", "Полностью переработанная версия от 25.10.2026. Процедурная реконструкция по описанию конструкций; исходных обмеров и фотограмметрии нет.", "",
      "## Состав и размещение", "",
      "- `Assets/Environment/Props/Benches_RU/Prefabs` — 15 независимых префабов (5 конструкций × 3 состояния).",
      "- `Models` — 15 настоящих FBX, экспортированных Blender 5.2.2 LTS; в каждом LOD0/LOD1/LOD2.",
      "- `Textures` — 45 PNG в девяти общих PBR-наборах; **все 4096×4096**, рассчитаны в этом размере, без увеличения из 2K. Unity также импортировал все карты в 4096×4096.",
      "- `Materials` — 18 материалов ассетов и один нейтральный материал демонстрационного пола. URP/Lit, URP 17.6.0, PC_RPAsset из настроек качества основного проекта.",
      "- `Scenes/Demo_Benches_RU.unity` — пять рядов и три столбца, одинаковое освещение, фронтальная обзорная камера.",
      "- `Source~/Benches_RU_Master.blend` — редактируемые детали и экспортные LOD; относительные пути к соседней папке Textures. Суффикс `~` исключает повторный автоматический Blender→FBX импорт исходника в Unity.",
      "- `Editor/BenchPrefabBuilder.cs` — сборщик с защитой пути: запуск разрешён только в `work/Benches_RU/UnityProject`, чтобы не заменять пользовательскую сцену.", "",
      "## Что переработано", "",
      "Ажурные чугунные опоры с настоящими просветами, замкнутые металлические профили, сварная дворовая рама, скошенные бетонные опоры с вычтенными сколами, цельный перфорированный лист с отверстиями в LOD0, новая деревянная геометрия и нормали. Сломанные доски удалены или укорочены; крепёж не создаётся за их сохранившимися границами. Органические отложения сосредоточены в опорных стыках.", "",
      "UV0 дерева ориентирован вдоль досок и использует общие поверхности с разными смещениями; его перекрытия при тайлинге допустимы. Отдельный UV2 упакован для lightmap. Цветные слои краски, обнажённая древесина, оголённая сталь, ржавчина и бетон имеют разные физические карты. Дерево не имеет металлического отклика. Карта Roughness поставляется отдельно, а для Unity Smoothness записана в alpha MetallicSmoothness.", "",
      "## Геометрия и префабы", "",
      "Одна единица — метр, корневой Scale=(1,1,1), опора около Y=0, лицевая сторона +Z. Статические объекты без Rigidbody. Коллайдеры деревянных деталей получены из сохранившихся отдельных компонентов; опоры и металлический лист используют простые BoxCollider. Это упрощённая физическая оболочка, не покадровая копия каждого болта.", "",
      "LOD переключаются по высоте на экране: 0.15 / 0.05 / 0.012. Для дальнего LOD перфорированной модели применена отдельная более редкая геометрия отверстий. Разрушенная лавочка без спинки имеет менее 15 тысяч треугольников из-за реально отсутствующих досок; бюджет не дополнен невидимой геометрией. Два перфорированных варианта немного превышают ориентир 40 тысяч (порядка 40.0–40.2 тысяч): сохранены настоящие отверстия и четыре опоры. Это исключения к приблизительному бюджету, а не результат точного попадания всех моделей в диапазон.", "",
      "| Префаб | LOD0 | LOD1 | LOD2 | Коллайдеры |", "|---|---:|---:|---:|---:|"]
    for bench in unity["benches"]:
        report.append(f"| {bench['name']} | {bench['lod0Triangles']} | {bench['lod1Triangles']} | {bench['lod2Triangles']} | {bench['colliderCount']} |")
    report += ["", "## Проверки", "",
      "Чистый Blender reimport 15/15: масштаб, границы, конечные координаты, три LOD, материальные слоты и UV2. Пересечения UV2-треугольников проверены для всех трёх LOD; численная граница значимого пересечения — 1e-6 площади атласа, исходные счётчики сохранены в JSON.", "",
      "Unity 6000.6.3f1 / URP 17.6.0: 15 FBX, 15 префабов, все материалы и необходимые карты, UV2 во всех мешах, масштабы, опора, LOD и составные коллайдеры. Проверено отдельное холодное повторное открытие сохранённой сцены. Размеры PNG и импортированных текстур проверены отдельно.", "",
      "Обзорные и близкие кадры получены через URP SingleCameraRequest с двумя запросами для прогрева буферов и временным отключением GPU Resident Drawer только в изолированном контексте захвата. Первый одноразовый холодный запрос давал неверные цвета при правильных сохранённых материалах. Настройка PC_RPAsset возвращается обратно после каждого снимка. Это ограничение метода захвата раскрыто отдельно от пригодности шейдера.", "",
      "Дополнительно получен настоящий игровой кадр после 24 Play-кадров с GPU Resident Drawer / InstancedDrawing включённым: `RuntimeFrame_GPUResident.png`. На нём правильные материалы и все 15 лавочек. Общий статус этого отдельного прогона — **FAIL**, поскольку UnityEditor.Search.SearchDatabase выбросил ArgumentOutOfRangeException при индексации; полный стек сохранён в `RuntimeFrameAudit.json`. Ошибка не замаскирована техническими PASS остальных проверок. FPS и длительная игровая стабильность этим коротким захватом не доказаны.", "",
      "При установке сохранены .meta/GUID, коллизий с существующими GUID не найдено, побайтовые SHA-256 всех скопированных файлов совпали. Packages, ProjectSettings и пользовательские сцены основного проекта этим пакетом не заменяются.", "",
      "## Границы результата", "",
      "Технические проверки и визуальные кадры относятся к отдельному проекту с профилем URP основного checkout. Основной Play, standalone, массовое размещение/FPS, baked light и gameplay/save-интеграция не проверялись. Это процедурные ассеты; уровень AAA и точное соответствие конкретной реальной лавочке не подтверждены. Субмиллиметровые контакты всех деталей не являются доказанными полным mesh-intersection аудитом.", "",
      "Первый черновик забракован визуально и не установлен; исторические неудачные проверки находятся только в рабочей папке. Финальные результаты и близкие кадры: `Verification/Benches_RU`."]
    doc=ROOT/"Docs/Benches_RU.md"
    doc.parent.mkdir(exist_ok=True)
    doc.write_text("\n".join(report)+"\n",encoding="utf-8")
    evidence_target=ROOT/"Verification/Benches_RU"
    evidence_target.mkdir(parents=True,exist_ok=True)
    for name in EVIDENCE+("InstallAudit.json",): shutil.copy2(VERIFY/name,evidence_target/name)
    shutil.copy2(VERIFY/"RuntimeFrameAudit.json",evidence_target/"RuntimeFrameAudit.json")
    for path in VERIFY.glob("*.png"): shutil.copy2(path,evidence_target/path.name)
    summary="\n".join(name+": PASS" for name in EVIDENCE)+"\nInstallAudit.json: PASS\nRuntimeFrameAudit.json: FAIL - UnityEditor.Search.SearchDatabase ArgumentOutOfRangeException; 24 actual frames captured with InstancedDrawing.\n"
    (evidence_target/"ValidationSummary_RU.txt").write_text(summary,encoding="utf-8")
    archive=ROOT/"outputs/Benches_RU_Unity.zip"
    archive.parent.mkdir(exist_ok=True)
    package_files=[p for p in TARGET.rglob("*") if p.is_file()]+[TARGET.with_suffix(".meta"),doc]
    package_files += [p for p in evidence_target.iterdir() if p.is_file()]
    with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED,compresslevel=3,allowZip64=True) as zipped:
        for path in package_files: zipped.write(path,path.relative_to(ROOT).as_posix())
    with zipfile.ZipFile(archive) as zipped:
        bad=zipped.testzip()
        if bad: raise RuntimeError("ZIP CRC error: "+bad)
    (VERIFY/"ArchiveAudit.json").write_text(json.dumps(dict(result="PASS",entries=len(package_files),bytes=archive.stat().st_size,sha256=sha(archive)),indent=2),encoding="utf-8")
    shutil.copy2(VERIFY/"ArchiveAudit.json",evidence_target/"ArchiveAudit.json")
    archive.with_suffix(".zip.sha256").write_text(sha(archive)+"  "+archive.name+"\n",encoding="ascii")
    print("BENCHES_INSTALLED_AND_PACKAGED",len(files),"files; archive",archive.stat().st_size,"bytes")

if __name__=="__main__": main()
