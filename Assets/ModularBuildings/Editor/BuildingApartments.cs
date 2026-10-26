using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.Interaction;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingApartments
    {
        private static int Hash(string text,int seed){unchecked{uint h=(uint)seed^2166136261;foreach(char c in text??""){h^=c;h*=16777619;}return (int)h;}}
        public static void Add(GeneratedBuilding b,int seed)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Квартиры создаются только в Edit.");
            if(b==null||b.profile==null||b.profile.section==null)throw new InvalidOperationException("Нужен новый дом с профилем.");
            if(b.GetComponentsInChildren<GeneratedApartment>(true).Length>0)throw new InvalidOperationException("Квартиры уже созданы; ручное содержимое не заменяется.");
            if(b.profile.section.sectionWidth<14.9f)throw new InvalidOperationException("Планировки требуют секции15м: узкий9м шаблон не даёт достаточной площади для кухни и2–4 жилых комнат.");
            var candidates=b.GetComponentsInChildren<ApartmentSlot>(true).Where(s=>s.slotId!=null&&(s.slotId.EndsWith("/L",StringComparison.Ordinal)||s.slotId.EndsWith("/R",StringComparison.Ordinal))&&s.manualContent!=null&&s.doorHinge!=null&&s.doorLeaf!=null&&b.transform.InverseTransformPoint(s.transform.position).y>=-.01f&&b.transform.InverseTransformPoint(s.transform.position).y<=6.01f).OrderBy(s=>s.slotId,StringComparer.Ordinal).ToList();
            if(candidates.Count==0)throw new InvalidOperationException("Нет пригодных квартирных слотов первых трёх этажей.");
            if(candidates.Any(s=>s.manualContent.childCount>0))throw new InvalidOperationException("ManualContent уже содержит авторские объекты. Разрешено только новое здание.");
            // All other entries stay physically closed; an L2 entrance must not become a second route into the same home.
            foreach(var unused in b.GetComponentsInChildren<ApartmentSlot>(true))
            {
                if(unused.doorHinge!=null)unused.doorHinge.localRotation=Quaternion.identity;
                if(unused.doorLeaf!=null)unused.doorLeaf.localRotation=Quaternion.identity;
                foreach(var interaction in unused.GetComponentsInChildren<DoorInteractable>(true))interaction.enabled=false;
            }
            var random=new System.Random(Hash(b.buildingId,seed));
            for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var t=candidates[i];candidates[i]=candidates[j];candidates[j]=t;}
            int count=random.Next(1,Math.Min(3,candidates.Count)+1);
            var created=new List<GameObject>();
            try
            {
                foreach(var slot in candidates.Take(count))
                {
                    var root=new GameObject("Generated apartment");root.transform.SetParent(slot.manualContent,false);created.Add(root);
                    // Author geometry in building-local axes; the slot remains the owner, not the coordinate system.
                    root.transform.rotation=b.transform.rotation;
                    var slotLocal=b.transform.InverseTransformPoint(slot.transform.position);bool right=slot.slotId.EndsWith("/R",StringComparison.Ordinal);
                    int entrance=int.Parse(slot.slotId.Split('/').First(p=>p.StartsWith("E")).Substring(1))-1;
                    float sectionX=entrance*b.profile.section.sectionWidth;
                    float inner=right?sectionX+1.82f:sectionX-7.25f;
                    Vector3 origin=new Vector3(inner,slotLocal.y+.12f,-1.25f);
                    root.transform.position=b.transform.TransformPoint(origin);root.transform.localScale=Vector3.one;
                    var apartment=root.AddComponent<GeneratedApartment>();apartment.apartmentId=slot.slotId;apartment.layoutSeed=Hash(slot.slotId,seed);apartment.buildingLocalBounds=new Bounds(origin+new Vector3(2.7f,1.35f,5.65f),new Vector3(5.4f,2.7f,11.3f));
                    float corridorX=right?.5f:4.9f;float roomX=right?3.2f:2.2f;float wallX=right?1.05f:4.35f;
                    int living=random.Next(2,5);bool bath=random.Next(2)==0,toilet=random.Next(2)==0;
                    var kinds=new List<GeneratedRoomKind>{GeneratedRoomKind.Kitchen};for(int i=0;i<living;i++)kinds.Add(GeneratedRoomKind.Living);
                    int serviceIndex=Math.Min(3,kinds.Count);
                    if(bath)kinds.Insert(serviceIndex++,GeneratedRoomKind.Bathroom);if(toilet)kinds.Insert(serviceIndex,GeneratedRoomKind.Toilet);
                    bool exteriorEnd=right?entrance==b.entrances-1:entrance==0;
                    var lengths=RoomLengths(kinds,exteriorEnd);float z=0;
                    int accessibleLiving=random.Next(living);int livingIndex=0;
                    for(int i=0;i<kinds.Count;i++)
                    {
                        var kind=kinds[i];float length=lengths[i];
                        var room=new GameObject(kind+" "+(i+1));room.transform.SetParent(root.transform,false);room.transform.localPosition=new Vector3(roomX,0,z+length*.5f);
                        var metadata=room.AddComponent<GeneratedApartmentRoom>();metadata.kind=kind;metadata.floorSize=new Vector2(4.2f,length-.12f);metadata.area=metadata.floorSize.x*metadata.floorSize.y;
                        metadata.availableForLoot=kind==GeneratedRoomKind.Kitchen || (kind==GeneratedRoomKind.Living ? livingIndex++==accessibleLiving||random.Next(2)==0 : random.Next(2)==0);
                        metadata.furnishingAnchor=Anchor(room.transform,"Furnishing anchor",Vector3.zero);
                        metadata.doorwayAnchor=Anchor(root.transform,"Doorway "+i,new Vector3(wallX,0,z+length*.5f));
                        float doorwayWidth=Mathf.Min(1f,length-.16f),side=(length-doorwayWidth)*.5f;
                        Box(root.transform,"Partition side",new Vector3(wallX,1.35f,z+side*.5f),new Vector3(.12f,2.7f,side));
                        Box(root.transform,"Partition side",new Vector3(wallX,1.35f,z+length-side*.5f),new Vector3(.12f,2.7f,side));
                        Box(root.transform,"Door lintel",new Vector3(wallX,2.4f,z+length*.5f),new Vector3(.12f,.6f,doorwayWidth));
                        if(!metadata.availableForLoot)metadata.closedDoor=Box(root.transform,"Closed room door — "+kind,new Vector3(wallX,1,z+length*.5f),new Vector3(.08f,2,doorwayWidth)).GetComponent<BoxCollider>();
                        if(i>0)Box(root.transform,"Room dividing wall",new Vector3(roomX,1.35f,z),new Vector3(4.2f,2.7f,.12f));
                        z+=length;
                    }
                    // Front/rear and the two outer ends already have the real facade with windows/balcony exits.
                    // Only internal section boundaries need an apartment-owned wall; never back an opening with a solid lining.
                    if(right&&entrance<b.entrances-1)Box(root.transform,"Apartment internal section boundary",new Vector3(5.4f,1.35f,5.65f),new Vector3(.12f,2.7f,11.3f));
                    if(!right&&entrance>0)Box(root.transform,"Apartment internal section boundary",new Vector3(0,1.35f,5.65f),new Vector3(.12f,2.7f,11.3f));
                    var doorObject=slot.doorHinge.parent.gameObject;var door=doorObject.GetComponent<DoorInteractable>();if(door==null)door=doorObject.AddComponent<DoorInteractable>();
                    var leaf=slot.doorLeaf.GetComponent<BoxCollider>();if(leaf==null)leaf=slot.doorLeaf.GetComponentInChildren<BoxCollider>();if(leaf==null)throw new InvalidOperationException("Нет BoxCollider полотна входа.");
                    door.enabled=true;var data=new SerializedObject(door);data.FindProperty("hinge").objectReferenceValue=slot.doorHinge;data.FindProperty("leaf").objectReferenceValue=leaf;data.FindProperty("openAngle").floatValue=125;data.ApplyModifiedPropertiesWithoutUndo();
                    // Choose the rotation whose closed-leaf centre moves out of the apartment (negative slot.forward).
                    var centre=slot.doorHinge.InverseTransformPoint(leaf.transform.TransformPoint(leaf.center));float scorePlus=Vector3.Dot(slot.transform.InverseTransformVector(slot.doorHinge.TransformVector(Quaternion.Euler(0,125,0)*centre-centre)),Vector3.forward);
                    float scoreMinus=Vector3.Dot(slot.transform.InverseTransformVector(slot.doorHinge.TransformVector(Quaternion.Euler(0,-125,0)*centre-centre)),Vector3.forward);
                    apartment.entranceAngle=scorePlus<scoreMinus?125:-125;apartment.entranceDoor=door;door.SetInitialOpenPose(125,Mathf.Sign(apartment.entranceAngle));
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=b.profile.stairWallMaterial;
                }
            }
            catch{foreach(var go in created)if(go)Object.DestroyImmediate(go);throw;}
        }
        private static float[] RoomLengths(List<GeneratedRoomKind> kinds,bool exteriorEnd)
        {
            const int total=226; // 11.30 m, exact 5 cm layout lattice.
            var result=new float[kinds.Count];var failed=new HashSet<(int,int)>();
            bool Search(int room,int start)
            {
                if(room==kinds.Count)return start==total;
                if(failed.Contains((room,start)))return false;
                int minimum=kinds[room]==GeneratedRoomKind.Bathroom||kinds[room]==GeneratedRoomKind.Toilet?21:30;
                int remaining=0;for(int j=room+1;j<kinds.Count;j++)remaining+=kinds[j]==GeneratedRoomKind.Bathroom||kinds[j]==GeneratedRoomKind.Toilet?21:30;
                float desired=(total-start)/(float)(kinds.Count-room);
                foreach(int end in Enumerable.Range(start+minimum,Math.Max(0,total-remaining-start-minimum+1)).OrderBy(e=>Mathf.Abs(e-start-desired)).ThenBy(e=>e))
                {
                    float z=end*.05f;
                    // The two 1.2 m end-facade apertures are at building Z3/6, i.e. apartment Z4.25/7.25.
                    // Reserve the full opening/recess and partition thickness: approximately Z3.3–5.2 / 6.3–8.2.
                    if(exteriorEnd&&room<kinds.Count-1&&(Mathf.Abs(z-4.25f)<.94f||Mathf.Abs(z-7.25f)<.94f))continue;
                    result[room]=(end-start)*.05f;
                    if(Search(room+1,end))return true;
                }
                failed.Add((room,start));return false;
            }
            if(!Search(0,0))throw new InvalidOperationException("Нельзя разместить комнаты без перегородки в торцевом окне.");
            return result;
        }
        private static Transform Anchor(Transform parent,string name,Vector3 p){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;return go.transform;}
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;return go;}
        public static List<string> Validate(GeneratedBuilding b)
        {
            var errors=new List<string>();if(b==null){errors.Add("Нет дома.");return errors;}
            var apartments=b.GetComponentsInChildren<GeneratedApartment>(true);if(apartments.Length<1||apartments.Length>3)errors.Add("Нужно1–3 квартиры на дом.");
            foreach(var a in apartments)
            {
                if(a.buildingLocalBounds.min.y<-.01f||a.buildingLocalBounds.max.y>9.001f)errors.Add(a.apartmentId+": область квартиры вне первых трёх этажей (Y0–9м).");
                if(Vector3.Distance(b.transform.InverseTransformPoint(a.transform.position),a.buildingLocalBounds.min)>.001f)errors.Add(a.apartmentId+": metadata bounds не соответствует локальному origin квартиры.");
                var rooms=a.GetComponentsInChildren<GeneratedApartmentRoom>();if(rooms.Count(r=>r.kind==GeneratedRoomKind.Kitchen)!=1||rooms.Count(r=>r.kind==GeneratedRoomKind.Living)<2||rooms.Count(r=>r.kind==GeneratedRoomKind.Living)>4)errors.Add(a.apartmentId+": неверный состав комнат.");
                if(rooms.Count(r=>r.availableForLoot)<2||!rooms.Any(r=>r.kind==GeneratedRoomKind.Kitchen&&r.availableForLoot)||!rooms.Any(r=>r.kind==GeneratedRoomKind.Living&&r.availableForLoot))errors.Add(a.apartmentId+": нужны доступные кухня и жилая комната.");
                if(Mathf.Abs(Mathf.Abs(a.entranceAngle)-125)>.1f||a.entranceDoor==null)errors.Add(a.apartmentId+": вход не открыт на125°.");
                var slot=a.GetComponentInParent<ApartmentSlot>();
                if(slot==null||slot.doorHinge==null||Mathf.Abs(Mathf.DeltaAngle(slot.doorHinge.localEulerAngles.y,a.entranceAngle))>.1f)errors.Add(a.apartmentId+": фактический угол петли не совпадает с125°.");
                if(slot==null)continue;
                var allBoxes=a.GetComponentsInChildren<BoxCollider>(true);
                bool Blocked(Vector3 world)=>allBoxes.Any(c=>c.enabled&&c.gameObject.activeInHierarchy&&Vector3.Distance(c.ClosestPoint(world),world)<.01f);
                bool right=slot.slotId.EndsWith("/R",StringComparison.Ordinal);
                float corridorX=right?.5f:4.9f;
                for(float z=.2f;z<11.15f;z+=.25f)
                    if(Blocked(a.transform.TransformPoint(new Vector3(corridorX,1,z)))){errors.Add(a.apartmentId+": перегородка перекрывает коридор.");break;}
                foreach(var room in rooms.Where(r=>r.availableForLoot))
                {
                    if(room.doorwayAnchor==null)continue;
                    var doorway=a.transform.InverseTransformPoint(room.doorwayAnchor.position);var target=a.transform.InverseTransformPoint(room.transform.position);
                    for(int n=0;n<=20;n++)
                    {
                        var local=Vector3.Lerp(new Vector3(corridorX,1,doorway.z),new Vector3(target.x,1,target.z),n/20f);
                        if(Blocked(a.transform.TransformPoint(local))){errors.Add(a.apartmentId+": закрыт проход в "+room.name);break;}
                    }
                    if(room.furnishingAnchor!=null)
                    {
                        // Anchors lie exactly on the authored floor. Bounds centre/extents subtraction and
                        // the rotated hierarchy round-trip can differ by a few float ULPs at that boundary.
                        const float tolerance=.001f;
                        var point=b.transform.InverseTransformPoint(room.furnishingAnchor.position);
                        var min=a.buildingLocalBounds.min;var max=a.buildingLocalBounds.max;
                        if(point.x<min.x-tolerance||point.x>max.x+tolerance||point.y<min.y-tolerance||point.y>max.y+tolerance||point.z<min.z-tolerance||point.z>max.z+tolerance)
                            errors.Add(a.apartmentId+": точка "+room.name+" вне квартиры: point="+point.ToString("F6")+" min="+min.ToString("F6")+" max="+max.ToString("F6"));
                    }
                }
                foreach(var room in rooms)if(room.area<=0||room.floorSize.x<.8f||room.floorSize.y<.8f||room.furnishingAnchor==null||room.doorwayAnchor==null||!room.availableForLoot&&room.closedDoor==null)errors.Add(a.apartmentId+": некорректные метаданные/блокировка комнаты.");
            }
            for(int i=0;i<apartments.Length;i++)for(int j=i+1;j<apartments.Length;j++)if(apartments[i].buildingLocalBounds.Intersects(apartments[j].buildingLocalBounds))errors.Add("Квартиры пересекаются: "+apartments[i].apartmentId+" / "+apartments[j].apartmentId);
            return errors;
        }
        /// <summary>Read-only corridor/doorway probes against every enabled inherited building collider. No Physics scene mutation.</summary>
        public static List<string> ValidateInheritedPaths(GeneratedBuilding b)
        {
            var errors=new List<string>();if(b==null){errors.Add("Нет дома.");return errors;}
            var colliders=b.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy).ToArray();
            bool Hit(Vector3 start,Vector3 end,out Collider obstruction)
            {
                foreach(var c in colliders)
                {
                    // ClosestPoint is defined for these primitive/convex shapes, never for non-convex MeshCollider.
                    bool canContain=c is BoxCollider||c is SphereCollider||c is CapsuleCollider||c is MeshCollider m&&m.convex;
                    if(canContain&&Vector3.Distance(c.ClosestPoint((start+end)*.5f),(start+end)*.5f)<.0001f){obstruction=c;return true;}
                    var delta=end-start;float length=delta.magnitude;
                    if(length>.0001f&&(c.Raycast(new Ray(start,delta/length),out _,length)||c.Raycast(new Ray(end,-delta/length),out _,length))){obstruction=c;return true;}
                }
                obstruction=null;return false;
            }
            void Segment(GeneratedApartment apartment,Vector3 start,Vector3 end,string label)
            {
                var direction=end-start;var side=Vector3.Cross(Vector3.up,direction.normalized)*.18f;
                foreach(float height in new[]{.45f,1f,1.7f})foreach(float lateral in new[]{-1f,0f,1f})
                {
                    var a=start+Vector3.up*height+side*lateral;var z=end+Vector3.up*height+side*lateral;
                    if(Hit(a,z,out var obstacle)){errors.Add(apartment.apartmentId+": путь «"+label+"» перекрыт "+obstacle.name+" / "+obstacle.GetType().Name);return;}
                }
            }
            foreach(var apartment in b.GetComponentsInChildren<GeneratedApartment>(true))
            {
                var slot=apartment.GetComponentInParent<ApartmentSlot>();if(slot==null){errors.Add("Квартира без слота.");continue;}
                bool right=slot.slotId.EndsWith("/R",StringComparison.Ordinal);
                float corridorX=right?.5f:4.9f;
                var door=slot.transform.position;
                var corridor=apartment.transform.TransformPoint(new Vector3(corridorX,0,1.25f));
                var outside=door-slot.transform.forward*.8f;
                // Root origins are at floor+0.12; use the same height at both sides of the doorway.
                outside+=b.transform.up*.12f;
                Segment(apartment,outside,corridor,"вход из лестничного коридора");
                var ownBoxes=apartment.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.enabled&&c.gameObject.activeInHierarchy).ToArray();
                int entrance=int.Parse(slot.slotId.Split('/').First(p=>p.StartsWith("E")).Substring(1))-1;
                bool internalEdge=right?entrance<b.entrances-1:entrance>0;
                float outerX=right?5.4f:0;
                if(internalEdge)
                {
                    // Probe the entire former back-door bypass, including behind every closed room.
                    foreach(float y in new[]{.45f,1f,1.7f})for(float z=.12f;z<11.2f;z+=.2f)
                    {
                        var from=apartment.transform.TransformPoint(new Vector3(outerX-.25f,y,z));
                        var to=apartment.transform.TransformPoint(new Vector3(outerX+.25f,y,z));
                        var delta=to-from;
                        if(!ownBoxes.Any(c=>c.Raycast(new Ray(from,delta.normalized),out _,delta.magnitude)||c.Raycast(new Ray(to,-delta.normalized),out _,delta.magnitude)))
                        {errors.Add(apartment.apartmentId+": незамкнутая граница соседней секции: z="+z.ToString("F2")+" y="+y);break;}
                    }
                }
                // Use actual facade module transforms: Front/Rear include V6 balcony replacements;
                // Window_1/2 are the two measured openings of each exterior end facade.
                foreach(var module in b.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Front_",StringComparison.Ordinal)||t.name.StartsWith("Rear_",StringComparison.Ordinal)||((t.name=="Window_1"||t.name=="Window_2")&&(t.parent.name=="LeftEnd"||t.parent.name=="RightEnd"))))
                {
                    var centre=module.TransformPoint(new Vector3(0,1.5f,0));
                    var inApartment=apartment.transform.InverseTransformPoint(centre);
                    // Restrict to this floor and lateral strip, with the real facade thickness/offset included.
                    if(inApartment.y<.5f||inApartment.y>2.2f||inApartment.x<-.7f||inApartment.x>6.1f||inApartment.z<-.7f||inApartment.z>12)continue;
                    var normal=module.forward;
                    foreach(float horizontal in new[]{-.25f,0f,.25f})
                    {
                        var point=centre+module.right*horizontal;
                        var from=point-normal*.8f;var to=point+normal*.8f;var delta=to-from;
                        if(ownBoxes.Any(c=>c.Raycast(new Ray(from,delta.normalized),out _,delta.magnitude)||c.Raycast(new Ray(to,-delta.normalized),out _,delta.magnitude)))
                            errors.Add(apartment.apartmentId+": новая перегородка закрывает фасадный проём "+module.name+" / "+module.parent.name+" point="+point.ToString("F3")+" collider="+string.Join(",",ownBoxes.Where(c=>c.Raycast(new Ray(from,delta.normalized),out _,delta.magnitude)||c.Raycast(new Ray(to,-delta.normalized),out _,delta.magnitude)).Select(c=>c.name)));
                    }
                }
                foreach(var room in apartment.GetComponentsInChildren<GeneratedApartmentRoom>().Where(r=>r.availableForLoot&&r.doorwayAnchor!=null))
                {
                    float z=apartment.transform.InverseTransformPoint(room.doorwayAnchor.position).z;
                    var approach=apartment.transform.TransformPoint(new Vector3(corridorX,0,z));
                    Segment(apartment,corridor,approach,"к комнате "+room.name);
                    Segment(apartment,approach,room.transform.position,"проём "+room.name);
                }
            }
            return errors;
        }
    }
}
