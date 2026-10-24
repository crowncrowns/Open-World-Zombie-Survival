using UnityEngine;

namespace ZombieSurvival.ModularRoofKit
{
    [DisallowMultipleComponent]
    public sealed class RoofKitModule : MonoBehaviour
    {
        [Tooltip("Категория независимого модуля кровли, водостока или ограждения.")]
        public string category;
        [Tooltip("Семейство взаимозаменяемых секций с одинаковыми соединениями.")]
        public string family;
        [Tooltip("Состояние геометрии и покрытия этого префаба.")]
        public string condition;
        [Tooltip("Длина прямой секции в метрах; 0 для фасонных деталей.")]
        public float length;
        [Tooltip("Seed небольшого оттеночного разброса. Геометрия задаётся отдельным префабом.")]
        public int variationSeed=17;
        [Range(0,.12f),Tooltip("Максимальное отклонение яркости покрытия, доля 0–0,12.")]
        public float tintVariation=.045f;

        void OnEnable() => ApplyVariation();
        public void ApplyVariation()
        {
            // A property block keeps shared project materials and their GUIDs unchanged.
            var random=new System.Random(variationSeed);
            float k=1+((float)random.NextDouble()*2-1)*tintVariation;
            var block=new MaterialPropertyBlock();
            foreach(var r in GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(block);block.SetColor("_BaseColor",new Color(k,k*.998f,k*.992f,1));r.SetPropertyBlock(block);block.Clear();
            }
        }
    }
}
