using System;
using UnityEngine;

namespace ZombieSurvival.Audio
{
    public enum ActorSound
    {
        [InspectorName("Шаг")] Walk, [InspectorName("Бег")] Run, [InspectorName("Тихий шаг (Alt)")] Quiet,
        [InspectorName("Приседание")] Crouch, [InspectorName("Ползание")] Crawl,
        [InspectorName("Прыжок")] Jump, [InspectorName("Приземление")] Land, [InspectorName("Перекат")] Roll,
        [InspectorName("Подтягивание")] Climb, [InspectorName("Голос / рычание")] Voice,
        [InspectorName("Атака")] Attack, [InspectorName("Получение урона")] Hurt, [InspectorName("Смерть")] Death
    }

    [Serializable]
    public sealed class ActorSoundCue
    {
        [Tooltip("Действие, которому соответствует этот набор звуков.")]
        [SerializeField] private ActorSound sound;
        [Tooltip("Варианты клипов. Выбираются случайно без немедленного повторения одного варианта.")]
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [Tooltip("Громкость этого действия от 0 до 1. На игровой слух зомби не влияет — он настраивается в PlayerNoiseEmitter.")]
        [SerializeField, Range(0f,1f)] private float volume = .6f;
        [Tooltip("Диапазон случайной высоты тона: X — минимум, Y — максимум. 1 — исходный тон.")]
        [SerializeField] private Vector2 pitch = new Vector2(.95f,1.05f);
        public ActorSound Sound => sound;
        public AudioClip[] Clips => clips;
        public float Volume => volume;
        public Vector2 Pitch => pitch;
    }

    /// <summary>Общий неизменяемый набор звуков. Состояние вариантов хранит конкретный ActorAudioEmitter.</summary>
    [CreateAssetMenu(menuName="Zombie Survival/Звуки актёра",fileName="ActorAudioProfile")]
    public sealed class ActorAudioProfile : ScriptableObject
    {
        [Tooltip("Наборы звуков по действиям. Не добавляйте два набора одного действия: используется первый.")]
        [SerializeField] private ActorSoundCue[] cues = Array.Empty<ActorSoundCue>();
        public ActorSoundCue Find(ActorSound sound)
        {
            foreach (var cue in cues) if (cue != null && cue.Sound == sound && cue.Clips != null && cue.Clips.Length > 0) return cue;
            return null;
        }
    }
}
