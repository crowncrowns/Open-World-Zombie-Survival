using UnityEditor;
using UnityEngine;
using ZombieSurvival.Interaction;
using ZombieSurvival.Player;

[CustomEditor(typeof(PlayerInteractor))]
public sealed class PlayerInteractorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var actor=(PlayerInteractor)target; var motor=actor.GetComponent<FirstPersonController>();
        string reason;
        if (!actor.isActiveAndEnabled) reason="Компонент взаимодействия выключен.";
        else if (!motor.IsAlive) reason="Персонаж мёртв.";
        else if (!motor.AcceptingGameplayInput) reason="Игровой ввод закрыт: проверь рюкзак/меню, фокус окна и захват курсора.";
        else if (actor.ViewCamera==null || !actor.ViewCamera.isActiveAndEnabled) reason="Нет активной камеры взаимодействия.";
        else if (motor.Parkour!=null && motor.Parkour.OwnsMovement) reason="Идёт действие паркура.";
        else if (!actor.IsAvailable) reason="Взаимодействие запрещено в текущей позе или в воздухе.";
        else if (actor.Target==null) reason="Управление доступно. Интерактивный объект в центре экрана не выбран: проверь расстояние, препятствия и маски слоёв.";
        else reason="Цель: "+actor.Target.DisplayName+". "+(actor.CanUseTarget?"Действие доступно по "+actor.BindingLabel:"Сама цель запрещает действие.");
        EditorGUILayout.HelpBox(reason,actor.CanUseTarget?MessageType.Info:MessageType.None);
    }
}
