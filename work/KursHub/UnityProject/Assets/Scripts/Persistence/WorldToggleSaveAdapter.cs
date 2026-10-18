using System;
using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(ToggleInteractable))]
    public sealed class WorldToggleSaveAdapter : WorldSaveAdapter
    {
        [Serializable] private sealed class State { public bool on; public int marker = 1; }
        public override string Key => "toggle";
        public override string Capture() => JsonUtility.ToJson(new State { on = GetComponent<ToggleInteractable>().IsOn });
        public override void Validate(string json, SavedWorldObject owner)
        { if (Read<State>(json).marker != 1) throw new InvalidOperationException("Некорректный блок переключателя."); }
        public override void ApplySilent(string json) => GetComponent<ToggleInteractable>().RestoreStateSilently(Read<State>(json).on);
        public override void Publish() => GetComponent<ToggleInteractable>().NotifyWorldRestore();
    }
}
