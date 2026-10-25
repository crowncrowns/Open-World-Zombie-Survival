using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PostApocalypse.Balconies.Editor
{
    public static class BalconyPropSupportValidation
    {
        [Serializable] sealed class Catalog { public Module[] balconies; }
        [Serializable] sealed class Module { public string type; public string state; public Prop[] props; }
        [Serializable] sealed class Prop { public string name; public float[] box; public float surface; public string support; }
        [Serializable] sealed class Result { public string name; public int props; public int rayChecks; public string[] errors; public string[] diagnostics; }
        [Serializable] sealed class Report { public bool passed; public int balconies; public int props; public int rayChecks; public Result[] results; public string[] errors; }

        public static string ReportDirectory()
        {
            var project = Directory.GetParent(Application.dataPath);
            for (var directory = project; directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName,"Tools/PostApocalypseBalconies/build.py")))
                    return Path.Combine(directory.FullName,"Verification/PostApocalypseBalconies");
            return Path.Combine(project.FullName,"Verification/PostApocalypseBalconies");
        }

        public static List<string> CheckAll()
        {
            var errors=new List<string>();var results=new List<Result>();
            var text=AssetDatabase.LoadAssetAtPath<TextAsset>(BalconyKitBuilder.Root+"/Editor/BalconyPropSupport.json");
            if(text==null){errors.Add("Balcony prop support manifest is missing");return errors;}
            var catalog=JsonUtility.FromJson<Catalog>(text.text);
            if(catalog?.balconies==null||catalog.balconies.Length!=40){errors.Add("Expected 40 modules in prop support manifest");return errors;}
            int totalProps=0,totalRays=0;
            foreach(var module in catalog.balconies)
            {
                string directory=AssetDatabase.FindAssets("t:Prefab",new[]{BalconyKitBuilder.Prefabs})
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p).StartsWith("Balcony_"+module.type+"_",StringComparison.Ordinal)
                        &&Path.GetFileNameWithoutExtension(p).EndsWith("_"+module.state,StringComparison.Ordinal));
                var localErrors=new List<string>();var diagnostics=new List<string>();int rays=0;GameObject root=null;
                string name=directory==null?module.type+"_"+module.state:Path.GetFileNameWithoutExtension(directory);
                try
                {
                    if(directory==null)throw new InvalidDataException("Prefab not found");
                    root=PrefabUtility.LoadPrefabContents(directory);
                    var collider=root.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c=>c.name=="COLLISION");
                    if(collider==null||collider.sharedMesh==null)throw new InvalidDataException("Floor collider is missing");
                    Physics.SyncTransforms();
                    foreach(var prop in module.props)
                    {
                        totalProps++;
                        if(prop.box==null||prop.box.Length!=4){localErrors.Add(prop.name+": invalid footprint");continue;}
                        float x0=prop.box[0],x1=prop.box[1],z0=prop.box[2],z1=prop.box[3];
                        Func<float,float,bool> supported=(x,z)=>
                        {
                            rays++;
                            bool found=collider.Raycast(new Ray(new Vector3(x,prop.surface+.05f,z),Vector3.down),out var hit,.12f);
                            bool result=found&&Mathf.Abs(hit.point.y-prop.surface)<=.004f;
                            if(!result&&diagnostics.Count<3)
                                diagnostics.Add($"{prop.name}: x={x:F4}, z={z:F4}, expectedY={prop.surface:F4}, hit={(found?hit.point.y.ToString("F4"):"none")}, mesh={collider.sharedMesh.name}");
                            return result;
                        };
                        if(prop.support!="surviving floor")
                        {
                            if(!supported((x0+x1)*.5f,.51f))localErrors.Add(prop.name+": support plate has no crossbeam underneath");
                            continue;
                        }
                        foreach(float u in new[]{.06f,.5f,.94f})
                        foreach(float v in new[]{.06f,.5f,.94f})
                            if(!supported(Mathf.Lerp(x0,x1,u),Mathf.Lerp(z0,z1,v)))localErrors.Add(prop.name+": a foot or footprint edge is above a floor opening");
                        int covered=0;
                        for(int x=0;x<7;x++)for(int z=0;z<7;z++)
                            if(supported(Mathf.Lerp(x0,x1,(x+.5f)/7),Mathf.Lerp(z0,z1,(z+.5f)/7)))covered++;
                        if(covered/49f<.88f)localErrors.Add(prop.name+": insufficient surviving floor beneath footprint");
                    }
                }
                catch(Exception ex){localErrors.Add(ex.Message);}
                finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
                localErrors=localErrors.Distinct().ToList();
                errors.AddRange(localErrors.Select(e=>name+": "+e));totalRays+=rays;
                results.Add(new Result{name=name,props=module.props.Length,rayChecks=rays,errors=localErrors.ToArray(),diagnostics=diagnostics.ToArray()});
            }
            var report=new Report{passed=errors.Count==0,balconies=results.Count,props=totalProps,rayChecks=totalRays,results=results.ToArray(),errors=errors.ToArray()};
            string output=ReportDirectory();Directory.CreateDirectory(output);
            WriteReport(Path.Combine(output,"unity_prop_support.json"),JsonUtility.ToJson(report,true));
            Debug.Log($"BALCONY_PROP_SUPPORT_{(report.passed?"PASS":"FAIL")} {report.balconies} balconies / {report.props} props / {report.rayChecks} rays");
            return errors;
        }

        public static void WriteReport(string path,string contents)
        {
            // Replacing a complete file also works while an editor/search viewer
            // has the preceding report memory-mapped on Windows.
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temporary,contents);
            if(File.Exists(path))File.Replace(temporary,path,null);
            else File.Move(temporary,path);
        }
    }
}
