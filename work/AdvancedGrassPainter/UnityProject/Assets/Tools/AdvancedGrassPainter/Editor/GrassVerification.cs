using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdvancedGrassPainter.Editor
{
    public static class GrassVerification
    {
        const string Root="Assets/Tools/AdvancedGrassPainter";
        [Serializable] public sealed class Report
        {
            public string unity, pipeline, savedHash;
            public bool passed;
            public int plants, blades, chunks, rejected, draws;
            public long meshBytes;
            public double generationMs;
            public List<string> checks=new List<string>();
        }
        static Report report;
        static void Require(bool condition,string name){if(!condition)throw new Exception("GRASS CHECK FAILED: "+name);report.checks.Add("PASS "+name);Debug.Log("GRASS PASS "+name);}
        static void Isolated(){if(!Application.dataPath.Replace('\\','/').Contains("/work/AdvancedGrassPainter/"))throw new InvalidOperationException("Verification may only run in work/AdvancedGrassPainter isolated projects.");}
        static Material Mat(string name,Color color)
        {
            string path=Root+"/Demo/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;return m;
        }
        static GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 scale,Material material,int layer=8,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=layer;go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Pipeline()
        {
            Type assetType=Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            Type rendererType=Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            if(assetType==null||rendererType==null)throw new Exception("URP package missing in isolated verification project");
            var rp=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Root+"/Demo/Demo_URP.asset");
            if(!rp)
            {
                var rd=ScriptableObject.CreateInstance(rendererType);AssetDatabase.CreateAsset(rd,Root+"/Demo/Demo_Renderer.asset");
                rp=(RenderPipelineAsset)ScriptableObject.CreateInstance(assetType);AssetDatabase.CreateAsset(rp,Root+"/Demo/Demo_URP.asset");
                var so=new SerializedObject(rp);var array=so.FindProperty("m_RendererDataList");array.arraySize=1;array.GetArrayElementAtIndex(0).objectReferenceValue=rd;
                so.FindProperty("m_ShadowDistance").floatValue=100;so.FindProperty("m_MainLightShadowmapResolution").intValue=2048;so.ApplyModifiedPropertiesWithoutUndo();
            }
            GraphicsSettings.defaultRenderPipeline=rp;QualitySettings.renderPipeline=rp;
        }
        public static void BuildAndCheck()
        {
            Isolated();report=new Report{unity=Application.unityVersion,pipeline="URP 17.6.0"};
            try
            {
                Directory.CreateDirectory(Root+"/Verification");Directory.CreateDirectory(Root+"/Demo");Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Presets");AssetDatabase.Refresh();
                var shader=Shader.Find("AdvancedGrassPainter/URP Grass");Require(shader,"URP shader imported");
                var grassMat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Grass_URP.mat");if(!grassMat){grassMat=new Material(shader);AssetDatabase.CreateAsset(grassMat,Root+"/Materials/Grass_URP.mat");}
                grassMat.enableInstancing=true;
                for(int i=0;i<6;i++)
                {
                    string path=Root+"/Presets/"+((GrassKind)i)+".asset";var preset=AssetDatabase.LoadAssetAtPath<GrassPreset>(path);
                    if(!preset){preset=ScriptableObject.CreateInstance<GrassPreset>();AssetDatabase.CreateAsset(preset,path);}
                    preset.settings=new GrassSettings{kind=(GrassKind)i,height=i==0?.28f:i==3?.76f:.48f,width=i==0?.016f:.025f,spacing=i==0?.13f:i==3?.31f:.23f,density=i==0?.95f:.83f};
                    if(i==2){preset.settings.rootColor=new Color(.22f,.16f,.07f);preset.settings.tipColor=preset.settings.dryColor;}
                    EditorUtility.SetDirty(preset);
                }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Pipeline();
                var ground=Shape("Uneven mesh soil",PrimitiveType.Plane,new Vector3(0,-.025f,0),new Vector3(4.8f,1,4),Mat("Soil",new Color(.14f,.115f,.08f)),0);
                var concrete=Mat("OldConcrete",new Color(.35f,.32f,.27f));var stone=Mat("Stone",new Color(.25f,.26f,.23f));var rust=Mat("FenceRust",new Color(.19f,.105f,.065f));
                Shape("Building walls",PrimitiveType.Cube,new Vector3(-6,2.5f,3),new Vector3(7,5,7),concrete);
                Shape("Building ledge",PrimitiveType.Cube,new Vector3(-6,.22f,3),new Vector3(7.4f,.44f,7.4f),stone);
                Shape("Stone A colliderless",PrimitiveType.Sphere,new Vector3(3,.32f,-1),new Vector3(2.6f,1.5f,1.8f),stone,8,false);
                Shape("Stone B",PrimitiveType.Sphere,new Vector3(5,.25f,2),new Vector3(1.6f,1.1f,2.8f),stone);
                Shape("Stone C tilted",PrimitiveType.Cube,new Vector3(1,.15f,3),new Vector3(1.6f,.9f,1.2f),stone).transform.rotation=Quaternion.Euler(12,35,14);
                for(int i=0;i<10;i++)Shape("Fence post "+i,PrimitiveType.Cube,new Vector3(-4+i*1.6f,.7f,-6),new Vector3(.09f,1.4f,.09f),rust);
                for(int i=0;i<2;i++)Shape("Fence rail "+i,PrimitiveType.Cube,new Vector3(3.2f,.42f+i*.65f,-6),new Vector3(14.6f,.06f,.07f),rust);
                var arch=Shape("Overhead canopy",PrimitiveType.Cube,new Vector3(9,2.2f,6),new Vector3(3,.3f,3),concrete);
                var grass=new GameObject("Urban grass — editable layer").AddComponent<GrassDataStorage>();grass.material=grassMat;grass.gameObject.AddComponent<GrassWindController>();
                var s=AssetDatabase.LoadAssetAtPath<GrassPreset>(Root+"/Presets/NeglectedUrban.asset").settings.Copy();s.radius=14;s.edgeFalloff=.25f;s.surfaceLayers=1;s.obstacleLayers=~0;s.density=.9f;s.spacing=.21f;
                using(var collision=new GrassCollisionManager())
                {
                    collision.Refresh();Require(collision.Count>=30,"collider and colliderless renderer proxy cache");var gen=new GrassGenerationSystem(collision);
                    gen.Apply(grass,new Vector3(0,0,0),s,GrassBrushMode.Fill);Require(grass.plants.Count>500,"Fill around building rocks and fence creates grass");
                    string hash=Hash(grass);int count=grass.plants.Count;
                    var twin=new GameObject("Determinism check").AddComponent<GrassDataStorage>();gen.Apply(twin,Vector3.zero,s,GrassBrushMode.Fill);Require(Hash(twin)==hash,"identical seed and scene produce identical records");UnityEngine.Object.DestroyImmediate(twin.gameObject);
                    gen.Regenerate(grass,s);Require(Hash(grass)==hash,"locked seed regeneration matches stroke history");
                    var changed=s.Copy();changed.lockSeed=false;changed.globalSeed++;
                    gen.Regenerate(grass,changed);Require(Hash(grass)!=hash,"new seed changes distribution");string newHash=Hash(grass);changed.lockSeed=true;gen.Regenerate(grass,changed);Require(Hash(grass)==newHash,"new seed persists in stroke history after locking");
                    var geometryChange=changed.Copy();geometryChange.height*=.65f;gen.Regenerate(grass,geometryChange);Require(Hash(grass)!=newHash,"regeneration applies changed geometry settings");
                    var restore=s.Copy();restore.lockSeed=false;gen.Regenerate(grass,restore);Require(Hash(grass)==hash,"explicit original seed and settings restore the original field");
                    int violations=0;
                    foreach(var p in grass.plants)if(!collision.Project(p.position,s,out var hit)||!collision.Accept(p,hit,s))violations++;
                    Require(violations==0,"full blade envelopes plus reserved GPU wind pass collision recheck");
                    int independent=0;
                    foreach(var p in grass.plants)for(int blade=0;blade<GrassMeshGenerator.BladeCount(p);blade++)
                    {
                        var b=GrassMeshGenerator.GetBlade(p,blade);
                        for(int lod=0;lod<3;lod++){int segments=lod==0?6:lod==1?3:2;for(int j=0;j<segments;j++){float t=j/(float)segments,u=(j+1)/(float)segments;float r=b.HalfWidth(t)*1.05f+p.clearance+p.wind*u*u+b.height*.006f;
                            if(Physics.CheckCapsule(b.Center(t),b.Center(u),r,1<<8,QueryTriggerInteraction.Ignore))independent++;}}
                    }
                    Require(independent==0,"independent source-scene Physics.CheckCapsule finds no intersections in ALL THREE LODs");
                    Require(!grass.plants.Any(p=>p.position.x>7.5f&&p.position.x<10.5f&&p.position.z>4.5f&&p.position.z<7.5f),"no plants underneath canopy");
                    Require(!grass.plants.Any(p=>p.position.x>-9.5f&&p.position.x<-2.5f&&p.position.z>-.5f&&p.position.z<6.5f),"no roots inside building");
                    Require(!grass.plants.Any(p=>Vector3.Distance(p.position,new Vector3(3,0,-1))<.7f),"colliderless stone has a real geometry exclusion");
                    Undo.ClearAll();int undoBefore=grass.plants.Count;string undoHash=Hash(grass);Undo.IncrementCurrentGroup();
                    gen.Apply(grass,new Vector3(0,0,-2),new GrassSettings{radius=2.5f},GrassBrushMode.Erase);Undo.FlushUndoRecordObjects();int erased=grass.plants.Count;
                    Require(erased<undoBefore,"Erase removes an individual region");Undo.PerformUndo();Require(Hash(grass)==undoHash,"Undo restores exact records");Undo.PerformRedo();Require(grass.plants.Count==erased,"Redo reapplies erase");
                    grass.Changed();
                    // A moved obstacle must remove only newly invalid plants.
                    var moving=Shape("Moving test obstacle",PrimitiveType.Cube,new Vector3(2,.6f,-3.5f),new Vector3(3,1.2f,2),concrete);
                    int removed=gen.Cleanup(grass,s);Require(removed>0,"moved obstacle cleanup removes intersections");UnityEngine.Object.DestroyImmediate(moving);collision.Refresh();
                    var replace=s.Copy();replace.kind=GrassKind.Dry;replace.replaceFrom=GrassKind.NeglectedUrban;replace.radius=3;
                    gen.Apply(grass,new Vector3(4,0,-3),replace,GrassBrushMode.Replace);Require(grass.plants.Any(p=>p.kind==GrassKind.Dry),"Replace changes grass species after collision validation");
                    gen.Apply(grass,new Vector3(4,0,-3),replace,GrassBrushMode.Smooth);Require(grass.plants.Count>0,"Smooth keeps a valid editable field");
                    // Large Terrain test stays outside the composition and uses its real TerrainCollider.
                    var terrainData=new TerrainData{heightmapResolution=65,size=new Vector3(128,8,128)};var heights=new float[65,65];for(int z=0;z<65;z++)for(int x=0;x<65;x++)heights[z,x]=.06f*Mathf.Sin(x*.1f)*Mathf.Sin(z*.13f)+.1f;terrainData.SetHeights(0,0,heights);
                    var terrain=Terrain.CreateTerrainGameObject(terrainData);terrain.name="128m Terrain verification";terrain.transform.position=new Vector3(100,0,100);terrain.layer=0;
                    var td=new GameObject("Terrain temporary grass").AddComponent<GrassDataStorage>();var ts=s.Copy();ts.radius=23;ts.spacing=.55f;ts.projection=8;
                    gen.Apply(td,new Vector3(150,1,150),ts,GrassBrushMode.Fill);Require(td.plants.Count>100,"46m brush area on 128m nonflat Terrain");
                    Require(td.plants.All(p=>Mathf.Abs(p.position.y-(terrain.GetComponent<Terrain>().SampleHeight(p.position)))<.004f),"Terrain roots match sampled height");
                    UnityEngine.Object.DestroyImmediate(td.gameObject);UnityEngine.Object.DestroyImmediate(terrain);UnityEngine.Object.DestroyImmediate(terrainData);collision.Refresh();
                    for(int type=0;type<6;type++)
                    {
                        var p=GrassGenerationSystem.CreatePlant(new Vector3(18,0,-13+type*4),Vector3.up,new GrassSettings{kind=(GrassKind)type,height=.5f},527+type);
                        var mesh=GrassMeshGenerator.Build(new List<GrassPlant>{p},0,Vector3.zero);Require(mesh.vertexCount>100&&mesh.bounds.size.y>.02f,"organic segmented geometry "+(GrassKind)type);UnityEngine.Object.DestroyImmediate(mesh);
                    }
                    // A narrow support-mesh ray rejects a vertical wall even when its normal has the allowed layer.
                    var steep=Shape("Steep allowed mesh",PrimitiveType.Plane,new Vector3(30,2,0),new Vector3(.5f,1,.5f),concrete,0);steep.transform.rotation=Quaternion.Euler(0,0,70);collision.Refresh();
                    Require(!collision.Project(new Vector3(30,2,0),s,out _),"70 degree surface rejected by 55 degree slope limit");UnityEngine.Object.DestroyImmediate(steep);
                    var meshOnly=Shape("Colliderless paint surface",PrimitiveType.Plane,new Vector3(40,1,0),new Vector3(.6f,1,.6f),concrete,0,false);collision.Refresh();
                    Require(collision.Project(new Vector3(40,1,0),s,out var meshHit)&&Mathf.Abs(meshHit.point.y-1)<.002f,"painting projection works on colliderless mesh ground");UnityEngine.Object.DestroyImmediate(meshOnly);
                    var clipped=new GameObject("Bounds fill check").AddComponent<GrassDataStorage>();var bs=s.Copy();bs.radius=6;bs.edgeFalloff=0;
                    var bounds=new Bounds(new Vector3(13,0,1),new Vector3(2,5,5));gen.Apply(clipped,bounds.center,bs,GrassBrushMode.Fill,bounds);
                    Require(clipped.plants.Count>5&&clipped.plants.All(p=>bounds.Contains(p.position)),"Fill is clipped to selection Bounds");string clippedHash=Hash(clipped);gen.Regenerate(clipped,bs);Require(Hash(clipped)==clippedHash,"bounded Fill seed history is reproducible");UnityEngine.Object.DestroyImmediate(clipped.gameObject);
                    var regional=new GameObject("Selected zone regeneration").AddComponent<GrassDataStorage>();var rs=s.Copy();rs.radius=2;gen.Apply(regional,new Vector3(12,0,-2),rs,GrassBrushMode.Fill);string regionalHash=Hash(regional);rs.height*=.5f;gen.Regenerate(regional,rs,new Bounds(new Vector3(12,5,-2),new Vector3(5,.1f,5)));Require(regional.plants.Count>0&&Hash(regional)!=regionalHash&&regional.strokes.Count==0,"selected zone regeneration uses horizontal Bounds and preserves the edited baseline");UnityEngine.Object.DestroyImmediate(regional.gameObject);
                    var compound=new GameObject("One mesh ground plus wall");compound.transform.position=new Vector3(60,0,0);compound.layer=0;
                    var large=Shape("Oversized colliderless enclosure",PrimitiveType.Cube,new Vector3(0,300,0),Vector3.one*1000,concrete,8,false);collision.Refresh();var ls=s.Copy();ls.overhead=0;
                    Require(collision.Project(new Vector3(12,0,-3),ls,out var largeGround)&&!collision.Accept(GrassGenerationSystem.CreatePlant(largeGround.point,largeGround.normal,ls,71),largeGround,ls),"oversized spatial-index entry still rejects roots inside a closed mesh");UnityEngine.Object.DestroyImmediate(large);collision.Refresh();
                    var combined=new Mesh();combined.vertices=new[]{new Vector3(-300,0,-300),new Vector3(300,0,-300),new Vector3(-300,0,300),new Vector3(300,0,300),new Vector3(0,0,-3),new Vector3(0,2,-3),new Vector3(0,0,3),new Vector3(0,2,3)};combined.triangles=new[]{0,2,1,1,2,3,4,5,6,6,5,7};combined.RecalculateNormals();combined.RecalculateBounds();compound.AddComponent<MeshCollider>().sharedMesh=combined;collision.Refresh();
                    var sameSettings=s.Copy();sameSettings.clearance=.15f;sameSettings.overhead=0;sameSettings.height=.3f;
                    Require(collision.Project(new Vector3(59.95f,0,0),sameSettings,out var sameGround),"combined ground and wall has a paintable ground hit");
                    var nearWall=GrassGenerationSystem.CreatePlant(sameGround.point,sameGround.normal,sameSettings,31);
                    Require(!collision.Accept(nearWall,sameGround,sameSettings),"wall inside the supporting ground mesh still blocks the full blade envelope");
                    UnityEngine.Object.DestroyImmediate(compound);UnityEngine.Object.DestroyImmediate(combined);
                }
                var sun=new GameObject("Evening sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(32,-35,0);sun.intensity=1.7f;sun.color=new Color(1,.86f,.67f);sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.36f,.42f,.48f);var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.28f,.33f,.38f));RenderSettings.ambientProbe=sh;
                var camera=new GameObject("Grass demo camera").AddComponent<Camera>();camera.transform.position=new Vector3(15,10,-17);camera.transform.LookAt(new Vector3(-1,.5f,0));camera.nearClipPlane=.06f;camera.farClipPlane=180;camera.backgroundColor=new Color(.32f,.39f,.44f);camera.clearFlags=CameraClearFlags.SolidColor;
                grass.Changed();grass.rendering.RebuildIfNeeded();report.plants=grass.plants.Count;report.blades=grass.plants.Sum(GrassMeshGenerator.BladeCount);report.chunks=grass.rendering.manager.chunks.Count;report.meshBytes=grass.rendering.manager.bytes;report.rejected=grass.rejectedObstacles;report.generationMs=grass.generationMilliseconds;
                Require(report.chunks>4,"chunk partitioning");
                long[] verts=new long[3];foreach(var c in grass.rendering.manager.chunks.Values)for(int i=0;i<3;i++)verts[i]+=c.meshes[i].vertexCount;
                Require(verts[0]>verts[1]&&verts[1]>verts[2],"LOD reduces segments and density with nested subsets");
                var chunk=grass.rendering.manager.chunks.First();var meshBefore=chunk.Value.meshes[0];grass.Changed();grass.rendering.RebuildIfNeeded();
                Require(grass.rendering.manager.chunks[chunk.Key].meshes[0]==meshBefore,"unchanged chunk retains its mesh on rebuild");
                report.savedHash=Hash(grass);EditorSceneManager.SaveScene(grass.gameObject.scene,Root+"/Demo/AdvancedGrassPainter_Demo.unity");AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(Root+"/Demo/AdvancedGrassPainter_Demo.unity");grass=UnityEngine.Object.FindAnyObjectByType<GrassDataStorage>();Require(Hash(grass)==report.savedHash,"scene reload preserves all plant data and seeds");
                grass.rendering.RebuildIfNeeded();Require(grass.rendering.manager.chunks.Count==report.chunks,"scene reload rebuilds transient meshes");
                RenderViews();report.draws=grass.rendering.submittedDraws;Require(report.draws>0,"native camera submits visible grass chunks");
                foreach(var message in ShaderUtil.GetShaderMessages(shader))Require(message.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,"shader compiler "+message.message);
                report.passed=true;File.WriteAllText(Root+"/Verification/URP_report.json",JsonUtility.ToJson(report,true));Debug.Log("ADVANCED_GRASS_VERIFICATION_PASS");
            }
            catch(Exception e){report.passed=false;report.checks.Add(e.ToString());File.WriteAllText(Root+"/Verification/URP_report.json",JsonUtility.ToJson(report,true));Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static string Hash(GrassDataStorage data)
        {
            string json=JsonUtility.ToJson(new PlantList{plants=data.plants});using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","");
        }
        [Serializable]sealed class PlantList{public List<GrassPlant> plants;}
        public static void ReloadCheck()
        {
            Isolated();report=JsonUtility.FromJson<Report>(File.ReadAllText(Root+"/Verification/URP_report.json"));EditorSceneManager.OpenScene(Root+"/Demo/AdvancedGrassPainter_Demo.unity");
            var data=UnityEngine.Object.FindAnyObjectByType<GrassDataStorage>();Require(Hash(data)==report.savedHash,"cold Unity process reload preserves exact plant hash");
            data.rendering.RebuildIfNeeded();Require(data.rendering.manager.chunks.Count==report.chunks,"cold process rebuilds chunks");report.passed=true;File.WriteAllText(Root+"/Verification/URP_report.json",JsonUtility.ToJson(report,true));
            Debug.Log("ADVANCED_GRASS_COLD_RELOAD_PASS");
        }
        static void RenderViews()
        {
            var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            var poses=new[]{(new Vector3(15,10,-17),new Vector3(-1,.4f,0),"Overview"),(new Vector3(3,1.2f,-5.2f),new Vector3(1,.2f,-1),"Grass_Close"),(new Vector3(-.5f,1.4f,-4),new Vector3(-3.3f,.3f,0),"Building_Edge"),(new Vector3(5,.8f,-8),new Vector3(3,.3f,-5.9f),"Fence")};
            foreach(var pose in poses)
            {
                camera.transform.position=pose.Item1;camera.transform.LookAt(pose.Item2);var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Root+"/Verification/"+pose.Item3+".png",tex.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}

