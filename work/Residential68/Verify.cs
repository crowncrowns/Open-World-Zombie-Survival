var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Residential68_Test/Residential_Building_Test.unity");
var root=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Shkolnaya_68"));
UnityEngine.Physics.SyncTransforms();
var issues=new System.Collections.Generic.List<string>();int probes=0;int unsupported=0;float minHead=100;
void Probe(UnityEngine.Vector3 foot,string name){
 var hits=UnityEngine.Physics.OverlapCapsule(foot+UnityEngine.Vector3.up*.37f,foot+UnityEngine.Vector3.up*1.57f,.29f,1<<29,UnityEngine.QueryTriggerInteraction.Ignore).Where(c=>c.gameObject.scene==scene).ToArray();probes++;
 if(hits.Length>0)issues.Add(name+" "+foot.ToString("F2")+" : "+string.Join(",",hits.Select(c=>c.name)));
 if(!UnityEngine.Physics.RaycastAll(foot+UnityEngine.Vector3.up*.04f,UnityEngine.Vector3.down,.30f,1<<29).Any(h=>h.collider.gameObject.scene==scene)){unsupported++;issues.Add("No support: "+name+foot.ToString("F2"));}
 var ceilings=UnityEngine.Physics.RaycastAll(foot+UnityEngine.Vector3.up*.2f,UnityEngine.Vector3.up,5,1<<29).Where(h=>h.collider.gameObject.scene==scene).OrderBy(h=>h.distance).ToArray();if(ceilings.Length>0)minHead=Mathf.Min(minHead,ceilings[0].distance+.2f);
}
foreach(float c in new[]{7.5f,25.5f,43.5f})for(int level=0;level<3;level++){
 float y=level*3;
 for(int i=0;i<=40;i++){float t=i/40f;float z=-1.68f+t*2.65f;float h=Mathf.Min(1.5f,.1666667f+(z+1.7f)*(1.5f/2.7f));Probe(new UnityEngine.Vector3(c-.9f,y+h+.02f,z),"up flight "+level);Probe(new UnityEngine.Vector3(c+.9f,y+1.5f+h+.02f,-.7f-z),"return flight "+level);}
 for(int i=0;i<=20;i++){float x=c-.9f+i*.09f;Probe(new UnityEngine.Vector3(x,y+1.52f,1.75f),"turn landing");}
 for(int i=0;i<=20;i++){float z=(level==0?-5.8f:-5.4f)+i*(level==0?.155f:.135f);Probe(new UnityEngine.Vector3(c,y+.02f,z),"foyer");}
}
var missing=root.GetComponentsInChildren<UnityEngine.Transform>(true).SelectMany(t=>t.GetComponents<UnityEngine.Component>()).Count(c=>!c);
var report=new {probeCount=probes,blocked=issues.Count,unsupported,minimumVerticalHeadroom=minHead,missingComponents=missing,issues=issues.Take(60).ToArray(),prefabInstances=root.GetComponentsInChildren<UnityEngine.Transform>(true).Count(t=>UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)),thinFlights=root.GetComponentsInChildren<UnityEngine.MeshFilter>().Count(m=>m.sharedMesh&&UnityEditor.AssetDatabase.GetAssetPath(m.sharedMesh).Contains("ThinStairs_Visual")),meshColliders=root.GetComponentsInChildren<UnityEngine.MeshCollider>().Length};
System.IO.File.WriteAllText("work/Residential68/geometry-check.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;

