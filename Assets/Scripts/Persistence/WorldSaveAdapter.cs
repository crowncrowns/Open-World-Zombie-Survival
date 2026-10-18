using System;
using UnityEngine;

namespace ZombieSurvival.Persistence
{
    [Serializable] public sealed class WorldStateBlock
    {
        public string key;
        public int version = 1;
        public string json;
    }
    [Serializable] public sealed class SavedWorldObject
    {
        public string id, prefabId;
        public int prefabRevision;
        public bool spawned, exists = true, active = true;
        public Vector3 position, scale = Vector3.one;
        public Quaternion rotation = Quaternion.identity;
        public bool[] colliders;
        public WorldStateBlock[] blocks;
    }
    [Serializable] public sealed class WorldSceneState
    {
        public int version = 1;
        public string scope;
        public SavedWorldObject[] objects;
    }

    /// <summary>Расширение единого снимка. Validate не меняет мир; ApplySilent не вызывает игровые события.
    /// Publish вызывается только после восстановления мира, денег, слотов и персонажа.
    /// Новый адаптер должен проверять весь свой JSON и не бросать исключения из ApplySilent после Validate.</summary>
    public abstract class WorldSaveAdapter : MonoBehaviour
    {
        public abstract string Key { get; }
        public virtual int Version => 1;
        public abstract string Capture();
        public abstract void Validate(string json, SavedWorldObject owner);
        public abstract void ApplySilent(string json);
        public virtual void Publish() { }
        internal static T Read<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json) || json.Length > 1024 * 1024) throw new InvalidOperationException("Отсутствует или слишком велик блок состояния мира.");
            var value = JsonUtility.FromJson<T>(json);
            return value ?? throw new InvalidOperationException("Пустой блок состояния мира.");
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool ValidPosition(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && value.sqrMagnitude < 1e12f;
    }
}
