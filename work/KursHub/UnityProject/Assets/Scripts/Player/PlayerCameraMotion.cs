using UnityEngine;

namespace ZombieSurvival.Player
{
    /// <summary>Optional position-only presentation. Base camera posture/aim still belongs to the motor.</summary>
    [DefaultExecutionOrder(-500), DisallowMultipleComponent, RequireComponent(typeof(FirstPersonController))]
    public sealed class PlayerCameraMotion : MonoBehaviour
    {
        [SerializeField, Tooltip("Включить покачивание камеры. По умолчанию выключено для комфорта; на скорость, коллайдер и сохранение не влияет.")]
        private bool motionEnabled;
        [SerializeField, Tooltip("Камера этого персонажа; при потере ссылки ищется среди дочерних объектов.")]
        private Camera viewCamera;
        [SerializeField, Tooltip("Максимальное смещение камеры по осям при ходьбе, м.")]
        private Vector3 amplitude=new Vector3(.008f,.012f,0);
        [SerializeField, Min(.1f), Tooltip("Частота цикла ходьбы, циклов/с.")]
        private float walkFrequency=1.7f;
        [SerializeField, Min(.1f), Tooltip("Частота цикла бега, циклов/с.")]
        private float runFrequency=2.4f;
        [SerializeField, Min(0), Tooltip("Множитель амплитуды при беге.")]
        private float runAmplitude=1.3f;
        [SerializeField, Min(0), Tooltip("Скорость плавного изменения смещения, м/с.")]
        private float blendSpeed=.12f;
        private FirstPersonController motor;
        private Vector3 offset,lastPosition;
        private bool applied;
        private float phase;
        private void OnEnable(){motor=GetComponent<FirstPersonController>();if(viewCamera==null)viewCamera=GetComponentInChildren<Camera>();Clear();}
        private void OnDisable()=>Clear();
        private void RemoveOffset()
        {
            if(applied && viewCamera!=null && (viewCamera.transform.localPosition-lastPosition).sqrMagnitude<.000001f)viewCamera.transform.localPosition-=offset;
            applied=false;
        }
        private void Clear(){RemoveOffset();offset=Vector3.zero;phase=0;}
        private void Update()=>RemoveOffset();
        private void LateUpdate()
        {
            if(viewCamera==null || motor==null)return;
            bool active=motionEnabled && motor.AcceptingGameplayInput && motor.IsGrounded && !motor.IsProne && motor.PlanarVelocity.sqrMagnitude>.1f;
            Vector3 goal=Vector3.zero;
            if(active)
            {
                phase+=Time.deltaTime*(motor.IsSprinting?runFrequency:walkFrequency)*Mathf.PI*2;
                float scale=motor.IsSprinting?runAmplitude:motor.IsCrouching?.4f:1;
                goal=Vector3.Scale(amplitude,new Vector3(Mathf.Sin(phase),Mathf.Sin(phase*2),0))*scale;
            }
            offset=Vector3.MoveTowards(offset,goal,blendSpeed*Time.deltaTime);
            viewCamera.transform.localPosition+=offset;lastPosition=viewCamera.transform.localPosition;applied=true;
        }
    }
}
