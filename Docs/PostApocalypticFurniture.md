# Мебель квартир · 200 предметов

Основная инструкция: [README](../Assets/PostApocalypticFurniture/Documentation/README_RU.md). Полный [каталог](../Assets/PostApocalypticFurniture/Documentation/CATALOG_RU.md), [отчёт проверки](../Verification/PostApocalypticFurniture/Report_RU.md).

Взаимодействие исключительно через прежний PlayerInteractor на **F**. Переносимые экземпляры регистрируются в существующем реестре мира командой Tools/Environment/Furniture/Зарегистрировать выбранную мебель в сохранении мира. Износ хранится на FurnitureWear. Изолированные Showcase и StressTest находятся в DemoScenes набора.

<!-- FURNITURE_PROFESSIONAL_AUDIT_BEGIN -->
## Профессиональная доработка и её доказательства

Приняты перечисленные технические проверки. Полный [отчёт](../Verification/PostApocalypticFurniture/ProfessionalAudit/Report_RU.md), реестр проблем, source/PBR/native проверки и200сравнений находятся в `Verification/PostApocalypticFurniture/ProfessionalAudit`. Фотореференсы не предоставлены; типы взяты из исходного ТЗ. Изменения GUID и локальных prefab IDs проверены отдельно от добавления новых авторских деталей. Движущийся витой провод158 имеет три настоящих mesh LOD и следует за трубкой без собственного ввода/сохранения. Нативная проверка провода: PASS.
<!-- FURNITURE_PROFESSIONAL_AUDIT_END -->
