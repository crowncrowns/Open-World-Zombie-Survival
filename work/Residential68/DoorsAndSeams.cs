var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Residential68_Test/Residential_Building_Test.unity");var root=scene.GetRootGameObjects().Single();UnityEngine.Physics.SyncTransforms();var errors=new System.Collections.Generic.List<string>();int probes=0;
void Test(UnityEngine.Vector3 p,string label){probes++;var hits=UnityEngine.Physics.OverlapCapsule(p+UnityEngine.Vector3.up*.37f,p+UnityEngine.Vector3.up*1.57f,.29f,1<<29,UnityEngine.QueryTriggerInteraction.Ignore).Where(c=>c.gameObject.scene==scene).ToArray();if(hits.Length>0)errors.Add(label+" "+p+" "+string.Join(",",hits.Select(c=>c.name)));if(!UnityEngine.Physics.RaycastAll(p+UnityEngine.Vector3.up*.04f,UnityEngine.Vector3.down,.30f,1<<29).Any(h=>h.collider.gameObject.scene==scene))errors.Add("No support "+label+p);}
for(int s=0;s<3;s++)for(int f=0;f<4;f++){
 float c=7.5f+18*s,y=3*f;
 if(f==0)for(int i=0;i<=20;i++)Test(new UnityEngine.Vector3(c,.02f,-7+i*.1f),"entry");
 foreach(float x in new[]{4.5f+18*s,13.5f+18*s})for(int i=0;i<=20;i++)Test(new UnityEngine.Vector3(x,y+.02f,-4.5f+i*.1f),"unit door");
 if(f>0)for(int i=0;i<=30;i++)Test(new UnityEngine.Vector3(c+.9f,y+.02f,-4.8f+i*.1f),"upper landing seam");
}
var result=new {probes,issues=errors.Count,errors=errors.Take(30).ToArray()};System.IO.File.WriteAllText("work/Residential68/door-seam-check.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
