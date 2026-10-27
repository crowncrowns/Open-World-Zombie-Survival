using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    public sealed class GeneratedPublicFloorFinish : MonoBehaviour
    {
        [Tooltip("Исходный mesh только для отрисовки; коллизия не заменяется.")] public Mesh originalMesh;
        [Tooltip("Исходные материалы всех подмешей.")] public Material[] originalMaterials;
        [Tooltip("Отдельный производный mesh отделки.")] public Mesh finishedMesh;
        [Tooltip("Клетчатый материал общественной зоны.")] public Material checker;
        [Tooltip("Подмеш только верхней поверхности общественного пола.")] public int checkerSubmesh;
    }
}
