using UnityEngine;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;
using ZombieSurvival.Player;

namespace ZombieSurvival.Audio
{
    /// <summary>Шаг звучит ровно в момент игрового события шума, без второго несинхронного таймера.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ActorAudioEmitter),typeof(PlayerNoiseEmitter))]
    public sealed class PlayerMovementAudio : MonoBehaviour
    {
        private FirstPersonController motor;
        private ActorAudioEmitter emitter;
        private void OnEnable()
        {
            motor = GetComponent<FirstPersonController>(); emitter = GetComponent<ActorAudioEmitter>();
            GameplayNoise.Emitted -= Hear; GameplayNoise.Emitted += Hear;
        }
        private void OnDisable() => GameplayNoise.Emitted -= Hear;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            foreach (var audio in FindObjectsByType<PlayerMovementAudio>(FindObjectsInactive.Include)) if (audio.isActiveAndEnabled) audio.OnEnable();
        }
        private void Hear(GameplayNoise.Signal signal)
        {
            if (motor == null || !motor.IsAlive || signal.EmitterId != ActorIdentity.Entity(transform)) return;
            switch (signal.Kind)
            {
                case NoiseKind.Footstep: emitter.Play(motor.IsProne ? ActorSound.Crawl : motor.IsCrouching ? ActorSound.Crouch
                    : motor.IsSprinting ? ActorSound.Run : motor.IsSlowWalking ? ActorSound.Quiet : ActorSound.Walk); break;
                case NoiseKind.Jump: emitter.Play(ActorSound.Jump); break;
                case NoiseKind.Landing: emitter.Play(ActorSound.Land); break;
                case NoiseKind.Roll: emitter.Play(ActorSound.Roll); break;
                case NoiseKind.Climb: emitter.Play(ActorSound.Climb); break;
            }
        }
    }
}
