using UnityEngine;

namespace ZombieSurvival.Furniture
{
    /// <summary>Authoring volume for placement. It does not invent inventory items or a second storage system.</summary>
    [DisallowMultipleComponent]
    public sealed class FurniturePlacementZone : MonoBehaviour
    {
        [SerializeField, Tooltip("Свободный внутренний объём в локальных координатах зоны, м.")]
        private Bounds localBounds = new Bounds(Vector3.zero, new Vector3(.4f, .2f, .3f));
        [SerializeField, Tooltip("Название полки или отделения для расстановки предметов.")]
        private string label = "Полка";
        public Bounds LocalBounds => localBounds;
        public string Label => label;
        public void Configure(string displayLabel, Bounds volume) { label = displayLabel; localBounds = volume; }
        public bool ContainsWorldPoint(Vector3 point) => localBounds.Contains(transform.InverseTransformPoint(point));
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.2f, .8f, .9f, .5f); Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        }
    }
}
