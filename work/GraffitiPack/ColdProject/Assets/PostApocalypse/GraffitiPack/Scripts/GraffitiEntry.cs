using UnityEngine;

namespace PostApocalypse.Graffiti
{
    [CreateAssetMenu(menuName = "Graffiti/Entry")]
    public sealed class GraffitiEntry : ScriptableObject
    {
        [Tooltip("Номер композиции в каталоге (1–50).")] public int number;
        [Tooltip("Проверенное русское название композиции.")] public string title;
        [Tooltip("Категория граффити.")] public string category;
        [Tooltip("Исходная цветовая RGBA-текстура в sRGB.")] public Texture2D texture;
        [Tooltip("Независимый префаб с прозрачным освещаемым материалом.")] public GameObject prefab;
        [Tooltip("Ширина композиции по умолчанию, м.")] public float width = 1;
        public float Aspect => texture ? (float)texture.width / texture.height : 1;
    }
}
