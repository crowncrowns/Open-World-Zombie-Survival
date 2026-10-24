using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiWallVerify
{
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Vector3.zero;wall.transform.localScale=new Vector3(3,3,.2f);wall.isStatic=false;
        UnityEngine.Object.DestroyImmediate(wall.GetComponent<Collider>());GraffitiSurfaceQuery.Invalidate();
        var ray=new Ray(new Vector3(0,0,-3),Vector3.forward);
        void Check(bool value,string name){if(!value)throw new Exception(name);Debug.Log("WALL_PASS "+name);}
        Check(GraffitiPainter.Hit(ray,1,out var h)&&h.transform==wall.transform,"Nonstatic wall without Collider");
        Check(Mathf.Abs(h.point.z+.1f)<.001f&&h.normal.z<-.99f,"Exact visible wall plane and normal");
        var root=new GameObject("Building root with broad collision");wall.transform.SetParent(root.transform,true);root.AddComponent<BoxCollider>().size=new Vector3(4,4,2);Physics.SyncTransforms();
        Check(GraffitiPainter.Hit(ray,1,out h)&&h.transform==wall.transform&&Mathf.Abs(h.point.z+.1f)<.001f,"Building root bounds do not replace visible facade");UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
        Check(GraffitiPainter.Unsupported(h.point,Quaternion.identity,2,2,1,false)==0,"49 samples on colliderless wall");
        Check(!GraffitiPainter.Hit(ray,2,out _),"LayerMask still enforced");
        wall.GetComponent<MeshRenderer>().enabled=false;Check(!GraffitiPainter.Hit(ray,1,out _),"Hidden renderer rejected");wall.GetComponent<MeshRenderer>().enabled=true;
        var body=wall.AddComponent<Rigidbody>();body.isKinematic=true;Check(!GraffitiPainter.Hit(ray,1,out _),"Rigidbody wall rejected");UnityEngine.Object.DestroyImmediate(body);
        var animator=wall.AddComponent<Animator>();Check(!GraffitiPainter.Hit(ray,1,out _),"Animated wall rejected");UnityEngine.Object.DestroyImmediate(animator);
        var mesh=UnityEngine.Object.Instantiate(wall.GetComponent<MeshFilter>().sharedMesh);wall.GetComponent<MeshFilter>().sharedMesh=mesh;mesh.UploadMeshData(true);
        Check(GraffitiPainter.Hit(ray,1,out h),"Non-readable mesh supported");
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0,0,-1);blocker.AddComponent<Rigidbody>().isKinematic=true;Physics.SyncTransforms();GraffitiSurfaceQuery.Invalidate();
        Check(!GraffitiPainter.Hit(ray,1,out _),"Cannot paint through physical actor");UnityEngine.Object.DestroyImmediate(blocker);
        var terrainData=new TerrainData();terrainData.heightmapResolution=33;terrainData.size=new Vector3(4,1,4);var terrain=Terrain.CreateTerrainGameObject(terrainData);terrain.transform.position=new Vector3(10,0,0);Physics.SyncTransforms();
        Check(GraffitiPainter.Hit(new Ray(new Vector3(11,3,1),Vector3.down),1,out h)&&h.transform==terrain.transform,"Terrain retained");
        File.WriteAllText(Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack/wall_fix_checks.json"),"{\"passed\":11,\"nonStaticColliderlessWall\":true,\"nonReadableMesh\":true,\"buildingRootBoundsIgnored\":true,\"supportSamples\":49,\"actorExclusion\":true,\"terrain\":true}");
        Debug.Log("GRAFFITI_WALL_FIX_PASS=11");
    }
}
