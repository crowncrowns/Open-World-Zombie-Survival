using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Player;
using ZombieSurvival.Persistence;

/// <summary>Русские названия и оригинальные Tooltip; значения хранятся только в исходных SerializedProperty.</summary>
public class CharacterStage11Inspector : Editor
{
    private static readonly Dictionary<string,string> Labels=new Dictionary<string,string>
    {
        {"needsEnabled","Потребности включены"},{"foodCapacity","Максимальная сытость"},{"waterCapacity","Максимальная вода"},
        {"startingFood","Начальная сытость"},{"startingWater","Начальная вода"},{"foodPerMinute","Расход сытости в минуту"},{"waterPerMinute","Расход воды в минуту"},
        {"sprintMultiplier","Расход при беге"},{"emptyDamagePerSecond","Урон от истощения в секунду"},{"resetOnRespawn","Сброс при возрождении"},{"consumables","Еда и напитки"},
        {"showHud","Показать HUD"},{"item","Тип предмета"},{"food","Восстановление сытости"},{"water","Восстановление воды"},{"healing","Лечение"},
        {"fileName","Имя файла сохранения"},{"itemCatalog","Каталог типов предметов"},{"saveInput","Сохранить"},{"loadInput","Загрузить"},
        {"requireApplicationFocus","Требовать фокус окна"},{"obstructionMask","Слои проверки препятствий"},{"supportDistance","Допуск расстояния до опоры"},{"worldRegistry","Реестр области мира"},
        {"persistentId","Постоянный ID"},{"motionEnabled","Покачивание включено"},{"viewCamera","Камера игрока"},{"amplitude","Амплитуда, м"},
        {"walkFrequency","Частота ходьбы, Гц"},{"runFrequency","Частота бега, Гц"},{"runAmplitude","Амплитуда при беге"},{"blendSpeed","Плавность перехода, м/с"},
        {"action","Действие консоли"},{"interactionEnabled","Взаимодействие включено"},{"displayName","Название"},{"onInteracted","После взаимодействия"}
    };
    public override void OnInspectorGUI()
    {
        serializedObject.Update();var p=serializedObject.GetIterator();bool children=true;
        while(p.NextVisible(children))
        {
            children=false;if(p.name=="m_Script")continue;
            EditorGUILayout.PropertyField(p,new GUIContent(Labels.TryGetValue(p.name,out var name)?name:p.displayName,p.tooltip),true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
[CustomEditor(typeof(PlayerNeeds))] public sealed class PlayerNeedsInspector:CharacterStage11Inspector{}
[CustomEditor(typeof(PlayerSaveSystem))] public sealed class PlayerSaveInspector:CharacterStage11Inspector{}
[CustomEditor(typeof(PlayerCameraMotion))] public sealed class PlayerMotionInspector:CharacterStage11Inspector{}
[CustomEditor(typeof(ConsumableDefinition))] public sealed class ConsumableInspector:CharacterStage11Inspector{}
[CustomEditor(typeof(SaveIdentity))] public sealed class SaveIdentityInspector:CharacterStage11Inspector{}
[CustomEditor(typeof(SurvivalTestConsole))] public sealed class SurvivalConsoleInspector:CharacterStage11Inspector{}
