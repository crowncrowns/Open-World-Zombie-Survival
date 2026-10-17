using System;
using UnityEngine;

namespace ZombieSurvival.AI
{
    /// <summary>Gameplay hearing events, independent from AudioSource playback.</summary>
    public static class GameplayNoise
    {
        public readonly struct Signal
        {
            public readonly Vector3 Position;
            public readonly float Loudness;
            public readonly float Range;
            public readonly EntityId EmitterId;

            public Signal(Vector3 position, float loudness, float range, EntityId emitterId)
            {
                Position = position;
                Loudness = loudness;
                Range = range;
                EmitterId = emitterId;
            }
        }

        public static event Action<Signal> Emitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Emitted = null;

        public static void Emit(Vector3 position, float loudness, float range, GameObject emitter = null)
        {
            if (loudness <= 0f || range <= 0f) return;
            Emitted?.Invoke(new Signal(position, loudness, range,
                emitter != null ? emitter.transform.root.gameObject.GetEntityId() : default));
        }
    }
}
