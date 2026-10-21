using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;

namespace ZombieSurvival.Presentation
{
    /// <summary>Isolated visual copy. Gameplay components never become active on the copy.</summary>
    internal sealed class ModelPreview : IDisposable
    {
        private GameObject stage, model;
        private Camera camera;
        private RenderTexture texture;
        private PlayableGraph graph;
        private AnimationClipPlayable idle;
        private float clock, nextFrame;
        private readonly float idleLength;
        internal RenderTexture Texture => texture;
        internal GameObject Model => model;
        internal ModelPreview(GameObject prefab, int width, int height, AnimationClip clip = null, bool character = false)
        {
            stage = new GameObject("UI visual preview") { hideFlags = HideFlags.HideAndDontSave };
            stage.SetActive(false); stage.transform.position = new Vector3(-30000, -30000, -30000);
            model = UnityEngine.Object.Instantiate(prefab, stage.transform, false);
            model.name = "Visual copy";
            var definition = model.GetComponentInChildren<PlayerModelDefinition>(true);
            var modelAnimator = definition != null && definition.ModelAnimator != null ? definition.ModelAnimator : model.GetComponentInChildren<Animator>(true);
            var excluded = new HashSet<Renderer>();
            var showInPortrait = new HashSet<Renderer>();
            if (character && definition != null)
            {
                foreach (var renderer in definition.FirstPersonOnlyRenderers) if (renderer != null) excluded.Add(renderer);
                foreach (var renderer in definition.FirstPersonHiddenRenderers) if (renderer != null) showInPortrait.Add(renderer);
            }
            foreach (var group in model.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (var renderer in lods[i].renderers) if (renderer != null) excluded.Add(renderer);
            }
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) UnityEngine.Object.DestroyImmediate(script);
            foreach (var component in model.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component is MeshFilter || component is Renderer
                    || character && component is Animator) continue;
                // Parent is inactive: even WorldItem, UAL drivers and AnimationEvents cannot run during cloning.
                UnityEngine.Object.DestroyImmediate(component);
            }
            foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                bool hasMesh = renderer is SkinnedMeshRenderer skinnedMesh ? skinnedMesh.sharedMesh != null
                    : renderer is MeshRenderer && renderer.GetComponent<MeshFilter>()?.sharedMesh != null;
                renderer.enabled = (renderer.enabled || showInPortrait.Contains(renderer)) && !excluded.Contains(renderer) && hasMesh;
                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            var animator = modelAnimator;
            foreach (var candidate in model.GetComponentsInChildren<Animator>(true))
            { candidate.fireEvents = false; candidate.applyRootMotion = false; if (candidate != animator) candidate.enabled = false; }
            if (character && animator != null && clip != null)
            {
                animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Inventory idle"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                idle = AnimationClipPlayable.Create(graph, clip); idle.SetApplyFootIK(false); idle.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Visual idle", animator); output.SetSourcePlayable(idle);
                idleLength = Mathf.Max(.01f, clip.length); graph.Play();
            }
            var cameraObject = new GameObject("Preview camera"); cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; data.volumeLayerMask = 0; data.renderShadows = false;
            AddLight("Warm key", new Vector3(-2, 3, 4), 10f, new Color(1, .9f, .76f));
            AddLight("Cool fill", new Vector3(3, 1, -3), 7f, new Color(.71f, .8f, 1));
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "UI model", antiAliasing = 1 };
            texture.Create(); camera.targetTexture = texture;
            stage.SetActive(true);
            if (graph.IsValid()) graph.Evaluate(0);
            Bounds bounds = new Bounds(stage.transform.position, Vector3.one * .01f); bool found = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            float aspect = width / (float)height;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z) / aspect) * (character ? 1.12f : 1.4f);
            var direction = character ? new Vector3(.12f, .02f, 1) : new Vector3(1, .5f, 1);
            camera.transform.position = bounds.center + direction.normalized * Mathf.Max(4, bounds.size.magnitude * 3);
            camera.transform.LookAt(bounds.center);
            Render(0, true);
        }
        private void AddLight(string name, Vector3 position, float intensity, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(stage.transform, false); go.transform.localPosition = position;
            // Local point lights cannot replace the scene's main directional light in URP.
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = 12; light.intensity = intensity;
            light.color = color; light.cullingMask = 1 << 31; light.shadows = LightShadows.None;
        }
        internal void Render(float delta, bool force = false)
        {
            if (stage == null) return;
            clock += delta;
            if (graph.IsValid()) { idle.SetTime(clock % idleLength); graph.Evaluate(0); }
            if (!force && Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 1f / 30f; camera.Render();
        }
        internal RenderTexture DetachTexture() { var result = texture; camera.targetTexture = null; texture = null; return result; }
        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            if (stage != null) { stage.SetActive(false); UnityEngine.Object.Destroy(stage); }
        }
    }

    internal static class ItemModelIcons
    {
        private static readonly Dictionary<ItemDefinition, RenderTexture> Cache = new Dictionary<ItemDefinition, RenderTexture>();
        private static readonly HashSet<ItemDefinition> Pending = new HashSet<ItemDefinition>();
        private static ModelPreviewPump pump;
        internal static Texture Get(ItemDefinition type)
        {
            if (type == null || type.WorldPrefab == null) return null;
            if (Cache.TryGetValue(type, out var cached) && cached != null && cached.IsCreated()) return cached;
            // Get вызывают OnGUI рюкзака/хотбара/торговли. Camera.Render внутри URP RenderLoop приводит к повторному UniversalCameraData.
            // На miss только заявка; выключенная preview-камера рендерится из LateUpdate отдельного presentation pump.
            Pending.Add(type);
            if(pump==null){var go=new GameObject("UI preview queue"){hideFlags=HideFlags.HideAndDontSave};pump=go.AddComponent<ModelPreviewPump>();}
            return null;
        }
        internal static void RenderPending()
        {
            int remaining=2;
            while(Pending.Count>0 && remaining-->0)
            {
                ItemDefinition type=null;foreach(var next in Pending){type=next;break;}Pending.Remove(type);
                if(type==null || type.WorldPrefab==null)continue;
                if(Cache.TryGetValue(type,out var old)&&old!=null)
                {if(old.IsCreated())continue;old.Release();UnityEngine.Object.Destroy(old);}
                using(var preview=new ModelPreview(type.WorldPrefab,256,256))Cache[type]=preview.DetachTexture();
                Pending.Remove(type); // Вложенный GUI вызов во время Camera.Render мог снова запросить ещё не опубликованную иконку.
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (var texture in Cache.Values) if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            Cache.Clear();
            Pending.Clear();if(pump!=null)UnityEngine.Object.Destroy(pump.gameObject);pump=null;
        }
    }
}
