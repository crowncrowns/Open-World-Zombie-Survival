using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingGenerator
    {
        public const string Folder="Assets/ModularBuildings/Generated";
        const string Library="Assets/Maps/Buildings/";
        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
        public static BuildingSeriesProfile CreateProfile()
        {
            System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var path=Folder+"/DemoProfile.asset";
            var existing=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(path);
            if(existing) return existing;
            var p=ScriptableObject.CreateInstance<BuildingSeriesProfile>();
            p.section=new SectionTemplate();
            p.solidWall=Load(Library+"Walls/Prefabs/Outside/Wall_Solid_Module.prefab");
            p.windowWall=Load(Library+"Walls/Prefabs/Outside/Wall_Window_Static.prefab");
            p.entranceWall=Load(Library+"Walls/Prefabs/Outside/Wall_Door_Base.prefab");
            p.apartmentWall=Load(Library+"Walls/Prefabs/Inside/Room_Wall_Door_Base.prefab");
            p.door=Load(Library+"Doors/Prefabs/Door_Single_Metal.prefab");
            p.floor=Load(Library+"Floors/Prefabs/Floor_3x3.prefab");
            p.stair=Load("Assets/Environment/Stairwell/ThinConcrete/ThinConcreteStairs_9Steps.prefab");
            AssetDatabase.CreateAsset(p,path); AssetDatabase.SaveAssetIfDirty(p); return p;
        }
        public static void Preflight(BuildingSeriesProfile p,int year,int floors,int entrances)
        {
            if(!p || p.section==null) throw new InvalidOperationException("Нужен профиль и шаблон секции.");
            if(year<1800 || year>2200 || floors<1 || floors>9 || entrances<1 || entrances>8)
                throw new InvalidOperationException("MVP: год 1800–2200, этажи 1–9, подъезды 1–8.");
            var s=p.section;
            if(s.templateId!="U-9x9-2-apartments" || s.sectionWidth!=9 || s.sectionDepth!=9 || s.floorHeight!=3 || s.firstFlight!=new Vector3(-.75f,.75f,2.85f) || s.secondFlight!=new Vector3(.75f,2.25f,2.85f))
                throw new InvalidOperationException("MVP поддерживает только фиксированный проверяемый шаблон U-9x9.");
            foreach(var source in new[]{p.solidWall,p.windowWall,p.entranceWall,p.apartmentWall,p.door,p.floor,p.stair})
                if(!source || !PrefabUtility.IsPartOfPrefabAsset(source)) throw new InvalidOperationException("Не назначен исходный prefab.");
            CheckBounds(p.solidWall,new Vector3(3,3,.4f));
            CheckBounds(p.floor,new Vector3(3,.2f,3));
            CheckBounds(p.stair,new Vector3(1.2f,1.6602f,2.7f));
        }
        static void CheckBounds(GameObject source,Vector3 expected)
        {
            var b=new Bounds();bool first=true;
            foreach(var r in source.GetComponentsInChildren<Renderer>(true)){
                var local=r.localBounds;var m=source.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
                for(int i=0;i<8;i++){
                    var point=m.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){b=new Bounds(point,Vector3.zero);first=false;}else b.Encapsulate(point);
                }
            }
            if(first||Vector3.Distance(b.size,expected)>.002f)throw new InvalidOperationException("Изменился обмер prefab "+source.name+": "+b.size+". Пересмотрите шаблон.");
        }
        static Transform Group(string name,Transform parent,Vector3 position)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;
        }
        static GameObject Part(GameObject source,Transform parent,string name,Vector3 position,float yaw=0,Vector3? scale=null)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
            go.name=name;go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            go.transform.localScale=scale??Vector3.one;return go;
        }
        static void Slab(BuildingSeriesProfile p,Transform t,string name,float x,float z,float w,float d,float y)
            =>Part(p.floor,t,name,new Vector3(x,y,z),0,new Vector3(w/3,1,d/3));
        static void ClosedWindowCollision(Transform window)
        {
            // The measured static FBX has no glass collider. Add only an instance-owned invisible blocker.
            var c=Group("ClosedWindowCollision",window,Vector3.zero).gameObject.AddComponent<BoxCollider>();
            c.center=new Vector3(0,1.5f,.05f);c.size=new Vector3(1.2f,1.2f,.03f);
        }
        static void Rail(Transform parent,string name,Vector3 a,Vector3 b)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=(a+b)/2;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            go.transform.localScale=new Vector3(.06f,(b-a).magnitude,.06f);
        }
        public static GeneratedBuilding Build(BuildingSeriesProfile p,int year,int floors,int entrances,string id,Scene scene,bool unique=true)
        {
            Preflight(p,year,floors,entrances);
            if(string.IsNullOrWhiteSpace(id))throw new InvalidOperationException("Нужен постоянный ID здания.");
            // No destructive regeneration: an occupied identity fails before any mutation.
            if(unique && scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GeneratedBuilding>(true)).Any(b=>b.buildingId==id))
                throw new InvalidOperationException("ID уже существует. Существующий дом и ручные изменения защищены. Создайте новую версию с другим ID.");
            var root=new GameObject("Building_"+id); SceneManager.MoveGameObjectToScene(root,scene);
            try {
                var b=root.AddComponent<GeneratedBuilding>();b.buildingId=id;b.profile=p;b.year=year;b.floors=floors;b.entrances=entrances;
                for(int e=0;e<entrances;e++){
                    var section=Group("Entrance_"+(e+1),root.transform,new Vector3(e*9,0,0));
                    for(int f=0;f<floors;f++){
                        var level=Group("Floor_"+(f+1),section,new Vector3(0,f*3,0));
                        for(int bay=0;bay<3;bay++){
                            float x=(bay-1)*3;
                            bool entry=f==0&&bay==1;
                            var front=Part(entry?p.entranceWall:p.windowWall,level,"Front_"+bay,new Vector3(x,0,-1.5f),180);
                            if(!entry)ClosedWindowCollision(front.transform);
                            var rear=Part(p.windowWall,level,"Rear_"+bay,new Vector3(x,0,7.5f));ClosedWindowCollision(rear.transform);
                        }
                        // End walls stop at facade inner faces; one party wall per junction.
                        if(e==0)Part(p.solidWall,level,"LeftEnd",new Vector3(-4.5f,0,3),90,new Vector3(8.6f/3,1,1));
                        Part(p.solidWall,level,e==entrances-1?"RightEnd":"PartyWall",new Vector3(4.5f,0,3),90,new Vector3(8.6f/3,1,1));
                        Slab(p,level,"MainLanding",0,.1f,8.6f,2.8f,0);
                        Slab(p,level,"LeftApartmentSlab",-2.9f,3.6f,2.8f,4.2f,0);
                        Slab(p,level,"RightApartmentSlab",2.9f,3.6f,2.8f,4.2f,0);
                        Slab(p,level,"RearSlab",0,6.5f,8.6f,1.6f,0);
                        if(f==0)Slab(p,level,"GroundBelowShaft",0,3.6f,3,4.2f,0);
                        for(int side=-1;side<=1;side+=2){
                            var slot=Group("ApartmentSlot_"+(side<0?"L":"R"),level,new Vector3(side*1.55f,0,0));
                            slot.localRotation=Quaternion.Euler(0,side*90,0);
                            var data=slot.gameObject.AddComponent<ApartmentSlot>();
                            data.slotId=id+"/E"+(e+1).ToString("D2")+"/F"+(f+1).ToString("D2")+"/"+(side<0?"L":"R");
                            Part(p.apartmentWall,slot,"ApartmentDoorWall",Vector3.zero);
                            var door=Part(p.door,slot,"ApartmentDoor",Vector3.zero);
                            data.doorHinge=door.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Hinge");
                            data.doorLeaf=door.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Leaf");
                            data.doorHinge.localRotation=Quaternion.identity;data.doorLeaf.localRotation=Quaternion.identity;
                            data.manualContent=Group("ManualContent",slot,new Vector3(0,0,.25f));
                            // Shaft sides: wall above landing extends only rearwards, not across doors.
                            Part(p.solidWall,level,"ShaftSide_"+side,new Vector3(side*1.55f,0,3.6f),90,new Vector3(4.2f/3,1,.25f));
                        }
                        Part(p.solidWall,level,"ShaftRear",new Vector3(0,0,5.75f),0,new Vector3(1,1,.25f));
                        if(f<floors-1){
                            Part(p.stair,level,"Flight_Up",p.section.firstFlight);
                            Part(p.stair,level,"Flight_Return",p.section.secondFlight,180);
                            Slab(p,level,"IntermediateLanding",0,4.95f,3,1.5f,1.5f);
                            for(int r=0;r<2;r++){
                                float x=r==0?-.12f:.12f; float y=r==0?0:3;
                                var a=new Vector3(x,y,1.5f);var z=new Vector3(x,1.5f,4.2f);
                                Rail(level,"Handrail",a+Vector3.up,z+Vector3.up);
                                for(int post=0;post<=6;post++){
                                    var point=Vector3.Lerp(a,z,post/6f); Rail(level,"Baluster",point,point+Vector3.up);
                                }
                            }
                        }
                        else if(f>0){
                            Rail(level,"TopFloorGuard",new Vector3(-1.35f,1,1.5f),new Vector3(-.15f,1,1.5f));
                            for(int post=0;post<=6;post++)Rail(level,"TopFloorGuardPost",new Vector3(-1.35f+post*.2f,0,1.5f),new Vector3(-1.35f+post*.2f,1,1.5f));
                        }
                    }
                    Slab(p,section,"Roof",0,3,8.6f,8.6f,floors*3);
                }
                Physics.SyncTransforms();return b;
            }catch{Object.DestroyImmediate(root);throw;}
        }
        public static List<string> Validate(GeneratedBuilding b,bool pristine=false)
        {
            var errors=new List<string>(); if(!b){errors.Add("Нет здания.");return errors;}
            try{Preflight(b.profile,b.year,b.floors,b.entrances);}catch(Exception ex){errors.Add(ex.Message);return errors;}
            if(Vector3.Distance(b.transform.lossyScale,Vector3.one)>.001f||Vector3.Angle(b.transform.up,Vector3.up)>.01f){errors.Add("MVP: корень здания должен иметь scale=1 и вертикальную ось Y.");return errors;}
            var slots=b.GetComponentsInChildren<ApartmentSlot>(true);
            if(slots.Length!=b.floors*b.entrances*2)errors.Add("Число ApartmentSlot не соответствует профилю.");
            if(slots.Select(s=>s.slotId).Distinct().Count()!=slots.Length)errors.Add("Повторные ID слотов.");
            if(b.gameObject.scene.IsValid()&&b.gameObject.scene.isLoaded){
                var all=b.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GeneratedBuilding>(true)).ToArray();
                if(all.Count(g=>g.buildingId==b.buildingId)>1)errors.Add("ID здания продублирован в сцене.");
            }
            foreach(var s in slots){
                if(string.IsNullOrEmpty(s.slotId)||!s.slotId.StartsWith(b.buildingId+"/",StringComparison.Ordinal))errors.Add("Повреждён ID слота: "+s.name);
                if(!s.manualContent || !s.doorHinge || !s.doorLeaf)errors.Add(s.slotId+": нет точек привязки/двери.");
                else if(pristine&&(Quaternion.Angle(s.doorHinge.localRotation,Quaternion.identity)>.01f||Quaternion.Angle(s.doorLeaf.localRotation,Quaternion.identity)>.01f))errors.Add(s.slotId+": дверь исходно открыта.");
            }
            foreach(var t in b.GetComponentsInChildren<Transform>(true)){
                if(PrefabUtility.IsPrefabAssetMissing(t.gameObject))errors.Add("Missing nested prefab: "+t.name);
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add("Missing script: "+t.name);
                if(t.localScale.x<=0||t.localScale.y<=0||t.localScale.z<=0)errors.Add("Отрицательный/нулевой scale: "+t.name);
            }
            foreach(var r in b.GetComponentsInChildren<MeshRenderer>(true))
                if(r.sharedMaterials.Any(m=>!m)||!r.GetComponent<MeshFilter>()||!r.GetComponent<MeshFilter>().sharedMesh)errors.Add("Нет mesh/material: "+r.name);
            // Capsule route samples follow both ramps and the intermediate turn, not arbitrary stair placement.
            Physics.SyncTransforms();
            foreach(var t in b.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Front_",StringComparison.Ordinal)||t.name.StartsWith("Rear_",StringComparison.Ordinal))){
                if(t.name=="Front_1"&&t.parent.name=="Floor_1")continue;
                if(!Physics.RaycastAll(t.TransformPoint(new Vector3(0,1.5f,-.6f)),t.forward,1.2f).Any(h=>h.collider.transform.IsChildOf(t)))errors.Add("Нет коллизии закрытого окна: "+t.name);
            }
            for(int e=0;e<b.entrances;e++)for(int f=0;f<b.floors-1;f++)for(int flight=0;flight<2;flight++)for(int n=0;n<=27;n++){
                float u=n/27f;float z=flight==0?1.5f+2.7f*u:4.2f-2.7f*u;
                var p=b.transform.TransformPoint(new Vector3(e*9+(flight==0?-.75f:.75f),f*3+flight*1.5f+u*1.5f,z));
                var support=Physics.RaycastAll(p+Vector3.up*.6f,Vector3.down,1,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(b.transform)).OrderBy(h=>h.distance).ToArray();
                if(support.Length==0){errors.Add("Нет опоры марша E"+e+" F"+f);continue;}
                p.y=support[0].point.y+.065f;
                var hits=Physics.OverlapCapsule(p+Vector3.up*.3f,p+Vector3.up*1.5f,.29f,~0,QueryTriggerInteraction.Ignore);
                if(hits.Any(c=>c.transform.IsChildOf(b.transform)))errors.Add("Заблокирован марш E"+e+" F"+f+" flight"+flight+" sample"+n);
            }
            return errors;
        }
        public static string SaveNewPrefab(GeneratedBuilding b,string requested)
        {
            if(!requested.StartsWith(Folder+"/",StringComparison.Ordinal)||!requested.EndsWith(".prefab"))throw new InvalidOperationException("Сохранение только в папку Generated.");
            var errors=Validate(b);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            string path=AssetDatabase.GenerateUniqueAssetPath(requested);
            PrefabUtility.SaveAsPrefabAsset(b.gameObject,path);return path;
        }
    }
}
