from pathlib import Path
import hashlib
import json
import re
import shutil
import zipfile

base=Path(__file__).resolve().parents[1]
repo=base.parents[1]
asset=base/'UnityProject/Assets/Environment/Windows/MetalGrilles'
out=repo/'Verification/MetalGrilles'
out.mkdir(parents=True,exist_ok=True)
reports={
 'blender_reimport':base/'Verification/blender_reimport.json',
 'source_and_sweep':base/'Verification/source_scene_validation.json',
 'unity_build':base/'UnityProject/Assets/Validation/WindowGrilles/build_report.json',
 'unity_reopen':base/'UnityProject/Assets/Validation/WindowGrilles/reopen_report.json',
 'unity_play':base/'UnityProject/Assets/Validation/WindowGrilles/play_report.json',
}
loaded={key:json.loads(path.read_text(encoding='utf-8-sig')) for key,path in reports.items()}
for key,j in loaded.items():
    if not j['status'].endswith('_PASS'):raise RuntimeError(key+' failed: '+j['status'])
for typ in range(1,6):
    for state in 'ABC':
        if not(asset/f'Prefabs/Grille_{typ:02d}_{state}.prefab').is_file():raise RuntimeError('Prefab missing')
        if not(asset/f'Verification/Grille_{typ:02d}_{state}.png').is_file():raise RuntimeError('Preview missing')

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda:stream.read(4*1024*1024),b''):h.update(chunk)
    return h.hexdigest()

for key,path in reports.items():
    shutil.copy2(path,out/(key+'.json'))
    shutil.copy2(path,asset/'Verification'/(key+'.json'))

for name in ('unity_verified_play.log','unity_acceptance_build.log','unity_acceptance_play.log','unity_acceptance_reopen.log'):
    path=base/'Verification'/name
    if path.is_file():
        shutil.copy2(path,out/name)
        shutil.copy2(path,asset/'Verification'/name)

build=loaded['unity_reopen']
rows=[]
for item in build['items']:
    code=item['id'].replace('SM_','')
    rows.append('| '+code+' | '+' / '.join(str(v) for v in item['lodTriangles'])+' | '+str(item['colliderCount'])+' |')
