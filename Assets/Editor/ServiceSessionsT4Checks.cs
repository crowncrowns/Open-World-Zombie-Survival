using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Interaction;
using ZombieSurvival.Player;
using ZombieSurvival.Services;

public static class ServiceSessionsT4Checks
{
    [MenuItem("Tools/Character Lab/Check T4 Service Access (Play)")]
    private static void RunFromMenu() => Debug.Log(Run());
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Запустите Play: проверяется живой игрок.");
        var actor = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();
        var motor = actor.GetComponent<FirstPersonController>();
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube); root.name = "T4 access check (temporary)";
        root.transform.position = actor.ViewCamera.transform.position + actor.ViewCamera.transform.forward * 1.5f;
        root.transform.localScale = Vector3.one * .5f;
        var point = root.AddComponent<TraderServicePoint>();
        GameObject wall = null; var passed = new List<string>();
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("T4 FAILED: " + name); passed.Add(name); };
        try
        {
            Physics.SyncTransforms(); check(point.CanAccess(actor, out _), "nearby visible service permits physical access");
            root.transform.position += actor.ViewCamera.transform.forward * 6; Physics.SyncTransforms();
            check(!point.CanAccess(actor, out _), "distant service rejects direct API");
            root.transform.position -= actor.ViewCamera.transform.forward * 6;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "T4 occlusion check (temporary)";
            wall.transform.position = actor.ViewCamera.transform.position + actor.ViewCamera.transform.forward * .7f;
            wall.transform.localScale = Vector3.one * .5f; Physics.SyncTransforms();
            check(!point.CanAccess(actor, out _), "wall rejects direct API, not only selection ray");
            UnityEngine.Object.DestroyImmediate(wall); wall = null; Physics.SyncTransforms();
            point.enabled = false; check(!point.CanAccess(actor, out _), "disabled component rejects access"); point.enabled = true;
            root.SetActive(false); check(!point.CanAccess(actor, out _), "inactive object rejects access"); root.SetActive(true);
            root.GetComponent<Collider>().enabled = false; check(!point.CanAccess(actor, out _), "disabled collider rejects access"); root.GetComponent<Collider>().enabled = true;
            var so = new SerializedObject(point); so.FindProperty("interactionEnabled").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
            check(!point.CanAccess(actor, out _), "interaction permission checked independently of UI");
            return string.Join("\n", passed);
        }
        finally { if (wall != null) UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(root); }
    }
}
