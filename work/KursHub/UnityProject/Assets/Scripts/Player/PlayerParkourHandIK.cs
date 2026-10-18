using UnityEngine;

namespace ZombieSurvival.Player
{
    /// <summary>Optional Humanoid presentation; traversal physics never depend on this component.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerParkourHandIK : MonoBehaviour
    {
        private Animator animator;
        private PlayerParkour parkour;
        private PlayerCrawlAnimation crawl;
        private float weight;

        public void Configure(PlayerParkour owner)
        {
            parkour = owner;
            animator = GetComponent<Animator>();
            crawl = GetComponentInParent<PlayerCrawlAnimation>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman) return;
            if (layerIndex != 0) return;
            if (crawl != null && crawl.ApplyIK(animator)) { weight = 0f; return; }
            bool reaching = parkour != null && parkour.UseHandIK && (parkour.IsHanging
                || parkour.State == PlayerParkour.TraversalState.Climbing && parkour.ActionProgress < parkour.HandIKClimbReleaseProgress);
            weight = Mathf.MoveTowards(weight, reaching ? 1f : 0f,
                (parkour != null ? parkour.HandIKBlendSpeed : 8f) * Time.deltaTime);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            if (parkour == null || weight <= 0f) return;
            Vector3 tangent = Vector3.Cross(parkour.LedgeNormal, Vector3.up);
            Vector3 center = parkour.LedgePoint + Vector3.up * parkour.HandHeightOffset + parkour.LedgeNormal * parkour.HandOutset;
            animator.SetIKPosition(AvatarIKGoal.LeftHand, center - tangent * parkour.HandSpacing);
            animator.SetIKPosition(AvatarIKGoal.RightHand, center + tangent * parkour.HandSpacing);
        }
    }
}
