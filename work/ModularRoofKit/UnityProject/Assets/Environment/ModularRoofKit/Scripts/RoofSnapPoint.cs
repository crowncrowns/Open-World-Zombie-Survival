using UnityEngine;

namespace ZombieSurvival.ModularRoofKit
{
    [DisallowMultipleComponent]
    public sealed class RoofSnapPoint : MonoBehaviour
    {
        [Tooltip("Тип стыка: Pipe, Gutter, Fence, Roof, RoofSide или Flashing.")]
        public string kind;
        [Tooltip("Совместимый диаметр, ширина или высота соединения в метрах.")]
        public float size;
        public bool Compatible(RoofSnapPoint other) => other && other!=this && kind==other.kind && Mathf.Abs(size-other.size)<.0001f;
        void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(.1f,.8f,.9f);Gizmos.DrawWireSphere(transform.position,.018f);
            Gizmos.DrawLine(transform.position,transform.position+transform.forward*.16f);
        }
    }
}
