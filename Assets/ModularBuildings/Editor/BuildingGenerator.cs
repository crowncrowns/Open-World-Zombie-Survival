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
            var path=Folder+"/DemoProfileV5.asset";
            var existing=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(path);
            if(existing)return existing;
            var p=Object.Instantiate(CreateLegacyProfile());
            p.name="DemoProfileV5";p.profileId="demo-u-stair-v5-wide";
            p.section=new SectionTemplate {templateId="U-15x12-3-apartments-flat",sectionWidth=15};
            AssetDatabase.CreateAsset(p,path);DemoAssetSetup.EnsureWideRoof(p);AssetDatabase.SaveAssetIfDirty(p);return p;
        }
        static BuildingSeriesProfile CreateLegacyProfile()
        {
            System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var path=Folder+"/DemoProfileV4.asset";
            var existing=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(path);
            if(existing) return existing;
            var p=ScriptableObject.CreateInstance<BuildingSeriesProfile>();
            p.section=new SectionTemplate();
            p.solidWall=Load(Library+"Walls/Prefabs/Outside/Wall_Solid_Module.prefab");
            p.windowWall=Load(Library+"Walls/Prefabs/Outside/Wall_Window_Interactive.prefab");
            p.entranceWall=Load(Library+"Walls/Prefabs/Outside/Wall_Door_Base.prefab");
            p.apartmentWall=Load(Library+"Walls/Prefabs/Inside/Room_Wall_Door_Base.prefab");
            p.door=Load(Library+"Doors/Prefabs/Door_Single_Metal.prefab");
            p.floor=Load(Library+"Floors/Prefabs/Floor_3x3.prefab");
            p.stair=Load("Assets/Maps/Buildings/Stairs/Prefabs/Stairwell_3m_UShape.prefab");
            AssetDatabase.CreateAsset(p,path);DemoAssetSetup.EnsureV4(p);AssetDatabase.SaveAssetIfDirty(p);return p;
        }
        public static void Preflight(BuildingSeriesProfile p,int year,int floors,int entrances)
        {
            if(!p || p.section==null) throw new InvalidOperationException("Нужен профиль и шаблон секции.");
            if(year<1800 || year>2200 || floors<1 || floors>9 || entrances<1 || entrances>8)
                throw new InvalidOperationException("MVP: год 1800–2200, этажи 1–9, подъезды 1–8.");
            var s=p.section;
            bool known=(s.templateId=="U-9x12-3-apartments-flat"&&s.sectionWidth==9)||(s.templateId=="U-15x12-3-apartments-flat"&&s.sectionWidth==15);
            if(!known || s.sectionDepth!=12 || s.floorHeight!=3 || s.firstFlight!=new Vector3(-.9f,.75f,5.85f) || s.secondFlight!=new Vector3(.9f,2.25f,5.85f))
                throw new InvalidOperationException("Поддерживаются демонстрационные шаблоны U-9x12 и U-15x12 с тремя квартирами и почти плоской кровлей.");
            foreach(var source in new[]{p.solidWall,p.windowWall,p.entranceWall,p.apartmentWall,p.door,p.floor,p.stair})
                if(!source || !PrefabUtility.IsPartOfPrefabAsset(source)) throw new InvalidOperationException("Не назначен исходный prefab.");
            CheckBounds(p.solidWall,new Vector3(3,3,.4f));
            CheckBounds(p.floor,new Vector3(3,.2f,3));
            if(AssetDatabase.GetAssetPath(p.stair)!="Assets/Maps/Buildings/Stairs/Prefabs/Stairwell_3m_UShape.prefab"||!p.stair.transform.Find("Stairs_Flight_01")||!p.stair.transform.Find("Stairs_Landing_01"))throw new InvalidOperationException("Выберите профиль V3 с исходным Stairwell_3m_UShape.");
            if(p.foundationDepth<2.9f||p.foundationDepth>6)throw new InvalidOperationException("Глубина цоколя V3: 3–6 м.");
            if(FacadeDetailChecks.IsForbiddenWindow(AssetDatabase.GetAssetPath(p.windowWall)))throw new InvalidOperationException("Запрещён оконный prefab. Выберите профиль V4.");
            if(!p.interiorWindowLining||!p.facadeMaterial||!p.approvedWorldFacadeShader)throw new InvalidOperationException("Нужен полный профиль V4 с внутренней облицовкой и фасадным материалом.");
            if(p.requireWorldSpaceFacade&&p.facadeMaterial.shader!=p.approvedWorldFacadeShader)throw new InvalidOperationException("Фасадный материал не соответствует утверждённому World Space shader профиля.");
            if(p.apartmentDoors==null||p.apartmentDoors.Length!=3||p.apartmentDoors.Any(x=>!x)||!p.entranceDoor||!p.ventCap||!p.ventDuct||!p.glassMaterial||!p.roofMaterial||p.roofMeshes==null||p.roofMeshes.Length!=8||p.roofMeshes.Any(m=>!m)||!p.chimneyCurb||!p.ductCurb)
                throw new InvalidOperationException("Неполная библиотека V2: создайте профиль V2 через меню генератора.");
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
        static void EndWall(BuildingSeriesProfile p,Transform level,string name,float x,float yaw)
        {
            var side=Group(name,level,new Vector3(x,0,4.5f));
            side.localRotation=Quaternion.Euler(0,yaw,0);
            AddEndWallModules(p,side);
        }
        public static void AddEndWallModules(BuildingSeriesProfile p,Transform side)
        {
            // Preserve the previous 11.6 m side-wall span. Two full-size 3 m windows per floor;
            // only the two solid end fillers shorten to 2.8 m. Frames/sashes never stretch.
            for(int bay=0;bay<4;bay++) {
                bool window=bay==1||bay==2;
                float offset=bay==0?-4.4f:bay==3?4.4f:(bay-1.5f)*3;
                var module=Part(window?p.windowWall:p.solidWall,side,(window?"Window_":"Solid_")+bay,new Vector3(offset,0,0),0,window?Vector3.one:new Vector3(2.8f/3,1,1));
                if(window){ClosedWindowCollision(module.transform);ApplyGlass(module.transform,p.glassMaterial);}
            }
        }
        static void ApplyGlass(Transform window,Material material)
        {
            foreach(var r in window.GetComponentsInChildren<MeshRenderer>(true)){
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(r.name.IndexOf("glass",StringComparison.OrdinalIgnoreCase)>=0||materials[i]&&materials[i].name.IndexOf("glass",StringComparison.OrdinalIgnoreCase)>=0)materials[i]=material;
                r.sharedMaterials=materials;
            }
        }
        static void Surface(GameObject go,Material material)
        {
            if(!material)return;
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>())r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();
        }
        static void MeshPart(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
        {
            var go=Group(name,parent,position).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        static GameObject FinishBox(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;Surface(go,material);return go;
        }
        static void EntranceLining(BuildingSeriesProfile p,Transform level,bool door)
        {
            var lining=Group("EntranceInteriorLining",level,Vector3.zero);
            var panel=Part(door?p.apartmentWall:p.interiorWindowLining,lining,door?"DoorOpeningLining":"WindowOpeningLining",new Vector3(0,0,-1.29f),0,new Vector3(1,1,door?.2f:.05f));Surface(panel,p.stairWallMaterial);
            float bottom=door?0:.9f,top=2.1f,half=door?.5f:.6f;
            foreach(int side in new[]{-1,1})FinishBox(lining,"GreenRevealSide",new Vector3(side*(half-.01f),(bottom+top)/2,-1.39f),new Vector3(.02f,top-bottom,.22f),p.stairWallMaterial);
            FinishBox(lining,"GreenRevealTop",new Vector3(0,top-.01f,-1.39f),new Vector3(half*2,.02f,.22f),p.stairWallMaterial);
            if(!door)FinishBox(lining,"GreenRevealSill",new Vector3(0,bottom+.01f,-1.39f),new Vector3(half*2,.02f,.22f),p.stairWallMaterial);
        }
        static void CornerClosures(BuildingSeriesProfile p,Transform root,int floors,int entrances)
        {
            var corners=Group("FacadeCornerClosures",root,Vector3.zero);float height=floors*3+p.foundationDepth;
            float width=p.section.sectionWidth,half=width/2;
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})FinishBox(corners,"Corner_"+x+"_"+z,new Vector3(x<0?-half-.1f:(entrances-1)*width+half+.1f,(floors*3-p.foundationDepth)/2,z<0?-1.5f:10.5f),new Vector3(.2f,height,.4f),p.facadeMaterial);
        }
        static void FacadeMaterials(BuildingSeriesProfile p,Transform root)
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()){
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++)if(materials[i]&&materials[i].shader==p.approvedWorldFacadeShader){materials[i]=p.facadeMaterial;changed=true;}
                if(changed)renderer.sharedMaterials=materials;
            }
        }
        static void Apartment(BuildingSeriesProfile p,Transform level,string id,int e,int f,string key,float x,float z,int variant)
        {
            var slot=Group("ApartmentSlot_"+key,level,new Vector3(x,0,z));slot.localRotation=Quaternion.Euler(0,x<0?-90:90,0);
            var data=slot.gameObject.AddComponent<ApartmentSlot>();data.slotId=id+"/E"+(e+1).ToString("D2")+"/F"+(f+1).ToString("D2")+"/"+key;
            var wall=Part(p.apartmentWall,slot,"ApartmentDoorWall",Vector3.zero);Surface(wall,p.stairWallMaterial);
            var door=Part(p.apartmentDoors[variant],slot,"ApartmentDoor",Vector3.zero);
            data.doorHinge=door.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Hinge");data.doorLeaf=door.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Leaf");
            data.doorHinge.localRotation=Quaternion.identity;data.doorLeaf.localRotation=Quaternion.identity;
            data.manualContent=Group("ManualContent",slot,new Vector3(0,0,.25f));
        }
        static void Roof(BuildingSeriesProfile p,Transform parent,int floors,int entrances)
        {
            float width=p.section.sectionWidth;
            var roof=Group("Roof_Flat_2Percent",parent,new Vector3(0,floors*3,0));
            MeshPart(roof,"WeatherRoof",p.roofMeshes[entrances-1],p.roofMaterial,Vector3.zero);
            // One continuous ceiling slab and membrane: no double faces or exposed section seams.
            var deck=Part(p.floor,roof,"RoofCeiling",new Vector3((entrances-1)*width/2,0,4.5f),0,new Vector3((entrances*width-.4f)/3,1,11.6f/3));Surface(deck,p.ceilingMaterial);
            for(int e=0;e<entrances;e++){
                var stack=Group("VentilationStack_E"+(e+1),roof,new Vector3(e*width+2.9f,0,7.7f));
                MeshPart(stack,"FlashedCurb",p.chimneyCurb,p.roofMaterial,Vector3.zero);
                float y=p.chimneyCurb.bounds.max.y;
                // Hollow brick shaft, with a supported metal weather cap and an air gap beneath it.
                Part(p.solidWall,stack,"BrickFront",new Vector3(0,y,-.82f),0,new Vector3(1.2f/3,1.3f/3,.16f/.4f));
                Part(p.solidWall,stack,"BrickBack",new Vector3(0,y,.82f),0,new Vector3(1.2f/3,1.3f/3,.16f/.4f));
                foreach(int side in new[]{-1,1}){
                    Part(p.solidWall,stack,"BrickSide",new Vector3(side*.52f,y,0),90,new Vector3(1.48f/3,1.3f/3,.16f/.4f));
                    foreach(float z in new[]{-.7f,.7f}){
                        var support=GameObject.CreatePrimitive(PrimitiveType.Cube);support.name="CapSupport";support.transform.SetParent(stack,false);
                        support.transform.localPosition=new Vector3(side*.46f,y+1.4f,z);support.transform.localScale=new Vector3(.04f,.2f,.04f);Surface(support,p.roofMaterial);
                    }
                }
                Part(p.ventCap,stack,"MetalVentCap",new Vector3(0,y+1.5f,0));
                var duct=Group("VentilationDuct_E"+(e+1),roof,new Vector3(e*width-2.8f,0,.8f));
                MeshPart(duct,"FlashedCurb",p.ductCurb,p.roofMaterial,Vector3.zero);
                Part(p.ventDuct,duct,"ExistingVentAndPipe",new Vector3(0,p.ductCurb.bounds.max.y,0),-90);
            }
        }
        static void Rail(Transform parent,string name,Vector3 a,Vector3 b)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=(a+b)/2;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            go.transform.localScale=new Vector3(.06f,(b-a).magnitude,.06f);
        }
        public static GeneratedBuilding Build(BuildingSeriesProfile p,int year,int floors,int entrances,string id,Scene scene,bool unique=true,Material exteriorMaterial=null,bool includeEntranceDoors=false,Material baseTrimMaterial=null)
        {
            Preflight(p,year,floors,entrances);
            if(string.IsNullOrWhiteSpace(id))throw new InvalidOperationException("Нужен постоянный ID здания.");
            // No destructive regeneration: an occupied identity fails before any mutation.
            if(unique && scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GeneratedBuilding>(true)).Any(b=>b.buildingId==id))
                throw new InvalidOperationException("ID уже существует. Существующий дом и ручные изменения защищены. Создайте новую версию с другим ID.");
            var root=new GameObject("Building_"+id); SceneManager.MoveGameObjectToScene(root,scene);
            try {
                var b=root.AddComponent<GeneratedBuilding>();b.buildingId=id;b.profile=p;b.year=year;b.floors=floors;b.entrances=entrances;
                float width=p.section.sectionWidth,half=width/2;int bays=Mathf.RoundToInt(width/3),middle=bays/2;
                b.revision=width==15?"demo-v5-wide":"demo-v4";b.endWindowRuleVersion=1;
                for(int e=0;e<entrances;e++){
                    var section=Group("Entrance_"+(e+1),root.transform,new Vector3(e*width,0,0));
                    for(int f=0;f<floors;f++){
                        var level=Group("Floor_"+(f+1),section,new Vector3(0,f*3,0));
                        for(int bay=0;bay<bays;bay++){
                            float x=(bay-middle)*3;
                            bool entry=f==0&&bay==middle;
                            var front=Part(entry?p.entranceWall:p.windowWall,level,"Front_"+bay,new Vector3(x,0,-1.5f),180);
                            if(!entry){ClosedWindowCollision(front.transform);ApplyGlass(front.transform,p.glassMaterial);}
                            else {Surface(front,p.facadeMaterial);Part(p.entranceDoor,front.transform,"EntranceDoor",Vector3.zero);}
                            var rear=Part(p.windowWall,level,"Rear_"+bay,new Vector3(x,0,10.5f));ClosedWindowCollision(rear.transform);ApplyGlass(rear.transform,p.glassMaterial);
                        }
                        EntranceLining(p,level,f==0);
                        if(e==0)EndWall(p,level,"LeftEnd",-half,-90);
                        if(e==entrances-1)EndWall(p,level,"RightEnd",half,90);
                        // Section boundaries have neither a party wall nor a floor gap. Only stair enclosures remain.
                        float left=e==0?-half+.2f:-half,right=e==entrances-1?half-.2f:half;
                        Slab(p,level,"MainLandingAndOpenFloor",(left+right)/2,1.6f,right-left,5.8f,0);
                        Slab(p,level,"LeftOpenFloor",(left-1.55f)/2,6.6f,-1.55f-left,4.2f,0);
                        Slab(p,level,"RightOpenFloor",(right+1.55f)/2,6.6f,right-1.55f,4.2f,0);
                        Slab(p,level,"RearOpenFloor",(left+right)/2,9.525f,right-left,1.55f,0);
                        if(f==0)Slab(p,level,"GroundBelowShaft",0,6.6f,3,4.2f,0);
                        Apartment(p,level,id,e,f,"L",-1.55f,0,0);
                        Apartment(p,level,id,e,f,"R",1.55f,0,1);
                        Apartment(p,level,id,e,f,"L2",-1.55f,3,2);
                        Surface(Part(p.solidWall,level,"LobbyRight",new Vector3(1.55f,0,3),90,new Vector3(1,1,.25f)),p.stairWallMaterial);
                        foreach(int side in new[]{-1,1})Surface(Part(p.solidWall,level,"ShaftSide_"+side,new Vector3(side*1.55f,0,6.6f),90,new Vector3(4.2f/3,1,.25f)),p.stairWallMaterial);
                        Surface(Part(p.solidWall,level,"ShaftRear",new Vector3(0,0,8.75f),0,new Vector3(1,1,.25f)),p.stairWallMaterial);
                        if(f<floors-1){
                            var module=Part(p.stair,level,"StairwellModule",new Vector3(0,0,4.2f));
                            module.transform.Find("Stairs_Landing_02").gameObject.SetActive(false);
                            var landing=module.transform.Find("Stairs_Landing_01");landing.localPosition=new Vector3(0,1.4f,3.775f);landing.localScale=new Vector3(3.1f,.2f,1.55f);
                        }
                        else if(f>0){
                            Rail(level,"TopFloorGuard",new Vector3(-1.35f,1,4.5f),new Vector3(-.15f,1,4.5f));
                            for(int post=0;post<=6;post++)Rail(level,"TopFloorGuardPost",new Vector3(-1.35f+post*.2f,0,4.5f),new Vector3(-1.35f+post*.2f,1,4.5f));
                        }
                    }
                }
                var foundation=Group("TerrainFoundation",root.transform,Vector3.zero);float center=(entrances-1)*width/2;
                Part(p.solidWall,foundation,"FoundationFront",new Vector3(center,-p.foundationDepth,-1.5f),180,new Vector3(entrances*width/3,p.foundationDepth/3,1));
                Part(p.solidWall,foundation,"FoundationRear",new Vector3(center,-p.foundationDepth,10.5f),0,new Vector3(entrances*width/3,p.foundationDepth/3,1));
                Part(p.solidWall,foundation,"FoundationLeft",new Vector3(-half,-p.foundationDepth,4.5f),90,new Vector3(11.6f/3,p.foundationDepth/3,1));
                Part(p.solidWall,foundation,"FoundationRight",new Vector3((entrances-1)*width+half,-p.foundationDepth,4.5f),90,new Vector3(11.6f/3,p.foundationDepth/3,1));
                Roof(p,root.transform,floors,entrances);
                CornerClosures(p,root.transform,floors,entrances);FacadeMaterials(p,root.transform);
                for(int pass=0;pass<3;pass++){
                    SeamUnion.Resolve(root);
                    var overlaps=CoplanarAudit.Scan(root);
                    if(overlaps.overlapPairs==0&&overlaps.unreadable.Count==0)break;
                }
                if(exteriorMaterial||includeEntranceDoors)BuildingExteriorMaterials.Apply(b,exteriorMaterial?exteriorMaterial:p.facadeMaterial,includeEntranceDoors,false);
                BuildingBaseTrim.Add(b,baseTrimMaterial);
                BuildingEntranceStairs.Refit(b,null,false,false);
                Physics.SyncTransforms();return b;
            }catch{Object.DestroyImmediate(root);throw;}
        }
        public static List<string> Validate(GeneratedBuilding b,bool pristine=false)
        {
            var errors=new List<string>(); if(!b){errors.Add("Нет здания.");return errors;}
            try{Preflight(b.profile,b.year,b.floors,b.entrances);}catch(Exception ex){errors.Add(ex.Message);return errors;}
            if(Vector3.Distance(b.transform.lossyScale,Vector3.one)>.001f||Vector3.Angle(b.transform.up,Vector3.up)>.01f){errors.Add("MVP: корень здания должен иметь scale=1 и вертикальную ось Y.");return errors;}
            var slots=b.GetComponentsInChildren<ApartmentSlot>(true);
            if(slots.Length!=b.floors*b.entrances*3)errors.Add("Число ApartmentSlot не соответствует профилю.");
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
                if(t.name=="Front_"+(Mathf.RoundToInt(b.profile.section.sectionWidth/3)/2)&&t.parent.name=="Floor_1")continue;
                if(!Physics.RaycastAll(t.TransformPoint(new Vector3(0,1.5f,-.6f)),t.forward,1.2f).Any(h=>h.collider.transform.IsChildOf(t)))errors.Add("Нет коллизии закрытого окна: "+t.name);
            }
            for(int e=0;e<b.entrances;e++)for(int f=0;f<b.floors-1;f++)for(int flight=0;flight<2;flight++)for(int n=0;n<=27;n++){
                float u=n/27f;float z=flight==0?4.5f+2.7f*u:7.2f-2.7f*u;
                var p=b.transform.TransformPoint(new Vector3(e*b.profile.section.sectionWidth+(flight==0?-.9f:.9f),f*3+flight*1.5f+u*1.5f,z));
                var support=Physics.RaycastAll(p+Vector3.up*.6f,Vector3.down,1,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(b.transform)).OrderBy(h=>h.distance).ToArray();
                if(support.Length==0){errors.Add("Нет опоры марша E"+e+" F"+f);continue;}
                p.y=support[0].point.y+.065f;
                var hits=Physics.OverlapCapsule(p+Vector3.up*.3f,p+Vector3.up*1.5f,.29f,~0,QueryTriggerInteraction.Ignore);
                if(hits.Any(c=>c.transform.IsChildOf(b.transform)))errors.Add("Заблокирован марш E"+e+" F"+f+" flight"+flight+" sample"+n);
            }
            BuildingQuality.Check(b,pristine,errors);
            FacadeDetailChecks.Check(b,pristine,errors);
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
