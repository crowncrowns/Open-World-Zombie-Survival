using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
    /// <summary>Owned interior wall skin, never part of the authored apartment prefab.</summary>
    public sealed class GeneratedInteriorLining : MonoBehaviour
    {
        [Tooltip("Квартира-владелец облицовки.")] public GeneratedApartment apartment;
        [Tooltip("Фасадный модуль с настоящим оконным/балконным проёмом.")] public Transform facadeModule;
        [Tooltip("Материал фактической боковой стены комнаты.")] public Material roomMaterial;
        [Tooltip("Производные меши только этой облицовки.")] public Mesh[] meshes;
        [Tooltip("Источник тонкой облицовки.")] public GameObject template;
        [Tooltip("Границы комнаты вдоль фасада в локальных координатах модуля.")] public Vector2 interval;
    }
}
