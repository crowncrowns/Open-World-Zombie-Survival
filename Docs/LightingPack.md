# LightingPack — актуальная ревизия всех 100 префабов

25.10.2026: переработаны все 100 префабов и все 100 FBX, 25 URP Lit материалов и 197 PNG 2K. GUID 100 префабов и 5231 прежних внутренних fileID сохранены. Исходные файлы до изменения — в `work/LightingPack/ProfessionalAudit/Before`.

Исправлены пустые меши в 12 префабах, профили плафонов/колб, отражатели, крепёж, вводы и декор. UV2 поставляются из Blender. На финальном чистом FBX re-import: 0 открытых и неманифолдных рёбер, 0 вырожденных граней. Отдельный Unity import/cold всех 100, 1276 Play и 25 Editor проверок прошли. Снято 1800 native кадров до/после и подготовлены сравнения для каждого объекта.

Полный отчёт, таблица всех 100 объектов и ограничения: [ProfessionalAudit/Report_RU.md](../Verification/LightingPack/ProfessionalAudit/Report_RU.md).

![Обновлённые Used](../Verification/LightingPack/ProfessionalAudit/After_Used.jpg)

Исходные фото-референсы не предоставлены; точное фото-сходство и AAA не подтверждены. Main Play/build/FPS, baked GI и gameplay/save-интеграция не проверялись. В отчёте отдельно отражены SearchDatabase exception редактора, приближённость коллизий и границы процедурных PBR/стекла. Пользовательские сцены, Packages и ProjectSettings не изменялись.
