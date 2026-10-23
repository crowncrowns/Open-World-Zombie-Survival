using UnityEngine;

namespace AK74Reference
{
    public sealed class AK74ResetPresentationOnExit : StateMachineBehaviour
    {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Interrupted reloads cannot leave a detached or duplicate magazine on the prefab.
            animator.GetComponent<AK74Presentation>()?.ResetMagazineVisibility();
        }
    }
}
