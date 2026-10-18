using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Audio
{
    /// <summary>Общий проигрыватель игрока/NPC: два пространственных канала, здоровье и варианты клипов.
    /// AudioSource не является датчиком AI: GameplayNoise остаётся отдельным контрактом слуха.</summary>
    [DisallowMultipleComponent]
    public sealed class ActorAudioEmitter : MonoBehaviour
    {
        [Tooltip("Общий набор звуков этого актёра. Можно заменить для любого нового монстра или NPC.")]
        [SerializeField] private ActorAudioProfile profile;
        [Tooltip("Канал шагов и движений. Если не назначен, создаётся дочерний AudioSource без проигрывания при запуске.")]
        [SerializeField] private AudioSource movementSource;
        [Tooltip("Отдельный канал голоса, атак и смерти, чтобы шаг не обрывал рычание.")]
        [SerializeField] private AudioSource voiceSource;
        [Tooltip("Общий множитель громкости актёра. Не изменяет шум для AI и индикатор скрытности.")]
        [SerializeField, Range(0f,1f)] private float masterVolume = .8f;
        [Tooltip("Доля трёхмерного позиционирования: 1 — звук идёт от актёра, 0 — одинаков в обоих ушах.")]
        [SerializeField, Range(0f,1f)] private float spatialBlend = 1f;
        [Tooltip("Расстояние полной громкости вокруг актёра, м.")]
        [SerializeField, Min(.1f)] private float minimumDistance = 2f;
        [Tooltip("Предельная дистанция затухания звука актёра, м. Это слышимый звук, не радиус игрового слуха AI.")]
        [SerializeField, Min(1f)] private float maximumDistance = 26f;
        [Tooltip("Слои земли для определения FootstepSurface под ногами.")]
        [SerializeField] private LayerMask groundMask = ~0;
        private Health health;
        private readonly Dictionary<ActorSound,int> previous = new Dictionary<ActorSound,int>();
        public event Action<ActorSound,AudioClip> SoundPlayed;
        public AudioSource MovementSource => movementSource;
        public AudioSource VoiceSource => voiceSource;
        public ActorAudioProfile Profile => profile;
        public bool VoiceIsPlaying => voiceSource != null && voiceSource.isPlaying;
        private void OnEnable() => Initialize();
        private void Initialize()
        {
            Unsubscribe();
            health = GetComponent<Health>();
            if (health != null) { health.Damaged += OnDamaged; health.Died += OnDied; health.Revived += StopAll; health.WorldStateRestored += StopAll; }
            if (movementSource == null) movementSource = CreateSource("Movement audio");
            if (voiceSource == null) voiceSource = CreateSource("Voice audio");
            Configure(movementSource); Configure(voiceSource); StopAll(); previous.Clear();
        }
        private AudioSource CreateSource(string name)
        {
            var existing = transform.Find(name);
            if (existing != null && existing.TryGetComponent<AudioSource>(out var found)) return found;
            var child = new GameObject(name); child.transform.SetParent(transform,false); child.transform.localPosition = Vector3.up * .2f;
            return child.AddComponent<AudioSource>();
        }
        private void Configure(AudioSource source)
        {
            source.playOnAwake = false; source.loop = false; source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = minimumDistance;
            source.maxDistance = Mathf.Max(minimumDistance+.1f,maximumDistance); source.dopplerLevel = 0f;
        }
        private void Unsubscribe()
        {
            if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; health.Revived -= StopAll; health.WorldStateRestored -= StopAll; }
        }
        private void OnDisable() { Unsubscribe(); StopAll(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            foreach (var audio in FindObjectsByType<ActorAudioEmitter>(FindObjectsInactive.Include))
                if (audio.isActiveAndEnabled) audio.Initialize();
        }
        private void OnDamaged(DamageInfo damage) { if (!health.IsDead) Play(ActorSound.Hurt); }
        private void OnDied() { StopAll(); Play(ActorSound.Death); }
        public void StopAll() { if (movementSource != null) movementSource.Stop(); if (voiceSource != null) voiceSource.Stop(); }
        public bool Play(ActorSound sound)
        {
            if (!isActiveAndEnabled || profile == null || masterVolume <= 0f || health != null && health.IsDead && sound != ActorSound.Death) return false;
            bool movement = (int)sound <= (int)ActorSound.Climb;
            var surface = movement ? SurfaceCue(sound) : null;
            var cue = profile.Find(sound) ?? surface;
            if (cue == null || cue.Volume <= 0f) return false;
            var clips = surface != null ? surface.Clips : cue.Clips;
            int index = UnityEngine.Random.Range(0,clips.Length);
            if (clips.Length > 1 && previous.TryGetValue(sound,out int last) && last == index) index = (index+1) % clips.Length;
            for (int i = 0; i < clips.Length && clips[index] == null; i++) index = (index+1) % clips.Length;
            var clip = clips[index]; if (clip == null) return false;
            previous[sound] = index;
            var source = movement ? movementSource : voiceSource;
            if (source == null) return false;
            // Голосовые реплики не накладываются друг на друга; смерть/удар заменяют спокойное рычание.
            source.Stop(); source.pitch = UnityEngine.Random.Range(Mathf.Max(.1f,cue.Pitch.x),Mathf.Max(.1f,cue.Pitch.y));
            source.volume = masterVolume * cue.Volume; source.clip = clip; source.Play();
            SoundPlayed?.Invoke(sound,clip); return true;
        }
        private ActorSoundCue SurfaceCue(ActorSound sound)
        {
            RaycastHit[] hits = Physics.RaycastAll(transform.position+Vector3.up*.3f,Vector3.down,1.5f,groundMask,QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; FootstepSurface surface = null;
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance; surface = hit.collider.GetComponentInParent<FootstepSurface>();
            }
            return surface != null && surface.Profile != null ? surface.Profile.Find(sound) : null;
        }
    }
}
