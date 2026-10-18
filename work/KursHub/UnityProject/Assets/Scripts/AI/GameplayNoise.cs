using System;
using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.AI
{
    public enum NoiseKind { Generic, Footstep, Jump, Landing, Roll, Climb, Interaction, Gunshot, Melee }
    /// <summary>Gameplay hearing events, independent from AudioSource playback.</summary>
    public static class GameplayNoise
    {
        public readonly struct Signal
        {
            public readonly Vector3 Position;
            public readonly float Loudness;
            public readonly float Range;
            public readonly EntityId EmitterId;
            public readonly NoiseKind Kind;
            public readonly FactionMask SourceFaction;

            public Signal(Vector3 position, float loudness, float range, EntityId emitterId,
                NoiseKind kind = NoiseKind.Generic, FactionMask sourceFaction = FactionMask.None)
            {
                Position = position;
                Loudness = loudness;
                Range = range;
                EmitterId = emitterId;
                Kind = kind; SourceFaction = sourceFaction;
            }
        }

        public static event Action<Signal> Emitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Emitted = null;

        public static void Emit(Vector3 position, float loudness, float range, GameObject emitter = null, NoiseKind kind = NoiseKind.Generic)
        {
            if (loudness <= 0f || range <= 0f || float.IsNaN(loudness) || float.IsInfinity(loudness)
                || float.IsNaN(range) || float.IsInfinity(range)) return;
            var actor = emitter != null ? emitter.GetComponentInParent<ActorIdentity>() : null;
            Emitted?.Invoke(new Signal(position, loudness, range,
                emitter != null ? ActorIdentity.Entity(emitter.transform) : default,
                kind, actor != null ? actor.FactionBit : FactionMask.None));
        }
    }
}
