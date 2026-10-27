# Окраска Building_demo-building-012

Только указанный дом в SampleScene_CorrectedRoads_20261025 окрашен готовыми материалами:

- AgedOchreFacade_01/M_AgedOchreFacade_01_URP: жёлтая основа,184наружных Renderer.
- AgedCoralFacade_01/M_AgedCoralFacade_01_URP:90Renderer красных крупных ступенчатых участков спереди/сзади и на торцах.
- WeatheredPlinth_01/M_WeatheredPlinth_01_URP: существующий BaseTrim,1Renderer.

Все материалы взяты из Assets/Maps/Buildings/Textures. Их исходники не редактировались; MaterialsBefore/After подтверждают3неизменных SHA. Входные двери, рамы, стекло и интерьер не окрашивались. primary exteriorMaterialOverride=yellow, baseTrimMaterialOverride=plinth; отдельные красные поля сохранены локальными назначениями Renderer. Глобальный Apply материала фасада впоследствии заменит этот рисунок одним цветом — использовать адресное редактирование.

PaintResult.json: pass=true;317154Transform сцены,209960незатронутых Renderer и3квартиры проверены на сохранность. ID, профиль и прочие метаданные дома сохранены; разрешено изменение только двух material override. Native Front.png/Rear.png из изолированной Preview Scene осмотрены. Дом/окружение не перестраивались, геометрия не менялась. SaveComplete.txt подтверждает сохранение рабочей сцены22:19:10UTC26.10.2026 (01:19:10МСК27.10), dirty=False.

Резервная дисковая копия сцены перед окраской — путь в BackupPath.txt; на начало работы активная сцена была dirty=False. Внешние исполняемые body: Tools/BuildingRules/PaintDemo012_20261027.cs.txt и CaptureDemo012_20261027.cs.txt; в Assets код не добавлялся, domain reload не требовался. Покраска выполнена с Undo. Две предварительные проверки безопасно откатили назначения из-за проверки JSON-обёртки; после уточнения проверки единственные изменения метаданных подтверждены. Commit/push не выполнялись.
