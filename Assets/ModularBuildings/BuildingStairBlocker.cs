using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    [DisallowMultipleComponent]
    public sealed class BuildingStairBlocker : MonoBehaviour
    {
        [Tooltip("Будущий prefab обвалившейся лестницы/мусора. После назначения нажмите кнопку замены в Inspector.")]
        public GameObject replacementPrefab;
        [HideInInspector] public GameObject generatedVisual;
        [Tooltip("Отдельная постоянная коллизия сохраняет непроходимость после замены внешнего вида.")]
        public BoxCollider passageBlocker;
    }
}
