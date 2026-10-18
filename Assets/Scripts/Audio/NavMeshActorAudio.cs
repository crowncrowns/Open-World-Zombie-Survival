using UnityEngine;
using UnityEngine.AI;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;

namespace ZombieSurvival.Audio
{
    /// <summary>Адаптер движения и атак любого NPC с NavMeshAgent, а не только ZombieMale.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ActorAudioEmitter),typeof(NavMeshAgent))]
    public sealed class NavMeshActorAudio : MonoBehaviour
    {
        [Tooltip("Пройденное расстояние между обычными шагами NPC, м.")]
        [SerializeField, Min(.1f)] private float walkStride = .9f;
        [Tooltip("Пройденное расстояние между шагами бегущего NPC, м.")]
        [SerializeField, Min(.1f)] private float runStride = 1.7f;
        [Tooltip("Пройденное расстояние между шорохами ползания NPC, м.")]
        [SerializeField, Min(.1f)] private float crawlStride = .8f;
        [Tooltip("Скорость, начиная с которой звучат клипы бега, м/с.")]
        [SerializeField, Min(.1f)] private float runThreshold = 2f;
        [Tooltip("Интервал спокойного рычания: X — минимум, Y — максимум, с. При атаке отдельный звук.")]
        [SerializeField] private Vector2 voiceInterval = new Vector2(4f,9f);
        [Tooltip("Разрешить периодические голосовые звуки. Для молчаливых NPC выключите.")]
        [SerializeField] private bool idleVoice = true;
        [Tooltip("Перемещение за кадр больше этого значения не считается шагами, м. Отсекает телепортацию.")]
        [SerializeField, Min(1f)] private float teleportDistance = 4f;
        private ActorAudioEmitter emitter;
        private NavMeshAgent agent;
        private EnemyCrawlPursuit crawl;
        private EnemyAttack[] attacks;
        private IDamageable health;
        private Vector3 lastPosition;
        private float travelled,nextVoice;
        private ActorSound previousGait;
        private void OnEnable() => Initialize();
        private void Initialize()
        {
            Unsubscribe(); emitter=GetComponent<ActorAudioEmitter>(); agent=GetComponent<NavMeshAgent>();
            crawl=GetComponent<EnemyCrawlPursuit>(); health=DamageReceiver.Find(this); attacks=GetComponents<EnemyAttack>();
            foreach (var attack in attacks) attack.AnimationStarted += Attack;
            lastPosition=transform.position; travelled=0f; ScheduleVoice();
        }
        private void OnDisable() => Unsubscribe();
        internal void ResetAfterWorldLoad() { lastPosition=transform.position;travelled=0;ScheduleVoice(); }
        private void Unsubscribe() { if (attacks != null) foreach (var attack in attacks) if (attack != null) attack.AnimationStarted -= Attack; }
        private void Attack(EnemyAttack attack) { emitter.Play(ActorSound.Attack); ScheduleVoice(); }
        private void ScheduleVoice() => nextVoice=Time.time+Random.Range(Mathf.Max(1f,voiceInterval.x),Mathf.Max(1f,voiceInterval.y));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay()
        {
            foreach (var audio in FindObjectsByType<NavMeshActorAudio>(FindObjectsInactive.Include)) if (audio.isActiveAndEnabled) audio.Initialize();
        }
        private void LateUpdate()
        {
            Vector3 delta=Vector3.ProjectOnPlane(transform.position-lastPosition,Vector3.up); lastPosition=transform.position;
            if (Time.deltaTime<=0f || !DamageReceiver.IsAlive(health) || !agent.enabled || !agent.isOnNavMesh) { travelled=0f; return; }
            bool busy=false; foreach (var attack in attacks) if (attack.IsBusy) busy=true;
            if (idleVoice && !busy && Time.time>=nextVoice && !emitter.VoiceIsPlaying) { emitter.Play(ActorSound.Voice); ScheduleVoice(); }
            if (busy || crawl!=null && crawl.IsTransitioning || delta.sqrMagnitude<.000001f || delta.sqrMagnitude>teleportDistance*teleportDistance) { travelled=0f; return; }
            var gait=crawl!=null && crawl.IsCrawling ? ActorSound.Crawl : delta.magnitude/Time.deltaTime>=runThreshold ? ActorSound.Run : ActorSound.Walk;
            if (gait!=previousGait) { travelled=0f;previousGait=gait; }
            travelled+=delta.magnitude;
            float stride=gait==ActorSound.Crawl ? crawlStride : gait==ActorSound.Run ? runStride : walkStride;
            if (travelled>=stride) { travelled%=stride; emitter.Play(gait); }
        }
    }
}
