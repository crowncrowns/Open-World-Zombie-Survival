using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Interaction;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    /// <summary>Whole authored apartments, never scaled down or clipped into an entrance strip.</summary>
    public static class BuildingApartments
    {
        private static int Hash(string text,int seed){unchecked{uint h=(uint)seed^2166136261;foreach(char c in text??""){h^=c;h*=16777619;}return (int)h;}}
        private static bool Overlap(Rect a,Rect b)=>a.xMin<b.xMax&&a.xMax>b.xMin&&a.yMin<b.yMax&&a.yMax>b.yMin;
        public static void Add(GeneratedBuilding b,int seed)
        {
            if(Application.isPlaying||b==null||b.profile==null)throw new InvalidOperationException("Нужен новый дом в Edit.");
            if(b.GetComponentsInChildren<GeneratedApartment>(true).Length>0)throw new InvalidOperationException("Новый prefab-планировщик не заменяет существующие слоты/квартиры.");
            var layout=b.profile.apartmentLayout;var random=new System.Random(Hash(b.buildingId,seed));
            var layoutErrors=ApartmentPrefabPlanner.Validate(layout);if(layoutErrors.Count>0)throw new InvalidOperationException(string.Join("\n",layoutErrors));
            var sourceWalls=layout.prefab.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).Select(r=>BoundsIn(layout.prefab.transform,r)).Where(r=>r.size.y>2.8f).ToArray();
            var openings=FacadeOpenings(b);
            bool Accept(PlacedApartment candidate)
            {
                var matrix=Matrix4x4.TRS(candidate.RootLocalPosition,Quaternion.Euler(0,candidate.Yaw,0),Vector3.one);
                foreach(var wall in sourceWalls)if(CutsOpening(TransformBounds(wall,matrix),candidate.Floor,openings))return false;
                return true;
            }
            var slots=b.GetComponentsInChildren<ApartmentSlot>(true).Where(s=>b.transform.InverseTransformPoint(s.transform.position).y<=6.01f).OrderBy(s=>s.slotId,StringComparer.Ordinal).ToArray();
            if(slots.Any(s=>s.manualContent==null||s.manualContent.childCount>0))throw new InvalidOperationException("Только новые пустые ManualContent слотов.");
            var entries=slots.Select(s=>new ApartmentPrefabPlanner.EntrySlot{Id=s.slotId,Floor=Mathf.RoundToInt(b.transform.InverseTransformPoint(s.transform.position).y/3),EntryLocal=b.transform.InverseTransformPoint(s.transform.position),Inward=b.transform.InverseTransformDirection(s.transform.forward)}).ToArray();
            var placements=ApartmentPrefabPlanner.PlanFromSlots(layout,b.floors,b.entrances,Hash(b.buildingId,seed),random.Next(1,4),BuildingFootprint.SectionWidth(b.profile,b.entrances),entries,Accept);
            foreach(var unused in b.GetComponentsInChildren<ApartmentSlot>(true))
            {
                unused.doorHinge.localRotation=Quaternion.identity;unused.doorLeaf.localRotation=Quaternion.identity;
                foreach(var d in unused.GetComponentsInChildren<DoorInteractable>(true))d.enabled=false;
            }
            var created=new List<GameObject>();
            try
            {
                for(int i=0;i<placements.Count;i++)
                {
                    var placed=placements[i];var slot=slots.Single(s=>s.slotId==placed.SlotId);string id=slot.slotId;
                    var manual=new GameObject("Generated apartment content");manual.transform.SetParent(slot.manualContent,false);created.Add(manual);
                    var doorObject=slot.doorHinge.parent.gameObject;
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(layout.prefab,manual.transform);
                    instance.transform.SetPositionAndRotation(b.transform.TransformPoint(placed.RootLocalPosition),b.transform.rotation*Quaternion.Euler(0,placed.Yaw,0));instance.transform.localScale=Vector3.one;
                    var apartment=manual.AddComponent<GeneratedApartment>();apartment.apartmentId=id;apartment.layoutSeed=Hash(id,seed);apartment.layout=layout;apartment.sourcePrefab=layout.prefab;apartment.prefabInstance=instance;apartment.floor=placed.Floor;
                    apartment.occupiedRegions=placed.Regions.ToArray();apartment.connectorRegions=placed.ConnectorRegions.ToArray();apartment.connectorPath=placed.ConnectorPath.ToArray();apartment.entryLocal=placed.EntryLocal;apartment.approachLocal=placed.ApproachPoint;
                    float x0=apartment.occupiedRegions.Min(r=>r.xMin),x1=apartment.occupiedRegions.Max(r=>r.xMax),z0=apartment.occupiedRegions.Min(r=>r.yMin),z1=apartment.occupiedRegions.Max(r=>r.yMax);
                    apartment.buildingLocalBounds=new Bounds(new Vector3((x0+x1)/2,placed.Floor*3+layout.roomHeight/2,(z0+z1)/2),new Vector3(x1-x0,layout.roomHeight,z1-z0));
                    var door=ConfigureDoor(doorObject,slot.doorHinge,slot.doorLeaf);
                    apartment.entranceDoor=door;apartment.entranceAngle=OutwardAngle(slot,door);door.SetInitialOpenPose(125,Mathf.Sign(apartment.entranceAngle));
                    ConfigureInternalDoors(instance);

                    foreach(var marker in layout.rooms??new List<ApartmentPrefabRoomMarker>())
                    {
                        if(marker==null)continue;var point=new GameObject(marker.title??marker.kind.ToString());point.transform.SetParent(manual.transform,false);point.transform.position=instance.transform.TransformPoint(marker.position);
                        var room=point.AddComponent<GeneratedApartmentRoom>();room.kind=marker.kind;room.availableForLoot=marker.availableForLoot;
                        room.furnishingAnchor=point.transform;room.doorwayAnchor=point.transform;
                        // Unknown room polygons/areas are deliberately not inferred from a marker or the whole-apartment bounds.
                        room.area=0;room.floorSize=Vector2.zero;
                    }
                }
            }
            catch{foreach(var root in created)if(root)Object.DestroyImmediate(root);throw;}
            ConfigureEntranceAccess(b);
        }
        private static Transform EntranceSection(Transform t,GeneratedBuilding b)
        {
            while(t && t.parent!=b.transform)t=t.parent;
            return t && t.parent==b.transform && t.name.StartsWith("Entrance_",StringComparison.Ordinal)?t:null;
        }
        /// <summary>Apartment access follows its persisted authored-open door, not future loot markers or transient IsOpen.</summary>
        private static bool HasAvailableApartment(GeneratedBuilding b,Transform section)
            =>b.GetComponentsInChildren<GeneratedApartment>(true).Any(a=>EntranceSection(a.transform,b)==section && a.entranceDoor && a.entranceDoor.enabled && new SerializedObject(a.entranceDoor).FindProperty("hasAuthoredInitialOpen").boolValue);
        private static IEnumerable<Transform> GroundEntranceDoors(Transform section)
            =>section.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="EntranceDoor" && t.parent && t.parent.parent && t.parent.parent.name=="Floor_1");
        private static float EntranceOutwardSign(GeneratedBuilding b,Transform hinge,BoxCollider leaf,Quaternion closed)
        {
            var center=hinge.InverseTransformPoint(leaf.transform.TransformPoint(leaf.center));
            var parentRotation=hinge.parent?hinge.parent.rotation:Quaternion.identity;
            float Score(float sign)=>b.transform.InverseTransformVector(parentRotation*closed*(Quaternion.Euler(0,105*sign,0)*center-center)).z;
            return Score(1)<Score(-1)?1:-1;
        }
        private static void ConfigureEntranceAccess(GeneratedBuilding b)
        {
            foreach(Transform section in b.transform)
            {
                if(!section.name.StartsWith("Entrance_",StringComparison.Ordinal) || !HasAvailableApartment(b,section))continue;
                foreach(var owner in GroundEntranceDoors(section))
                {
                    var hinge=owner.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="Door_Hinge");
                    var leaf=owner.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="Door_Leaf");
                    if(!hinge||!leaf)throw new InvalidOperationException(section.name+": у входной двери нет канонической петли/полотна.");
                    // The generated instance is still in its closed authored pose here. Preserve
                    // its real closed reference; never rotate the prefab asset or assume Euler zero.
                    var interaction=ConfigureDoor(owner.gameObject,hinge,leaf);
                    var box=leaf.GetComponent<BoxCollider>()??leaf.GetComponentInChildren<BoxCollider>();
                    var serialized=new SerializedObject(interaction);
                    var closed=serialized.FindProperty("hasAuthoredInitialOpen").boolValue?serialized.FindProperty("authoredClosedRotation").quaternionValue:hinge.localRotation;
                    interaction.SetInitialOpenPose(105,EntranceOutwardSign(b,hinge,box,closed));
                }
            }
        }
        public static List<string> ValidateEntranceAccess(GeneratedBuilding b)
        {
            var errors=new List<string>();if(!b){errors.Add("Нет дома.");return errors;}
            foreach(Transform section in b.transform)
            {
                if(!section.name.StartsWith("Entrance_",StringComparison.Ordinal))continue;
                bool expected=HasAvailableApartment(b,section);var doors=GroundEntranceDoors(section).ToArray();
                if(doors.Length!=1){errors.Add(section.name+": нужен один вход первого этажа.");continue;}
                var owner=doors[0];var interaction=owner.GetComponent<DoorInteractable>();
                var hinge=owner.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="Door_Hinge");
                if(!hinge){errors.Add(section.name+": нет петли входа.");continue;}
                if(!expected)
                {
                    var source=PrefabUtility.GetCorrespondingObjectFromSource(hinge);
                    if(source && Quaternion.Angle(hinge.localRotation,source.localRotation)>.01f)errors.Add(section.name+": подъезд без доступной квартиры должен оставаться закрытым.");
                    continue;
                }
                if(!interaction||!interaction.enabled){errors.Add(section.name+": вход недоступен.");continue;}
                var so=new SerializedObject(interaction);var closed=so.FindProperty("authoredClosedRotation").quaternionValue;
                float degrees=so.FindProperty("openAngle").floatValue,sign=so.FindProperty("authoredInitialSign").floatValue;
                var leaf=so.FindProperty("leaf").objectReferenceValue as BoxCollider;
                if(!so.FindProperty("hasAuthoredInitialOpen").boolValue || degrees<100 || degrees>110 || !leaf || so.FindProperty("hinge").objectReferenceValue!=hinge)
                    errors.Add(section.name+": нет персистентного начального открытия100–110°.");
                else if(Quaternion.Angle(hinge.localRotation,closed*Quaternion.Euler(0,degrees*sign,0))>.01f || sign!=EntranceOutwardSign(b,hinge,leaf,closed))
                    errors.Add(section.name+": вход открыт не наружу/не соответствует закрытой опоре.");
            }
            return errors;
        }
        private static DoorInteractable ConfigureDoor(GameObject owner,Transform hinge,Transform leaf)
        {
            var box=leaf.GetComponent<BoxCollider>()??leaf.GetComponentInChildren<BoxCollider>();if(!box)throw new InvalidOperationException("У двери нет BoxCollider полотна.");
            var door=owner.GetComponent<DoorInteractable>()??owner.AddComponent<DoorInteractable>();
            var so=new SerializedObject(door);so.FindProperty("hinge").objectReferenceValue=hinge;so.FindProperty("leaf").objectReferenceValue=box;so.ApplyModifiedPropertiesWithoutUndo();door.enabled=true;return door;
        }
        private static float OutwardAngle(ApartmentSlot slot,DoorInteractable door)
        {
            var leaf=slot.doorLeaf.GetComponent<BoxCollider>()??slot.doorLeaf.GetComponentInChildren<BoxCollider>();
            var centre=slot.doorHinge.InverseTransformPoint(leaf.transform.TransformPoint(leaf.center));
            float Score(float angle)=>Vector3.Dot(slot.transform.InverseTransformVector(slot.doorHinge.TransformVector(Quaternion.Euler(0,angle,0)*centre-centre)),Vector3.forward);
            return Score(125)<Score(-125)?125:-125;
        }
        private static void ConfigureInternalDoors(GameObject instance)
        {
            foreach(var owner in instance.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Door_Single_Metal"))
            {
                var hinge=owner.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Door_Hinge");var leaf=owner.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Door_Leaf");if(!hinge||!leaf)continue;
                // Canonical frame-relative pose, only on this authored apartment instance.
                // Source Door10 had an offset fallen hinge; a disabled MonoBehaviour would still run Awake.
                hinge.localPosition=new Vector3(-.435f,0,.03f);hinge.localRotation=Quaternion.identity;hinge.localScale=Vector3.one;
                leaf.localPosition=new Vector3(.435f,1.0275f,-.03f);leaf.localRotation=Quaternion.identity;leaf.localScale=new Vector3(.87f,2.035f,.05f);
                var interaction=ConfigureDoor(owner.gameObject,hinge,leaf);interaction.SetInitialOpenPose(95,1);
            }
        }
        private sealed class FacadeOpening
        {public int floor;public float x,z,halfWidth;public string name;}
        private static List<FacadeOpening> FacadeOpenings(GeneratedBuilding b)
        {
            var openings=new List<FacadeOpening>();
            foreach(var module in b.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Front_",StringComparison.Ordinal)||t.name.StartsWith("Rear_",StringComparison.Ordinal)))
            {
                var p=b.transform.InverseTransformPoint(module.position);int floor=Mathf.RoundToInt(p.y/3);
                bool balcony=module.GetComponentsInChildren<Transform>(true).Any(t=>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)??"").Contains("BalconyExit"));
                bool groundDoor=module.name.StartsWith("Front_",StringComparison.Ordinal)&&floor==0&&Mathf.Abs(p.x-Mathf.Round(p.x/BuildingFootprint.SectionWidth(b.profile,b.entrances))*BuildingFootprint.SectionWidth(b.profile,b.entrances))<.1f;
                openings.Add(new FacadeOpening{floor=floor,x=p.x,z=p.z,halfWidth=balcony||BuildingGenerator.IsTripleWindow(module)?.9f:groundDoor?.5f:.6f,name=module.name+" floor="+floor+" x="+p.x.ToString("F2")});
            }
            return openings;
        }
        private static Bounds BoundsIn(Transform root,Renderer renderer)
        {
            var matrix=root.worldToLocalMatrix*renderer.transform.localToWorldMatrix;return TransformBounds(renderer.localBounds,matrix);
        }
        private static Bounds TransformBounds(Bounds bounds,Matrix4x4 matrix)
        {
            bool first=true;Bounds result=default;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {var p=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}return result;
        }
        private static bool CutsOpening(Bounds wall,int floor,List<FacadeOpening> openings)
        {
            if(wall.size.y<2.8f)return false;
            return openings.Any(o=>o.floor==floor&&wall.min.z<=o.z+.1f&&wall.max.z>=o.z-.1f&&wall.min.x<o.x+o.halfWidth-.005f&&wall.max.x>o.x-o.halfWidth+.005f);
        }
        public static List<string> ValidateFacadeOpenings(GeneratedBuilding b)
        {
            var errors=new List<string>();if(!b){errors.Add("Нет дома.");return errors;}var openings=FacadeOpenings(b);
            foreach(var apartment in b.GetComponentsInChildren<GeneratedApartment>(true))
            foreach(var renderer in apartment.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled))
            {
                var wall=InBuilding(b,renderer);if(CutsOpening(wall,apartment.floor,openings))errors.Add(apartment.apartmentId+": квартирная стена закрывает фасадный проём: "+renderer.name+" bounds="+wall);
            }
            return errors;
        }
        private static Bounds InBuilding(GeneratedBuilding b,Renderer renderer)
        {
            bool first=true;Bounds bounds=default;var box=renderer.localBounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {var p=b.transform.InverseTransformPoint(renderer.transform.TransformPoint(box.center+Vector3.Scale(box.extents,new Vector3(x,y,z))));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}return bounds;
        }
        public static List<string> Validate(GeneratedBuilding b)
        {
            var errors=new List<string>();if(!b){errors.Add("Нет дома.");return errors;}
            var apartments=b.GetComponentsInChildren<GeneratedApartment>(true);if(apartments.Length<1||apartments.Length>3)errors.Add("Нужно1–3 целых prefab-квартиры.");
            foreach(var a in apartments)
            {
                if(!a.layout||!a.sourcePrefab||!a.prefabInstance){errors.Add(a.apartmentId+": потерян источник/экземпляр.");continue;}
                if(PrefabUtility.GetCorrespondingObjectFromSource(a.prefabInstance)!=a.sourcePrefab)errors.Add(a.apartmentId+": потеряна nested prefab-связь.");
                if((a.prefabInstance.transform.localScale-Vector3.one).sqrMagnitude>.000001f)errors.Add(a.apartmentId+": квартира масштабирована.");
                var floor=b.transform.InverseTransformPoint(a.prefabInstance.transform.TransformPoint(new Vector3(a.layout.entryFloorPosition.x,a.layout.floorY,a.layout.entryFloorPosition.z)));
                if(Mathf.Abs(floor.y-a.floor*3)>.001f||Vector3.Distance(floor,a.entryLocal)>.002f)errors.Add(a.apartmentId+": пол/вход не совмещены с этажом: "+floor);
                if(a.floor<0||a.floor>=Math.Min(3,b.floors))errors.Add(a.apartmentId+": этаж вне первых трёх.");
                if(a.occupiedRegions==null||a.occupiedRegions.Length==0){errors.Add(a.apartmentId+": нет occupiedRegions.");continue;}
                float width=BuildingFootprint.SectionWidth(b.profile,b.entrances);
                foreach(var r in a.occupiedRegions)if(r.xMin<-width*.5f-.1501f||r.xMax>(b.entrances-1)*width+width*.5f+.1501f||r.yMin<-1.7001f||r.yMax>10.7001f)errors.Add(a.apartmentId+": область выходит за фасад.");
                var slot=a.GetComponentInParent<ApartmentSlot>();if(!slot||!a.entranceDoor||!a.entranceDoor.enabled||Mathf.Abs(Mathf.Abs(a.entranceAngle)-125)>.01f||Mathf.Abs(Mathf.DeltaAngle(slot.doorHinge.localEulerAngles.y,a.entranceAngle))>.01f)errors.Add(a.apartmentId+": вход не открыт на125°.");
            }
            for(int i=0;i<apartments.Length;i++)for(int j=i+1;j<apartments.Length;j++)if(apartments[i].occupiedRegions!=null&&apartments[j].occupiedRegions!=null&&apartments[i].floor==apartments[j].floor&&apartments[i].occupiedRegions.Any(a=>apartments[j].occupiedRegions.Any(c=>Overlap(a,c))))errors.Add("Пересечение квартир "+apartments[i].apartmentId+" / "+apartments[j].apartmentId);
            errors.AddRange(ValidateEntranceAccess(b));errors.AddRange(ValidateFacadeOpenings(b));errors.AddRange(ValidateFixedLobbyAndDoors(b));errors.AddRange(ValidateNoGeneratedWalls(b));return errors;
        }
        public static List<string> ValidateFixedLobbyAndDoors(GeneratedBuilding b)
        {
            var errors=new List<string>();if(!b){errors.Add("Нет дома.");return errors;}
            var slots=b.GetComponentsInChildren<ApartmentSlot>(true);
            if(slots.Length!=Math.Min(b.floors,4)*b.entrances*3)errors.Add("Фиксированный подъезд потерял исходные3слота на этаж.");
            foreach(var slot in slots)
            {
                var expected=slot.slotId.EndsWith("/R",StringComparison.Ordinal)?new Vector3(1.55f,0,0):slot.slotId.EndsWith("/L2",StringComparison.Ordinal)?new Vector3(-1.55f,0,3):new Vector3(-1.55f,0,0);
                if(Vector3.Distance(slot.transform.localPosition,expected)>.001f)errors.Add(slot.slotId+": сдвинута исходная дверь подъезда.");
                bool selected=slot.GetComponentInChildren<GeneratedApartment>(true)!=null;
                if(!selected&&Mathf.Abs(Mathf.DeltaAngle(slot.doorHinge.localEulerAngles.y,0))>.01f)errors.Add(slot.slotId+": невыбранный вход должен оставаться закрытым.");
            }
            foreach(var apartment in b.GetComponentsInChildren<GeneratedApartment>(true))
            {
                if(apartment.connectorRegions!=null&&apartment.connectorRegions.Length>0||apartment.connectorPath!=null&&apartment.connectorPath.Length>0)errors.Add(apartment.apartmentId+": direct-entry не допускает тамбур.");
                var slot=apartment.GetComponentInParent<ApartmentSlot>();
                if(slot&&Vector3.Distance(b.transform.InverseTransformPoint(slot.transform.position),apartment.entryLocal)>.001f)errors.Add(apartment.apartmentId+": вход prefab не совпадает точно с фиксированной дверью.");
                foreach(var owner in apartment.prefabInstance.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Door_Single_Metal"))
                {
                    var hinge=owner.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Door_Hinge");var leaf=owner.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Door_Leaf");
                    if(!hinge||!leaf||Vector3.Distance(hinge.localPosition,new Vector3(-.435f,0,.03f))>.001f||Vector3.Distance(leaf.localPosition,new Vector3(.435f,1.0275f,-.03f))>.001f)errors.Add(apartment.apartmentId+": полотно не на канонической петле рамы "+owner.parent.name);
                }
            }
            return errors;
        }
        public static List<string> ValidateNoGeneratedWalls(GeneratedBuilding b)
        {
            var errors=new List<string>();if(!b){errors.Add("Нет дома.");return errors;}
            foreach(var apartment in b.GetComponentsInChildren<GeneratedApartment>(true))
            {
                if(!apartment.prefabInstance)continue;
                foreach(var component in apartment.GetComponentsInChildren<Component>(true).Where(c=>c is Renderer||c is MeshFilter||c is Collider))
                {
                    if(!component.transform.IsChildOf(apartment.prefabInstance.transform))
                        errors.Add(apartment.apartmentId+": дополнительная геометрия/коллизия за дверью: "+component.name+" / "+component.GetType().Name);
                    else if(PrefabUtility.GetCorrespondingObjectFromSource(component)==null)
                        errors.Add(apartment.apartmentId+": добавленный компонент внутри prefab instance без source: "+component.name+" / "+component.GetType().Name);
                }
            }
            return errors;
        }
        public static List<string> ValidateInheritedPaths(GeneratedBuilding b)
        {
            var errors=new List<string>();if(!b){errors.Add("Нет дома.");return errors;}
            foreach(var apartment in b.GetComponentsInChildren<GeneratedApartment>(true))
            {
                float sectionWidth=BuildingFootprint.SectionWidth(b.profile,b.entrances);
                int floor=apartment.floor;var potentialDoors=new HashSet<Collider>();
                foreach(var door in b.GetComponentsInChildren<DoorInteractable>(true).Where(d=>d.enabled))
                {var data=new SerializedObject(door);var leaf=data.FindProperty("leaf").objectReferenceValue as Collider;if(leaf)potentialDoors.Add(leaf);var hinge=data.FindProperty("hinge").objectReferenceValue as Transform;if(hinge)foreach(var moving in hinge.GetComponentsInChildren<Collider>(true))potentialDoors.Add(moving);}
                var colliders=b.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&!potentialDoors.Contains(c)&&c.bounds.max.y>b.transform.position.y+floor*3+.15f&&c.bounds.min.y<b.transform.position.y+floor*3+2).ToArray();
                var cachedBounds=colliders.Select(c=>c.bounds).ToArray();
                var cells=new Dictionary<Vector2Int,List<int>>();
                for(int index=0;index<colliders.Length;index++)
                {
                    var bounds=cachedBounds[index];
                    for(int x=Mathf.FloorToInt(bounds.min.x);x<=Mathf.FloorToInt(bounds.max.x);x++)
                    for(int z=Mathf.FloorToInt(bounds.min.z);z<=Mathf.FloorToInt(bounds.max.z);z++)
                    {var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list)){list=new List<int>();cells.Add(key,list);}list.Add(index);}
                }
                bool Clear(Vector3 a,Vector3 z)
                {
                    var delta=z-a;float length=delta.magnitude;if(length<.001f)return true;
                    var sweep=new Bounds((a+z)/2,new Vector3(Mathf.Abs(delta.x)+.4f,Mathf.Abs(delta.y)+.4f,Mathf.Abs(delta.z)+.4f));
                    var nearby=new HashSet<int>();
                    for(int x=Mathf.FloorToInt(sweep.min.x);x<=Mathf.FloorToInt(sweep.max.x);x++)
                    for(int zCell=Mathf.FloorToInt(sweep.min.z);zCell<=Mathf.FloorToInt(sweep.max.z);zCell++)
                        if(cells.TryGetValue(new Vector2Int(x,zCell),out var list))foreach(int index in list)nearby.Add(index);
                    foreach(int index in nearby)
                    {
                        if(!cachedBounds[index].Intersects(sweep))continue;var c=colliders[index];
                        bool primitive=c is BoxCollider||c is SphereCollider||c is CapsuleCollider||c is MeshCollider m&&m.convex;
                        if(primitive&&Vector3.Distance(c.ClosestPoint((a+z)/2),(a+z)/2)<.0001f)return false;
                        if(c.Raycast(new Ray(a,delta/length),out _,length)||c.Raycast(new Ray(z,-delta/length),out _,length))return false;
                    }
                    return true;
                }
                var edgeCache=new Dictionary<(Vector2,Vector2),bool>();
                bool Walk(Vector2 a,Vector2 z)
                {
                    if(edgeCache.TryGetValue((a,z),out bool known))return known;
                    var direction=z-a;var side=direction.sqrMagnitude>0?new Vector2(-direction.y,direction.x).normalized*.18f:Vector2.zero;
                    foreach(float height in new[]{.45f,1f,1.7f})foreach(float lateral in new[]{-1f,0f,1f})
                    {var from=b.transform.TransformPoint(new Vector3(a.x+side.x*lateral,floor*3+height,a.y+side.y*lateral));var to=b.transform.TransformPoint(new Vector3(z.x+side.x*lateral,floor*3+height,z.y+side.y*lateral));if(!Clear(from,to)){edgeCache[(a,z)]=edgeCache[(z,a)]=false;return false;}}edgeCache[(a,z)]=edgeCache[(z,a)]=true;return true;
                }
                var start=new Vector2(apartment.connectorPath!=null&&apartment.connectorPath.Length>0?apartment.connectorPath[0].x:apartment.approachLocal.x,apartment.connectorPath!=null&&apartment.connectorPath.Length>0?apartment.connectorPath[0].z:apartment.approachLocal.z);var entry=new Vector2(apartment.entryLocal.x,apartment.entryLocal.z);
                var inward=b.transform.InverseTransformDirection(apartment.prefabInstance.transform.TransformDirection(apartment.layout.inwardDirection)).normalized;
                var inside=entry+new Vector2(inward.x,inward.z)*.8f;
                if(!Walk(start,inside)){errors.Add(apartment.apartmentId+": заблокирован непосредственный вход фиксированная дверь→prefab.");continue;}
                // Local half-metre BFS to a real entrance lobby; checks potential geometry with operable leaves excluded only.
                const float step=.5f;var origin=start;var queue=new Queue<Vector2Int>();var visited=new HashSet<Vector2Int>();queue.Enqueue(Vector2Int.zero);visited.Add(Vector2Int.zero);bool reached=false;
                while(queue.Count>0&&!reached)
                {
                    var node=queue.Dequeue();var p=origin+(Vector2)node*step;
                    for(int e=0;e<b.entrances;e++)if(Mathf.Abs(p.x-e*sectionWidth)<.55f&&p.y>-.9f&&p.y<3.8f){reached=true;break;}
                    foreach(var offset in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                    {
                        var next=node+offset;if(visited.Contains(next))continue;var point=origin+(Vector2)next*step;
                        if(point.x<-sectionWidth*.5f+.3f||point.x>(b.entrances-1)*sectionWidth+sectionWidth*.5f-.3f||point.y<-1.2f||point.y>10.2f||!Walk(p,point))continue;
                        visited.Add(next);queue.Enqueue(next);
                    }
                }
                if(!reached)errors.Add(apartment.apartmentId+": внешний подход не связан с лестничным вестибюлем.");
                var targets=apartment.GetComponentsInChildren<GeneratedApartmentRoom>().Where(r=>r.availableForLoot&&r.furnishingAnchor!=null).ToArray();
                var targetPositions=targets.ToDictionary(r=>r,r=>{var t=b.transform.InverseTransformPoint(r.furnishingAnchor.position);return new Vector2(t.x,t.z);});
                var foundRooms=new HashSet<GeneratedApartmentRoom>();
                var frontier=new Queue<Vector2Int>();var seen=new HashSet<Vector2Int>();frontier.Enqueue(Vector2Int.zero);seen.Add(Vector2Int.zero);
                while(frontier.Count>0&&foundRooms.Count<targets.Length)
                {
                    var node=frontier.Dequeue();var p=entry+(Vector2)node*.25f;
                    foreach(var room in targets)
                    {
                        if(foundRooms.Contains(room))continue;var targetXZ=targetPositions[room];
                        if(Vector2.Distance(p,targetXZ)<.7f&&Walk(p,targetXZ))foundRooms.Add(room);
                    }
                    foreach(var offset in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                    {
                        var next=node+offset;if(seen.Contains(next))continue;var point=entry+(Vector2)next*.25f;
                        if(!apartment.occupiedRegions.Any(r=>r.Contains(point))||!Walk(p,point))continue;seen.Add(next);frontier.Enqueue(next);
                    }
                }
                foreach(var room in targets.Where(r=>!foundRooms.Contains(r)))errors.Add(apartment.apartmentId+": нет потенциального прохода к "+room.name+" (оперативные полотна исключены, стены учтены).");
            }
            return errors;
        }
    }
}
