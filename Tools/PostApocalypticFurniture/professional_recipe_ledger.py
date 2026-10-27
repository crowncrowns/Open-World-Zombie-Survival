"""Inventory, preserved authored identifiers and per-category revision ledger.

This records actual exported data and completed source preflights; native
rendering/motion acceptance remains a separate evidence gate.
"""
import json,hashlib,zipfile
from pathlib import Path
from catalog import ROOT,WORK,ASSET,VERIFY

before=WORK/'ProfessionalAudit/Before/Assets/PostApocalypticFurniture'
old=json.loads((before/'Documentation/catalog.json').read_text(encoding='utf-8'))['assets']
new=json.loads((ASSET/'Documentation/catalog.json').read_text(encoding='utf-8'))['assets']
old={q['id']:q for q in old}
first=json.loads((VERIFY/'preflight_01_10.json').read_text(encoding='utf-8'))
second=json.loads((VERIFY/'preflight_11_20.json').read_text(encoding='utf-8'))
assert first['summary']['count']==100 and not first['errors']
assert len(second)==100 and all(not q['issues'] for q in second)
base_issues=[
 dict(key='UV_METRIC',severity='HIGH',affected_ids=list(range(1,201)),before_problem='Проекционная UV0 не учитывала локальный поворот досок/цилиндрическую поверхность; отдельные LOD имели схлопывание или сильное растяжение.',fix='Метрические локальные и цилиндрические UV0; независимый ремонт анизотропии после упрощения. Нативное отображение проверяется отдельно.',status='SOURCE_PREFLIGHT_RECORDED'),
 dict(key='NORMALS',severity='HIGH',affected_ids=list(range(1,201)),before_problem='Плоские фаски и неподготовленные угловые нормали; в прежних LOD2 найдены обращённые замкнутые острова.',fix='Раздельная сварка примитивов, проверка ориентированного объёма, авторские взвешенные угловые нормали и жёсткие границы.',status='SOURCE_PREFLIGHT_RECORDED'),
 dict(key='UV2_DENSITY',severity='MEDIUM',affected_ids=list(range(1,201)),before_problem='Прежний UV2 выдавал одинаковую ячейку каждому треугольнику независимо от его площади.',fix='Метрические копланарные острова с отдельными непересекающимися прямоугольниками и полями; повторный независимый аудит.',status='SOURCE_PREFLIGHT_RECORDED'),
 dict(key='PBR',severity='HIGH',affected_ids=list(range(1,201)),before_problem='Поверхности имели независимый шум вместо согласованного физического микрорельефа и нестрогую периодичность; свойства металла/покрытий смешивались.',fix='36 оригинальных наборов по8 карт; согласованные height/normal/micro-AO, проводящие и диэлектрические области, периодическая структура и масштаб.',status='PBR_GENERATED_NATIVE_REVIEW_SEPARATE'),
 dict(key='PAD_SEAMS',severity='HIGH',affected_ids=list(range(1,21))+[25,54,55,57,61,62,63,64,65,66,67,68,69,70,186],before_problem='Горизонтальная плоскость шва и прогиба применялась также к вертикальным спинкам/подлокотникам; leather piping использовал дерево.',fix='Тонкая ось определяется формой подушки; шов следует фактическому периметру, локальные складки и материал обивки.',status='SOURCE_CHANGED_NATIVE_REVIEW_SEPARATE'),
 dict(key='LIGHTING_SHADER',severity='HIGH',affected_ids=list(range(1,201)),before_problem='Шейдер использовал только SH и не имел Meta-прохода для полноценного lightmap workflow.',fix='LIGHTMAP_ON, shadow mask, reflection-probe варианты и собственный Meta; реальное контрольное запекание — отдельный native gate.',status='IMPLEMENTED_BAKE_WITNESS_REQUIRED'),
]
category_fixes={
 1:('UPHOLSTERY_CASE','Каркас/цоколь и крепёж мягкой мебели были упрощены.','Несущие царги, опоры, узлы, крепления обивки и исправленные реальные точки пуговиц.'),
 2:('ARMCHAIR_BUILD','Узлы мягких кресел и опоры были упрощены.','Профили деревянных деталей, опоры, крепления и полноценный многоступенчатый механизм019.'),
 3:('CHAIR_BUILD','Базовые стулья имели простые опоры/оболочки.','Профиль ножек, царги/угловые узлы; непрерывная формованная оболочка026.'),
 4:('TABLE_JOINERY','Стыки ножек/царг и поверхности столов требовали конструктивной детализации.','Профили/крепления и соединения в сохранённых габаритах; настоящее покрытие039.'),
 5:('DESK_JOINERY','Сборочные детали столов, ручки и направляющие были упрощены.','Вложенные направляющие, крепёж, опоры и действительная посадка ручек.'),
 6:('BED_FRAME','Каркасы кроватей и сетка060 не давали полного конструктивного чтения.','Узлы рам/труб и настоящие замкнутые ромбические звенья сетки060; пуговицы054 посажены на геометрию.'),
 7:('BEDDING_BUILD','Разрыв064 был собран из отдельных валиков; отдельные складки имели чрезмерную регулярность.','Цельный периметр матраса с локальным рваным окном/поролоном/пружинами; сдержанные формы складок.'),
 8:('CABINET_JOINERY','Крепления полок/ручек и задники шкафов требовали детализации.','Четверти задников, крепёж, опоры и соединения; имена подвижных деталей сохранены.'),
 9:('NIGHTSTAND_JOINERY','Направляющие и узлы небольших ящиков/ручек были упрощены.','Составные направляющие и реальные соединения/посадка ручек.'),
 10:('DRESSER_JOINERY','Конструктивные узлы/рамки требовали доработки.','Крепления и направляющие; соединения рамки097 выполнены без совпадающих угловых граней.'),
 11:('SHELF_BUILD','Корпуса/фасады и соединения стеллажей были базовыми.','Рамочно-филёнчатые фасады, уплотнители стекла и детали сборки.'),
 12:('KITCHEN_BUILD','У113 пересекались L-плиты/имелись оторванные опоры; мойка114 была прямоугольной.','Единые L-плиты с опёртыми ногами113; штампованная чаша114 со сливом и сифоном.'),
 13:('APPLIANCE_BUILD','Некоторые стёкла духовок имели непрозрачную подложку.','Настоящие проёмы и двойные стёкла, замкнутые панели с отверстиями, полости и приборные узлы.'),
 14:('FRIDGE_BUILD','Корпусные кромки/уплотнения/опора оборудования были упрощены.','Фланцы, уплотнители, петлевые крепления, опора агрегата и различимые фасадные детали.'),
 15:('RADIO_BUILD','Пять клавиш141–143 были статичны; панели/фронты и крепления требовали детализации.','15 отдельных F-клавиш с собственными станциями; углублённые рамки/решётки/крепления, полые отсеки и цельные панели.'),
 16:('ELECTRONICS_BUILD','156 стоял на мебельных ножках; провод158 не следовал за трубкой.','Пьедестал CRT156; замкнутый исходный провод и настоящий динамический mesh с3LOD158; панели и реальные соединения.'),
 17:('RUG_SURFACE','Рисунок и тканевая микроструктура нуждались в независимой проверке.','Отдельные рисунки сохранены, карты2K и согласованные ворс/normal/height; силуэт и повреждения сохранены.'),
 18:('LIGHT_FIXTURE_BUILD','Сборочные узлы светильников и материал стекла/металла требовали ревизии.','Объёмные узлы, поверхности проводников/покрытий, собственные выключатели/регуляторы с авторскими осями.'),
 19:('HALLWAY_BUILD','Корпуса/фасады/стыки и детали вешалок требовали ревизии.','Сборочные детали и опоры; радиальные ответвления183 входят в центральную стойку без совпадающих торцевых граней.'),
 20:('BATHROOM_BUILD','Оболочки пластиковых дверец пересекались; зона197 проходила через добавленный разделитель.','Цельные формованные дверцы с возвратным фланцем; полезная зона197 ограничена свободным объёмом.'),
}
for category,(key,problem,fix) in category_fixes.items():base_issues.append(dict(key=key,severity='HIGH',affected_ids=list(range((category-1)*10+1,category*10+1)),before_problem=problem,fix=fix,status='SOURCE_CHANGED_NATIVE_REVIEW_SEPARATE'))
assets=[]
for row in new:
    prior=old[row['id']];parts={p['name']:p for p in row['parts']};oldparts={p['name']:p for p in prior['parts']}
    missing=sorted(set(oldparts)-set(parts));assert not missing,(row['id'],missing)
    for name in parts:
        seen=set();current=name
        while parts[current].get('parent'):
            if current in seen:raise AssertionError('Cyclic part hierarchy')
            seen.add(current);current=parts[current]['parent'];assert current in parts
    file=Path('Models')/row['category']/(row['slug']+'.fbx')
    sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
    assert (ASSET/file).is_file() and max(row['triangles'])<=18000
    changed=[name for name in oldparts if any(oldparts[name].get(k)!=parts[name].get(k) for k in ['pivot','axis','amount','kind'])]
    assets.append(dict(id=row['id'],name=row['name'],slug=row['slug'],source_status='SOURCE_PREFLIGHT_RECORDED',issues=[q['key'] for q in base_issues if row['id'] in q['affected_ids']],
        before=dict(triangles=prior['triangles'],parts=len(prior['parts']),zones=len(prior['zones']),fbxSHA256=sha(before/file)),
        after=dict(triangles=row['triangles'],parts=len(parts),zones=len(row['zones']),fbxSHA256=sha(ASSET/file)),retainedPartNames=len(oldparts),removedPartNames=missing,addedPartNames=sorted(set(parts)-set(oldparts)),changedMotionDefinitions=changed))
for start,end,name in [(1,100,'geometry_01_10.json'),(101,200,'geometry_11_20.json')]:
    rows=[a for a in assets if start<=a['id']<=end]
    report=dict(passed=True,scope='Exported inventory, stable authored part IDs and source preflight. Does not replace native artistic or motion acceptance.',referencePhotosSupplied=False,assets=rows,issues=[q for q in base_issues if any(start<=i<=end for i in q['affected_ids'])])
    (VERIFY/'ProfessionalAudit'/name).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
top=Path(str(before)+'.meta')
if not top.exists():
    with zipfile.ZipFile(WORK/'ProfessionalAudit/Before/outputs/PostApocalypticFurniture_Unity.zip') as z:top.write_bytes(z.read('Assets/PostApocalypticFurniture.meta'))
print('PROFESSIONAL_RECIPE_LEDGER_PASS',len(assets),'retained original part names; missing=0')
