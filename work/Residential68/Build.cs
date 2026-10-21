
if (EditorApplication.isPlaying) throw new Exception("Edit mode required");
string folder="Assets/Residential68_Test";
string scenePath=folder+"/Residential_Building_Test.unity";
if (System.IO.File.Exists(scenePath)) throw new Exception("Scene already exists; refuse overwrite");
System.IO.Directory.CreateDirectory(folder);
AssetDatabase.Refresh();
var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var root=new GameObject("Shkolnaya_68 — approximation");
GameObject Group(string name,Transform parent) {var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
Material Mat(string name,Color col) {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=col;AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
var brick=Mat("Approx_RedBrick",new Color(.48f,.21f,.135f));
var cream=Mat("Approx_LightStone",new Color(.76f,.73f,.63f));
var roofMat=Mat("Approx_DarkRoof",new Color(.18f,.21f,.20f));
var baseMat=Mat("Approx_Plinth",new Color(.28f,.19f,.16f));
var railMat=Mat("Approx_Metal",new Color(.13f,.16f,.16f));
var groundMat=Mat("Approx_Ground",new Color(.30f,.34f,.27f));
// A small original tiling texture, not a photograph of the building.
var tex=new Texture2D(256,128,TextureFormat.RGB24,false);tex.name="Approx_BrickPattern";
var px=new Color[256*128];
for(int yy=0;yy<128;yy++)for(int xx=0;xx<256;xx++){int row=yy/16;int bx=(xx+(row%2)*32)%64;bool mortar=yy%16<1||bx<1;float n=((xx*17+yy*29)%23)/230f;px[yy*256+xx]=mortar?new Color(.48f,.43f,.36f):new Color(.62f+n,.27f+n*.5f,.18f+n*.4f);}
tex.SetPixels(px);tex.Apply();System.IO.File.WriteAllBytes(folder+"/Approx_BrickPattern.png",tex.EncodeToPNG());AssetDatabase.ImportAsset(folder+"/Approx_BrickPattern.png");
brick.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Approx_BrickPattern.png"));brick.color=Color.white;brick.SetTextureScale("_BaseMap",new Vector2(3,3));
GameObject Box(string name,Vector3 pos,Vector3 size,Material mat,Transform parent,bool collision=true) {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
string walls="Assets/Maps/Buildings/Walls/Prefabs/";
GameObject Pref(string path,string name,Vector3 pos,float yaw,Transform parent) {var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception(path);var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);g.transform.localScale=Vector3.one;return g;}
void PaintWall(GameObject g) {foreach(var r in g.GetComponentsInChildren<Renderer>(true)) if(r.name.StartsWith("Wall_"))r.sharedMaterial=brick;}
void OpenDoor(GameObject g) {var pivot=g.transform.TransformPoint(new Vector3(-.44f,0,0));foreach(var t in g.GetComponentsInChildren<Transform>().Where(t=>t.name=="Door_Leaf"||t.name.StartsWith("Door_Handle")))t.RotateAround(pivot,g.transform.up,100);}
void Window(Vector3 pos,float yaw,Transform parent,bool interactive=false) {var g=Pref(walls+(interactive?"Wall_Window_Interactive":"Wall_Window_Static_Variant")+".prefab",interactive?"Interactive window — source unchanged":"Window bay",pos,yaw,parent);PaintWall(g);var wall=g.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="Wall_Window");var c=wall.gameObject.AddComponent<MeshCollider>();c.sharedMesh=wall.sharedMesh;c.convex=false;}
void Solid(Vector3 pos,float yaw,Transform parent) {var g=Pref(walls+"Wall_Solid_Module.prefab","Solid bay",pos,yaw,parent);PaintWall(g);}
void Door(Vector3 pos,float yaw,Transform parent) {var g=Pref(walls+"Wall_Door_Metal.prefab","Open metal door — approximation",pos,yaw,parent);PaintWall(g);OpenDoor(g);}
void Floor(float x,float y,float z,float sx,float sz,Transform parent) {var g=Pref("Assets/Maps/Buildings/Floors/Prefabs/Floor_3x3.prefab","Floor tile",new Vector3(x,y,z),0,parent);g.transform.localScale=new Vector3(sx/3,1,sz/3);}
float[] stairsX={7.5f,25.5f,43.5f};
var shell=Group("Exterior — dimensions 54 x 15 m APPROXIMATION",root.transform);
var interiors=Group("Interior zoning — APPROXIMATION, not a surveyed plan",root.transform);
var stairs=Group("Three stair cores — 3 m storeys",root.transform);
for(int level=0;level<4;level++) {
 float y=level*3;
 var storey=Group("Storey "+(level+1)+" — Y="+y+"m",shell.transform);
 for(int b=0;b<18;b++) {
  float x=1.5f+b*3;
  bool entrance=stairsX.Any(c=>Mathf.Abs(c-x)<.1f);
  if(level==0&&entrance)Door(new Vector3(x,y,-6),180,storey.transform);else Window(new Vector3(x,y,-6),180,storey.transform);
  Window(new Vector3(x,y,9),0,storey.transform,level==0&&b==2);
 }
 for(int b=0;b<5;b++){Window(new Vector3(0,y,-4.5f+b*3),-90,storey.transform);Window(new Vector3(54,y,-4.5f+b*3),90,storey.transform);}
 var fl=Group("Floor "+(level+1)+" — cutouts preserved",interiors.transform);
 for(int ix=0;ix<18;ix++)for(int iz=0;iz<5;iz++) {
  float x=ix*3+1.5f,z=-4.5f+iz*3;
  bool stairCol=stairsX.Any(c=>Mathf.Abs(c-x)<.1f);
  if(level>0&&stairCol)continue;
  Floor(x,y,z,3,3,fl.transform);
 }
 if(level>0)foreach(float c in stairsX){Floor(c,y,-4.75f,3,2.5f,fl.transform);Floor(c,y,5.75f,3,6.5f,fl.transform);}
 var plan=Group("Level "+(level+1)+" — 3+2+3 unit zoning APPROXIMATION",interiors.transform);
 for(int s=0;s<3;s++) {
  float lo=s*18, c=stairsX[s];
  // Foyer runs from the entry to the lower/upper stair landing; side doors serve approximate units.
  for(int b=0;b<6;b++){float x=lo+1.5f+3*b;if(Mathf.Abs(x-c)<.1f)continue;if(b==1||b==4)Door(new Vector3(x,y,-3.5f),0,plan.transform);else Solid(new Vector3(x,y,-3.5f),0,plan.transform);}
  // Stair enclosure, with clear access from the foyer and a 1.2 m clear flight width.
  Box("Core side wall",new Vector3(c-1.7f,y+1.5f,.0f),new Vector3(.2f,3,7),cream,plan.transform);
  Box("Core side wall",new Vector3(c+1.7f,y+1.5f,.0f),new Vector3(.2f,3,7),cream,plan.transform);
  Box("Core rear wall",new Vector3(c,y+1.5f,2.7f),new Vector3(3.4f,3,.2f),cream,plan.transform);
  // Room divisions have explicit 1.2 m gaps, so every zone is reachable.
  foreach(float xx in new[]{lo+3,lo+12}) {
   Box("Room divider left of opening",new Vector3(xx-1.2f,y+1.5f,3.4f),new Vector3(2.2f,3,.15f),cream,plan.transform);
   Box("Room divider right of opening",new Vector3(xx+1.2f,y+1.5f,3.4f),new Vector3(.2f,3,.15f),cream,plan.transform);
   Box("Opening lintel",new Vector3(xx+.1f,y+2.6f,3.4f),new Vector3(.9f,.8f,.15f),cream,plan.transform);
  }
  if(s!=1) { // Third unit is a rear zone reached by a side passage, explicitly hypothetical.
   Box("Third unit division A",new Vector3(lo+12.3f,y+1.5f,5.4f),new Vector3(2.6f,3,.15f),cream,plan.transform);
   Box("Third unit division B",new Vector3(lo+16.4f,y+1.5f,5.4f),new Vector3(2.8f,3,.15f),cream,plan.transform);
   Box("Third unit door lintel",new Vector3(lo+14.3f,y+2.6f,5.4f),new Vector3(1.4f,.8f,.15f),cream,plan.transform);
  }
  if(s<2)Box("Section party wall",new Vector3(lo+18,y+1.5f,1.5f),new Vector3(.2f,3,14.6f),brick,plan.transform);
 }
}
for(int s=0;s<3;s++)for(int level=0;level<3;level++) {
 var g=Pref("Assets/Maps/Buildings/Stairs/Prefabs/Stairwell_3m_UShape.prefab","Core "+(s+1)+" / "+(level+1)+" to "+(level+2),new Vector3(stairsX[s],level*3,-2),0,stairs.transform);
 // Source prefab already references the new thin mesh; normalize only the scene root Y pivot.
 if(g.GetComponentsInChildren<MeshFilter>().Count(m=>m.sharedMesh&&AssetDatabase.GetAssetPath(m.sharedMesh).Contains("ThinStairs_Visual"))!=2)throw new Exception("Expected two thin flights");
}
var detail=Group("Photo-inspired details — spacing and sizes APPROXIMATION",shell.transform);
Box("Plinth front",new Vector3(27,.25f,9.23f),new Vector3(54,.5f,.07f),baseMat,detail.transform);
// No continuous rear plinth through entrance openings.
for(int b=0;b<18;b++){float x=1.5f+b*3;if(!stairsX.Contains(x))Box("Plinth rear",new Vector3(x,.25f,-6.23f),new Vector3(3,.5f,.07f),baseMat,detail.transform);}
foreach(float y in new[]{.6f,11.7f,12.05f}) {Box("Front cornice",new Vector3(27,y,9.15f),new Vector3(54.5f,.16f,.55f),cream,detail.transform,false);Box("Rear cornice",new Vector3(27,y,-6.15f),new Vector3(54.5f,.16f,.55f),cream,detail.transform,false);}
foreach(float x in new[]{0f,54f})Box("End cornice",new Vector3(x,12.05f,1.5f),new Vector3(.55f,.16f,15.5f),cream,detail.transform,false);
// Projecting enclosed bays and open balconies, observed as features, approximate in count and placement.
foreach(float x in new[]{4.5f,25.5f,49.5f})for(int level=1;level<4;level++){
 float y=level*3;
 Box("Projecting bay floor",new Vector3(x,y-.10f,9.7f),new Vector3(2.5f,.2f,1.4f),cream,detail.transform);
 Box("Bay apron",new Vector3(x,y+.45f,10.35f),new Vector3(2.5f,.9f,.15f),brick,detail.transform);
 Box("Bay roof edge",new Vector3(x,y+2.75f,9.75f),new Vector3(2.65f,.16f,1.5f),cream,detail.transform);
 foreach(float dx in new[]{-1.16f,0f,1.16f})Box("Bay pale mullion",new Vector3(x+dx,y+1.8f,10.35f),new Vector3(.09f,1.8f,.12f),cream,detail.transform);
 Box("Bay sill",new Vector3(x,y+.95f,10.35f),new Vector3(2.5f,.1f,.15f),cream,detail.transform);
 foreach(float dx in new[]{-1.22f,1.22f})Box("Bay cheek",new Vector3(x+dx,y+1.4f,9.7f),new Vector3(.15f,2.7f,1.4f),brick,detail.transform);
}
foreach(float x in new[]{13.5f,37.5f})for(int level=1;level<4;level++){
 float y=level*3;Box("Balcony slab",new Vector3(x,y-.1f,9.7f),new Vector3(2.5f,.2f,1.4f),cream,detail.transform);
 Box("Balcony front guard",new Vector3(x,y+.55f,10.35f),new Vector3(2.5f,1.1f,.1f),railMat,detail.transform);
 foreach(float dx in new[]{-1.2f,1.2f})Box("Balcony side guard",new Vector3(x+dx,y+.55f,9.7f),new Vector3(.1f,1.1f,1.4f),railMat,detail.transform);
}
foreach(float c in stairsX) {Box("Entry canopy",new Vector3(c,2.45f,-6.7f),new Vector3(2.2f,.15f,1.6f),roofMat,detail.transform);Floor(c,0,-7.2f,3,2,detail.transform);}
// Closed gabled roof mesh, ridge along local X; one metre equals one metre.
var roofObj=new GameObject("Pitched roof — APPROXIMATION");roofObj.transform.SetParent(detail.transform,false);
var mesh=new Mesh();mesh.name="Approx_Roof";
mesh.vertices=new[]{new Vector3(-.5f,12,-6.5f),new Vector3(54.5f,12,-6.5f),new Vector3(-.5f,14.3f,1.5f),new Vector3(54.5f,14.3f,1.5f),new Vector3(-.5f,12,9.5f),new Vector3(54.5f,12,9.5f)};
mesh.triangles=new[]{0,2,1,1,2,3,2,4,3,3,4,5,0,4,2,1,3,5,0,1,4,1,5,4};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Approx_Roof.asset");roofObj.AddComponent<MeshFilter>().sharedMesh=mesh;roofObj.AddComponent<MeshRenderer>().sharedMaterial=roofMat;roofObj.AddComponent<MeshCollider>().sharedMesh=mesh;
// Front central triangular pediment, visible on the public photograph.
var ped=new GameObject("Central front gable — approximation");ped.transform.SetParent(detail.transform,false);var pm=new Mesh();pm.vertices=new[]{new Vector3(22.5f,11.9f,9.28f),new Vector3(31.5f,11.9f,9.28f),new Vector3(27,15.1f,9.28f),new Vector3(22.5f,11.9f,9.05f),new Vector3(31.5f,11.9f,9.05f),new Vector3(27,15.1f,9.05f)};pm.triangles=new[]{0,1,2,5,4,3,0,3,1,1,3,4,1,4,2,2,4,5,2,5,0,0,5,3};pm.RecalculateNormals();AssetDatabase.CreateAsset(pm,folder+"/Approx_Pediment.asset");ped.AddComponent<MeshFilter>().sharedMesh=pm;ped.AddComponent<MeshRenderer>().sharedMaterial=brick;
Box("Ground — test only",new Vector3(27,-.35f,1.5f),new Vector3(85,.3f,45),groundMat,root.transform);
var info=Group("CONFIRMED: 1955 / brick / 4 storeys / 3 entries. Everything else see report",root.transform);
foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
var lighting=Group("Test lighting",root.transform);var sun=Group("Sun",lighting.transform).AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.transform.rotation=Quaternion.Euler(45,-35,0);sun.cullingMask=1<<29;
var camObj=Group("Overview camera — no Player changes",root.transform);var cam=camObj.AddComponent<Camera>();cam.transform.position=new Vector3(-18,23,45);cam.transform.LookAt(new Vector3(27,5,1.5f));cam.fieldOfView=48;cam.nearClipPlane=.1f;cam.farClipPlane=200;cam.cullingMask=1<<29;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.57f,.68f,.77f);
RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssetIfDirty(brick);
var sv=SceneView.lastActiveSceneView;if(sv)sv.LookAt(new Vector3(27,5,1.5f),Quaternion.Euler(20,-125,0),45);
return new {scene=scenePath,root=root.name,objects=root.GetComponentsInChildren<Transform>(true).Length,originalScene=original.path,originalDirty=original.isDirty};

