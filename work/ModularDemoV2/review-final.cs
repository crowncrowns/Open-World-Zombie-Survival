var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/ModularBuildings/Tests/DemoResidentialV2_Review.unity");
try{
 var roots=scene.GetRootGameObjects();var building=roots.SelectMany(g=>g.GetComponentsInChildren<ZombieSurvival.ModularBuildings.GeneratedBuilding>()).Single();
 var camera=roots.SelectMany(g=>g.GetComponentsInChildren<UnityEngine.Camera>()).Single();camera.scene=scene;camera.nearClipPlane=.03f;camera.farClipPlane=200;
 var directory="Verification/ModularDemoV2";System.IO.Directory.CreateDirectory(directory);
 System.Action<string,UnityEngine.Vector3,UnityEngine.Vector3,float> capture=(name,position,target,fov)=>{
  camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fov;
  var rt=new UnityEngine.RenderTexture(1500,1000,24);var previous=UnityEngine.RenderTexture.active;
  camera.targetTexture=rt;camera.Render();UnityEngine.RenderTexture.active=rt;
  var tex=new UnityEngine.Texture2D(1500,1000,UnityEngine.TextureFormat.RGB24,false);tex.ReadPixels(new UnityEngine.Rect(0,0,1500,1000),0,0);tex.Apply();System.IO.File.WriteAllBytes(directory+"/"+name+".png",tex.EncodeToPNG());
  camera.targetTexture=null;UnityEngine.RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
 };
 capture("Exterior",new UnityEngine.Vector3(43,24,-32),new UnityEngine.Vector3(13.5f,7,4.5f),50);
 capture("Back",new UnityEngine.Vector3(-14,22,35),new UnityEngine.Vector3(13.5f,7,4.5f),52); capture("RoofClose",new UnityEngine.Vector3(7,19,12),new UnityEngine.Vector3(2.9f,16.1f,7.7f),52); capture("EntranceDoor",new UnityEngine.Vector3(0,1.25f,-4),new UnityEngine.Vector3(0,1.1f,-1.5f),50); capture("Roof",new UnityEngine.Vector3(36,27,22),new UnityEngine.Vector3(13.5f,15,4.5f),52);
 foreach(float z in new[]{0f,3f,6f}){
  var light=new UnityEngine.GameObject("InspectionLight").AddComponent<UnityEngine.Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=UnityEngine.LightType.Point;light.transform.position=new UnityEngine.Vector3(0,2.4f,z);light.range=9;light.intensity=2;
 }
 capture("Lobby",new UnityEngine.Vector3(0,1.65f,-.7f),new UnityEngine.Vector3(0,1.8f,6),82);
 foreach(var slot in building.GetComponentsInChildren<ZombieSurvival.ModularBuildings.ApartmentSlot>().Where(s=>s.slotId.Contains("/E01/F01/"))){
  var pos=slot.transform.TransformPoint(new UnityEngine.Vector3(0,1.08f,-1.5f));var target=slot.transform.TransformPoint(new UnityEngine.Vector3(0,1.08f,0));capture("Door_"+slot.name,pos,target,78);
 }
 building.transform.Find("Roof_Flat_2Percent").gameObject.SetActive(false);
 foreach(var t in building.GetComponentsInChildren<UnityEngine.Transform>().Where(t=>t.parent&&t.parent.name.StartsWith("Entrance_")&&t.name.StartsWith("Floor_")&&t.name!="Floor_1").ToArray())t.gameObject.SetActive(false);
 capture("OpenFloorPlan",new UnityEngine.Vector3(13.5f,36,-4),new UnityEngine.Vector3(13.5f,0,4.5f),54);
 return "rendered Exterior, Roof, Lobby, 3 doors, OpenFloorPlan";
}finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}

