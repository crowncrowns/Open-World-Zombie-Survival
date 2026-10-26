from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/LightingPack/UnityProject/Assets/PostApocalypse/LightingPack'
VERIFY=ROOT/'Verification/LightingPack'
MATERIALS=['Steel','Aluminium','Plastic','Glass','Bulb','Rubber','Copper','ClearGlass']
# Coordinates in metres, Unity: X right, Y up, Z away from mounting surface.
# ceiling fixtures have their pivot on the ceiling and extend downwards.
ROWS=[
('Entrance','Entrance_Oval','Овальный с решёткой','oval',.282,.155,.105,'wall',3,6,105),
('Entrance','Entrance_Tablet','Круглая таблетка','tablet',.241,.241,.1,'wall',2.5,5,115),
('Entrance','Entrance_AntiVandal','Круглый антивандальный','armored',.24,.24,.085,'wall',3,6,100),
('Entrance','Entrance_LongCage','Продолговатый с решёткой','longcage',.42,.14,.11,'wall',3,6,100),
('Entrance','Entrance_OpenSocket','Открытая лампа на стене','wallsocket',.10,.19,.17,'wall',1.8,4,120),
('Entrance','Entrance_DoorPlastic','Пластиковый над дверью','doorplastic',.33,.14,.095,'wall',2.5,5,110),
('Entrance','Entrance_Visor','С наклонным козырьком','visor',.31,.20,.19,'wall',3,6,90),
('Entrance','Entrance_Ceiling','Потолочный для площадки','ceilingring',.29,.29,.105,'ceiling',3,6,120),
('Exterior','Exterior_SovietArm','Консольный советский','soviet',.32,.19,.69,'arm',6,13,100),
('Exterior','Exterior_LEDArm','Плоский LED на кронштейне','ledarm',.29,.07,.52,'arm',6,12,100),
('Exterior','Exterior_Entry','Фонарь над входом','entry',.25,.36,.28,'wall',3.5,7,90),
('Exterior','Exterior_YardFlood','Дворовый прожектор','flood',.38,.29,.16,'wall',8,16,65),
('Exterior','Exterior_Canopy','Под козырьком','canopy',.38,.24,.08,'ceiling',3.5,7,110),
('Exterior','Exterior_Globe','Шар на кронштейне','globe',.30,.42,.40,'arm',4,8,130),
('Exterior','Exterior_Underpass','Для подземного перехода','underpass',.48,.22,.12,'wall',4,8,100),
('Exterior','Exterior_Weatherproof','Длинный фасадный','weatherlong',1.20,.076,.066,'wall',5,8,110),
('Interior','Interior_TwinTube','Двухтрубный люминесцентный','twintube',1.25,.24,.09,'ceiling',4,7,115),
('Interior','Interior_SingleTube','Однотрубный','singletube',.65,.085,.07,'ceiling',2.5,5,115),
('Interior','Interior_Office','Офисный растровый','office',.60,.60,.085,'ceiling',4,7,110),
('Interior','Interior_Round','Круглый потолочный плафон','round',.34,.34,.13,'ceiling',3,6,120),
('Interior','Interior_Pendant','Подвесной с отражателем','pendant',.40,.40,.58,'ceiling',5,10,85),
('Interior','Interior_BareBulb','Лампа накаливания','barebulb',.064,.064,.16,'ceiling',1.8,4,140),
('Interior','Interior_Cage','Лампа в металлической клетке','cage',.16,.16,.27,'ceiling',2.5,5,130),
('Interior','Interior_LEDPanel','Встраиваемая LED панель','panel',.60,.60,.025,'ceiling',4,7,110),
('Industrial','Industrial_Sealed','Герметичный промышленный','sealed',.34,.18,.15,'wall',3.5,7,100),
('Industrial','Industrial_Adjustable','Регулируемый прожектор','adjustable',.30,.26,.23,'wall',6,12,60),
('Industrial','Industrial_Emergency','Аварийный с аккумулятором','emergency',.36,.17,.09,'wall',2,5,110),
('Industrial','Industrial_Portable','Переносная строительная','portable',.16,.16,.38,'floor',3,6,125),
('Industrial','Industrial_Basement','Подвальный технический','basement',.24,.13,.13,'wall',2.5,5,100),
('Industrial','Industrial_OldFlood','Старый толстостекольный','oldflood',.36,.32,.28,'wall',7,14,55),
]
def catalog():
    return [dict(id=i+1,category=r[0],name=r[1],title=r[2],shape=r[3],width=r[4],height=r[5],depth=r[6],mount=r[7],intensity=r[8],range=r[9],angle=r[10]) for i,r in enumerate(ROWS)]
