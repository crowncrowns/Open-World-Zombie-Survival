using UnityEngine;

namespace AdvancedGrassPainter
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GrassWindController : MonoBehaviour
    {
        [Tooltip("Сила ветра, доля зарезервированного при генерации смещения."), Range(0, 1)] public float strength = .65f;
        [Tooltip("Направление ветра в плоскости земли, градусы."), Range(0, 360)] public float direction = 35;
        [Tooltip("Скорость движения волн, м/с."), Range(0, 10)] public float speed = 1.4f;
        [Tooltip("Частота порывов, Гц."), Range(0, 5)] public float gustFrequency = .27f;
        [Tooltip("Доля мелкой турбулентности, 0–1."), Range(0, 1)] public float turbulence = .28f;
        [Tooltip("Жёсткость травинок, 0–1."), Range(0, 1)] public float stiffness = .2f;
        [Tooltip("Пространственная частота волн, 1/м."), Range(.01f, 5)] public float noiseScale = .5f;
        [Tooltip("Автоматически изменять фазу ветра во времени.")] public bool animate = true;
        [Tooltip("Фаза ветра, секунды. Используется при выключенной автоматической анимации.")] public float phaseTime;
        public void Apply(MaterialPropertyBlock block)
        {
            float angle = direction * Mathf.Deg2Rad;
            block.SetVector("_GrassWind", new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), strength * (1 - stiffness), speed));
            block.SetVector("_GrassGust", new Vector4(gustFrequency, turbulence, noiseScale, 0));
            block.SetFloat("_GrassTime",animate?(Application.isPlaying?Time.time:Time.realtimeSinceStartup):phaseTime);
        }
    }
}
