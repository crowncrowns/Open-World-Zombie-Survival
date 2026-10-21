var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Residential68_Test/Residential_Building_Test.unity");var root=scene.GetRootGameObjects().Single();var interior=root.transform.Find("Interior zoning — APPROXIMATION, not a surveyed plan");var mat=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Residential68_Test/Approx_LightStone.mat");
// Only remove objects created by this task, never prefab source content.
foreach(var t in interior.GetComponentsInChildren<UnityEngine.Transform>().Where(t=>t.name.StartsWith("Room divider")||t.name=="Opening lintel"||t.name.StartsWith("Third unit division")||t.name=="Third unit door lintel").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
void Wall(string name,UnityEngine.Vector3 a,UnityEngine.Vector3 b,UnityEngine.Transform p){var g=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);g.name=name;g.layer=29;g.transform.SetParent(p,false);g.transform.localPosition=(a+b)*.5f+UnityEngine.Vector3.up*1.5f;g.transform.localScale=new UnityEngine.Vector3(UnityEngine.Vector3.Distance(a,b),3,.15f);g.transform.localRotation=UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.Cross((b-a).normalized,UnityEngine.Vector3.up));g.GetComponent<UnityEngine.Renderer>().sharedMaterial=mat;}
void Across(string name,float x0,float x1,float y,float z,float door,UnityEngine.Transform p){Wall(name,new UnityEngine.Vector3(x0,y,z),new UnityEngine.Vector3(door-.6f,y,z),p);Wall(name,new UnityEngine.Vector3(door+.6f,y,z),new UnityEngine.Vector3(x1,y,z),p);}
void Along(string name,float x,float y,float z0,float z1,float door,UnityEngine.Transform p){Wall(name,new UnityEngine.Vector3(x,y,z0),new UnityEngine.Vector3(x,y,door-.6f),p);Wall(name,new UnityEngine.Vector3(x,y,door+.6f),new UnityEngine.Vector3(x,y,z1),p);}
for(int f=0;f<4;f++)for(int s=0;s<3;s++){
 float x=18*s,y=3*f;var p=new UnityEngine.GameObject("Approximate apartment rooms "+(s+1)+" / "+(f+1));p.transform.SetParent(interior,false);
 // Generic kitchen/bathroom and living-room zoning, not an attributed real floor plan.
 Across("Approx left kitchen / entrance",x+.3f,x+5.65f,y,-.1f,x+4.5f,p.transform);
 Along("Approx left bath wall",x+2.3f,y,-3.25f,-.2f,-1.5f,p.transform);
 Across("Approx left bedrooms",x+.3f,x+5.65f,y,3.4f,x+4.5f,p.transform);
 Across("Approx right kitchen / entrance",x+10.95f,x+17.7f,y,-.1f,x+13.5f,p.transform);
 Along("Approx right bath wall",x+16,y,-3.25f,-.2f,-1.5f,p.transform);
 Across("Approx right rooms",x+10.95f,x+17.7f,y,3.4f,x+13.5f,p.transform);
 if(s!=1){
  Across("Approx rear apartment boundary",x+.3f,x+17.7f,y,5.4f,x+9.9f,p.transform);
  Wall("Approx left unit side extension",new UnityEngine.Vector3(x+5.8f,y,2.8f),new UnityEngine.Vector3(x+5.8f,y,5.3f),p.transform);
  Along("Approx passage to rear apartment",x+10.8f,y,-3.35f,5.3f,-2.1f,p.transform);
  // Access through the existing foyer wall is opened with a scene-only gap by disabling our solid bay.
  var wall=interior.GetComponentsInChildren<UnityEngine.Transform>().FirstOrDefault(t=>t.name=="Solid bay"&&UnityEngine.Vector3.Distance(t.position,new UnityEngine.Vector3(x+10.5f,y,-3.5f))<.1f);if(wall)wall.gameObject.SetActive(false);
  // A narrow vestibule opening above, with retained wall pieces to either side.
  Across("Approx rear passage foyer wall",x+9.1f,x+11.9f,y,-3.5f,x+9.9f,p.transform);
  foreach(float xx in new[]{x+5.6f,x+12.3f})Along("Approx rear room split",xx,y,5.6f,8.75f,6.3f,p.transform);
 }
}
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);return "Generic apartment room zoning added; all marked approximation";
