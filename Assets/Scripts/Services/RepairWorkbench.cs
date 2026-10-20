using UnityEngine;
namespace ZombieSurvival.Services
{
    [DisallowMultipleComponent,RequireComponent(typeof(WorkbenchServicePoint),typeof(ServicePresentation))]
    public sealed class RepairWorkbench:MonoBehaviour
    {
        [SerializeField,InspectorName("Правила ремонта"),Tooltip("Необязательная замена общих правил. При пустой ссылке загружается Resources/RepairPolicies/Default; отсутствие правил запрещает ремонт.")]
        private RepairPolicy policyOverride;
        public RepairPolicy Policy=>policyOverride!=null?policyOverride:Resources.Load<RepairPolicy>("RepairPolicies/Default");
    }
}
