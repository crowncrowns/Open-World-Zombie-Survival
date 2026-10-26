using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PostApocalypse.Balconies;

namespace PostApocalypse.Balconies.Editor
{
    public static class BalconyKitValidation
    {
        sealed class Record
        {
            public string name;
            public string model;
            public bool passed;
            public List<string> checks = new List<string>();
            public List<string> errors = new List<string>();
            public int[] lodTriangles = new int[4];
            public Bounds bounds;
            public Bounds colliderBounds;
        }

        static readonly string[] Types = {
            "Balcony_01_Soviet", "Balcony_02_Khrushchevka", "Balcony_03_GlazedSoviet",
            "Balcony_04_ModernLoggia", "Balcony_05_Wooden", "Balcony_06_Fortified",
            "Balcony_07_Industrial", "Balcony_08_Overgrown", "Balcony_09_Burned", "Balcony_10_Collapsed"
        };

        [MenuItem("Tools/Zombie Survival/Post-Apocalypse Balconies/Validate Package")]
        public static void Run()
        {
            var records = new List<Record>();
            foreach (string type in Types)
            foreach (string state in new[] {"A","B","C","D"})
                records.Add(ValidatePrefab(type+"_"+state, BalconyKitBuilder.Prefabs+"/"+type+"/"+type+"_"+state+".prefab", true));

            int propCount=ValidateCategory("Props",BalconyKitBuilder.Prefabs+"/Props",16,records);
            int accessoryCount=ValidateCategory("Accessories",BalconyKitBuilder.Prefabs+"/Accessories",3,records);
            var errors=records.SelectMany(r=>r.errors.Select(e=>r.name+": "+e)).ToList();
            ValidateTextures(errors);
            ValidateMaterials(errors);
            ValidateModelImporters(errors);
            ValidateStateGeometry(records,errors);
            ValidateAdjacency(records.Where(r=>Types.Any(t=>r.name.StartsWith(t+"_",StringComparison.Ordinal))).ToList(),errors);
            errors.AddRange(BalconyPropSupportValidation.CheckAll());
            string sceneAbsolute=Path.Combine(Application.dataPath,"Environment/PostApocalypse/Balconies/Demo/Balcony_Showcase.unity");
            bool sceneExists=File.Exists(sceneAbsolute);
            if(!sceneExists)errors.Add("Missing showcase scene: "+BalconyKitBuilder.DemoScene);

            var report=new SerializableReport {
                passed=errors.Count==0,
                target="Unity 6000.6.3f1; URP 17.6.0",
                balconyPrefabs=records.Count(r=>Types.Any(t=>r.name.StartsWith(t+"_",StringComparison.Ordinal))),
                propPrefabs=propCount, doorAccessoryPrefabs=accessoryCount, showcaseScene=sceneExists,
                checks=new[]{"prefab and FBX references","four LOD levels and non-increasing triangle counts","visible materials and PBR map references","instancing-ready URP materials","floor collider bounds","3m adjacency envelope","named wall/door/front mount points","texture resolution","FBX import and lightmap UV settings","state geometry diversity","showcase scene file"},
                errors=errors.ToArray(),
                prefabs=records.Select(r=>new SerializableRecord {name=r.name,model=r.model,passed=r.passed,lodTriangles=r.lodTriangles,bounds=ToArray(r.bounds),colliderBounds=ToArray(r.colliderBounds),checks=r.checks.ToArray(),errors=r.errors.ToArray()}).ToArray()
            };
            string json=JsonUtility.ToJson(report,true);
            string path=Path.Combine(BalconyPropSupportValidation.ReportDirectory(),"unity_validation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));BalconyPropSupportValidation.WriteReport(path,json);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(errors.Count==0)Debug.Log($"BALCONY_KIT_VALIDATION_PASS 40 balconies / {propCount} props / {accessoryCount} door accessories");
            else Debug.LogError("BALCONY_KIT_VALIDATION_FAIL\n"+string.Join("\n",errors));
        }

        // JsonUtility does not serialize anonymous object graphs; this wrapper keeps the
        // report deterministic through a compact explicit JSON payload.
        [Serializable]
        sealed class SerializableReport
        {
            public bool passed;
            public string target;
            public int balconyPrefabs;
            public int propPrefabs;
            public int doorAccessoryPrefabs;
            public bool showcaseScene;
            public string[] checks;
            public string[] errors;
            public SerializableRecord[] prefabs;
        }

        [Serializable]
        sealed class SerializableRecord
        {
            public string name;
            public string model;
            public bool passed;
            public int[] lodTriangles;
            public float[] bounds;
            public float[] colliderBounds;
            public string[] checks;
            public string[] errors;
        }

        static float[] ToArray(Bounds b) => new[]{b.min.x,b.min.y,b.min.z,b.max.x,b.max.y,b.max.z};

        static Record ValidatePrefab(string name,string prefabPath,bool balcony)
        {
            var result=new Record{name=name,model=""};
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(prefab==null){result.errors.Add("Prefab missing: "+prefabPath);result.passed=false;return result;}
            result.checks.Add("prefab reference exists");
            var marker=prefab.GetComponent<BalconyKitModule>();
            if(balcony)
            {
                if(marker==null)result.errors.Add("BalconyKitModule missing");
                else if(!marker.HasValidMountLayout())result.errors.Add("Wall, door, or front mount points do not match authored dimensions");
                else result.checks.Add("3 m grid and door mount points");
            }
            var lod=prefab.GetComponent<LODGroup>();
            if(lod==null)result.errors.Add("LODGroup missing");
            else
            {
                var levels=lod.GetLODs();
                if(levels.Length!=4)result.errors.Add("Expected exactly four LOD levels, found "+levels.Length);
                else
                {
                    for(int i=0;i<4;i++)
                    {
                        var meshes=levels[i].renderers.Where(r=>r!=null).SelectMany(r=>r.GetComponents<MeshFilter>()).Select(m=>m.sharedMesh).Where(m=>m!=null).Distinct().ToArray();
                        int triangles=meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(s=>(int)(m.GetIndexCount(s)/3)));result.lodTriangles[i]=triangles;
                        if(meshes.Length==0)result.errors.Add("LOD"+i+" has no mesh");
                        if(i>0 && triangles>result.lodTriangles[i-1])result.errors.Add("LOD triangle count increases at LOD"+i);
                    }
                    result.checks.Add("four populated LODs");
                }
            }
            var geometry=prefab.transform.Find("Geometry");
            if(geometry==null)result.errors.Add("FBX instance missing under Geometry");
            else
            {
                result.model=PrefabUtility.GetCorrespondingObjectFromSource(geometry.gameObject)?.name ?? geometry.name;
                var renderers=geometry.GetComponentsInChildren<Renderer>(true).Where(r=>r.name!="COLLISION").ToArray();
                if(renderers.Length==0)result.errors.Add("No renderers in imported FBX");
                else
                {
                    foreach(var renderer in renderers)
                    {
                        if(renderer.sharedMaterials.Any(m=>m==null))result.errors.Add("Null material on "+renderer.name);
                        foreach(var material in renderer.sharedMaterials.Where(m=>m!=null))
                            if(material.name.StartsWith("M_Balcony_",StringComparison.Ordinal) && !material.HasProperty("_BaseMap"))result.errors.Add("Material has no Base Map: "+material.name);
                    }
                    result.checks.Add("materials assigned");
                }
                var lod0=FindNamed(geometry,"LOD0");
                if(lod0!=null)
                {
                    var rs=lod0.GetComponentsInChildren<Renderer>(true).Where(r=>r.name!="COLLISION").ToArray();
                    if(rs.Length>0){result.bounds=rs.Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});}
                }
                var col=FindNamed(geometry,"COLLISION");
                var meshCollider=col==null?null:col.GetComponent<MeshCollider>();
                if(meshCollider==null || meshCollider.sharedMesh==null)result.errors.Add("Missing simplified floor MeshCollider");
                else
                {
                    result.colliderBounds=TransformBounds(meshCollider.sharedMesh.bounds,meshCollider.transform);
                    result.checks.Add("floor collider follows the authored broken floor mesh");
                }
            }
            result.passed=result.errors.Count==0;
            return result;
        }

        static Transform FindNamed(Transform root,string name)
        {
            if(root.name==name)return root;
            foreach(Transform child in root){var found=FindNamed(child,name);if(found!=null)return found;}
            return null;
        }

        static Bounds TransformBounds(Bounds local,Transform t)
        {
            var result=new Bounds(t.TransformPoint(local.center),Vector3.zero);
            var ext=local.extents;
            for(int i=0;i<8;i++)result.Encapsulate(t.TransformPoint(local.center+Vector3.Scale(ext,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return result;
        }

        static int ValidateCategory(string category,string directory,int expected,List<Record> records)
        {
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{directory}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
            if(paths.Length!=expected)
            {
                records.Add(new Record{name=category,errors=new List<string>{$"Expected {expected} prefabs, found {paths.Length}"},passed=false});
            }
            foreach(var path in paths)records.Add(ValidatePrefab(Path.GetFileNameWithoutExtension(path),path,false));
            return paths.Length;
        }

        static void ValidateTextures(List<string> errors)
        {
            foreach(string family in new[]{"Concrete","PaintedMetal","CorrodedMetal","Timber","Glazing","Organic"})
            foreach(string kind in new[]{"BaseColor","Normal","Mask"})
            {
                string path=BalconyKitBuilder.Textures+"/"+family+"_"+kind+".png";
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(tex==null)errors.Add("Texture missing: "+path);
                else if(tex.width<2048||tex.height<2048)errors.Add("Texture below 2K: "+path+" ("+tex.width+"x"+tex.height+")");
            }
        }

        static void ValidateMaterials(List<string> errors)
        {
            var materials=AssetDatabase.FindAssets("t:Material",new[]{BalconyKitBuilder.Materials})
                .Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m=>m!=null).ToArray();
            if(materials.Length!=15)errors.Add("Expected 15 shared balcony materials, found "+materials.Length);
            foreach(var material in materials)
            {
                if(material.shader==null||material.shader.name!="Universal Render Pipeline/Lit")errors.Add(material.name+" is not using URP/Lit");
                if(!material.HasProperty("_BaseMap")||material.GetTexture("_BaseMap")==null)errors.Add(material.name+" has no Base Map texture");
                if(material.HasProperty("_BumpMap")&&material.GetTexture("_BumpMap")==null)errors.Add(material.name+" has no Normal Map texture");
                if(material.HasProperty("_MetallicGlossMap")&&material.GetTexture("_MetallicGlossMap")==null)errors.Add(material.name+" has no packed Mask Map texture");
                if(!material.enableInstancing)errors.Add(material.name+" has GPU instancing disabled");
            }
        }

        static void ValidateModelImporters(List<string> errors)
        {
            var paths=AssetDatabase.FindAssets("t:Model",new[]{BalconyKitBuilder.Models})
                .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(paths.Length!=59)errors.Add("Expected 59 FBX source models, found "+paths.Length);
            foreach(string path in paths)
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null){errors.Add("No ModelImporter for "+path);continue;}
                if(!importer.bakeAxisConversion)errors.Add("Axis conversion is not baked for "+path);
                if(!importer.generateSecondaryUV)errors.Add("Secondary lightmap UV generation is disabled for "+path);
                if(importer.globalScale!=1f)errors.Add("FBX scale is not 1 Unity unit per metre for "+path);
            }
        }

        static void ValidateStateGeometry(List<Record> records,List<string> errors)
        {
            foreach(string type in Types)
            {
                var group=records.Where(r=>r.name.StartsWith(type+"_",StringComparison.Ordinal)&&r.name.Length==type.Length+2).OrderBy(r=>r.name).ToArray();
                if(group.Length!=4)continue;
                var signatures=group.Select(r=>string.Join("|",r.lodTriangles)+"|"+Mathf.RoundToInt(r.bounds.size.x*1000)+"|"+Mathf.RoundToInt(r.bounds.size.y*1000)+"|"+Mathf.RoundToInt(r.bounds.size.z*1000)).Distinct().Count();
                if(signatures<2)errors.Add(type+" states share the same LOD/geometry bounds signature");
            }
        }

        static void ValidateAdjacency(List<Record> records,List<string> errors)
        {
            foreach(var r in records)
            {
                if(r.bounds.size.x>2.72f)errors.Add(r.name+" exceeds the 2.7 m width envelope");
                if(r.bounds.min.x< -1.37f||r.bounds.max.x>1.37f)errors.Add(r.name+" crosses a neighboring facade module boundary");
                if(r.bounds.min.z<-.02f)errors.Add(r.name+" protrudes behind the Z=0 facade plane");
                float depth=r.name.StartsWith("Balcony_04_",StringComparison.Ordinal)?1.5f:r.name.StartsWith("Balcony_05_",StringComparison.Ordinal)?1.4f:1.2f;
                if(r.bounds.max.z>depth+.03f)errors.Add(r.name+" exceeds its authored balcony depth");
                if(r.bounds.max.y>2.25f)errors.Add(r.name+" exceeds the 2.1 m doorway/headroom envelope unexpectedly");
                if(r.bounds.size.y>3f)errors.Add(r.name+" could overlap a balcony one 3 m storey above");
                if(r.colliderBounds.min.z<-.02f||r.colliderBounds.max.z>depth+.03f)errors.Add(r.name+" floor collider is outside its balcony slab");
            }
        }
    }
}
