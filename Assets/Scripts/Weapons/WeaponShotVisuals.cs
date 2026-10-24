using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieSurvival.Weapons
{
    /// <summary>
    /// Визуальный след выстрела для любого WeaponDefinition. Этот компонент не делает raycast,
    /// не выбирает цель и не меняет патроны/урон: конечную точку ему передаёт PlayerWeapons.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponShotVisuals : MonoBehaviour
    {
        private const int TracerPoolSize = 8;
        private readonly List<LineRenderer> tracers = new List<LineRenderer>(TracerPoolSize);
        private readonly List<float> tracerExpiry = new List<float>(TracerPoolSize);
        private Material effectMaterial;
        private ParticleSystem particles;
        private int nextTracer;

        public void Show(WeaponDefinition definition, Vector3 muzzle, Vector3 endpoint, Vector3 direction,
            bool hit, Vector3 hitNormal)
        {
            if (definition == null) return;
            if (definition.showShotTracer) DrawTracer(definition, muzzle, endpoint);

            if (!definition.showMuzzleFlash && (!hit || !definition.showImpactSparks)) return;
            EnsureParticles();
            if (particles == null) return;
            if (!particles.isPlaying) particles.Play(false);

            if (definition.showMuzzleFlash)
            {
                Color flash = definition.shotTracerColor;
                Vector3 forward = direction.sqrMagnitude > .001f ? direction.normalized : transform.forward;
                Emit(muzzle, forward * 1.5f + Random.insideUnitSphere * .25f, flash, .065f, .045f);
                Emit(muzzle, forward * 2f + Random.insideUnitSphere * .35f, Color.white, .045f, .025f);
            }

            if (hit && definition.showImpactSparks)
            {
                Vector3 normal = hitNormal.sqrMagnitude > .001f ? hitNormal.normalized
                    : direction.sqrMagnitude > .001f ? -direction.normalized : Vector3.up;
                int count = Mathf.Clamp(definition.impactSparkCount, 1, 16);
                for (int i = 0; i < count; i++)
                {
                    Vector3 velocity = normal * Random.Range(.35f, 1.15f) + Random.insideUnitSphere * .45f;
                    if (Vector3.Dot(velocity, normal) < .05f) velocity += normal * .5f;
                    Emit(endpoint + normal * .006f, velocity, definition.impactSparkColor,
                        Random.Range(.13f, .24f), Random.Range(.018f, .038f));
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < tracers.Count; i++)
                if (tracers[i] != null) tracers[i].enabled = false;
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < tracers.Count; i++)
                if (tracers[i] != null && tracers[i].enabled && now >= tracerExpiry[i]) tracers[i].enabled = false;
        }

        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            if (effectMaterial != null) Destroy(effectMaterial);
        }

        private void DrawTracer(WeaponDefinition definition, Vector3 start, Vector3 end)
        {
            EnsureTracePool();
            if (tracers.Count == 0) return;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < .01f) return;

            int tracerIndex = nextTracer;
            LineRenderer line = tracers[tracerIndex];
            nextTracer = (nextTracer + 1) % tracers.Count;
            float maxLength = Mathf.Max(.1f, definition.shotTracerMaxLength);
            if (distance > maxLength) end = start + delta / distance * maxLength;
            line.startWidth = Mathf.Max(.001f, definition.shotTracerWidth);
            line.endWidth = line.startWidth * .22f;
            Color head = definition.shotTracerColor;
            Color tail = head; tail.a = 0f;
            line.startColor = head;
            line.endColor = tail;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.enabled = true;
            tracerExpiry[tracerIndex] = Time.unscaledTime + Mathf.Max(.01f, definition.shotTracerSeconds);
        }

        private void EnsureTracePool()
        {
            if (effectMaterial == null) CreateMaterial();
            if (effectMaterial == null) return;
            while (tracers.Count < TracerPoolSize)
            {
                var go = new GameObject("Shot tracer " + tracers.Count);
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = effectMaterial;
                line.enabled = false;
                tracers.Add(line);
                tracerExpiry.Add(0f);
            }
        }

        private void EnsureParticles()
        {
            if (particles != null) return;
            if (effectMaterial == null) CreateMaterial();
            var go = new GameObject("Shot sparks");
            go.transform.SetParent(transform, false);
            particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 128;
            main.startLifetime = .2f;
            main.startSpeed = 0f;
            main.startSize = .03f;
            main.startColor = Color.white;
            main.gravityModifier = .15f;
            var emission = particles.emission;
            emission.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (effectMaterial != null) renderer.sharedMaterial = effectMaterial;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Emit(Vector3 position, Vector3 velocity, Color color, float lifetime, float size)
        {
            var settings = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color,
                startLifetime = lifetime,
                startSize = size
            };
            particles.Emit(settings, 1);
        }

        private void CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            effectMaterial = new Material(shader) { name = "Runtime weapon shot effects", hideFlags = HideFlags.HideAndDontSave };
            if (effectMaterial.HasProperty("_BaseColor")) effectMaterial.SetColor("_BaseColor", Color.white);
            if (effectMaterial.HasProperty("_Color")) effectMaterial.SetColor("_Color", Color.white);
            if (effectMaterial.HasProperty("_Surface")) effectMaterial.SetFloat("_Surface", 1f);
            if (effectMaterial.HasProperty("_ZWrite")) effectMaterial.SetFloat("_ZWrite", 0f);
            effectMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            effectMaterial.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
