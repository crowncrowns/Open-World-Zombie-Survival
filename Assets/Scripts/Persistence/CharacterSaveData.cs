using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Progression;

namespace ZombieSurvival.Persistence
{
    // Versioned disk DTOs use type IDs, never Unity instance IDs / ScriptableObject JSON references.
    // Future wallet/trader/quest adapters extend ONE format with migration; no parallel economy save file.
    [Serializable] internal sealed class SaveEnvelope { public string payload, sha256; }
    [Serializable] internal sealed class SavedItem
    {
        public bool occupied;
        public string typeId, instanceId;
        public int quantity, loadedRounds;
        public float durability;
    }
    [Serializable] internal sealed class SavedStorage { public string id; public List<SavedItem> slots=new List<SavedItem>(); }
    [Serializable] internal sealed class SavedWorldItem
    {
        public SavedItem item;
        public Vector3 position, scale;
        public Quaternion rotation;
        public bool active, enabled;
        public bool hasBody;
        public RigidbodyWorldState body;
    }
    [Serializable] internal sealed class CharacterSaveData
    {
        public int version=3;
        public string scene, savedUtc, equippedId;
        public Vector3 position;
        public float yaw, pitch, health, stamina, recoveryWait, food, water;
        public bool prone, crouched, exhausted;
        public ProgressionState progression;
        public string[] hotbar;
        public int hotbarSelected;
        public WorldSceneState worldState;
        public List<SavedStorage> storages=new List<SavedStorage>();
        public List<SavedWorldItem> world=new List<SavedWorldItem>();
    }
}
