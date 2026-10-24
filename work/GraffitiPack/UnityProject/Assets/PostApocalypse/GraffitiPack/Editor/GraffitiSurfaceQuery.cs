using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PostApocalypse.Graffiti.Editor
{
    public struct GraffitiSurfaceHit
    {
        public Vector3 point, normal;
        public Transform transform;
        public Collider collider;
        public float distance;
    }

    // Editor mesh intersection does not require a Collider, Read/Write or scene mutations.
    [InitializeOnLoad]
    public static class GraffitiSurfaceQuery
    {
        static readonly MethodInfo Intersect = typeof(HandleUtility).GetMethod("IntersectRayMesh", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null,
            new[]{typeof(Ray),typeof(Mesh),typeof(Matrix4x4),typeof(RaycastHit).MakeByRefType()},null);
        static MeshFilter[] meshes;
        public static void Invalidate() => meshes=null;
        static GraffitiSurfaceQuery()
        {
            EditorApplication.hierarchyChanged+=Invalidate;
            Undo.undoRedoPerformed+=Invalidate;
            EditorSceneManager.sceneOpened+=(s,m)=>Invalidate();
        }
        public static bool Movable(Transform t) => t.GetComponentInParent<Rigidbody>() || t.GetComponentInParent<CharacterController>() || t.GetComponentInParent<Animator>() || t.GetComponentInParent<GraffitiPlacement>() || t.GetComponentInParent<SkinnedMeshRenderer>();
        public static bool Hit(Ray ray,int mask,out GraffitiSurfaceHit result)
        {
            result=default;float nearest=1000;bool blocked=false,found=false;
            ray.direction=ray.direction.normalized;
            if(meshes==null)meshes=Resources.FindObjectsOfTypeAll<MeshFilter>().Where(m=>m && m.gameObject.scene.IsValid() && m.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(m) && !EditorSceneManager.IsPreviewScene(m.gameObject.scene)).ToArray();
            // Physical actors still occlude the brush. Building collision bounds must not replace its visible facade.
            foreach(var h in Physics.RaycastAll(ray,nearest,mask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(!h.collider.gameObject.activeInHierarchy || SceneVisibilityManager.instance.IsHidden(h.collider.gameObject))continue;
                if(!Movable(h.collider.transform) && h.collider.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.GetComponent<MeshRenderer>() && !f.GetComponentInParent<GraffitiPlacement>()))continue;
                if(h.distance>=nearest)continue;
                nearest=h.distance;blocked=!GraffitiPainter.Eligible(h.collider,mask);found=!blocked;
                result=new GraffitiSurfaceHit{point=h.point,normal=h.normal,transform=h.collider.transform,collider=h.collider,distance=h.distance};
            }
            if(Intersect!=null)foreach(var filter in meshes)
            {
                if(!filter || !filter.gameObject.activeInHierarchy || (mask&(1<<filter.gameObject.layer))==0)continue;
                var renderer=filter.GetComponent<MeshRenderer>();
                if(!renderer || !renderer.enabled || renderer.forceRenderingOff || !filter.sharedMesh || SceneVisibilityManager.instance.IsHidden(filter.gameObject))continue;
                if(!renderer.bounds.IntersectRay(ray,out float boundsDistance) || boundsDistance>nearest)continue;
                // Do not intersect ghost/paint meshes; they are editor overlays, never brush targets.
                if(filter.GetComponentInParent<GraffitiPlacement>())continue;
                object[] args={ray,filter.sharedMesh,filter.transform.localToWorldMatrix,default(RaycastHit)};
                if(!(bool)Intersect.Invoke(null,args))continue;
                var h=(RaycastHit)args[3];if(h.distance<0 || h.distance>=nearest)continue;
                nearest=h.distance;blocked=Movable(filter.transform);found=!blocked;
                var normal=h.normal.normalized;if(Vector3.Dot(normal,ray.direction)>0)normal=-normal;
                result=new GraffitiSurfaceHit{point=h.point,normal=normal,transform=filter.transform,collider=filter.GetComponent<Collider>(),distance=h.distance};
            }
            return found&&!blocked;
        }
    }
}