clearance=min(x['wallPlaneClearanceMetres'] for x in loaded['source_and_sweep']['hingeSweep'])
report='''# Проверка набора MetalGrilles — 25.10.2026

Созданы и установлены 15 самостоятельных префабов, 15 настоящих FBX и редактируемый Blender master с четырьмя LOD каждой модели. Геометрия и PBR изготовлены процедурно. Материалы предназначены для URP 17.6.0 / Unity 6000.6.3f1.

## Подтверждено

- Чистый Blender re-import всех 15 окончательных FBX: конечные координаты, UV, четыре группы LOD, убывание числа треугольников и точные имена шарниров каждого уровня.
- Unity: 15 загруженных FBX и 15 префабов, четыре LOD, назначенные URP Lit материалы и полный набор используемых карт, 48 исходных карт 4096×4096, корректные габариты и нижний pivot.
- Стена Demo имеет пять фактических отверстий 1×1 м; размеры измерены по границам её Renderer. Галерея содержит все 15 состояний в масштабе 1:1.
- Реальный изолированный Play: четыре распашных экземпляра, начальная закрытая поза, промежуточные углы, конечные ±105°, закрытие, синхронное движение всех четырёх LOD и коллайдеров, неподвижный корень.
- Непрерывный геометрический расчёт поворота обеих створок для трёх состояний: минимальный зазор до плоскости стены при рекомендованной установке — '''+f'{clearance*1000:.1f}'+''' мм. Все вершины, а следовательно и треугольники створок, остаются перед этой плоскостью.
- Blender master имеет 15 коллекций и 48 относительных путей изображений, которые разрешаются в файлы пакета.
- Просмотрены фронтальные/слегка косые кадры 15 вариантов и крупные кадры открытой/закрытой решётки. Проверка художественной точности конкретному изделию не проводилась.
- Каталожные и обзорные кадры сняты с принудительным LOD0. Отдельно приложены четыре уровня Classic B; в сохранённой Demo используется автоматический выбор LOD.
- Повторное открытие Unity отдельным процессом прошло с итоговым статусом WINDOW_GRILLES_REOPEN_PASS.

## Замечания к журналу редактора

В изолированном Play Unity зарегистрировал ArgumentOutOfRangeException в UnityEditor.Search.SearchDatabase / IndexationOnStartup. Это отдельное исключение редакторского поиска; сквозные проверки анимации завершились, их результат сохранён до выхода из Play и пережил domain reload. Также при импорте шаблонов Shader Graph встречались предупреждения о зависимостях служебных Terrain-шейдеров. Набор решёток использует назначенный URP Lit, его материалы и карты проверены, кадры получены. Журнал не объявляется полностью чистым; основные игровые сцены для обхода этих сообщений не запускались.

## Геометрия и коллизия

Основная рама 0,92×0,94 м входит в проём 1×1 м. Выносные крепления дают полную ширину 1,069 м, у усиленного типа — 1,089 м. Pivot находится в центре нижнего профиля, нижняя граница металла — Y=−0,02 м. Лицевая сторона +Z. Для установки: pivot на 20 мм выше нижнего края окна и на 70 мм впереди фасада.

Коллайдеры составные BoxCollider основных силовых элементов. Это упрощённая игровая коллизия; мелкие сварные валики, сколы, болты и точный профиль декоративных завитков в ней не воспроизводятся. MeshCollider и Rigidbody отсутствуют. У створок отдельные шарниры коллизии.

| Вариант | Треугольники LOD0 / 1 / 2 / 3 | BoxCollider |
|---|---:|---:|
'''+ '\n'.join(rows)+'''

## Файлы и ограничения

Пакет: `Assets/Environment/Windows/MetalGrilles`. Демонстрация: `DemoScene/MetalGrilles_Demo.unity`. Исходник: `Source~/MetalGrilles_Master.blend`. Управление створками: `WindowGrilleHinge.Open/Close/Toggle/SetOpen`; Animator использует отдельные Open и Close длительностью 0,75 с, а также неподвижную начальную ClosedPose.

Подробности UV, каналов PBR, управления Demo и референсы находятся в README_RU.md. Проверочные инструменты хранятся в work/WindowGrilles/Tools и Editor изолированного проекта. Unity builder защищён от запуска на основном checkout.

Это процедурная реконструкция; AAA и точное соответствие фотографиям не подтверждены. Осмотр кадров ограничен приложенными ракурсами. Плавность переходов LOD во всех условиях, FPS большого мира, baked light, standalone, основной Play и player/world-save не проверялись. Засов — визуальный; интеграция замков и ключей не добавлена.
'''
(out/'Report_RU.md').write_text(report,encoding='utf-8')
(asset/'Verification/Report_RU.md').write_text(report,encoding='utf-8')
docs='''# Оконные решётки MetalGrilles

Пакет находится в [Assets/Environment/Windows/MetalGrilles](../Assets/Environment/Windows/MetalGrilles/README_RU.md): 15 префабов, 15 FBX, четыре LOD, 48 карт 4K, URP Lit, составные коллайдеры и три распашных варианта с Animator.

- [Установка, размеры, управление и источники](../Assets/Environment/Windows/MetalGrilles/README_RU.md).
- [Отчёт проверки и бюджет геометрии](../Verification/MetalGrilles/Report_RU.md).
- [Демонстрационная сцена](../Assets/Environment/Windows/MetalGrilles/DemoScene/MetalGrilles_Demo.unity).
- [Редактируемый Blender master](../Assets/Environment/Windows/MetalGrilles/Source~/MetalGrilles_Master.blend).

Масштаб — метр. Рама 0,92×0,94 м совместима с отверстием 1×1 м; монтажные уши выступают на фасад. Pivot — центр нижнего профиля. Лицевая сторона +Z. При установке pivot поднимается на 20 мм от нижнего края отверстия и выносится на 70 мм от фасада. Ширина с креплениями — 1,069 м, у усиленного типа — 1,089 м.

Реальный Blender re-import 15/15, Unity 6000.6.3f1 / URP17.6.0, изолированный Play и отдельное повторное открытие прошли. Пользовательские игровые сцены и Packages не редактировались этой задачей. Main Play, standalone, большой мир/FPS, baked light и player/save-интеграция остаются за границами проверки. Художественный результат процедурный, точность конкретной фотографии и AAA не подтверждены.
'''
(repo/'Docs/MetalGrilles.md').write_text(docs,encoding='utf-8')

