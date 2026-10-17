using UnityEngine;
using UnityEngine.AI;

namespace ZombieSurvival.Combat
{
    public enum ActorFaction { [InspectorName("Нейтральные")] Neutral, [InspectorName("Выжившие (игрок / NPC)")] Survivor, [InspectorName("Заражённые (зомби)")] Infected, [InspectorName("Животные")] Wildlife }
    [System.Flags]
    public enum FactionMask { [InspectorName("Никого")] None = 0, [InspectorName("Нейтральные")] Neutral = 1, [InspectorName("Выжившие")] Survivor = 2, [InspectorName("Заражённые")] Infected = 4, [InspectorName("Животные")] Wildlife = 8, [InspectorName("Все")] All = ~0 }

    /// <summary>One identity per actor root, independent of its model, input and brain.</summary>
    [DisallowMultipleComponent]
    public sealed class ActorIdentity : MonoBehaviour
    {
        [Tooltip("Принадлежность этого актёра: выживший, заражённый, нейтральный или животное. Меняется здесь и во вкладке Общие окна настроек актёра.")]
        [SerializeField] private ActorFaction faction = ActorFaction.Neutral;
        [Tooltip("Кого ЭТОТ актёр считает врагом. Выберите несколько фракций галочками: у зомби — Выжившие, у игрока — Заражённые.")]
        [SerializeField] private FactionMask hostileFactions = FactionMask.None;
        [Tooltip("Считать ли врагами старых актёров без ActorIdentity. Для новых NPC всегда добавляйте ActorIdentity на корень.")]
        [SerializeField] private bool targetActorsWithoutIdentity;
        public ActorFaction Faction => faction;
        public FactionMask FactionBit => (FactionMask)(1 << (int)faction);
        public bool IsAlive => DamageReceiver.IsAlive(DamageReceiver.Find(this));
        public bool IsHostileTo(ActorIdentity other) => other != null
            ? other != this && IsHostileTo(other.FactionBit) : targetActorsWithoutIdentity;
        public bool IsHostileTo(FactionMask other) => (hostileFactions & other) != 0;
        public static Transform Root(Transform child)
        {
            if (child == null) return null;
            var identity = child.GetComponentInParent<ActorIdentity>();
            if (identity != null) return identity.transform;
            var target = child.GetComponentInParent<CombatTarget>();
            if (target != null) return target.transform;
            var navigation = child.GetComponentInParent<NavMeshAgent>();
            // Legacy agents still identify their own children. Plain props do not own their scene folder.
            return navigation != null ? navigation.transform : child;
        }
        public static EntityId Entity(Transform child) => child != null ? Root(child).gameObject.GetEntityId() : default;
    }
}
