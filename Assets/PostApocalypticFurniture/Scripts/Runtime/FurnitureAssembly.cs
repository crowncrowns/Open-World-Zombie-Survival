using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieSurvival.Furniture
{
    /// <summary>One authored piece of furniture. Its mechanisms have stable IDs within this root.</summary>
    [DisallowMultipleComponent]
    public sealed class FurnitureAssembly : MonoBehaviour
    {
        [SerializeField, Tooltip("Постоянный код модели. Не менять после создания сохранения без миграции.")]
        private string assetId;
        [SerializeField, Tooltip("Русское название модели в каталоге.")]
        private string displayName;
        [SerializeField, Tooltip("Категория мебели в каталоге.")]
        private string category;
        [SerializeField, Tooltip("Внутренние лампы отделений, включаемые открытыми внешними дверцами.")]
        private Light[] compartmentLights = Array.Empty<Light>();
        [SerializeField, Tooltip("Внешние дверцы. Свет остаётся включённым, пока открыта хотя бы одна.")]
        private FurnitureMechanism[] lightDoors = Array.Empty<FurnitureMechanism>();
        public string AssetId => assetId;
        public string DisplayName => displayName;
        public string Category => category;
        public void Configure(string id, string label, string group)
        { assetId = id; displayName = label; category = group; }
        public void ConfigureCompartmentLighting(Light[] targets, FurnitureMechanism[] doors)
        { compartmentLights = targets ?? Array.Empty<Light>(); lightDoors = doors ?? Array.Empty<FurnitureMechanism>(); RefreshCompartmentLighting(); }
        public void RefreshCompartmentLighting()
        {
            bool open = false;
            foreach (var door in lightDoors) if (door != null && door.OpenFraction > .03f) { open = true; break; }
            foreach (var lamp in compartmentLights) if (lamp != null) lamp.enabled = open;
        }
        private void OnEnable() => RefreshCompartmentLighting();
        private void LateUpdate() => RefreshCompartmentLighting();

        public FurnitureState CaptureState()
        {
            var motions = GetComponentsInChildren<FurnitureMechanism>(true);
            var devices = GetComponentsInChildren<FurnitureDevice>(true);
            var state = new FurnitureState { marker = 7319, schema = 1, assetId = assetId,
                mechanisms = new FurnitureMechanismState[motions.Length], devices = new FurnitureDeviceState[devices.Length] };
            for (int i = 0; i < motions.Length; i++) state.mechanisms[i] = motions[i].CaptureState();
            for (int i = 0; i < devices.Length; i++) state.devices[i] = devices[i].CaptureState();
            var wear = GetComponent<FurnitureWear>();
            state.hasWear = wear != null; state.wear = wear != null ? wear.CaptureState() : null;
            return state;
        }

        public bool ValidateState(FurnitureState state)
        {
            if (state == null || state.marker != 7319 || state.schema != 1 || state.assetId != assetId
                || state.mechanisms == null || state.devices == null) return false;
            var motions = GetComponentsInChildren<FurnitureMechanism>(true);
            var devices = GetComponentsInChildren<FurnitureDevice>(true);
            if (motions.Length != state.mechanisms.Length || devices.Length != state.devices.Length) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < motions.Length; i++)
            {
                var saved = Find(state.mechanisms, motions[i].StableId);
                if (!ids.Add(motions[i].StableId ?? "") || string.IsNullOrEmpty(motions[i].StableId)
                    || saved == null || !motions[i].ValidateState(saved) || !motions[i].ValidateInterlockState(saved, state.mechanisms)) return false;
            }
            ids.Clear();
            for (int i = 0; i < devices.Length; i++)
            {
                var saved = Find(state.devices, devices[i].StableId);
                if (!ids.Add(devices[i].StableId ?? "") || string.IsNullOrEmpty(devices[i].StableId)
                    || saved == null || !devices[i].ValidateState(saved)) return false;
            }
            var wear = GetComponent<FurnitureWear>();
            return state.hasWear == (wear != null) && (wear == null || wear.ValidateState(state.wear));
        }

        // Validation is deliberately separate: the world-save preflight must not touch live poses/audio.
        public void RestoreStateSilently(FurnitureState state)
        {
            foreach (var motion in GetComponentsInChildren<FurnitureMechanism>(true))
                motion.RestoreStateSilently(Find(state.mechanisms, motion.StableId));
            foreach (var device in GetComponentsInChildren<FurnitureDevice>(true))
                device.RestoreStateSilently(Find(state.devices, device.StableId));
            foreach (var control in GetComponentsInChildren<FurnitureControl>(true)) control.RefreshRestoredVisual();
            if (state.hasWear) GetComponent<FurnitureWear>().RestoreStateSilently(state.wear);
        }
        public void PublishRestoredState()
        {
            foreach (var device in GetComponentsInChildren<FurnitureDevice>(true)) device.RefreshPresentation(true);
            RefreshCompartmentLighting();
        }
        private static FurnitureMechanismState Find(FurnitureMechanismState[] values, string id)
        { FurnitureMechanismState result = null; foreach (var value in values) if (value != null && value.id == id) { if (result != null) return null; result = value; } return result; }
        private static FurnitureDeviceState Find(FurnitureDeviceState[] values, string id)
        { FurnitureDeviceState result = null; foreach (var value in values) if (value != null && value.id == id) { if (result != null) return null; result = value; } return result; }
    }

    [Serializable]
    public sealed class FurnitureState
    {
        public int marker, schema;
        public string assetId;
        public FurnitureMechanismState[] mechanisms;
        public FurnitureDeviceState[] devices;
        public bool hasWear;
        public FurnitureWearState wear;
    }
}