# Copy only the finished package. Existing user assets must not be overwritten.
target=repo/'Assets/Environment/Windows/MetalGrilles'
if target.exists():raise RuntimeError('Target already exists; inspect before replacing: '+str(target))
new_guids={}
for meta in [asset.with_suffix('.meta'),*asset.rglob('*.meta')]:
    if not meta.is_file():continue
    match=re.search(r'^guid:\s*(\w+)',meta.read_text(encoding='utf-8-sig'),re.M)
    if match:
        guid=match.group(1)
        if guid in new_guids:raise RuntimeError('Duplicate package GUID '+guid)
        new_guids[guid]=meta
for meta in (repo/'Assets').rglob('*.meta'):
    match=re.search(r'^guid:\s*(\w+)',meta.read_text(encoding='utf-8-sig',errors='replace'),re.M)
    if match and match.group(1) in new_guids:raise RuntimeError('Host GUID collision '+str(meta))

protected=[p for folder in ('Packages','ProjectSettings') for p in (repo/folder).rglob('*') if p.is_file()]
protected.append(repo/'Assets/CharacterTesting/CharacterTest.unity')
before={str(p.relative_to(repo)):sha(p) for p in protected if p.is_file()}
target.parent.mkdir(parents=True,exist_ok=True)
shutil.copytree(asset,target,ignore=shutil.ignore_patterns('*.blend1'))
root_meta=asset.with_suffix('.meta')
if root_meta.is_file():shutil.copy2(root_meta,target.with_suffix('.meta'))
manifest=[]
for source in sorted(asset.rglob('*')):
    if not source.is_file() or source.name.endswith('.blend1'):continue
    relative=source.relative_to(asset)
    expected=sha(source);actual=sha(target/relative)
    if actual!=expected:raise RuntimeError('Installation hash mismatch '+str(relative))
    manifest.append({'path':str(relative).replace('\\','/'),'bytes':source.stat().st_size,'sha256':actual})
changed=[p for p,h in before.items() if sha(repo/p)!=h]
if changed:raise RuntimeError('Protected files changed during installation '+str(changed))
install={'status':'INSTALLATION_HASH_PASS','fileCount':len(manifest),'guidCollisions':0,'protectedFilesUnchanged':len(before),'items':manifest}
(out/'installation_manifest.json').write_text(json.dumps(install,indent=2),encoding='utf-8')

archive=repo/'outputs/MetalGrilles_Unity.zip'
archive.parent.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as z:
    for p in sorted(target.rglob('*')):
        if p.is_file():z.write(p,p.relative_to(repo).as_posix())
    if target.with_suffix('.meta').is_file():z.write(target.with_suffix('.meta'),target.with_suffix('.meta').relative_to(repo).as_posix())
    z.write(repo/'Docs/MetalGrilles.md','Docs/MetalGrilles.md')
with zipfile.ZipFile(archive) as z:
    failed=z.testzip()
    if failed:raise RuntimeError('Archive CRC failed '+failed)
archive_report={'status':'ARCHIVE_PASS','path':str(archive),'bytes':archive.stat().st_size,'sha256':sha(archive),'crc':True}
(out/'archive_integrity.json').write_text(json.dumps(archive_report,indent=2),encoding='utf-8')
print('DELIVERY_PASS',len(manifest),'files',archive.stat().st_size,'archive bytes')
