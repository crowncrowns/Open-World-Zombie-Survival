using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TrashBinBuilder
{
    const string A="Assets/Environment/Props/TrashBin";
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.."));
    static string V=>Path.Combine(Root,"Verification/TrashBin");
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static Material MakeMaterial(string name,bool bin,bool glass=false,bool tin=false)
    {
        string path=A+"/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/Textures/"+(bin?"TrashBin_Albedo.png":"TrashContents_Atlas.png")));
        m.SetColor("_BaseColor",new Color(1,1,1,glass?.53f:1));
        m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/Textures/"+(bin?"TrashBin_Normal.png":"TrashContents_Normal.png")));
        m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",bin?.7f:1);
        if(bin){
            m.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/Textures/TrashBin_MetallicSmoothness.png"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);
            m.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/Textures/TrashBin_AO.png"));m.EnableKeyword("_OCCLUSIONMAP");m.SetFloat("_OcclusionStrength",.7f);
        }else {m.SetFloat("_Metallic",tin?.75f:0);m.SetFloat("_Smoothness",glass?.73f:tin?.42f:.22f);}
        if(glass){
            m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;
        }
        EditorUtility.SetDirty(m);return m;
    }
    static Dictionary<string,Material> Materials()
    {
        Directory.CreateDirectory(A+"/Materials");AssetDatabase.Refresh();
        var d=new Dictionary<string,Material>();
        foreach(string n in new[]{"M_TrashBin_GreenPaint","M_TrashBin_RustyMetal","M_TrashContents","M_TrashContents_PET","M_TrashContents_Tin"})
            d[n]=MakeMaterial(n,n.StartsWith("M_TrashBin_"),n.EndsWith("PET"),n.EndsWith("Tin"));
        return d;
    }
    static void ImportTextures()
    {
        foreach(string file in Directory.GetFiles(A+"/Textures","*.png")){
            string path=file.Replace('\\','/');var t=(TextureImporter)AssetImporter.GetAtPath(path);Require(t!=null,"Texture importer missing "+path);
            t.maxTextureSize=4096;t.textureCompression=TextureImporterCompression.CompressedHQ;t.mipmapEnabled=true;t.anisoLevel=8;
            t.sRGBTexture=path.Contains("Albedo")||path.Contains("Atlas");
            if(path.Contains("Normal")){t.textureType=TextureImporterType.NormalMap;t.convertToNormalmap=false;}
            t.alphaSource=TextureImporterAlphaSource.FromInput;t.SaveAndReimport();
        }
    }
    static void ImportModels(Dictionary<string,Material> mats)
    {
        foreach(string p in Directory.GetFiles(A+"/Models","*.fbx")){
            string path=p.Replace('\\','/');var mi=(ModelImporter)AssetImporter.GetAtPath(path);Require(mi!=null,"Model importer missing");
            mi.globalScale=1;mi.useFileScale=true;mi.importAnimation=false;mi.importCameras=false;mi.importLights=false;mi.isReadable=true;mi.generateSecondaryUV=false;
            mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var pair in mats)mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            mi.SaveAndReimport();
        }
    }
    static void ColliderBox(Transform parent,string name,Vector3 position,Vector3 size)
    {
        var o=new GameObject(name);o.transform.SetParent(parent,false);var c=o.AddComponent<BoxCollider>();c.center=position;c.size=size;o.isStatic=true;
    }
    static GameObject Prefab(string variant,Dictionary<string,Material> mats)
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(A+"/Models/SM_TrashBin_"+variant+".fbx");Require(model,"FBX import failed");
        // Keep the importer's axis/unit conversion on the visual children. Gameplay
        // colliders and the placement pivot must live under a metre-scale root.
        var imported=UnityEngine.Object.Instantiate(model);
        var go=new GameObject("PF_TrashBin_"+variant);
        while(imported.transform.childCount>0)imported.transform.GetChild(0).SetParent(go.transform,true);
        UnityEngine.Object.DestroyImmediate(imported);
        if(go.transform.childCount==1){
            var wrapper=go.transform.GetChild(0);
            if(wrapper.name.StartsWith("SM_TrashBin_")&&wrapper.GetComponents<Component>().Length==1){
                while(wrapper.childCount>0)wrapper.GetChild(0).SetParent(go.transform,true);
                UnityEngine.Object.DestroyImmediate(wrapper.gameObject);
            }
        }
        foreach(var old in go.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(old);
        foreach(var t in go.GetComponentsInChildren<Transform>(true)){t.name=System.Text.RegularExpressions.Regex.Replace(t.name,@"\.\d{3}$","");t.gameObject.isStatic=true;t.gameObject.SetActive(true);}
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true)){
            r.enabled=true;r.sharedMaterials=r.sharedMaterials.Select(m=>m&&mats.ContainsKey(m.name)?mats[m.name]:mats[r.name.Contains("Support")?"M_TrashBin_RustyMetal":"M_TrashBin_GreenPaint"]).ToArray();
            r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
        }
        var lod=go.AddComponent<LODGroup>();var levels=new LOD[3];
        for(int i=0;i<3;i++){
            int level=i;
            var rs=go.GetComponentsInChildren<Renderer>(true).Where(r=>{
                for(var t=r.transform;t!=go.transform;t=t.parent)if(t.name.Contains("LOD"+level))return true;
                return false;
            }).ToArray();Require(rs.Length>0,"Missing LOD"+i);levels[i]=new LOD(new[]{.30f,.13f,.035f}[i],rs);
        }
        lod.SetLODs(levels);lod.RecalculateBounds();lod.fadeMode=LODFadeMode.None;
        var coll=new GameObject("TrashBin_Colliders");coll.transform.SetParent(go.transform,false);
        ColliderBox(coll.transform,"Bottom",new Vector3(0,.247f,0),new Vector3(.33f,.008f,.275f));
        ColliderBox(coll.transform,"FrontWall",new Vector3(0,.565f,-.143f),new Vector3(.36f,.64f,.012f));
        ColliderBox(coll.transform,"BackWall",new Vector3(0,.565f,.143f),new Vector3(.36f,.64f,.012f));
        foreach(int s in new[]{-1,1}){
            ColliderBox(coll.transform,"SideWall"+s,new Vector3(s*.172f,.565f,0),new Vector3(.012f,.64f,.30f));
            ColliderBox(coll.transform,"Stand"+s,new Vector3(s*.202f,.324f,0),new Vector3(.025f,.63f,.02f));
            ColliderBox(coll.transform,"Foot"+s,new Vector3(s*.202f,.005f,0),new Vector3(.055f,.01f,.235f));
        }
        Directory.CreateDirectory(A+"/Prefabs");PrefabUtility.SaveAsPrefabAsset(go,A+"/Prefabs/"+go.name+".prefab");UnityEngine.Object.DestroyImmediate(go);return model;
    }
    static void Pipeline()
    {
        PlayerSettings.colorSpace=ColorSpace.Linear;
        Directory.CreateDirectory("Assets/Validation");
        var rd=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Validation/Renderer.asset");
        if(!rd){rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,"Assets/Validation/Renderer.asset");}
        var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Validation/URP.asset");
        if(!rp){rp=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(rp,"Assets/Validation/URP.asset");}
        rp.msaaSampleCount=4;rp.shadowDistance=4;rp.mainLightShadowmapResolution=4096;
        var serializedPipeline=new SerializedObject(rp);serializedPipeline.FindProperty("m_SoftShadowsSupported").boolValue=true;serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline=rp;QualitySettings.renderPipeline=rp;
        QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowResolution=UnityEngine.ShadowResolution.VeryHigh;
    }
    static void Light(string name,Vector3 angle,float intensity,Color color)
    {
        var o=new GameObject(name);o.transform.eulerAngles=angle;var l=o.AddComponent<Light>();l.type=LightType.Directional;l.intensity=intensity;l.color=color;l.shadows=LightShadows.Soft;
    }
    static void Render(Camera c,string path,Vector3 position,Vector3 target)
    {
        c.transform.position=position;c.transform.LookAt(target);var rt=new RenderTexture(1200,1400,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;rt.Create();c.targetTexture=rt;
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};RenderPipeline.SubmitRenderRequest(c,request);
        var prior=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1200,1400,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,1400),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=prior;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
    }
    static void Renders()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Pipeline();
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.35f,.40f);RenderSettings.ambientEquatorColor=new Color(.19f,.20f,.18f);RenderSettings.ambientGroundColor=new Color(.09f,.08f,.06f);
        Light("Key",new Vector3(43,-35,0),1.05f,new Color(1,.94f,.85f));Light("Fill",new Vector3(65,140,0),.30f,new Color(.8f,.87f,1));
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*.5f;floor.transform.position=new Vector3(0,-.004f,0);var fm=new Material(Shader.Find("Universal Render Pipeline/Lit"));fm.SetColor("_BaseColor",new Color(.24f,.23f,.21f));floor.GetComponent<Renderer>().sharedMaterial=fm;
        var co=new GameObject("ValidationCamera");var c=co.AddComponent<Camera>();co.AddComponent<UniversalAdditionalCameraData>();c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.19f,.205f,.22f);c.fieldOfView=36;c.nearClipPlane=.015f;c.farClipPlane=30;c.allowHDR=true;c.allowMSAA=true;
        foreach(string variant in new[]{"Empty","Filled"}){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(A+"/Prefabs/PF_TrashBin_"+variant+".prefab");var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);obj.GetComponent<LODGroup>().ForceLOD(0);
            for(int i=0;i<4;i++){
                float a=(i*90+25)*Mathf.Deg2Rad;Render(c,Path.Combine(V,variant+"_"+i+".png"),new Vector3(Mathf.Sin(a)*1.63f,1.12f,-Mathf.Cos(a)*1.63f),new Vector3(0,.46f,0));
            }
            Render(c,Path.Combine(V,variant+"_Top.png"),new Vector3(.12f,1.95f,-.10f),new Vector3(0,.55f,0));
            for(int i=1;i<3;i++){obj.GetComponent<LODGroup>().ForceLOD(i);Render(c,Path.Combine(V,variant+"_LOD"+i+".png"),new Vector3(.7f,1.12f,-1.46f),new Vector3(0,.46f,0));}
            UnityEngine.Object.DestroyImmediate(obj);
        }
        UnityEngine.Object.DestroyImmediate(fm);
    }
    [Serializable]class MeshReport{public string name;public int triangles,vertices;public bool uv,normals,tangents;}
    [Serializable]class VariantReport{public string variant;public int renderers,colliders;public Vector3 size;public int contentsVerticesOutsideCavity;public MeshReport[] lods;public int[] triangles;public bool materials,textures,origin,rigidbodyAbsent;}
    [Serializable]class Report{public string unity;public string pipeline;public VariantReport[] prefabs;public int placedCopies;public bool success;}
    static void Validate()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var reports=new List<VariantReport>();
        foreach(string variant in new[]{"Empty","Filled"}){
            string path=A+"/Prefabs/PF_TrashBin_"+variant+".prefab";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"Missing prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var lod=go.GetComponent<LODGroup>();Require(lod&&lod.lodCount==3,"LOD group invalid");
            var renderers=go.GetComponentsInChildren<MeshRenderer>(true);var bounds=new Bounds();bool first=true;
            foreach(var r in lod.GetLODs()[0].renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            var tris=new List<int>();var meshes=new List<MeshReport>();
            foreach(var level in lod.GetLODs()){
                int total=0;foreach(var r in level.renderers){var m=r.GetComponent<MeshFilter>().sharedMesh;total+=m.triangles.Length/3;
                    var mr=new MeshReport{name=m.name,vertices=m.vertexCount,triangles=m.triangles.Length/3,uv=m.uv.Length==m.vertexCount,normals=m.normals.Length==m.vertexCount,tangents=m.tangents.Length==m.vertexCount};
                    Require(mr.uv&&mr.normals&&mr.tangents,"Missing vertex data "+m.name);meshes.Add(mr);}
                tris.Add(total);
            }
            Require(tris[0]>tris[1]&&tris[1]>tris[2],"LOD triangle reduction invalid");
            bool materials=renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported&&!m.shader.name.Contains("Error")));
            bool textures=renderers.All(r=>r.sharedMaterials.All(m=>m.GetTexture("_BaseMap")!=null));
            Require(materials&&textures,"Missing materials/textures");Require(bounds.size.y>.87f&&bounds.size.y<1.02f&&bounds.size.x<.5f,"Incorrect scale");
            bool origin=Mathf.Abs(bounds.min.y)<.002f&&go.transform.position==Vector3.zero&&go.transform.localScale==Vector3.one&&Quaternion.Angle(go.transform.rotation,Quaternion.identity)<.001f;Require(origin,"Ground origin/scale/axis invalid");
            Require(go.transform.Find("TrashBin_Body")&&go.transform.Find("TrashBin_Supports")&&go.transform.Find("TrashBin_Colliders"),"Required hierarchy missing");
            if(variant=="Filled")Require(go.transform.Find("TrashContents/Plastic_Bottles")&&go.transform.Find("TrashContents/Paper_Waste")&&go.transform.Find("TrashContents/Plastic_Bags")&&go.transform.Find("TrashContents/Food_Packaging")&&go.transform.Find("TrashContents/Metal_Cans")&&go.transform.Find("TrashContents/Small_Debris"),"Contents hierarchy missing");
            Require(go.GetComponentsInChildren<Collider>().Length==9,"Collider count mismatch");Require(go.GetComponentsInChildren<Rigidbody>().Length==0,"Unexpected physics");
            var exactBounds=new Bounds();bool firstVertex=true;int outside=0;
            var trashRoot=go.transform.Find("TrashContents");
            for(int li=0;li<3;li++)foreach(var r in lod.GetLODs()[li].renderers){
                foreach(var v in r.GetComponent<MeshFilter>().sharedMesh.vertices){
                    var p=r.transform.TransformPoint(v);
                    Require(!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z),"Invalid imported vertex");
                    if(li==0){if(firstVertex){exactBounds=new Bounds(p,Vector3.zero);firstVertex=false;}else exactBounds.Encapsulate(p);}
                    if(trashRoot&&r.transform.IsChildOf(trashRoot)&&p.y<.878f){
                        float t=Mathf.Clamp01((p.y-.245f)/.635f);
                        if(Mathf.Abs(p.x)>.163f+.017f*t-.002f||Mathf.Abs(p.z)>.136f+.014f*t-.002f||p.y<.248f)outside++;
                    }
                }
            }
            Require(outside==0,$"Contents vertices outside tapered cavity: {outside}");
            Physics.SyncTransforms();
            Require(Physics.Raycast(new Vector3(0,1.2f,0),Vector3.down,out var bottomHit,2)&&Mathf.Abs(bottomHit.point.y-.251f)<.012f,$"Open cavity/bottom collider invalid ({bottomHit.collider?.name}, {bottomHit.point})");
            Require(Physics.Raycast(new Vector3(.8f,.75f,0),Vector3.left,out var wallHit,1)&&Mathf.Abs(wallHit.point.x-.178f)<.02f,$"Side collider invalid ({wallHit.collider?.name}, {wallHit.point})");
            reports.Add(new VariantReport{variant=variant,renderers=renderers.Length,colliders=9,size=exactBounds.size,contentsVerticesOutsideCavity=outside,lods=meshes.ToArray(),triangles=tris.ToArray(),materials=materials,textures=textures,origin=origin,rigidbodyAbsent=true});
            UnityEngine.Object.DestroyImmediate(go);
        }
        foreach(string variant in new[]{"Empty","Filled"}){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(A+"/Prefabs/PF_TrashBin_"+variant+".prefab");
            for(int i=0;i<8;i++){var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab);copy.transform.position=new Vector3(i*.7f,0,variant=="Empty"?0:1);}
        }
        File.WriteAllText(Path.Combine(V,"unity_validation.json"),JsonUtility.ToJson(new Report{unity=Application.unityVersion,pipeline=GraphicsSettings.defaultRenderPipeline.name,prefabs=reports.ToArray(),placedCopies=16,success=true},true));
    }
    public static void Build()
    {
        try{
            Directory.CreateDirectory(V);AssetDatabase.Refresh();ImportTextures();var mats=Materials();ImportModels(mats);Prefab("Empty",mats);Prefab("Filled",mats);Pipeline();AssetDatabase.SaveAssets();Validate();Renders();AssetDatabase.SaveAssets();
            Debug.Log("TRASHBIN_BUILD_PASS");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(V,"unity_error.txt"),e.ToString());EditorApplication.Exit(1);}
    }
    public static void Reopen()
    {
        try{Directory.CreateDirectory(V);Pipeline();Validate();Renders();Debug.Log("TRASHBIN_REOPEN_PASS");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
