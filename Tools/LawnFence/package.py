from pathlib import Path
import shutil,hashlib,json,zipfile,tarfile,os
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'work/LawnFence/UnityProject/Assets/Environment/Fences'
TARGET=ROOT/'Assets/Environment/Fences'
V=ROOT/'Verification/LawnFence'
assert 'LAWN_FENCE_NATIVE_BUILD_PASS' in (ROOT/'work/LawnFence/unity_final.log').read_text(errors='replace')
report=json.loads((V/'unity_import.json').read_text());assert report['passed']
assert not TARGET.exists(),'Refuse to overwrite an existing installed pack'
stage=ROOT/'work/LawnFence/Install/Fences';shutil.copytree(SRC,stage)
TARGET.parent.mkdir(parents=True,exist_ok=True)
shutil.copy2(SRC.with_suffix('.meta'),TARGET.with_suffix('.meta'))
os.replace(stage,TARGET)
copied=list(TARGET.rglob('*'));checks=[]
for p in copied:
    if p.is_file():
        q=SRC/p.relative_to(TARGET)
        digest=hashlib.sha256(p.read_bytes()).hexdigest();assert digest==hashlib.sha256(q.read_bytes()).hexdigest()
        checks.append({'path':p.relative_to(ROOT).as_posix(),'sha256':digest,'bytes':p.stat().st_size})
(V/'installed_hashes.json').write_text(json.dumps(checks,indent=2))

font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',30)
canvas=Image.new('RGB',(1600,1320),(30,34,36));draw=ImageDraw.Draw(canvas)
items=[('Intact_LOD0','01 — Старый, целый'),('Broken_Rods_LOD0','02 — Сломанные прутья'),('Bent_LOD0','03 — Сильно погнутый'),('Heavy_Rust_LOD0','04 — Сильная коррозия'),('Destroyed_LOD0','05 — Разрушенный'),('Daylight_line','Проверочная сцена Unity / URP')]
for i,(fn,label) in enumerate(items):
    x=(i%2)*800;y=(i//2)*440
    im=Image.open(V/(fn+'.png')).convert('RGB');im.thumbnail((800,390))
    canvas.paste(im,(x+(800-im.width)//2,y+50));draw.text((x+20,y+10),label,font=font,fill=(225,230,220))
canvas.save(V/'Preview.jpg',quality=94)

table='\n'.join('| '+r['name']+' | '+' / '.join(map(str,r['triangles']))+' |' for r in report['prefabs'])
text='''# Газонное ограждение: результат проверки

Пять основных префабов, дополнительная конечная стойка, 6 модельных FBX и 5 коллизионных FBX, 14 отдельных карт 4096×4096, 2 URP/Lit материала. Шаг модулей 1,8 м, центральная высота дуги 0,515 м над землёй. Источник — фотография; размеры приняты автором по заданию.

Проверено:
- Экспорт Blender 5.2.2 LTS штатным FBX exporter; каждый из 11 FBX повторно импортирован в чистую сцену. Все оболочки замкнуты, объём положителен, координаты конечны, UV присутствуют. Размеры проверены в мировых метрах.
- Импорт Unity 6000.6.3f1 / URP 17.6.0. Все пять префабов имеют три LOD с уменьшением числа треугольников, MeshFilter/Renderer, MeshCollider и CapsuleCollider опоры. Масштаб единичный, Rigidbody отсутствует, сокеты имеют общий шаг.
- Нормали, UV и тангенты доступны. Все обязательные ссылки на материалы и текстуры разрешаются. Каждый из пяти MeshCollider прошёл настоящий лучевой запрос PhysX в редакторе.
- Для каждого LOD сохранён кадр Camera.Render. Визуально просмотрены общий дневной ряд, пять вариантов, близкие сварные/ржавые поверхности и сравнительные кадры LOD.
- Внешних зависимостей на другие Assets вне комплекта нет; зависимости на стандартные пакеты URP сохранены в dependencies.txt.
- Файлы установленной папки побайтно сверены с проверенным проектом; контрольные суммы в installed_hashes.json. ZIP проходит полную CRC-проверку.

| Вариант | Треугольники LOD0 / LOD1 / LOD2, со стойкой |
|---|---|
'''+table+'''

## Художественная оценка и границы

Широкая низкая дуга, тонкие круглые прутья и общий советский дворовый характер воспроизведены. Умеренный износ и сильная ржавчина различаются; деформации сделаны геометрией. Это процедурный игровой комплект, а не измеренная копия, скан или гарантированное AAA-качество. Референс низкого разрешения не позволяет восстановить точную фактуру, конструкцию сварки и скрытую сторону.

Материалы процедурные; возможна заметность общих мотивов текстуры при очень близком сравнении. UV используют общие полосы атласа; уникального high-to-low bake нет. Сварка — пересекающиеся оболочки. Height не подключён как displacement, AO описывает микрорельеф, не уникальное затенение узлов. Демо-дорожка и трава упрощены и служат фоном проверки.

Ручная приёмка в GameView с реальным игроком, навигация NPC, массовое размещение/FPS, сборка игры и baked light не выполнялись. Работа в отдельном проверочном проекте не доказывает полноценную игровую интеграцию. Существующие карты не заполнялись; коммит/пуш не создавался.

Размещение и ограничения: Docs/LawnFences.md. Для прямого ряда каждая секция владеет левой стойкой; один PF_Fence_EndPost закрывает весь ряд. На очень острых поворотах нужно вручную проверить примыкания; разрушенная секция намеренно разрывает линию.
'''
(V/'Report_RU.md').write_text(text,encoding='utf-8')

files=[]
for base in [TARGET,ROOT/'Sources/LawnFence',ROOT/'Tools/LawnFence',V]:
    files.extend(p for p in base.rglob('*') if p.is_file() and p.suffix!='.blend1')
files.extend([TARGET.with_suffix('.meta'),ROOT/'Docs/LawnFences.md'])
out=ROOT/'outputs/LawnFence_Unity.zip'
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=4) as z:
    for p in files:z.write(p,p.relative_to(ROOT).as_posix())
with zipfile.ZipFile(out) as z:
    assert z.testzip() is None
    assert sum(n.endswith('.fbx') for n in z.namelist())==11
    assert sum(n.endswith('.prefab') for n in z.namelist())==6
with tarfile.open(ROOT/'outputs/LawnFence_Unity.unitypackage','r:gz') as t:
    paths=[t.extractfile(p).read().decode() for p in t.getmembers() if p.name.endswith('/pathname')]
    assert sum(p.endswith('.prefab') for p in paths)==6
    assert sum(p.endswith('.fbx') for p in paths)==11
(V/'archive_integrity.json').write_text(json.dumps({'zip':out.name,'crc_passed':True,'files':len(files),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'unitypackage_prefabs':6,'unitypackage_fbx':11},indent=2))
print('LAWN_FENCE_INSTALLED_AND_ARCHIVE_VERIFIED',out.stat().st_size)
