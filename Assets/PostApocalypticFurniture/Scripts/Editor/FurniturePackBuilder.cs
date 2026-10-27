using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using ZombieSurvival.Furniture;
using ZombieSurvival.Interaction;
using ZombieSurvival.Persistence;
using ZombieSurvival.Player;

namespace ZombieSurvival.Furniture.Editor
{
    public static class FurniturePackBuilder
    {
        const string Root = "Assets/PostApocalypticFurniture";
        static string Proof => Path.GetFullPath(Path.Combine(Application.dataPath,Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/")?"../../../../Verification/PostApocalypticFurniture":"../Verification/PostApocalypticFurniture"));
        [Serializable] public sealed class Catalog { public Item[] assets; }
        [Serializable] public sealed class Item {
            public int id,variant; public string category,categoryRu,name,slug; public int[] triangles;
            public Part[] parts; public Collision[] colliders; public Zone[] zones; public Lamp[] lights;
            public Device[] devices; public string[] damageGroups; public float[] boundsMin,boundsMax;
        }
        [Serializable] public sealed class Part { public string name,kind,label,parent; public float[] pivot,axis; public float amount; }
        [Serializable] public sealed class Collision { public string part,type; public float[] center,size,rotation,a,b; public float radius; }
        [Serializable] public sealed class Zone { public string name,part; public float[] center,size; }
        [Serializable] public sealed class Lamp { public string part,type; public float[] position; public float intensity,range; }
        [Serializable] public sealed class Device { public string kind,dial,needle,speaker; }
        [Serializable] public sealed class Entry {
            public string name,category; public int[] triangles; public int materials,lods,interactions,colliders,zones; public List<string> problems=new List<string>();
        }
        [Serializable] public sealed class Audit { public string unity; public bool passed; public Entry[] assets; }
        static Vector3 V(float[] a) => a!=null&&a.Length==3?new Vector3(a[0],a[1],a[2]):Vector3.zero;
        static void Guard() { if(!Application.dataPath.Replace('\\','/').Contains("/work/PostApocalypticFurniture/UnityProject/")) throw new InvalidOperationException("Сборщик работает только в work/PostApocalypticFurniture/UnityProject."); }
        static Catalog Read() => JsonUtility.FromJson<Catalog>(File.ReadAllText(Root+"/Documentation/catalog.json"));
        static void Folder(string p) { Directory.CreateDirectory(p); }
        static T Save<T>(T asset,string path) where T:UnityEngine.Object {
            Folder(Path.GetDirectoryName(path)); var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old!=null) { EditorUtility.CopySerialized(asset,old); UnityEngine.Object.DestroyImmediate(asset);return old; }
            AssetDatabase.CreateAsset(asset,path);return asset;
        }
        public static void Build()
        {
            Guard();Directory.CreateDirectory(Proof);
            try {
                var catalog=Read(); if(catalog.assets.Length!=200)throw new Exception("Expected exactly 200 furniture models");
                SetupPipeline(); ImportTextures(); var materials=CreateMaterials(); var playlist=CreatePlaylist();
                foreach(var row in catalog.assets) { BuildPrefab(row,materials,playlist);Debug.Log("FURNITURE_PREFAB "+row.id); }
                CreateScenes(catalog);AssetDatabase.SaveAssets();AssetDatabase.Refresh(); ValidateAll();
                Debug.Log("FURNITURE_BUILD_PASS 200");EditorApplication.Exit(0);
            } catch(Exception e) { File.WriteAllText(Path.Combine(Proof,"unity_build_failure.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1); }
        }
        public static void Probe() {
            Guard();SetupPipeline();ImportTextures();var c=Read();c.assets=c.assets.Where(r=>new[]{1,3,71,131,141}.Contains(r.id)).ToArray();var materials=CreateMaterials();var playlist=CreatePlaylist();foreach(var row in c.assets)BuildPrefab(row,materials,playlist);AssetDatabase.SaveAssets();Debug.Log("FURNITURE_AXIS_PROBE_PASS");RenderCatalog(c);
        }
        static void SetupPipeline()
        {
            var renderer=Save(ScriptableObject.CreateInstance<UniversalRendererData>(),Root+"/DemoScenes/Settings/Furniture_Renderer.asset");
            var pipe=Save(ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>(),Root+"/DemoScenes/Settings/Furniture_URP.asset");
            var so=new SerializedObject(pipe);var list=so.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            so.FindProperty("m_DefaultRendererIndex").intValue=0;so.ApplyModifiedPropertiesWithoutUndo();
            pipe.shadowDistance=50;pipe.msaaSampleCount=4;pipe.renderScale=1;
            GraphicsSettings.defaultRenderPipeline=pipe;QualitySettings.renderPipeline=pipe;
            QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowResolution=UnityEngine.ShadowResolution.High;
        }
        static void ImportTextures()
        {
            foreach(var path in Directory.GetFiles(Root+"/Textures","*.png",SearchOption.AllDirectories)) {
                var p=path.Replace('\\','/');var t=(TextureImporter)AssetImporter.GetAtPath(p);if(t==null)continue;
                bool normal=p.EndsWith("Normal.png"),linear=normal||p.EndsWith("Mask.png")||p.EndsWith("AO.png")||p.EndsWith("Metallic.png")||p.EndsWith("Roughness.png")||p.EndsWith("Height.png");
                var wantedType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                bool dirty=t.textureType!=wantedType||t.sRGBTexture==linear||t.maxTextureSize!=2048||!t.mipmapEnabled||t.wrapMode!=TextureWrapMode.Repeat||t.anisoLevel!=4||t.textureCompression!=TextureImporterCompression.CompressedHQ;
                if(!dirty)continue;
                t.textureType=wantedType;t.sRGBTexture=!linear;
                t.maxTextureSize=2048;t.mipmapEnabled=true;t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=4;
                t.textureCompression=TextureImporterCompression.CompressedHQ;t.SaveAndReimport();
            }
        }
        static Dictionary<string,Material> CreateMaterials()
        {
            var result=new Dictionary<string,Material>();
            var names=Directory.GetDirectories(Root+"/Textures").Select(Path.GetFileName).Concat(new[]{"Labels"}).Distinct();
            foreach(var name in names) {
                bool glass=name=="Glass";var shader=Shader.Find(glass?"Universal Render Pipeline/Lit":"PostApocalypticFurniture/Wear Lit");
                if(shader==null)throw new Exception("Missing furniture shader");var mat=new Material(shader){name=name,enableInstancing=true};
                string folder=Root+"/Textures/"+(name=="Labels"?"Paper":name)+"/";
                Texture2D Tex(string f)=>AssetDatabase.LoadAssetAtPath<Texture2D>(folder+f+".png");
                mat.SetTexture("_BaseMap",name=="Labels"?AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Labels_RU.png"):Tex("BaseColor"));
                float bump=1f;
                mat.SetTexture("_BumpMap",Tex("Normal"));mat.SetFloat("_BumpScale",bump);mat.EnableKeyword("_NORMALMAP");
                if(!glass){mat.SetColor("_DamageColor",new[]{"Steel","Rust","Chrome","Enamel","Brass"}.Contains(name)?new Color(.22f,.23f,.22f,1f):name.Contains("Fabric")||name.StartsWith("Carpet")?new Color(.28f,.25f,.19f,1f):new Color(.34f,.23f,.13f,1f));mat.SetTexture("_MaskMap",Tex("Mask"));mat.SetTexture("_WearMap",Tex("WearMask"));mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",1);}
                else { mat.SetColor("_BaseColor",new Color(.88f,.93f,.92f,.16f));mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;mat.SetOverrideTag("RenderType","Transparent");mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.82f); }
                if(name=="Emission")mat.SetColor("_EmissionColor",new Color(.20f,.13f,.04f));
                result[name]=Save(mat,Root+"/Materials/"+name+".mat");
            }
            return result;
        }
        static FurnitureRadioPlaylist CreatePlaylist()
        {
            var p=ScriptableObject.CreateInstance<FurnitureRadioPlaylist>();
            var clips=new[]{"OriginalInstrumental","QuietAtmosphere","EmptyFrequency","OriginalAnnouncer","SurvivorMessage","EmergencyBroadcast"};
            var names=new[]{"Музыка старого дома","Тихая волна","Пустая частота","Голос эфира","Станция Север","Экстренное сообщение"};
            p.Configure(clips.Select((c,i)=>new FurnitureRadioStation{label=names[i],clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/Radio/"+c+".wav")}).ToArray());
            return Save(p,Root+"/Audio/Radio/ApartmentStations.asset");
        }
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        static void SetName(Interactable c,string name) { var s=new SerializedObject(c);s.FindProperty("displayName").stringValue=name;s.ApplyModifiedPropertiesWithoutUndo(); }
        static AudioClip Clip(string folder,string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/"+folder+"/"+name+".wav");
        static void BuildPrefab(Item row,Dictionary<string,Material> materials,FurnitureRadioPlaylist playlist)
        {
            string path=Root+"/Models/"+row.category+"/"+row.slug+".fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(importer==null)throw new Exception("Missing FBX "+path);
            bool importChanged=importer.materialImportMode!=ModelImporterMaterialImportMode.None||importer.importAnimation||importer.importCameras||importer.importLights||!importer.bakeAxisConversion||importer.globalScale!=1||!importer.isReadable||!importer.generateSecondaryUV||importer.meshCompression!=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.bakeAxisConversion=true;importer.globalScale=1;importer.isReadable=true;importer.generateSecondaryUV=true;importer.meshCompression=ModelImporterMeshCompression.Off;if(importChanged)importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(model==null)throw new Exception("Corrupt FBX "+path);
            var go=new GameObject(row.slug);var assembly=go.AddComponent<FurnitureAssembly>();assembly.Configure(row.slug,row.name,row.category);
            try {
                var imported=(GameObject)PrefabUtility.InstantiatePrefab(model);imported.transform.SetParent(go.transform,false);imported.name="FBX_Source";
                // Unity recognizes _LOD# names and imports an automatic group. Keep exactly one authored owner.
                PrefabUtility.UnpackPrefabInstance(imported,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var automatic in imported.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(automatic);
                // FBX root pre-rotation is legal for static geometry but unsuitable as a mechanism's
                // authoring frame. Reparent the actual meshes in world space to explicit Unity axes.
                var visual=new GameObject("Visual");visual.transform.SetParent(go.transform,false);
                var transforms=new Dictionary<string,Transform>();
                foreach(var p in row.parts){var source=Find(imported.transform,p.name);if(source==null)throw new Exception(row.slug+" lost part "+p.name);
                    var t=new GameObject(p.name).transform;t.SetParent(visual.transform,false);t.localPosition=V(p.pivot);t.localRotation=Quaternion.identity;t.localScale=Vector3.one;
                    foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.transform.parent==source).ToArray())renderer.transform.SetParent(t,true);
                    transforms[p.name]=t;
                }
                foreach(var p in row.parts)if(!string.IsNullOrEmpty(p.parent)) {
                    if(!transforms.ContainsKey(p.parent)||p.parent==p.name)throw new Exception(row.slug+" invalid part parent "+p.name);
                    transforms[p.name].SetParent(transforms[p.parent],true);
                }
                UnityEngine.Object.DestroyImmediate(imported);
                if(row.id==158) {
                    var cordNode=new GameObject("TelephoneCord");cordNode.transform.SetParent(visual.transform,false);
                    var cord=cordNode.AddComponent<FurnitureCordVisual>();
                    cord.Configure(transforms["Body"],new Vector3(-.119f,.052f,-.040f),transforms["Receiver"],new Vector3(-.135f,.018f,0),materials["Rubber"],.0012f,.0055f,20,.07f);
                    var generated=cord.BakePreviewMeshes();var saved=new Mesh[3];
                    for(int i=0;i<3;i++)saved[i]=Save(generated[i],Root+"/LODs/F158_Cord_LOD"+i+".asset");
                    cord.SetPreviewMeshes(saved);
                }
                var meshRenderers=go.GetComponentsInChildren<MeshRenderer>(true);
                foreach(var r in meshRenderers){var mesh=r.GetComponent<MeshFilter>().sharedMesh;var names=mesh!=null?Enumerable.Range(0,mesh.subMeshCount).Select(i=>"Wood").ToArray():Array.Empty<string>();
                    // Names are retained in the FBX material remap table even when importing materials is disabled.
                    var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
                    var original=r.sharedMaterials;
                    for(int i=0;i<names.Length;i++)if(i<original.Length&&original[i]!=null)names[i]=original[i].name;
                    r.sharedMaterials=names.Select(n=>materials.ContainsKey(n)?materials[n]:materials["Wood"]).ToArray();
                }
                // FBX external material identifiers supply each mesh's authored slot order through a sidecar mapping.
                AssignAuthoredMaterials(row,go,materials);
                if(row.id==158&&transforms.ContainsKey("CordSource")) {
                    // The standalone FBX/blend contains the complete closed phone.
                    // The prefab replaces that one closed-pose tube with its deforming LODs.
                    foreach(var r in transforms["CordSource"].GetComponentsInChildren<MeshRenderer>(true))UnityEngine.Object.DestroyImmediate(r.gameObject);
                    meshRenderers=go.GetComponentsInChildren<MeshRenderer>(true);
                }
                foreach(var p in row.parts) {
                    Vector3 actual=go.transform.InverseTransformPoint(transforms[p.name].position);
                    if(Vector3.Distance(actual,V(p.pivot))>.005f)throw new Exception(row.slug+" FBX pivot/axis mismatch "+p.name+": "+actual+" expected "+V(p.pivot));
                }
                foreach(var c in row.colliders??Array.Empty<Collision>()) {
                    var part=transforms[c.part];var node=new GameObject("Collision");node.transform.SetParent(part,false);
                    if(c.type=="capsule") {var a=V(c.a);var b=V(c.b);node.transform.position=go.transform.TransformPoint((a+b)*.5f);node.transform.rotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
                        var capsule=node.AddComponent<CapsuleCollider>();capsule.direction=1;capsule.radius=c.radius;capsule.height=Mathf.Max(c.radius*2,(b-a).magnitude); }
                    else {node.transform.position=go.transform.TransformPoint(V(c.center));node.transform.rotation=Quaternion.Euler(V(c.rotation));var box=node.AddComponent<BoxCollider>();box.size=V(c.size);}
                }
                foreach(var z in row.zones??Array.Empty<Zone>()) {var n=new GameObject("Place_"+z.name);n.transform.SetParent(transforms[z.part],false);n.transform.position=go.transform.TransformPoint(V(z.center));n.AddComponent<FurniturePlacementZone>().Configure(z.name,new Bounds(Vector3.zero,V(z.size)));}
                var lamps=new List<Light>();foreach(var l in row.lights??Array.Empty<Lamp>()) {var n=new GameObject("FurnitureLight");n.transform.SetParent(transforms.ContainsKey(l.part)?transforms[l.part]:go.transform,false);n.transform.position=go.transform.TransformPoint(V(l.position));var light=n.AddComponent<Light>();light.type=LightType.Point;light.intensity=l.intensity;light.range=l.range;light.color=new Color(1,.79f,.52f);light.shadows=LightShadows.None;light.enabled=false;lamps.Add(light);}
                FurnitureDevice device=null;var data=row.devices?.FirstOrDefault();bool isFridge=data!=null&&data.kind=="fridge";
                if(data!=null&&!isFridge) {
                    device=go.AddComponent<FurnitureDevice>();device.SetStableId("main-device");var kind=data.kind=="radio"?FurnitureDeviceKind.Radio:data.kind=="light"?FurnitureDeviceKind.Light:data.kind=="television"?FurnitureDeviceKind.Television:FurnitureDeviceKind.General;
                    var indicators=meshRenderers.Where(r=>r.sharedMaterials.Any(m=>m!=null&&(m.name=="Emission"||m.name=="Screen"))).Cast<Renderer>().ToArray();
                    AudioSource source=kind==FurnitureDeviceKind.Radio||kind==FurnitureDeviceKind.Television?go.AddComponent<AudioSource>():null;
                    Transform Named(string n)=>string.IsNullOrEmpty(n)?null:Find(visual.transform,n);
                    device.Configure(kind,source,kind==FurnitureDeviceKind.Radio?playlist:null,lamps.ToArray(),indicators,Named(data.dial),Named(data.needle),Named(data.speaker),Clip("Electronics","Switch"),Clip("Electronics","Static"));
                    if(kind==FurnitureDeviceKind.Light||kind==FurnitureDeviceKind.Television){var settings=new SerializedObject(device);settings.FindProperty("indicatorColor").colorValue=kind==FurnitureDeviceKind.Light?new Color(1,.72f,.38f):new Color(.45f,.58f,.62f);settings.ApplyModifiedPropertiesWithoutUndo();}
                }
                foreach(var p in row.parts) {
                    var t=transforms[p.name];bool mechanism=new[]{"hinge","slide","lift","rotate"}.Contains(p.kind);
                    if(mechanism) {var m=t.gameObject.AddComponent<FurnitureMechanism>();bool rotation=p.kind=="hinge"||p.kind=="rotate";m.Configure(p.name,t,V(p.axis),p.amount,rotation,p.label,p.kind=="lift"?"Поднять":"Открыть",p.kind=="lift"?"Опустить":"Закрыть");m.ConfigureSound(Clip("Furniture",rotation?"Hinge":"Drawer"));SetName(m,row.name+" · "+p.label);}
                    else if(new[]{"power","channel","volume","brightness","tune"}.Contains(p.kind)) {if(device==null) {device=go.AddComponent<FurnitureDevice>();device.SetStableId("main-device");device.Configure(FurnitureDeviceKind.General,null,null,lamps.ToArray(),Array.Empty<Renderer>(),powerSound:Clip("Electronics","Switch"));}
                        var c=t.gameObject.AddComponent<FurnitureControl>();var action=(FurnitureControlKind)Enum.Parse(typeof(FurnitureControlKind),p.kind,true);c.Configure(p.name,device,action,p.label);c.ConfigureGeometry(V(p.axis),Mathf.Abs(p.amount)>1f,p.amount);SetName(c,row.name+" · "+p.label);
                        if(p.name.StartsWith("BandKey",StringComparison.Ordinal)&&int.TryParse(p.name.Substring(7),out int station))c.ConfigureStationPreset(station);
                        if(t.GetComponentsInChildren<Collider>().Length==0){var b=t.gameObject.AddComponent<BoxCollider>();b.size=new Vector3(.045f,.045f,.035f);}
                    }
                }
                if(isFridge)assembly.ConfigureCompartmentLighting(lamps.ToArray(),go.GetComponentsInChildren<FurnitureMechanism>(true).Where(m=>m.StableId.IndexOf("Freezer",StringComparison.OrdinalIgnoreCase)<0).ToArray());
                FurnitureMechanism Motion(string n)=>Find(visual.transform,n)?.GetComponent<FurnitureMechanism>();
                FurnitureInterlock Rule(FurnitureMechanism other,bool opening,float minimum,float maximum,string text)=>new FurnitureInterlock{other=other,whenOpening=opening,whenClosing=!opening,minimum=minimum,maximum=maximum,requireDestination=true,message=text};
                if(row.id==31)foreach(var side in new[]{"L","R"}){var leaf=Motion("DropLeaf_"+side);var gate=Motion("GateLeg_"+side);leaf.ConfigureInterlocks(new[]{Rule(gate,true,.95f,1,"Сначала сложите опору")});gate.ConfigureInterlocks(new[]{Rule(leaf,false,0,.05f,"Сначала поднимите крыло стола")});}
                if(row.id==35){var leaf=Motion("ExtensionLeaf");var left=Motion("SlidingTop_-1");var right=Motion("SlidingTop_1");leaf.ConfigureInterlocks(new[]{Rule(left,true,.95f,1,"Сначала раздвиньте обе половины стола"),Rule(right,true,.95f,1,"Сначала раздвиньте обе половины стола")});foreach(var top in new[]{left,right})top.ConfigureInterlocks(new[]{Rule(leaf,false,0,.05f,"Сначала опустите вкладку")});}
                if(row.id==1||row.id==2){var seat=Motion("LinenSeat");var back=Motion("FoldBack");seat.ConfigureInterlocks(new[]{Rule(back,true,0,.05f,"Сначала верните спинку")});back.ConfigureInterlocks(new[]{Rule(seat,true,0,.05f,"Сначала закройте бельевой ящик")});}
                if(row.id==27){var seat=Motion("FoldingSeat");var frame=Motion("FoldingRearFrame");frame.ConfigureInterlocks(new[]{Rule(seat,true,.95f,1,"Сначала поднимите сиденье")});seat.ConfigureInterlocks(new[]{Rule(frame,false,0,.05f,"Сначала разложите ножки")});}
                if(row.id==19) {
                    var main=Motion("BedExtension");var foot=Motion("BedFootExtension");var middle=Motion("BedRaise");var footLift=Motion("BedFootRaise");var legs=Motion("BedFootLegs");
                    var seat=Motion("LooseSeat");
                    main.ConfigureInterlocks(new[]{Rule(seat,true,0,.05f,"Сначала верните подушку сиденья"),Rule(foot,false,0,.05f,"Сначала задвиньте ножную секцию"),Rule(middle,false,0,.05f,"Сначала опустите среднюю секцию"),Rule(footLift,false,0,.05f,"Сначала опустите ножную секцию"),Rule(legs,false,0,.05f,"Сначала сложите опору")});
                    seat.ConfigureInterlocks(new[]{Rule(main,true,0,.05f,"Сначала сложите кресло-кровать")});
                    foot.ConfigureInterlocks(new[]{Rule(main,true,.95f,1,"Сначала выкатите основание"),Rule(middle,false,0,.05f,"Сначала опустите среднюю секцию"),Rule(footLift,false,0,.05f,"Сначала опустите ножную секцию"),Rule(legs,false,0,.05f,"Сначала сложите опору")});
                    middle.ConfigureInterlocks(new[]{Rule(main,true,.95f,1,"Сначала выкатите основание"),Rule(foot,true,.95f,1,"Сначала выдвиньте ножную секцию"),Rule(legs,false,0,.05f,"Сначала сложите опору")});
                    footLift.ConfigureInterlocks(new[]{Rule(main,true,.95f,1,"Сначала выкатите основание"),Rule(foot,true,.95f,1,"Сначала выдвиньте ножную секцию"),Rule(legs,false,0,.05f,"Сначала сложите опору")});
                    legs.ConfigureInterlocks(new[]{Rule(middle,true,.95f,1,"Сначала поднимите обе секции"),Rule(footLift,true,.95f,1,"Сначала поднимите обе секции")});
                }
                if(row.id==129){var door=Motion("DishwasherDoor");var baskets=new[]{Motion("Basket0"),Motion("Basket1")};foreach(var basket in baskets)basket.ConfigureInterlocks(new[]{Rule(door,true,.95f,1,"Сначала откройте дверцу")});door.ConfigureInterlocks(baskets.Select(b=>Rule(b,false,0,.05f,"Сначала задвиньте корзины")).ToArray());}
                if(isFridge&&Motion("FreezerFlap")!=null){var flap=Motion("FreezerFlap");var door=Motion("MainDoor");flap.ConfigureInterlocks(new[]{Rule(door,true,.95f,1,"Сначала откройте холодильник")});door.ConfigureInterlocks(new[]{Rule(flap,false,0,.05f,"Сначала закройте морозильное отделение")});}
                var lod=go.AddComponent<LODGroup>();var levels=new LOD[3];for(int i=0;i<3;i++){int index=i;levels[i]=new LOD(new[]{.60f,.25f,.075f}[i],meshRenderers.Where(r=>r.name.EndsWith("_LOD"+index)).Cast<Renderer>().ToArray());}lod.SetLODs(levels);lod.RecalculateBounds();lod.fadeMode=LODFadeMode.None;
                var wear=go.AddComponent<FurnitureWear>();wear.Configure(1009+row.id*79,.24f+row.variant*.045f,.16f+row.variant*.044f,.20f+row.variant*.07f,meshRenderers);
                if(row.damageGroups!=null&&row.damageGroups.Length>0)wear.SetDamageGroups(row.damageGroups.Select(n=>transforms[n].gameObject).ToArray(),row.damageGroups.Select(n=>.45f).ToArray());
                go.AddComponent<FurnitureWorldSaveAdapter>();
                string output=Root+"/Prefabs/"+row.category+"/"+row.slug+".prefab";Folder(Path.GetDirectoryName(output));PrefabUtility.SaveAsPrefabAsset(go,output);
            } finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        [Serializable] sealed class MaterialMap { public Slot[] meshes; }
        [Serializable] sealed class Slot { public string name; public string[] materials; }
        static void AssignAuthoredMaterials(Item row,GameObject go,Dictionary<string,Material> mats) {
            string path=Root+"/LODs/"+row.slug+".json";if(!File.Exists(path))throw new Exception("Missing authored material slots "+path);
            var map=JsonUtility.FromJson<MaterialMap>(File.ReadAllText(path));foreach(var entry in map.meshes){var r=Find(go.transform,entry.name)?.GetComponent<MeshRenderer>();if(r==null)throw new Exception("Material mesh missing "+entry.name);r.sharedMaterials=entry.materials.Select(n=>mats[n]).ToArray();}
        }
        static GameObject Instance(Item row,Vector3 pos,float yaw,int ordinal,List<WorldSaveObject> registry) {
            string p=Root+"/Prefabs/"+row.category+"/"+row.slug+".prefab";var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p));go.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));
            var id=new SerializedObject(go.GetComponent<SaveIdentity>());id.FindProperty("persistentId").stringValue="scene-furniture-"+ordinal.ToString("D5");id.ApplyModifiedPropertiesWithoutUndo();registry.Add(go.GetComponent<WorldSaveObject>());
            if(ordinal>=1000){float f=(ordinal*73%101)/100f;go.GetComponent<FurnitureWear>().Configure(ordinal*977,.15f+f*.7f,.12f+f*.65f,.1f+f*.8f,go.GetComponentsInChildren<Renderer>(true));}
            return go;
        }
        static Material Plain(string name,Color c) {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",c);return Save(m,Root+"/DemoScenes/Settings/"+name+".mat");}
        static GameObject Box(string name,Vector3 p,Vector3 s,Material m) {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
        static void Text(string text,Vector3 p,float scale=.10f) {var g=new GameObject("Label");g.transform.position=p;var t=g.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=56;t.characterSize=scale;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.19f,.17f,.12f);}
        static void Environment(Material floor,Vector3 size) {Box("Пол",new Vector3(size.x*.5f,-.075f,-size.z*.5f),new Vector3(size.x,.15f,size.z),floor);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.40f,.43f,.47f);var sun=new GameObject("Daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.transform.rotation=Quaternion.Euler(45,-35,0);sun.shadows=LightShadows.Soft;}
        static void Player(Vector3 p) {var go=new GameObject("FurnitureDemoPlayer");go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,180,0));var cc=go.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=Vector3.up*.9f;
            var cam=new GameObject("ViewCamera").AddComponent<Camera>();cam.transform.SetParent(go.transform,false);cam.transform.localPosition=new Vector3(0,1.65f,0);cam.nearClipPlane=.04f;cam.farClipPlane=200;cam.gameObject.AddComponent<AudioListener>();
            var motor=go.AddComponent<FirstPersonController>();var s=new SerializedObject(motor);s.FindProperty("inputActions").objectReferenceValue=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");s.FindProperty("playerCamera").objectReferenceValue=cam.transform;s.ApplyModifiedPropertiesWithoutUndo();
            var interactor=go.AddComponent<PlayerInteractor>();s=new SerializedObject(interactor);s.FindProperty("viewCamera").objectReferenceValue=cam;s.ApplyModifiedPropertiesWithoutUndo();go.AddComponent<FurnitureDemoHUD>().Configure(interactor);
        }
        static void Registry(List<WorldSaveObject> objects,string scope) {var r=new GameObject("FurnitureWorldSaveRegistry").AddComponent<WorldSaveRegistry>();var s=new SerializedObject(r);s.FindProperty("scopeId").stringValue=scope;var a=s.FindProperty("sceneObjects");a.arraySize=objects.Count;for(int i=0;i<objects.Count;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=objects[i];s.ApplyModifiedPropertiesWithoutUndo();}
        static void CreateScenes(Catalog c) {
            Folder(Root+"/DemoScenes");var floor=Plain("Concrete",new Color(.38f,.36f,.31f));var wall=Plain("Plaster",new Color(.58f,.57f,.48f));var platform=Plain("Plinth",new Color(.47f,.45f,.38f));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Environment(floor,new Vector3(41,0,115));var objects=new List<WorldSaveObject>();
            foreach(var r in c.assets){int cat=(r.id-1)/10;float x=3+r.variant*3.8f,z=-4-cat*5.5f;var g=Instance(r,new Vector3(x,0,z),0,r.id,objects);float lo=V(r.boundsMin).y;if(lo<-.005f)g.transform.position-=Vector3.up*lo;
                if(r.category=="Lighting"&&r.variant<3)g.transform.position+=Vector3.up*1.8f;
                Text(r.id.ToString("D3")+" · "+r.name+"\n"+r.triangles[0]+" треуг. · "+r.categoryRu,new Vector3(x,.08f,z+1.7f),.037f);
                if(r.variant==0){Text(r.categoryRu.ToUpperInvariant(),new Vector3(20,2.8f,z-1.6f),.15f);Box("Категория",new Vector3(20,1.5f,z-2.4f),new Vector3(40,3,.12f),wall);}
            }
            Player(new Vector3(3,.05f,-.3f));Registry(objects,"furniture-showcase-v1");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/DemoScenes/Furniture_Showcase.unity");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Environment(floor,new Vector3(56,0,38));objects.Clear();int ordinal=1000;
            int[] selection={5,11,23,31,45,53,67,72,81,92,86,111,112,121,124,134,141,155,162,174,181,184,191,195};
            Vector3[] layout={new Vector3(1.3f,0,-5.8f),new Vector3(.25f,0,-3.5f),new Vector3(3.1f,0,-.95f),new Vector3(3.1f,0,-2.05f),new Vector3(1.6f,0,-4.2f),new Vector3(7.0f,0,-5.65f),new Vector3(7.1f,.57f,-5.45f),new Vector3(9.6f,0,-5.25f),new Vector3(5.55f,0,-5.85f),new Vector3(9.45f,0,-3.5f),new Vector3(1.25f,0,-.75f),new Vector3(6,0,-.65f),new Vector3(6,1.35f,-.57f),new Vector3(6.8f,0,-.65f),new Vector3(6,.881f,-.63f),new Vector3(7.95f,0,-.65f),new Vector3(9.45f,.80f,-3.55f),new Vector3(1.25f,.46f,-.75f),new Vector3(1.6f,.008f,-3.8f),new Vector3(5.55f,.54f,-5.85f),new Vector3(9.75f,0,-1.0f),new Vector3(10.62f,.75f,-2.0f),new Vector3(9.2f,0,-7.02f),new Vector3(10.3f,0,-6.8f)};
            float[] turns={0,90,180,0,0,0,0,-90,0,-90,180,180,180,180,0,180,-90,180,0,0,-90,-90,0,0};
            for(int apartment=0;apartment<12;apartment++){
                float ox=3+(apartment%4)*13,oz=-3-(apartment/4)*12;Box("Квартира · стена",new Vector3(ox+4.6f,1.4f,oz-7.6f),new Vector3(11,2.8f,.15f),wall);Box("Квартира · перегородка",new Vector3(ox+4.8f,1.4f,oz-2),new Vector3(.12f,2.8f,6),wall);Box("Квартира · боковая стена",new Vector3(ox+10.75f,1.4f,oz-4),new Vector3(.12f,2.8f,7.2f),wall);
                for(int j=0;j<selection.Length;j++){var r=c.assets[selection[j]-1];var go=Instance(r,new Vector3(ox,0,oz)+layout[j],turns[j],ordinal++,objects);float min=V(r.boundsMin).y;if(min<0)go.transform.position-=Vector3.up*min;}
                Text("КВАРТИРА "+(apartment+1),new Vector3(ox+4.2f,2.7f,oz-7.48f),.10f);
            }
            Player(new Vector3(1,.05f,-.1f));Registry(objects,"furniture-stress-v1");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"/DemoScenes/Furniture_StressTest.unity");
            File.WriteAllText(Path.Combine(Proof,"scene_inventory.json"),"{\"showcase\":200,\"stressApartments\":12,\"stressFurniture\":"+objects.Count+"}");
        }
        [MenuItem("Tools/Environment/Furniture/Проверить 200 префабов")]
        public static void ValidateAll() {
            var entries=new List<Entry>();foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"})){var p=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var a=p.GetComponent<FurnitureAssembly>();var e=new Entry{name=p.name,category=a!=null?a.Category:"",triangles=new int[3]};
                var lod=p.GetComponent<LODGroup>();e.lods=lod!=null?lod.lodCount:0;if(e.lods!=3)e.problems.Add("Нужны три LOD");if(p.GetComponentsInChildren<LODGroup>(true).Length!=1)e.problems.Add("Renderer зарегистрирован в нескольких LODGroup");
                if(lod!=null)for(int i=0;i<Math.Min(3,lod.lodCount);i++)foreach(var r in lod.GetLODs()[i].renderers){if(r==null){e.problems.Add("Потерян LOD renderer");continue;}var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)e.problems.Add("Потерян mesh");else e.triangles[i]+=mesh.triangles.Length/3;}
                if(e.triangles[0]<=0||e.triangles[0]>18000)e.problems.Add("Бюджет LOD0");var all=p.GetComponentsInChildren<Renderer>(true);e.materials=all.SelectMany(r=>r.sharedMaterials).Distinct().Count();
                foreach(var r in all)if(r.sharedMaterials.Length==0||r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name=="Hidden/InternalErrorShader"))e.problems.Add("Материал или shader отсутствует");
                foreach(var t in p.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)e.problems.Add("Missing Script");
                e.interactions=p.GetComponentsInChildren<Interactable>(true).Length;e.colliders=p.GetComponentsInChildren<Collider>(true).Length;e.zones=p.GetComponentsInChildren<FurniturePlacementZone>(true).Length;
                if(e.colliders==0)e.problems.Add("Нет Collider");if(p.GetComponent<FurnitureWorldSaveAdapter>()==null)e.problems.Add("Нет адаптера сохранения");if(a==null||!a.ValidateState(a.CaptureState()))e.problems.Add("Невалидный блок состояния");entries.Add(e);
            }
            var audit=new Audit{unity=Application.unityVersion,passed=entries.Count==200&&entries.All(e=>e.problems.Count==0),assets=entries.OrderBy(e=>e.name).ToArray()};Directory.CreateDirectory(Proof);File.WriteAllText(Path.Combine(Proof,"unity_prefab_audit.json"),JsonUtility.ToJson(audit,true));
            if(!audit.passed)throw new Exception("Furniture prefab audit failed: "+string.Join("; ",entries.Where(e=>e.problems.Count>0).Select(e=>e.name+": "+string.Join(",",e.problems))));Debug.Log("FURNITURE_PREFAB_AUDIT_PASS 200");
        }
        public static void RenderAll() {
            Guard();SetupPipeline();RenderCatalog(Read());
        }
        static void RenderCatalog(Catalog c) {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Environment(Plain("PreviewFloor",new Color(.38f,.36f,.32f)),new Vector3(30,0,30));GameObject.Find("Пол").transform.position=new Vector3(0,-.075f,0);
            var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.backgroundColor=new Color(.30f,.32f,.33f);camera.clearFlags=CameraClearFlags.SolidColor;camera.fieldOfView=36;camera.nearClipPlane=.02f;camera.farClipPlane=60;
            var fill=new GameObject("Softbox").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1;fill.transform.rotation=Quaternion.Euler(25,140,0);
            string output=Path.Combine(Proof,"UnityPreviews");Directory.CreateDirectory(output);
            foreach(var row in c.assets){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+row.category+"/"+row.slug+".prefab");var go=UnityEngine.Object.Instantiate(prefab);go.GetComponent<LODGroup>().ForceLOD(0);go.GetComponent<FurnitureWear>().Apply();
                var renderers=go.GetComponentsInChildren<Renderer>();var b=new Bounds();bool first=true;foreach(var r in renderers.Where(r=>r.name.EndsWith("_LOD0"))){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}
                float radius=b.extents.magnitude;var target=b.center;camera.transform.position=target+new Vector3(1.3f,.83f,1.7f).normalized*Mathf.Max(.55f,radius*3.6f);camera.transform.LookAt(target);
                var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(512,512,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,row.id.ToString("D3")+".png"),tex.EncodeToPNG());
                RenderTexture.active=null;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("FURNITURE_NATIVE_RENDERS_PASS "+c.assets.Length);EditorApplication.Exit(0);
        }
        public static void ColdCheck() {
            Guard();ValidateAll();var counts=new List<int>();
            foreach(var scene in new[]{"Furniture_Showcase","Furniture_StressTest"}) {
                EditorSceneManager.OpenScene(Root+"/DemoScenes/"+scene+".unity",OpenSceneMode.Single);
                var roots=UnityEngine.Object.FindObjectsByType<WorldSaveObject>(FindObjectsInactive.Include);counts.Add(roots.Length);
                if(roots.Any(r=>string.IsNullOrEmpty(r.Id))||roots.Select(r=>r.Id).Distinct().Count()!=roots.Length)throw new Exception("Cold scene save IDs invalid "+scene);
                UnityEngine.Object.FindFirstObjectByType<WorldSaveRegistry>().ValidateCoverage();
            }
            if(counts[0]!=200||counts[1]!=288)throw new Exception("Cold scene inventory differs");
            File.WriteAllText(Path.Combine(Proof,"unity_cold_check.json"),"{\"passed\":true,\"showcase\":200,\"stress\":288,\"sceneIdsUnique\":true,\"registryCoverage\":true}");
            Debug.Log("FURNITURE_COLD_PASS");EditorApplication.Exit(0);
        }
        public static void ExportPackage() {
            Guard();AssetDatabase.Refresh();string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../outputs/PostApocalypticFurniture.unitypackage"));Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.ExportPackage(Root,path,ExportPackageOptions.Recurse);Debug.Log("FURNITURE_UNITYPACKAGE_PASS");EditorApplication.Exit(0);
        }
    }
}
