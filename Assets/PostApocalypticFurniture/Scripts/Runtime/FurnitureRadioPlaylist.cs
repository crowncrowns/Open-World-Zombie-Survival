using System;
using UnityEngine;

namespace ZombieSurvival.Furniture
{
    [Serializable]
    public sealed class FurnitureRadioStation
    {
        [Tooltip("Название вымышленной радиостанции.")]
        public string label = "Частота";
        [Tooltip("Разрешённый для игры AudioClip. Можно заменить собственным лицензированным контентом.")]
        public AudioClip clip;
    }
    [CreateAssetMenu(menuName = "Zombie Survival/Мебель/Плейлист радио", fileName = "FurnitureRadioPlaylist")]
    public sealed class FurnitureRadioPlaylist : ScriptableObject
    {
        [SerializeField, Tooltip("Станции и оригинальный либо лицензированный радиоконтент.")]
        private FurnitureRadioStation[] stations = Array.Empty<FurnitureRadioStation>();
        public int Count => stations != null ? stations.Length : 0;
        public FurnitureRadioStation Get(int index) => Count > 0 ? stations[Mathf.Clamp(index, 0, Count - 1)] : null;
        public void Configure(FurnitureRadioStation[] values) { stations = values ?? Array.Empty<FurnitureRadioStation>(); }
    }
}
