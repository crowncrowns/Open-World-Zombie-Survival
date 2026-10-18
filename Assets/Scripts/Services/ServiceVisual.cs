using UnityEngine;

namespace ZombieSurvival.Services
{
    /// <summary>Owns one replaceable appearance. Service state and physical access belong to its parent.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ServiceVisual : MonoBehaviour
    {
        [SerializeField, InspectorName("Префаб модели"), Tooltip("Готовый визуальный prefab из Project: Renderer, rig и Animator. Любые MonoBehaviour и NavMesh-компоненты отклоняются ДО создания; старый вид сохраняется. Полный NPC с AI нельзя использовать как декорацию.")]
        private GameObject visualPrefab;
        [SerializeField, InspectorName("Смещение модели, м"), Tooltip("Локальное смещение модели относительно корня сервиса, м. Корень сессии и его состояние не перемещаются.")]
        private Vector3 localPosition;
        [SerializeField, InspectorName("Поворот модели, градусы"), Tooltip("Локальный поворот визуальной модели вокруг корня сервиса, градусы.")]
        private Vector3 localEulerAngles;
        [SerializeField, InspectorName("Масштаб модели"), Tooltip("Дополнительный масштаб к исходному масштабу prefab. Значения должны быть положительными. Исходный prefab не изменяется.")]
        private Vector3 localScale = Vector3.one;
        [SerializeField, InspectorName("Материал модели (необязательно)"), Tooltip("Если назначен, заменяет материалы только управляемого экземпляра. None сохраняет материалы исходного prefab; для персонажа выбирайте URP-версию модели.")]
        private Material materialOverride;
        [SerializeField, InspectorName("Подгонять коллайдер доступа"), Tooltip("Подгонять указанный корневой BoxCollider по Renderer bounds модели после смены/настройки. Не выполняется каждый кадр анимации. None в поле модели сохраняет прежний коллайдер.")]
        private bool fitAccessCollider = true;
        [SerializeField, InspectorName("Корневой BoxCollider доступа"), Tooltip("Физический BoxCollider НА КОРНЕ сервиса, вне заменяемой модели. Если не назначен, берётся BoxCollider этого объекта. ServicePoint должен ссылаться на этот же коллайдер.")]
        private BoxCollider rootCollider;
        [SerializeField, HideInInspector] private Transform visualRoot;
        [SerializeField, HideInInspector] private GameObject instance, appliedPrefab;
        [SerializeField, HideInInspector] private Material appliedMaterial;
        private string validationMessage;
        public GameObject VisualPrefab => visualPrefab;
        public GameObject Instance => instance;
        public string ValidationMessage => validationMessage;

        private void OnEnable() => RequestRefresh();
        private void OnValidate() => RequestRefresh();
        private void RequestRefresh()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
            UnityEditor.EditorApplication.delayCall -= RefreshDeferred;
            UnityEditor.EditorApplication.delayCall += RefreshDeferred;
#else
            ApplyModel();
#endif
        }
#if UNITY_EDITOR
        private void RefreshDeferred() { if (this != null && isActiveAndEnabled && !UnityEditor.EditorUtility.IsPersistent(this)) ApplyModel(); }
        private void OnDisable() => UnityEditor.EditorApplication.delayCall -= RefreshDeferred;
#endif
        public bool SetModel(GameObject prefab)
        {
            Record(this); visualPrefab = prefab; return ApplyModel();
        }

