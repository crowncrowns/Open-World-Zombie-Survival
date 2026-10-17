using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using ZombieSurvival.AI;
using ZombieSurvival.Combat;
using ZombieSurvival.Player;
using ZombieSurvival.Interaction;

public static class ActorContractValidation
{
    [MenuItem("Tools/Zombie Survival/Validate Actors in Open Scene")]
    public static void Validate()
    {
        int errors = 0;
        foreach (var player in Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(player);
            var asset = so.FindProperty("inputActions").objectReferenceValue as InputActionAsset;
            if (asset == null) asset = InputSystem.actions;
            string map = so.FindProperty("playerActionMap").stringValue;
            var interactor = player.GetComponent<PlayerInteractor>();
            string interaction = interactor != null ? new SerializedObject(interactor).FindProperty("actionName").stringValue : "Interact";
            var configuredActions = new[] { "moveActionName", "lookActionName", "sprintActionName", "crouchActionName", "jumpActionName", "rollActionName", "proneActionName" }
                .Select(field => so.FindProperty(field).stringValue).Concat(new[] { interaction });
            foreach (var name in configuredActions)
                if (asset == null || asset.FindAction(map + "/" + name) == null)
                { Debug.LogError(player.name + ": missing " + map + "/" + name + " in Input Actions. Restore the configured action and its bindings.", player); errors++; }
            if (player.GetComponent<PlayerParkour>() == null || player.GetComponent<PlayerCrawlAnimation>() == null)
                Debug.LogWarning(player.name + ": traversal or crawl presentation component is missing.", player);
            if (player.GetComponent<ActorIdentity>() == null || player.GetComponent<CombatTarget>() == null || player.GetComponent<PlayerNoiseEmitter>() == null)
            { Debug.LogError(player.name + ": AI integration needs ActorIdentity, CombatTarget and PlayerNoiseEmitter.", player); errors++; }
        }
        foreach (var actor in Object.FindObjectsByType<ActorIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int pools = actor.GetComponents<MonoBehaviour>().Count(b => b is IDamageable);
            if (pools != 1)
            { Debug.LogError(actor.name + ": expected one health receiver on the actor root; found " + pools + ". Use Health or a compatible IDamageable.", actor); errors++; }
        }
        foreach (var brain in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (brain.GetComponent<ActorIdentity>() == null || brain.GetComponent<CombatTarget>() == null)
            { Debug.LogError(brain.name + ": set an ActorIdentity/faction and CombatTarget on the root.", brain); errors++; }
            if (brain.GetComponents<EnemyAttack>().Length == 0) Debug.LogWarning(brain.name + ": no attack attached; this can be a passive NPC.", brain);
            var agent = brain.GetComponent<NavMeshAgent>();
            if (agent != null && Application.isPlaying && agent.enabled && !agent.isOnNavMesh)
            { Debug.LogError(brain.name + ": agent is outside the matching baked NavMesh.", brain); errors++; }
        }
        if (errors > 0) throw new System.InvalidOperationException("Actor validation: " + errors + " errors. See the messages above.");
        Debug.Log("ACTOR_VALIDATION_OK: actor contracts and required player input actions are present.");
    }
}
