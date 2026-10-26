using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TerrainRoads;
using Object=UnityEngine.Object;

namespace ZombieSurvival.WorldAuthoring.Editor
{
    // Explicit one-shot authoring transaction. Never runs from OnEnable/OnValidate or Play.
    public static class TerrainReliefSmoothing
    {
        const string ExpectedScene="Assets/Scenes/SampleScene_CorrectedRoads_20261025.unity";
        const string Work="work/TerrainBroadSmoothing_20261026/";
        const string Report="Verification/TerrainBroadSmoothing_20261026/";
        sealed class Pose
        {public Transform target,parent;public Vector3 position,scale;public Quaternion rotation;}
        sealed class Session
        {
            public Scene scene;public Terrain terrain;public TerrainCollider collider;public TerrainData original,replacement,backupData;
            public float[,] before,after;public Vector3 origin,size;public int n;public TerrainGrounding.Plan grounding;
            public string backupDirectory,originalDiskHash,originalHeightsHash,paintHash,backupAsset,newAsset,stage;
            public readonly Dictionary<string,string> dependencies=new();
            public readonly List<Pose> poses=new();public readonly Dictionary<TerrainRoadSurfaceMap,string> maps=new();
            public bool applied;
        }
        static Session session;
        static void Stage(string value)
        {session.stage=value;Directory.CreateDirectory(Report);File.AppendAllText(Report+"Progress.txt",DateTime.UtcNow.ToString("O")+" "+value+"\n");}
        static void Guard()
        {if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=ExpectedScene)throw new InvalidOperationException("Нужна рабочая CorrectedRoads сцена в Edit после компиляции.");}
        static void Folder(string path)
        {string at="Assets";foreach(string part in path.Split('/').Skip(1)){string next=at+"/"+part;if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(at,part);at=next;}}
        static string Hash(byte[] data){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}
        static string FileHash(string path){using var sha=SHA256.Create();using var f=File.OpenRead(path);return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");}
        static byte[] Bytes(Array array){var data=new byte[Buffer.ByteLength(array)];Buffer.BlockCopy(array,0,data,0,data.Length);return data;}
        static string PaintHash(TerrainData d)
        {
            using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
            w.Write(Bytes(d.GetAlphamaps(0,0,d.alphamapWidth,d.alphamapHeight)));w.Write(Bytes(d.GetHoles(0,0,d.holesResolution,d.holesResolution)));
            foreach(var layer in d.terrainLayers)w.Write(AssetDatabase.GetAssetPath(layer));
            w.Write(d.detailResolution);if(d.detailWidth>0&&d.detailHeight>0)foreach(var layer in d.GetSupportedLayers(0,0,d.detailWidth,d.detailHeight))w.Write(Bytes(d.GetDetailLayer(0,0,d.detailWidth,d.detailHeight,layer)));
            foreach(var tree in d.treeInstances){w.Write(tree.position.x);w.Write(tree.position.y);w.Write(tree.position.z);w.Write(tree.prototypeIndex);w.Write(tree.widthScale);w.Write(tree.heightScale);w.Write(tree.rotation);w.Write(tree.color.r);w.Write(tree.color.g);w.Write(tree.color.b);w.Write(tree.color.a);}
            w.Flush();return Hash(stream.ToArray());
        }
        static float Sample(float[,] heights,Vector3 p)
        {
            var s=session;float x=Mathf.Clamp((p.x-s.origin.x)/s.size.x*(s.n-1),0,s.n-1),z=Mathf.Clamp((p.z-s.origin.z)/s.size.z*(s.n-1),0,s.n-1);
            int x0=(int)x,z0=(int)z,x1=Math.Min(x0+1,s.n-1),z1=Math.Min(z0+1,s.n-1);
            return Mathf.Lerp(Mathf.Lerp(heights[z0,x0],heights[z0,x1],x-x0),Mathf.Lerp(heights[z1,x0],heights[z1,x1],x-x0),z-z0);
        }
        static Vector3 Normal(Vector3 p)
        {float dx=session.size.x/(session.n-1),dz=session.size.z/(session.n-1);return new Vector3((Sample(session.after,p-Vector3.right*dx)-Sample(session.after,p+Vector3.right*dx))/(2*dx),1,(Sample(session.after,p-Vector3.forward*dz)-Sample(session.after,p+Vector3.forward*dz))/(2*dz)).normalized;}
        static string MapDesign(TerrainRoadSurfaceMap map)
        {var json=Newtonsoft.Json.Linq.JObject.Parse(EditorJsonUtility.ToJson(map));json.Remove("generated");return json.ToString(Newtonsoft.Json.Formatting.None);}
        public static string Prepare()
        {
            Guard();if(session!=null)throw new InvalidOperationException("Сессия уже подготовлена: "+session.stage);
            var scene=SceneManager.GetActiveScene();var terrain=Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include).Single(t=>t.gameObject.scene==scene);
            var d=terrain.terrainData;session=new Session{scene=scene,terrain=terrain,collider=terrain.GetComponent<TerrainCollider>(),original=d,n=d.heightmapResolution,origin=terrain.transform.position,size=d.size};
            Stage("prepare");
            try
            {
            var s=session;int n=s.n;var raw=d.GetHeights(0,0,n,n);s.before=new float[n,n];s.after=new float[n,n];s.originalHeightsHash=Hash(Bytes(raw));
            using(var old=new BinaryReader(File.OpenRead(Work+"BeforeHeights.f32")))using(var next=new BinaryReader(File.OpenRead(Work+"Candidate_Sigma150.f32")))
            {
                if(old.BaseStream.Length!=n*n*4L||next.BaseStream.Length!=n*n*4L)throw new InvalidOperationException("Размер карты не совпал.");
                for(int z=0;z<n;z++)for(int x=0;x<n;x++)
                {float h=raw[z,x]*s.size.y+s.origin.y;if(Mathf.Abs(h-old.ReadSingle())>.0002f)throw new InvalidOperationException("Terrain изменён после анализа.");float a=next.ReadSingle();if(!float.IsFinite(a)||a<s.origin.y||a>s.origin.y+s.size.y)throw new InvalidOperationException("Некорректная высота.");s.before[z,x]=h;s.after[z,x]=a;}
            }
            s.originalDiskHash=FileHash(AssetDatabase.GetAssetPath(d));s.paintHash=PaintHash(d);
            foreach(var path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(d),true).Where(p=>p!=AssetDatabase.GetAssetPath(d)&&File.Exists(p)))s.dependencies[path]=FileHash(path);
            foreach(var map in Object.FindObjectsByType<TerrainRoadSurfaceMap>(FindObjectsInactive.Include).Where(m=>m.gameObject.scene==scene&&m.isActiveAndEnabled))
            {map.CaptureMaterials();s.maps.Add(map,MapDesign(map));}
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(!s.maps.Keys.Any(m=>t!=m.transform&&t.IsChildOf(m.transform)))s.poses.Add(new Pose{target=t,parent=t.parent,position=t.position,rotation=t.rotation,scale=t.localScale});
            string project=Directory.GetParent(Application.dataPath).FullName;
            s.backupDirectory=Path.Combine(Directory.GetParent(project).FullName,"_ProjectBackups","TerrainBroadSmoothing_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(s.backupDirectory);
            File.Copy(scene.path,Path.Combine(s.backupDirectory,"SceneOnDiskBefore.unity"));File.Copy(AssetDatabase.GetAssetPath(d),Path.Combine(s.backupDirectory,"OriginalTerrain.asset"));File.Copy(AssetDatabase.GetAssetPath(d)+".meta",Path.Combine(s.backupDirectory,"OriginalTerrain.asset.meta"));
            if(!EditorSceneManager.SaveScene(scene,Path.Combine(s.backupDirectory,"LiveBefore.unity"),true))throw new InvalidOperationException("Не удалось сохранить копию живой сцены.");
            Stage("scene backup complete");Folder("Assets/TerrainEditing/Backups");s.backupData=Object.Instantiate(d);s.backupData.name="Vidnoe Before Broad Smoothing";
            s.backupAsset=AssetDatabase.GenerateUniqueAssetPath("Assets/TerrainEditing/Backups/Vidnoe_BeforeBroadSmoothing_20261026.asset");AssetDatabase.CreateAsset(s.backupData,s.backupAsset);
            s.grounding=TerrainGrounding.BuildPlan(scene,terrain,p=>Sample(s.before,p),p=>Sample(s.after,p),Normal);
            File.WriteAllText(Report+"GroundPlanBefore.json",s.grounding.ToJson());
            File.WriteAllText(Report+"Backup.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{directory=s.backupDirectory,terrainBackup=s.backupAsset,original=AssetDatabase.GetAssetPath(d),originalHash=s.originalDiskHash,paintHash=s.paintHash,scene=scene.path,transforms=s.poses.Count},Newtonsoft.Json.Formatting.Indented));
            Stage("prepared");return s.grounding.ToJson();
            }
            catch(Exception e){File.WriteAllText(Report+"PreparationError.txt",e.ToString());Stage("preparation failed");throw;}
        }
        public static string Apply()
        {
            Guard();var s=session;if(s==null||s.stage!="prepared")throw new InvalidOperationException("Сначала Prepare; текущее состояние: "+s?.stage);
            if(s.terrain.terrainData!=s.original)throw new InvalidOperationException("TerrainData заменён после анализа.");
            if(Hash(Bytes(s.original.GetHeights(0,0,s.n,s.n)))!=s.originalHeightsHash)throw new InvalidOperationException("Высоты изменены после анализа; новый план обязателен.");
            foreach(var pose in s.poses)if(!pose.target||pose.target.position!=pose.position||Quaternion.Angle(pose.target.rotation,pose.rotation)>.001f||pose.target.localScale!=pose.scale||pose.target.parent!=pose.parent)throw new InvalidOperationException("Сцена изменена после анализа; новый план обязателен.");
            Stage("applying terrain clone");
            try
            {
                Folder("Assets/TerrainEditing");s.replacement=Object.Instantiate(s.backupData);s.replacement.name="Vidnoe Smooth Relief 150m";
                var values=new float[s.n,s.n];for(int z=0;z<s.n;z++)for(int x=0;x<s.n;x++)values[z,x]=(s.after[z,x]-s.origin.y)/s.size.y;
                s.replacement.SetHeights(0,0,values);
                s.newAsset=AssetDatabase.GenerateUniqueAssetPath("Assets/TerrainEditing/Vidnoe_SmoothedRelief_150m_20261026.asset");AssetDatabase.CreateAsset(s.replacement,s.newAsset);
                if(PaintHash(s.replacement)!=s.paintHash)throw new InvalidOperationException("Раскраска/holes/растительность копии изменились.");
                Undo.RecordObject(s.terrain,"Плавный рельеф Видного");if(s.collider)Undo.RecordObject(s.collider,"Плавный рельеф Видного");
                s.terrain.terrainData=s.replacement;if(s.collider)s.collider.terrainData=s.replacement;s.applied=true;s.terrain.Flush();Physics.SyncTransforms();
                EditorUtility.SetDirty(s.terrain);if(s.collider)EditorUtility.SetDirty(s.collider);PrefabUtility.RecordPrefabInstancePropertyModifications(s.terrain);
                Stage("terrain assigned; grounding");s.grounding.Apply("Привязать окружение к плавному Terrain");
                var errors=s.grounding.Verify();if(errors.Count>0)throw new InvalidOperationException(string.Join(";",errors.Take(10)));
                File.WriteAllText(Report+"GroundPlanAfter.json",s.grounding.ToJson());EditorSceneManager.MarkSceneDirty(s.scene);Physics.SyncTransforms();SceneView.RepaintAll();
                Stage("applied; roads pending");return "Terrain и окружение обновлены; дороги ещё не перестроены.";
            }
            catch{Restore();throw;}
        }
        public static string UpdateRoads()
        {
            Guard();var s=session;if(s==null||s.stage!="applied; roads pending")throw new InvalidOperationException("Неверный этап.");Stage("rebuilding current roads");
            foreach(var map in s.maps.Keys)if(!map.Rebuild())throw new InvalidOperationException("Дороги: "+map.diagnostic);
            foreach(var road in Object.FindObjectsByType<TerrainConformingRoad>(FindObjectsInactive.Include).Where(r=>r.gameObject.scene==s.scene&&r.isActiveAndEnabled&&!r.network))if(!road.Rebuild())throw new InvalidOperationException("Отдельная дорога: "+road.diagnostic);
            Physics.SyncTransforms();Stage("roads updated");return string.Join("\n",s.maps.Keys.Select(m=>m.diagnostic));
        }
        public static string Verify()
        {
            Guard();var s=session;if(s==null||!s.applied)throw new InvalidOperationException("Нет применённого рельефа.");var errors=s.grounding.Verify();
            foreach(var p in s.poses)
            {
                if(!p.target){errors.Add("Потерян исходный объект.");continue;}
                if(Mathf.Abs(p.target.position.x-p.position.x)>.002f||Mathf.Abs(p.target.position.z-p.position.z)>.002f||Quaternion.Angle(p.rotation,p.target.rotation)>.01f||(p.scale-p.target.localScale).sqrMagnitude>1e-8f||p.target.parent!=p.parent)errors.Add("Изменена планировка/поворот/масштаб: "+p.target.name);
            }
            if(s.terrain.terrainData!=s.replacement||s.collider&&s.collider.terrainData!=s.replacement)errors.Add("Terrain/Collider не используют новый рельеф.");
            if(FileHash(AssetDatabase.GetAssetPath(s.original))!=s.originalDiskHash)errors.Add("Исходный Terrain.asset изменён.");
            if(Hash(Bytes(s.original.GetHeights(0,0,s.n,s.n)))!=s.originalHeightsHash)errors.Add("Исходная карта высот изменена в памяти.");
            foreach(var pair in s.dependencies)if(!File.Exists(pair.Key)||FileHash(pair.Key)!=pair.Value)errors.Add("Изменён исходный слой/текстура/зависимость Terrain: "+pair.Key);
            if(PaintHash(s.original)!=s.paintHash||PaintHash(s.replacement)!=s.paintHash)errors.Add("Раскраска/holes/растительность изменились.");
            foreach(var map in s.maps.Keys)if(MapDesign(map)!=s.maps[map])errors.Add("Изменены настройки/планировка/материалы дорог.");
            var actual=s.replacement.GetHeights(0,0,s.n,s.n);float maxError=0;
            using(var writer=new BinaryWriter(File.Create(Report+"AfterHeights.f32")))for(int z=0;z<s.n;z++)for(int x=0;x<s.n;x++){float h=actual[z,x]*s.size.y+s.origin.y;writer.Write(h);maxError=Mathf.Max(maxError,Mathf.Abs(h-s.after[z,x]));}
            if(maxError>.008f)errors.Add("Heightmap не соответствует плану: "+maxError);
            var probes=new List<object>();
            foreach(var map in s.maps.Keys)
            {
                int count=0;float min=float.PositiveInfinity,max=float.NegativeInfinity;
                foreach(var f in map.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Дорога ")||f.name.StartsWith("Узел ")))
                {var m=f.sharedMesh;if(!m)continue;var v=m.vertices;var tri=m.triangles;int step=Math.Max(3,tri.Length/60/3*3);for(int i=0;i<tri.Length;i+=step){var p=f.transform.TransformPoint((v[tri[i]]+v[tri[i+1]]+v[tri[i+2]])/3);float gap=p.y-(s.terrain.SampleHeight(p)+s.origin.y);min=Mathf.Min(min,gap);max=Mathf.Max(max,gap);count++;}}
                probes.Add(new{name=map.name,count,min,max});if(count==0||min<-.03f||max>.15f)errors.Add("Дороги не прилегают: "+min+".."+max);
            }
            var result=new{pass=errors.Count==0,errors,originalUnchanged=FileHash(AssetDatabase.GetAssetPath(s.original))==s.originalDiskHash,paintUnchanged=PaintHash(s.replacement)==s.paintHash,terrainDependenciesChecked=s.dependencies.Count,maxHeightQuantizationError=maxError,layoutObjects=s.poses.Count,groundingTransforms=s.grounding.TransformCount,groundingMeshes=s.grounding.MeshCount,grassLayers=s.grounding.GrassLayerCount,roads=probes,newTerrainAsset=s.newAsset,backupDirectory=s.backupDirectory};
            string json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);File.WriteAllText(Report+"Verification.json",json);return json;
        }
        public static string Commit()
        {
            Guard();var s=session;if(s==null||s.stage!="roads updated")throw new InvalidOperationException("Дороги ещё не обновлены.");
            var check=Newtonsoft.Json.Linq.JObject.Parse(Verify());if(!(bool)check["pass"])throw new InvalidOperationException("Не все проверки пройдены.");
            AssetDatabase.SaveAssetIfDirty(s.replacement);Stage("saving reviewed scene");if(!EditorSceneManager.SaveScene(s.scene))throw new InvalidOperationException("Не удалось сохранить сцену.");Stage("complete");return s.newAsset;
        }
        public static string Status()=>session==null?"No session":session.stage;
        public static void Restore()
        {
            var s=session;if(s==null)return;s.grounding?.Restore();if(s.terrain)s.terrain.terrainData=s.original;if(s.collider)s.collider.terrainData=s.original;s.terrain?.Flush();s.applied=false;
            foreach(var map in s.maps.Keys)if(map)map.Rebuild();foreach(var road in Object.FindObjectsByType<TerrainConformingRoad>(FindObjectsInactive.Include).Where(r=>r.gameObject.scene==s.scene&&r.isActiveAndEnabled&&!r.network))road.Rebuild();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(s.scene);Stage("restored before state; review before save");
        }
        public static string Capture(string label)
        {
            Guard();var s=session;if(s==null)throw new InvalidOperationException("Prepare first.");var b=Object.FindObjectsByType<ModularBuildings.GeneratedBuilding>(FindObjectsInactive.Include).FirstOrDefault(b=>b.gameObject.scene==s.scene&&b.name=="Building_demo-building-010");if(!b)throw new InvalidOperationException("Нет контрольного дома.");
            var centre=b.transform.TransformPoint(new Vector3((b.entrances-1)*ModularBuildings.BuildingFootprint.SectionWidth(b)/2,8,0));
            var cameraObject=new GameObject("Terrain relief review camera");cameraObject.hideFlags=HideFlags.HideAndDontSave;var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=s.scene;camera.fieldOfView=65;camera.aspect=16f/9;camera.nearClipPlane=.1f;camera.farClipPlane=1500;camera.transform.position=centre+b.transform.TransformVector(new Vector3(85,45,-115));camera.transform.LookAt(centre);
            var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Directory.CreateDirectory(Report);string path=Report+label+".png";File.WriteAllBytes(path,image.EncodeToPNG());return path;}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(cameraObject);}
        }
    }
}
