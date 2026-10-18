using System;
using UnityEngine;
using ZombieSurvival.Interaction;

namespace ZombieSurvival.Persistence
{
    [DisallowMultipleComponent, RequireComponent(typeof(DoorInteractable))]
    public sealed class WorldDoorSaveAdapter : WorldSaveAdapter
    {
        public override string Key => "door";
        public override string Capture() => JsonUtility.ToJson(GetComponent<DoorInteractable>().CaptureWorldState());
        public override void Validate(string json, SavedWorldObject owner)
        {
            var state = Read<DoorWorldState>(json);
            if (!GetComponent<DoorInteractable>().ValidateWorldState(state)) throw new InvalidOperationException("Некорректное состояние двери: " + owner.id);
        }
        public override void ApplySilent(string json) => GetComponent<DoorInteractable>().RestoreWorldState(Read<DoorWorldState>(json));
    }
}
