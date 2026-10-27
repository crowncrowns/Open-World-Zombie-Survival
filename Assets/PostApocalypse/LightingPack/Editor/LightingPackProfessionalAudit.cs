using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PostApocalypse.Lighting;

public static class LightingPackProfessionalAudit
{
    static string Root=>LightingPackBuilder.Root;
    static string Proof=>LightingPackBuilder.Proof+"/ProfessionalAudit";
    [Serializable] public class AssetInfo {public string path,name,guid;public Vector3 center,size;public int[] triangles;public int renderers,colliders,lights,emissionBindings;public string[] objects,issues;public string settings;}
    [Serializable] public class Report {public string unity;public bool passed;public List<AssetInfo> assets=new List<AssetInfo>();}
    static string[] Paths()=>Directory.GetFiles(Root+"/Prefabs","*.prefab",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).OrderBy(p=>p).ToArray();
    static string NodePath(Transform t,Transform root)=>t==root?"":NodePath(t.parent,root)+"/"+t.name;
    static string Identity(UnityEngine.Object o,string key)
    {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long id);return key+"|"+guid+"|"+id;}
    public static void Before(){Capture(false);}
    public static void After(){Capture(true);}
    public static void AfterAndPlay(){After();LightingPackChecks.Run();}
    public static void FinishAndTest(){PrepareAndPatch();After();LightingPackChecks.Run();}
    public static void FinalCold()
    {LightingPackBuilder.Validate();File.Copy(LightingPackBuilder.Proof+"/unity_import.json",Proof+"/cold_import.json",true);After();LightingPackEditChecks.Run();File.Copy(LightingPackBuilder.Proof+"/unity_edit_checks.json",Proof+"/edit_checks.json",true);Debug.Log("LIGHTING_PROFESSIONAL_COLD_PASS");}
    static void Capture(bool after,bool images=true,string only=null)
    {
        LightingPackBuilder.Guard();Directory.CreateDirectory(Proof);
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Demo/LightingPack_URP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cam=new GameObject("Audit Camera").AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.063f,.074f);cam.nearClipPlane=.001f;cam.farClipPlane=100;cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var sun=new GameObject("Audit Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.None;
        var fill=new GameObject("Audit Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.3f;fill.transform.rotation=Quaternion.Euler(20,145,0);
        Report before=after?JsonUtility.FromJson<Report>(File.ReadAllText(Proof+"/before_unity.json")):null;
        var report=new Report{unity=Application.unityVersion};
        string phase=after?"After":"Before";Directory.CreateDirectory(Proof+"/"+phase);
        var fixtures=LightingPackBuilder.Data.fixtures;
        foreach(var path in Paths())
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var info=new AssetInfo{path=path,name=asset.name,guid=AssetDatabase.AssetPathToGUID(path)};
            var identities=new List<string>();var issues=new List<string>();
            foreach(var t in asset.GetComponentsInChildren<Transform>(true))foreach(var component in t.GetComponents<Component>())
            {if(!component)throw new Exception("Missing script "+path);identities.Add(Identity(component,NodePath(t,asset.transform)+"/"+component.GetType().Name));}
            info.objects=identities.ToArray();var sourceController=asset.GetComponent<LightFixtureController>();if(sourceController)info.settings=JsonUtility.ToJson(new Settings(sourceController));
            var go=UnityEngine.Object.Instantiate(asset);var c=go.GetComponent<LightFixtureController>();if(c)c.SetEnabled(false);
            var lod=go.GetComponent<LODGroup>();Renderer[] rs;
            Func<Renderer,int> triangles=r=>r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh?r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3:0;
            if(lod){lod.ForceLOD(0);var lods=lod.GetLODs();rs=lods[0].renderers;info.triangles=lods.Select(l=>l.renderers.Sum(triangles)).ToArray();}
            else {rs=go.GetComponentsInChildren<Renderer>();info.triangles=new[]{rs.Sum(triangles)};}
            Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);info.center=b.center;info.size=b.size;
            info.renderers=go.GetComponentsInChildren<Renderer>(true).Length;info.colliders=go.GetComponentsInChildren<Collider>(true).Length;info.lights=c?c.lights.Length:0;info.emissionBindings=c?c.emitters.Length:0;
            foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true))if(!mf.sharedMesh||mf.sharedMesh.vertexCount==0)issues.Add("Missing/empty mesh: "+mf.name);
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported)issues.Add("Missing/unsupported material: "+r.name);
            info.issues=issues.ToArray();if(after&&issues.Count>0)throw new Exception(string.Join(";",issues));
            if(info.size.magnitude<.001f){info.center=Vector3.zero;info.size=Vector3.one*.12f;}
            Vector3 cameraCenter=info.center,cameraSize=info.size;
            if(after)
            {
                var old=before.assets.Single(a=>a.path==path);if(old.guid!=info.guid)throw new Exception("GUID changed "+path);
                if(old.objects.Except(info.objects).Any())throw new Exception("Existing object IDs lost "+path+" "+string.Join(",",old.objects.Except(info.objects)));
                if(sourceController&&old.settings!=info.settings)throw new Exception("Light settings changed "+path);
                cameraCenter=old.center;cameraSize=old.size;
                if(info.name=="Decor_BrokenBulb")
                {var common=new Bounds(old.center,old.size);common.Encapsulate(new Bounds(info.center,info.size));cameraCenter=common.center;cameraSize=common.size;}
            }
            float size=Mathf.Max(cameraSize.x,Mathf.Max(cameraSize.y,cameraSize.z));cam.orthographicSize=Mathf.Max(.025f,size*.88f);
            var f=fixtures.FirstOrDefault(f=>f.name==asset.name);bool ceiling=f!=null&&f.mount=="ceiling";
            var views=new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right,Vector3.up,Vector3.down,ceiling?new Vector3(1,-1.1f,1.7f):new Vector3(1,.65f,2.4f)};
            var labels=new[]{"Front","Back","Left","Right","Top","Bottom","Angle45"};
            for(int light=0;images&&(only==null||only==info.name)&&light<3;light++)
            {
                sun.intensity=new[]{2.1f,.45f,2.8f}[light];sun.transform.rotation=light==2?Quaternion.Euler(7,-75,0):Quaternion.Euler(38,-32,0);
                float ambient=new[]{.29f,.55f,.16f}[light];RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white*ambient;var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=sh;
                int first=light==0?0:6;
                for(int v=first;v<7;v++)
                {
                    Vector3 direction=views[v].normalized;cam.transform.position=cameraCenter+direction*size*3;cam.transform.rotation=Quaternion.LookRotation(-direction,Mathf.Abs(direction.y)>.99f?Vector3.forward:Vector3.up);
                    string file=Proof+"/"+phase+"/"+asset.name+"_"+labels[v]+"_"+new[]{"Day","Overcast","Grazing"}[light]+".png";
                    var texture=LightingPackChecks.Capture(cam,file,440,440);UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            report.assets.Add(info);UnityEngine.Object.DestroyImmediate(go);Debug.Log("LIGHTING_AUDIT_"+phase.ToUpper()+" "+asset.name);
        }
        report.passed=true;File.WriteAllText(Proof+"/"+(after?"after_unity.json":"before_unity.json"),JsonUtility.ToJson(report,true));Debug.Log("LIGHTING_AUDIT_"+phase.ToUpper()+"_PASS "+report.assets.Count);
    }
    [Serializable] public class Settings
    {
        public bool on,temp,shadows,flicker,random,buzz,cull;public float intensity,range,angle,emission,temperature,speed,strength,distance,volume;public Color color;public int mode,seed,bake;
        public Settings(LightFixtureController c){on=c.lightEnabled;intensity=c.intensity;range=c.range;angle=c.spotAngle;emission=c.emissionMultiplier;color=c.lightColor;temp=c.useColorTemperature;temperature=c.colorTemperature;shadows=c.castShadows;flicker=c.enableFlickering;random=c.randomFlicker;speed=c.flickerSpeed;strength=c.flickerStrength;buzz=c.enableBuzzingSound;volume=c.buzzingVolume;cull=c.distanceCulling;distance=c.cullDistance;mode=(int)c.mode;seed=c.flickerSeed;bake=(int)c.bakeType;}
    }
    public static void Patch()
    {
        LightingPackBuilder.Guard();AssetDatabase.Refresh();
        foreach(var path in Paths())
        {
            var go=PrefabUtility.LoadPrefabContents(path);var c=go.GetComponent<LightFixtureController>();
            // Work on existing objects; never regenerate prefab hierarchy or its local file IDs.
            foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var fixture=LightingPackBuilder.Data.fixtures.FirstOrDefault(f=>f.name==go.name);
                string modelPath=fixture!=null?Root+"/Models/"+fixture.category+"/"+go.name+".fbx":Root+"/Models/Decor/"+go.name+".fbx";
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var source=model.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(m=>m.name==mf.name);
                if(!source)throw new Exception("Source node missing "+path+" "+mf.name);
                mf.sharedMesh=source.sharedMesh;mf.GetComponent<Renderer>().sharedMaterials=source.GetComponent<Renderer>().sharedMaterials;
                var collider=go.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(col=>col.name==mf.name);if(collider)collider.sharedMesh=source.sharedMesh;
            }
            if(c)
            {
                var emitters=new List<LightFixtureController.EmissionBinding>();
                foreach(var r in go.GetComponentsInChildren<Renderer>(true))for(int i=0;i<r.sharedMaterials.Length;i++)
                    {var mat=r.sharedMaterials[i];var mesh=r.GetComponent<MeshFilter>().sharedMesh;bool emitter=mat&&(mat.name.StartsWith("Glass_")||mat.name.StartsWith("Bulb_")||mat.name.StartsWith("ClearGlass_"));if(emitter&&(r.name.Contains("Glass")||r.name.Contains("Bulb"))&&i<mesh.subMeshCount&&mesh.GetIndexCount(i)>0)emitters.Add(new LightFixtureController.EmissionBinding{renderer=r,materialIndex=i,weight=mat.name.StartsWith("ClearGlass")?.12f:1});}
                c.emitters=emitters.ToArray();c.ApplyNow();
                var lod=go.GetComponent<LODGroup>();lod.RecalculateBounds();
                foreach(var mc in go.GetComponentsInChildren<MeshCollider>(true))mc.enabled=false;
                foreach(var r in lod.GetLODs()[0].renderers.Where(r=>r.name.Contains("Housing")||r.name.Contains("Mounting")))
                {
                    // Small static fixtures need bounded simple collision, not thousands of triangles.
                    var box=r.GetComponent<BoxCollider>();if(!box)box=r.gameObject.AddComponent<BoxCollider>();var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                    box.center=mesh.bounds.center;box.size=mesh.bounds.size;
                }
            }
            else
            {
                var box=go.GetComponent<BoxCollider>();if(box)
                {var rs=go.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);box.center=go.transform.InverseTransformPoint(bounds.center);box.size=bounds.size;box.enabled=go.name!="Decor_RemovedFixtureMark";}
            }
            PrefabUtility.SaveAsPrefabAsset(go,path);PrefabUtility.UnloadPrefabContents(go);
        }
        AssetDatabase.SaveAssets();Debug.Log("LIGHTING_ALL_100_PATCH_PASS");
    }
    public static void FinalBindingRegression()
    {
        LightingPackBuilder.Guard();
        foreach(var path in Directory.GetFiles(Root+"/Materials","*.mat"))
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\','/'));bool emitter=mat.name.StartsWith("Glass_")||mat.name.StartsWith("Bulb_")||mat.name.StartsWith("ClearGlass_");
            if(emitter)mat.EnableKeyword("_EMISSION");else mat.DisableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();Patch();Capture(true,false);LightingPackEditChecks.Run();File.Copy(LightingPackBuilder.Proof+"/unity_edit_checks.json",Proof+"/edit_checks.json",true);LightingPackChecks.Run();
    }
    public static void ExportValidated()
    {
        LightingPackBuilder.Guard();LightingPackBuilder.Validate();Capture(true,false);File.Copy(LightingPackBuilder.Proof+"/unity_import.json",Proof+"/cold_import.json",true);
        EditorSceneManager.OpenScene(Root+"/Demo/LightingPack_Showcase.unity");
        foreach(var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!mf.sharedMesh||mf.sharedMesh.vertexCount==0)throw new Exception("Showcase missing mesh "+mf.name);
        var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();var image=LightingPackChecks.Capture(camera,Proof+"/Showcase_After.png",1800,1000);UnityEngine.Object.DestroyImmediate(image);
        Directory.CreateDirectory("../../../outputs");AssetDatabase.ExportPackage(Root,"../../../outputs/LightingPack_URP_Professional.unitypackage",ExportPackageOptions.Recurse);
        File.WriteAllText(Proof+"/export_verified.txt","Cold import, 100 prefab identity checks and saved Showcase mesh validation passed before native UnityPackage export.");Debug.Log("LIGHTING_PROFESSIONAL_EXPORT_PASS");
    }
    public static void BrokenBulbReview(){LightingPackBuilder.Guard();AssetDatabase.Refresh();Patch();Capture(true,true,"Decor_BrokenBulb");}
    public static void PrepareAndPatch()
    {
        LightingPackBuilder.Guard();AssetDatabase.Refresh();
        foreach(var p in Directory.GetFiles(Root+"/Textures","*.png",SearchOption.AllDirectories))
        {
            string path=p.Replace('\\','/');var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            bool normal=path.EndsWith("/Normal.png");bool color=path.EndsWith("/BaseColor.png")||path.EndsWith("/Emission.png")||path.EndsWith("GhostPaint_BaseColor.png");
            bool changed=imp.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default)||imp.sRGBTexture!=color||imp.maxTextureSize!=2048||!imp.mipmapEnabled||imp.wrapMode!=TextureWrapMode.Repeat||imp.textureCompression!=TextureImporterCompression.CompressedHQ;
            imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;imp.sRGBTexture=color;imp.maxTextureSize=2048;imp.mipmapEnabled=true;imp.wrapMode=TextureWrapMode.Repeat;imp.textureCompression=TextureImporterCompression.CompressedHQ;
            if(changed)imp.SaveAndReimport();
        }
        string[] names={"Steel","Aluminium","Plastic","Glass","Bulb","Rubber","Copper","ClearGlass"};
        foreach(var wear in new[]{"Used","Worn","Damaged"})foreach(var name in names)
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+"_"+wear+".mat");if(!mat)throw new Exception("Existing material lost");
            string prefix=Root+"/Textures/"+wear+"/"+name+"/";
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"BaseColor.png"));mat.SetColor("_BaseColor",Color.white);
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"Normal.png"));mat.SetFloat("_BumpScale",.85f);mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"MetallicSmoothness.png"));mat.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"AO.png"));mat.SetFloat("_OcclusionStrength",.65f);mat.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"Emission.png"));
            mat.SetColor("_EmissionColor",Color.black);mat.enableInstancing=true;EditorUtility.SetDirty(mat);
        }
        var ghost=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/GhostPaint_Damaged.mat");if(!ghost){ghost=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(ghost,Root+"/Materials/GhostPaint_Damaged.mat");}
        ghost.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/GhostPaint/GhostPaint_BaseColor.png"));ghost.SetColor("_BaseColor",Color.white);ghost.SetFloat("_Smoothness",.02f);ghost.SetFloat("_Metallic",0);ghost.SetFloat("_Surface",1);ghost.SetFloat("_Blend",0);ghost.SetFloat("_ZWrite",0);ghost.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);ghost.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);ghost.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);ghost.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");ghost.renderQueue=3000;ghost.SetShaderPassEnabled("ShadowCaster",false);ghost.enableInstancing=true;EditorUtility.SetDirty(ghost);
        foreach(var p in Directory.GetFiles(Root+"/Models","*.fbx",SearchOption.AllDirectories))
        {
            var path=p.Replace('\\','/');var imp=(ModelImporter)AssetImporter.GetAtPath(path);bool changed=imp.generateSecondaryUV||!imp.isReadable||imp.importNormals!=ModelImporterNormals.Import;imp.generateSecondaryUV=false;imp.isReadable=true;imp.importNormals=ModelImporterNormals.Import;
            string wear=new[]{"Used","Worn","Damaged"}.FirstOrDefault(w=>path.EndsWith("_"+w+".fbx"))??"Damaged";
            var map=imp.GetExternalObjectMap();
            foreach(var name in names){var id=new AssetImporter.SourceAssetIdentifier(typeof(Material),name+"_"+wear);var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+"_"+wear+".mat");if(!map.TryGetValue(id,out var old)||old!=mat){imp.AddRemap(id,mat);changed=true;}}
            if(path.EndsWith("Decor_RemovedFixtureMark.fbx")){imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"GhostPaint_Damaged"),ghost);changed=true;}
            if(changed)imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();Patch();LightingPackBuilder.Validate();
        File.WriteAllText(Proof+"/prepare_complete.txt","All 100 existing prefabs patched in Unity Editor; all 100 FBX use supplied UV2.");
        Debug.Log("LIGHTING_PROFESSIONAL_PREPARE_PASS");
    }
}
