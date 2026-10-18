using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        public enum FirstPersonVisibility { [InspectorName("Скрыть выбранные меши")] HideSelectedRenderers, [InspectorName("Только тени")] ShadowsOnly, [InspectorName("Показать полную модель")] ShowWholeModel }

        [Header("Внешность")]
        [Tooltip("Префаб только внешности, без контроллера игрока. Для применения используйте кнопку замены модели.")]
        [SerializeField] private GameObject modelPrefab;
        [Tooltip("Точка крепления внешности на игроке, вне заменяемой модели.")]
        [SerializeField] private Transform modelAnchor;
        [Tooltip("Локальное смещение внешности относительно точки крепления, м.")]
        [SerializeField] private Vector3 localPosition;
        [Tooltip("Локальный поворот внешности относительно точки крепления, градусов.")]
        [SerializeField] private Vector3 localEulerAngles;
        [Tooltip("Локальный масштаб внешности. Физическая капсула игрока не изменяется.")]
        [SerializeField] private Vector3 localScale = Vector3.one;
        [Tooltip("Отключать коллайдеры заменяемой внешности, чтобы они не мешали основной капсуле.")]
        [SerializeField] private bool disableModelColliders = true;

        [Header("Вид от первого лица")]
        [Tooltip("Собственная камера игрока, для которой применяется скрытие верхней части модели.")]
        [SerializeField] private Camera firstPersonCamera;
        [Tooltip("Способ скрытия верхней части модели от собственной камеры. Другие камеры видят полную модель.")]
        [SerializeField] private FirstPersonVisibility firstPersonVisibility = FirstPersonVisibility.HideSelectedRenderers;

        [Header("Анимация")]
        [Tooltip("Передавать движение и состояния игрока в параметры Animator.")]
        [SerializeField] private bool driveAnimator = true;
        [Tooltip("Общий множитель скорости анимации модели.")]
        [SerializeField, Min(0f)] private float animationPlaybackSpeed = 1f;
        [Tooltip("Физическая скорость, соответствующая клипу ходьбы, м/с. Нужна для настройки смешивания анимаций.")]
        [SerializeField, Min(0.01f)] private float walkAnimationSpeed = 4f;
        [Tooltip("Физическая скорость, соответствующая клипу бега, м/с. Нужна для смешивания анимации.")]
        [SerializeField, Min(0.01f)] private float runAnimationSpeed = 7f;
        [Tooltip("Физическая скорость, соответствующая клипу ходьбы сидя, м/с.")]
        [SerializeField, Min(0.01f)] private float crouchAnimationSpeed = 2f;
        [Tooltip("Время сглаживания параметров движения Animator, с.")]
        [SerializeField, Min(0f)] private float animationDamping = 0.1f;
        [Tooltip("Имя параметра Animator для движения вбок. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string moveXParameter = "MoveX";
        [Tooltip("Имя параметра Animator для движения вперёд/назад. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string moveZParameter = "MoveZ";
        [Tooltip("Имя параметра Animator для скорости движения. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string speedParameter = "Speed";
        [Tooltip("Имя параметра Animator для приседания. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string crouchParameter = "Crouched";
        [Tooltip("Имя параметра Animator для контакта с землёй. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string groundedParameter = "Grounded";
        [Tooltip("Имя параметра Animator для вертикальной скорости. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string verticalSpeedParameter = "VerticalSpeed";
        [Tooltip("Имя параметра Animator для текущего действия паркура. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string parkourStateParameter = "ParkourState";
        [Tooltip("Имя параметра Animator для положения лёжа. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string proneParameter = "Prone";
        [Tooltip("Имя параметра Animator для смерти. Должно совпадать с контроллером модели; отсутствующий параметр пропускается.")]
        [SerializeField] private string deadParameter = "Dead";

        [Tooltip("Текущий экземпляр внешности, обслуживается PlayerVisual автоматически.")]
        [SerializeField, HideInInspector] private GameObject modelInstance;
        [Tooltip("Служебная ссылка на применённый префаб внешности; обновляется PlayerVisual.")]
        [SerializeField, HideInInspector] private GameObject appliedPrefab;
        private CharacterController capsule;
        private FirstPersonController locomotion;
        private Animator animator;
        private RuntimeAnimatorController cachedController;
        private readonly Dictionary<int, AnimatorControllerParameterType> parameters = new Dictionary<int, AnimatorControllerParameterType>();
        private Renderer[] hiddenRenderers = new Renderer[0];
        private Renderer[] allRenderers = new Renderer[0];
        private Renderer[] firstPersonOnlyRenderers = new Renderer[0];
        private readonly Dictionary<Renderer, ShadowCastingMode> savedShadowModes = new Dictionary<Renderer, ShadowCastingMode>();
        private readonly Dictionary<Renderer, bool> savedRendererStates = new Dictionary<Renderer, bool>();

        public GameObject ModelPrefab => modelPrefab;
        public GameObject ModelInstance => modelInstance;
        public Animator ModelAnimator => animator;

        private void Awake()
        {
            capsule = GetComponent<CharacterController>();
            locomotion = GetComponent<FirstPersonController>();
            if (firstPersonCamera == null) firstPersonCamera = GetComponentInChildren<Camera>();
            if (modelInstance == null || appliedPrefab != modelPrefab) RebuildModel();
            else ConfigureInstance();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            RestoreRenderers();
        }

        /// <summary>Replace appearance at runtime, preserving the player's capsule, camera and input.</summary>
        public void SetModel(GameObject prefab)
        {
            modelPrefab = prefab;
            RebuildModel();
        }

        [ContextMenu("Apply / Replace Model")]
        public void RebuildModel()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && modelPrefab != null && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(modelPrefab))
            {
                Debug.LogError("Model Prefab must be a prefab asset from the Project window.", this);
                return;
            }
#endif
            if (modelAnchor != null && (!modelAnchor.IsChildOf(transform)
                || modelInstance != null && modelAnchor.IsChildOf(modelInstance.transform)))
            {
                Debug.LogError("Model Anchor must belong to the player, outside the replaceable model.", this);
                return;
            }
            if (modelPrefab != null && (modelPrefab.GetComponentInChildren<PlayerVisual>(true) != null
                || modelPrefab.GetComponentInChildren<FirstPersonController>(true) != null))
            {
                Debug.LogError("Choose a visual-only model prefab, not a complete player prefab.", this);
                return;
            }
            RestoreRenderers();
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, "Replace player model");
#endif
            if (modelAnchor == null)
            {
                var anchor = new GameObject("VisualRoot");
                anchor.transform.SetParent(transform, false);
                modelAnchor = anchor.transform;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(anchor, "Create visual root");
#endif
            }
            if (modelInstance != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(modelInstance);
                else
#endif
                {
                    modelInstance.SetActive(false);
                    Destroy(modelInstance);
                }
            }
            modelInstance = null;
            appliedPrefab = modelPrefab;
            if (modelPrefab != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    modelInstance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(modelPrefab, modelAnchor);
                    UnityEditor.Undo.RegisterCreatedObjectUndo(modelInstance, "Create player model");
                }
                else
#endif
                    modelInstance = Instantiate(modelPrefab, modelAnchor);
            }
            ConfigureInstance();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }

        private void ConfigureInstance()
        {
            animator = null;
            cachedController = null;
            parameters.Clear();
            hiddenRenderers = new Renderer[0];
            allRenderers = new Renderer[0];
            firstPersonOnlyRenderers = new Renderer[0];
            if (modelInstance == null) return;
            modelInstance.transform.localPosition = localPosition;
            modelInstance.transform.localRotation = Quaternion.Euler(localEulerAngles);
            modelInstance.transform.localScale = localScale;
            var definition = modelInstance.GetComponentInChildren<PlayerModelDefinition>(true);
            animator = definition != null && definition.ModelAnimator != null ? definition.ModelAnimator
                : modelInstance.GetComponentInChildren<Animator>(true);
            hiddenRenderers = definition != null ? definition.FirstPersonHiddenRenderers : new Renderer[0];
            firstPersonOnlyRenderers = definition != null ? definition.FirstPersonOnlyRenderers : new Renderer[0];
            foreach (var renderer in firstPersonOnlyRenderers)
            {
                if (renderer == null) continue;
                renderer.enabled = true;
                renderer.forceRenderingOff = true;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
            allRenderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            // The model supplies visuals only; physics and camera belong to the player root.
            if (disableModelColliders)
            {
                foreach (var collider in modelInstance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in modelInstance.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
            }
            foreach (var modelCamera in modelInstance.GetComponentsInChildren<Camera>(true)) modelCamera.enabled = false;
            foreach (var listener in modelInstance.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var modelAnimator in modelInstance.GetComponentsInChildren<Animator>(true)) modelAnimator.applyRootMotion = false;
            if (animator != null) animator.speed = animationPlaybackSpeed;
            if (Application.isPlaying && animator != null && animator.isHuman)
            {
                var traversal = GetComponent<PlayerParkour>();
                if (traversal != null || GetComponent<PlayerCrawlAnimation>() != null)
                {
                    var handIK = animator.GetComponent<PlayerParkourHandIK>();
                    if (handIK == null) handIK = animator.gameObject.AddComponent<PlayerParkourHandIK>();
                    handIK.Configure(traversal);
                }
            }
        }

        private void LateUpdate()
        {
            if (modelInstance == null || !driveAnimator || animator == null || animator.runtimeAnimatorController == null) return;
            if (cachedController != animator.runtimeAnimatorController)
            {
                cachedController = animator.runtimeAnimatorController;
                parameters.Clear();
                foreach (var parameter in animator.parameters) parameters[parameter.nameHash] = parameter.type;
            }
            animator.speed = animationPlaybackSpeed;
            Vector3 velocity = transform.InverseTransformDirection(capsule.velocity);
            Vector2 planar = new Vector2(velocity.x, velocity.z);
            float speed = planar.magnitude;
            bool crouched = locomotion != null && locomotion.IsCrouching;
            float normalizedSpeed = crouched ? speed / Mathf.Max(.01f, crouchAnimationSpeed)
                : speed <= walkAnimationSpeed ? speed / Mathf.Max(.01f, walkAnimationSpeed)
                : 1f + (speed - walkAnimationSpeed) / Mathf.Max(.01f, runAnimationSpeed - walkAnimationSpeed);
            Vector2 blend = speed > .001f ? planar / speed * Mathf.Min(normalizedSpeed, crouched ? 1f : 2f) : Vector2.zero;
            SetFloat(moveXParameter, blend.x);
            SetFloat(moveZParameter, blend.y);
            SetFloat(speedParameter, speed);
            SetBool(crouchParameter, crouched);
            SetBool(proneParameter, locomotion != null && locomotion.IsProne);
            SetBool(deadParameter, locomotion != null && !locomotion.IsAlive);
            SetBool(groundedParameter, locomotion != null ? locomotion.IsGrounded : capsule.isGrounded);
            SetFloat(verticalSpeedParameter, locomotion != null ? locomotion.VerticalSpeed : capsule.velocity.y);
            if (!string.IsNullOrEmpty(parkourStateParameter))
            {
                int hash = Animator.StringToHash(parkourStateParameter);
                if (parameters.TryGetValue(hash, out var type) && type == AnimatorControllerParameterType.Int)
                    animator.SetInteger(hash, locomotion != null && locomotion.Parkour != null ? (int)locomotion.Parkour.State : 0);
            }
        }

        private void SetFloat(string name, float value)
        {
            if (string.IsNullOrEmpty(name)) return;
            int hash = Animator.StringToHash(name);
            if (parameters.TryGetValue(hash, out var type) && type == AnimatorControllerParameterType.Float)
                animator.SetFloat(hash, value, animationDamping, Time.deltaTime);
        }

        private void SetBool(string name, bool value)
        {
            if (string.IsNullOrEmpty(name)) return;
            int hash = Animator.StringToHash(name);
            if (parameters.TryGetValue(hash, out var type) && type == AnimatorControllerParameterType.Bool) animator.SetBool(hash, value);
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            RestoreRenderers();
            if (camera != firstPersonCamera || modelInstance == null || firstPersonVisibility == FirstPersonVisibility.ShowWholeModel) return;
            var targets = firstPersonVisibility == FirstPersonVisibility.ShadowsOnly
                ? allRenderers : hiddenRenderers;
            foreach (var renderer in targets)
            {
                if (renderer == null || savedShadowModes.ContainsKey(renderer)) continue;
                savedShadowModes.Add(renderer, renderer.shadowCastingMode);
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            if (firstPersonVisibility == FirstPersonVisibility.HideSelectedRenderers)
                foreach (var renderer in firstPersonOnlyRenderers)
                {
                    if (renderer == null || savedRendererStates.ContainsKey(renderer)) continue;
                    savedRendererStates.Add(renderer, renderer.forceRenderingOff);
                    renderer.forceRenderingOff = false;
                }
        }

        private void AfterCamera(ScriptableRenderContext context, Camera camera) => RestoreRenderers();

        private void RestoreRenderers()
        {
            foreach (var pair in savedShadowModes) if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
            savedShadowModes.Clear();
            foreach (var pair in savedRendererStates) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            savedRendererStates.Clear();
        }
    }
}
