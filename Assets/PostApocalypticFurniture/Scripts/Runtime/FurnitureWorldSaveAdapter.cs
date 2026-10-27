using System;
using UnityEngine;
using ZombieSurvival.Persistence;

namespace ZombieSurvival.Furniture
{
    /// <summary>Participates in the existing atomic world snapshot; no separate file or runtime IDs.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(FurnitureAssembly)), RequireComponent(typeof(WorldSaveObject))]
    public sealed class FurnitureWorldSaveAdapter : WorldSaveAdapter
    {
        public override string Key => "apartment-furniture";
        public override string Capture() => JsonUtility.ToJson(GetComponent<FurnitureAssembly>().CaptureState());
        public override void Validate(string json, SavedWorldObject owner)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 1024 * 1024)
                throw new InvalidOperationException("Отсутствует или слишком велик блок мебели: " + owner.id);
            var state = JsonUtility.FromJson<FurnitureState>(json);
            if (!GetComponent<FurnitureAssembly>().ValidateState(state))
                throw new InvalidOperationException("Изменился состав или повреждено состояние мебели: " + owner.id);
        }
        public override void ApplySilent(string json)
            => GetComponent<FurnitureAssembly>().RestoreStateSilently(JsonUtility.FromJson<FurnitureState>(json));
        public override void Publish() => GetComponent<FurnitureAssembly>().PublishRestoredState();
    }
}