        public bool ApplyModel()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshDeferred;
            if (UnityEditor.EditorUtility.IsPersistent(this))
            { validationMessage = "Откройте prefab в Prefab Mode или измените модель экземпляра в сцене."; return false; }
#endif
            if (!ValidateModel(visualPrefab, out validationMessage)) return false;
            if (visualRoot != null && visualRoot.parent != transform
                || instance != null && (visualRoot == null || instance.transform.parent != visualRoot))
            { validationMessage = "Управляемый узел модели перемещён за пределы владельца. Замена отменена, чужие объекты не удалены."; return false; }
            if (localScale.x <= 0 || localScale.y <= 0 || localScale.z <= 0 || !Finite(localScale) || !Finite(localPosition) || !Finite(localEulerAngles))
            { validationMessage = "Нужны конечные значения позиции/поворота и положительный масштаб."; return false; }
            Record(this);
            if (rootCollider == null) rootCollider = GetComponent<BoxCollider>();
            if (fitAccessCollider && rootCollider != null && rootCollider.transform != transform)
            { validationMessage = "BoxCollider доступа должен находиться на корне сервиса."; return false; }
            if (visualRoot == null && visualPrefab != null)
            {
                var anchor = new GameObject("Service visual (managed)");
                anchor.transform.SetParent(transform, false); visualRoot = anchor.transform;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(anchor, "Создать узел модели сервиса");
#endif
            }
            if (appliedPrefab != visualPrefab || appliedMaterial != materialOverride || visualPrefab != null && instance == null)
            {
                GameObject replacement = null;
                if (visualPrefab != null)
                {
                    // Native cameras/audio/physics cannot activate before the instance is configured.
                    var staging = new GameObject("Service visual staging (temporary)"); staging.SetActive(false);
                    try
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            replacement = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(visualPrefab, staging.transform);
                        }
                        else
#endif
                            replacement = Instantiate(visualPrefab, staging.transform);
                        Configure(replacement);
                        replacement.transform.SetParent(visualRoot, false);
#if UNITY_EDITOR
                        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(replacement, "Создать модель сервиса");
#endif
                    }
                    finally
                    {
                        // This temporary node is never part of user Undo history.
#if UNITY_EDITOR
                        if (!Application.isPlaying) DestroyImmediate(staging);
                        else
#endif
                            Destroy(staging);
                    }
                }
                // Validate/create first; only our previous managed child is removed.
                Remove(instance); instance = replacement; appliedPrefab = visualPrefab; appliedMaterial = materialOverride;
            }
            if (instance != null) { Configure(instance); FitCollider(); }
            validationMessage = null;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorUtility.SetDirty(this); UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this); }
#endif
            return true;
        }
        public bool ValidateModel(GameObject prefab, out string reason)
        {
            reason = null; if (prefab == null) return true;
            if (prefab.scene.IsValid()) { reason = "Назначьте prefab-ассет из Project, а не объект сцены."; return false; }
#if UNITY_EDITOR
            if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab)) { reason = "Нужен готовый prefab или импортированная модель из Project."; return false; }
#endif
            foreach (var script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            { reason = "Нужен визуальный prefab без MonoBehaviour: найден " + (script == null ? "отсутствующий скрипт" : script.GetType().Name) + ". Старый вид сохранён."; return false; }
            if (prefab.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) != null || prefab.GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>(true) != null)
            { reason = "Навигация должна оставаться на корне актёра. Выберите визуальный prefab без NavMesh-компонентов."; return false; }
            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0) { reason = "В prefab нет Renderer модели."; return false; }
            return true;
        }
        private void Configure(GameObject model)
        {
            Record(model.transform);
            model.transform.localPosition = localPosition;
            model.transform.localRotation = Quaternion.Euler(localEulerAngles) * visualPrefab.transform.localRotation;
            model.transform.localScale = Vector3.Scale(visualPrefab.transform.localScale, localScale); Mark(model.transform);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) { Record(c); c.enabled = false; Mark(c); }
            foreach (var b in model.GetComponentsInChildren<Rigidbody>(true)) { Record(b); b.isKinematic = true; b.detectCollisions = false; Mark(b); }
            foreach (var c in model.GetComponentsInChildren<Camera>(true)) { Record(c); c.enabled = false; Mark(c); }
            foreach (var l in model.GetComponentsInChildren<AudioListener>(true)) { Record(l); l.enabled = false; Mark(l); }
            foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) { Record(audio); audio.playOnAwake = false; audio.enabled = false; Mark(audio); }
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) { Record(animator); animator.applyRootMotion = false; Mark(animator); }
            if (materialOverride != null)
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    Record(renderer);
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = materialOverride;
                    renderer.sharedMaterials = materials; Mark(renderer);
                }
        }
        private void FitCollider()
        {
            if (!fitAccessCollider || rootCollider == null || instance == null) return;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool first = true; Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                // SkinnedMeshRenderer localBounds can be relative to its root bone, not its Transform.
                // World bounds include that skinning transform; convert their corners to the service root.
                var b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = transform.InverseTransformPoint(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            if (first) return; Record(rootCollider);
            rootCollider.center = bounds.center; rootCollider.size = Vector3.Max(bounds.size, Vector3.one * .05f);
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorUtility.SetDirty(rootCollider); UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(rootCollider); }
#endif
        }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        private static void Record(Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObject(target, "Настроить модель сервиса");
#endif
        }
        private static void Mark(Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorUtility.SetDirty(target); UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target); }
#endif
        }
        private static void Remove(GameObject obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.Undo.DestroyObjectImmediate(obj); return; }
#endif
            obj.SetActive(false); Destroy(obj);
        }
    }
}
