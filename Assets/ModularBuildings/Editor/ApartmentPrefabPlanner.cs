using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    public sealed class PlacedApartment
    {
        public string SlotId { get; internal set; }
        public IReadOnlyList<Rect> ConnectorRegions { get; internal set; } = Array.Empty<Rect>();
        public IReadOnlyList<Vector3> ConnectorPath { get; internal set; } = Array.Empty<Vector3>();
        public ApartmentPrefabLayout Layout { get; }
        public int Floor { get; }
        public float Yaw { get; }
        public Vector3 RootLocalPosition { get; }
        public IReadOnlyList<Rect> Regions { get; }
        public Vector3 EntryLocal { get; }
        public Vector3 ApproachPoint { get; }
        internal PlacedApartment(ApartmentPrefabLayout layout,int floor,int quarter,Vector3 root,Rect[] regions,Vector3 entry,Vector3 approach)
        {Layout=layout;Floor=floor;Yaw=quarter*90;RootLocalPosition=root;Regions=new ReadOnlyCollection<Rect>(regions);EntryLocal=entry;ApproachPoint=approach;}
    }
    /// <summary>Pure floor-space planning. No instances, physics, Undo, asset or scene writes.</summary>
    public static class ApartmentPrefabPlanner
    {
        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        private static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        private static Vector2 Rotate(Vector2 v,int q)
        {switch(q){case 0:return v;case 1:return new Vector2(v.y,-v.x);case 2:return -v;default:return new Vector2(-v.y,v.x);}}
        private static bool Overlap(Rect a,Rect b)=>a.xMin<b.xMax&&a.xMax>b.xMin&&a.yMin<b.yMax&&a.yMax>b.yMin;
        private static bool Contains(Rect rect,Vector2 p,float margin=0)=>p.x>rect.xMin-margin&&p.x<rect.xMax+margin&&p.y>rect.yMin-margin&&p.y<rect.yMax+margin;
        private static Rect Turn(Rect rect,int q,Vector2 offset)
        {
            var points=new[]{Rotate(new Vector2(rect.xMin,rect.yMin),q),Rotate(new Vector2(rect.xMin,rect.yMax),q),Rotate(new Vector2(rect.xMax,rect.yMin),q),Rotate(new Vector2(rect.xMax,rect.yMax),q)};
            return Rect.MinMaxRect(points.Min(p=>p.x)+offset.x,points.Min(p=>p.y)+offset.y,points.Max(p=>p.x)+offset.x,points.Max(p=>p.y)+offset.y);
        }
        public static List<string> Validate(ApartmentPrefabLayout layout)
        {
            var errors=new List<string>();if(layout==null){errors.Add("Не задана планировка квартиры.");return errors;}
            if(layout.prefab==null||!EditorUtility.IsPersistent(layout.prefab)||!PrefabUtility.IsPartOfPrefabAsset(layout.prefab)||!AssetDatabase.IsMainAsset(layout.prefab))errors.Add("Нужен корень prefab asset из Project.");
            if(!Finite(layout.entryFloorPosition)||!Finite(layout.floorY)||!Finite(layout.roomHeight)||layout.roomHeight<=0)errors.Add("Нужны конечные координаты входа/пола и положительная высота.");
            if(Finite(layout.entryFloorPosition)&&Finite(layout.floorY)&&Mathf.Abs(layout.entryFloorPosition.y-layout.floorY)>.01f)errors.Add("EntryFloorPosition.Y должен совпадать с FloorY.");
            if(!Finite(layout.inwardDirection)||Mathf.Abs(layout.inwardDirection.y)>.0001f||new Vector2(layout.inwardDirection.x,layout.inwardDirection.z).sqrMagnitude<.0001f)errors.Add("Направление входа должно быть ненулевым горизонтальным вектором.");
            if(layout.footprintRegions==null||layout.footprintRegions.Length==0)errors.Add("Нет прямоугольников занимаемой площади.");
            else for(int i=0;i<layout.footprintRegions.Length;i++)
            {
                var a=layout.footprintRegions[i];if(!Finite(a.x)||!Finite(a.y)||!Finite(a.width)||!Finite(a.height)||a.width<=0||a.height<=0){errors.Add("Некорректная область "+i);continue;}
                for(int j=0;j<i;j++)if(Overlap(a,layout.footprintRegions[j]))errors.Add("Области footprint пересекаются: "+j+" / "+i);
            }
            if(layout.rooms!=null)foreach(var room in layout.rooms)if(room==null||!Finite(room.position))errors.Add("Некорректная точка помещения.");
            return errors;
        }
        private static bool ApproachesReachLobby(IReadOnlyList<PlacedApartment> apartments,int entrances,float width)
        {
            if(apartments.Count==0)return true;
            var obstacles=new List<Rect>();
            foreach(var apartment in apartments)foreach(var r in apartment.Regions)obstacles.Add(Rect.MinMaxRect(r.xMin-.2f,r.yMin-.2f,r.xMax+.2f,r.yMax+.2f));
            for(int e=0;e<entrances;e++)obstacles.Add(Rect.MinMaxRect(e*width-1.85f,4,e*width+1.85f,9.2f));
            bool Clear(Vector2 a,Vector2 b)
            {
                if(a.x<-width*.5f+.2f||a.x>(entrances-1)*width+width*.5f-.2f||a.y<-1.3f||a.y>10.3f||b.x<-width*.5f+.2f||b.x>(entrances-1)*width+width*.5f-.2f||b.y<-1.3f||b.y>10.3f)return false;
                foreach(var rect in obstacles)
                {
                    // Slab clipping tests the complete segment against a clearance-inflated footprint.
                    float enter=0,exit=1;var d=b-a;
                    bool Slab(float p,float delta,float low,float high)
                    {
                        if(Mathf.Abs(delta)<.000001f)return p>low&&p<high;
                        float x=(low-p)/delta,y=(high-p)/delta;if(x>y){float t=x;x=y;y=t;}enter=Mathf.Max(enter,x);exit=Mathf.Min(exit,y);return enter<exit;
                    }
                    if(Slab(a.x,d.x,rect.xMin,rect.xMax)&&Slab(a.y,d.y,rect.yMin,rect.yMax)&&enter<exit)return false;
                }
                return true;
            }
            foreach(var apartment in apartments)
            {
                var start=new Vector2(apartment.ApproachPoint.x,apartment.ApproachPoint.z);
                // Fast complete L-route covers the full-depth authored template without a grid search.
                bool reached=false;for(int e=0;e<entrances;e++)
                {var corner=new Vector2(start.x,2);var target=new Vector2(e*width,2);if(Clear(start,corner)&&Clear(corner,target)){reached=true;break;}}
                if(reached)continue;
                var queue=new Queue<Vector2Int>();var visited=new HashSet<Vector2Int>();queue.Enqueue(Vector2Int.zero);visited.Add(Vector2Int.zero);
                while(queue.Count>0&&!reached)
                {
                    var node=queue.Dequeue();var p=start+(Vector2)node*.5f;
                    for(int e=0;e<entrances;e++){var lobby=new Vector2(e*width,2);if(Vector2.Distance(p,lobby)<=.75f&&Clear(p,lobby)){reached=true;break;}}
                    foreach(var step in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                    {var next=node+step;if(visited.Contains(next))continue;var point=start+(Vector2)next*.5f;if(!Clear(p,point))continue;visited.Add(next);queue.Enqueue(next);}
                }
                if(!reached)return false;
            }
            return true;
        }
        public sealed class EntrySlot
        {public string Id;public int Floor;public Vector3 EntryLocal,Inward;}
        public static List<PlacedApartment> PlanFromSlots(ApartmentPrefabLayout layout,int floors,int entrances,int seed,int requestedCount,float sectionWidth,IReadOnlyList<EntrySlot> slots,Func<PlacedApartment,bool> accept=null)
        {
            var errors=Validate(layout);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            if(floors<1||floors>16||entrances<1||entrances>6||requestedCount<1||requestedCount>3||(sectionWidth!=15&&sectionWidth!=21)||slots==null)throw new InvalidOperationException("Некорректные параметры direct-slot планировщика.");
            var candidates=new List<PlacedApartment>();float minX=-sectionWidth*.5f-.15f,maxX=(entrances-1)*sectionWidth+sectionWidth*.5f+.15f;
            bool Crosses(Rect a,Rect b)=>a.xMin<b.xMax-.0001f&&a.xMax>b.xMin+.0001f&&a.yMin<b.yMax-.0001f&&a.yMax>b.yMin+.0001f;
            foreach(var slot in slots.OrderBy(s=>s.Id,StringComparer.Ordinal))
            {
                if(slot.Floor<0||slot.Floor>=Math.Min(floors,3))continue;
                for(int q=0;q<4;q++)
                {
                    var direction=Rotate(new Vector2(layout.inwardDirection.x,layout.inwardDirection.z).normalized,q);
                    if(Vector2.Dot(direction,new Vector2(slot.Inward.x,slot.Inward.z).normalized)<.9999f)continue;
                    var entryTurned=Rotate(new Vector2(layout.entryFloorPosition.x,layout.entryFloorPosition.z),q);
                    var offset=new Vector2(slot.EntryLocal.x,slot.EntryLocal.z)-entryTurned;
                    var regions=layout.footprintRegions.Select(r=>Turn(r,q,offset)).ToArray();
                    if(regions.Any(r=>r.xMin<minX-.0001f||r.xMax>maxX+.0001f||r.yMin<-1.7001f||r.yMax>10.7001f))continue;
                    bool blocked=false;
                    for(int e=0;e<entrances;e++)
                    {
                        float x=e*sectionWidth;
                        var lobby=Rect.MinMaxRect(x-1.5f,-1.3f,x+1.5f,4.5f);var shaft=Rect.MinMaxRect(x-1.5f,4.5f,x+1.5f,8.7f);
                        if(regions.Any(r=>Crosses(r,lobby)||Crosses(r,shaft))){blocked=true;break;}
                    }
                    if(blocked)continue;
                    var p=new PlacedApartment(layout,slot.Floor,q,new Vector3(offset.x,slot.EntryLocal.y-layout.floorY,offset.y),regions,slot.EntryLocal,slot.EntryLocal)
                    {SlotId=slot.Id,ConnectorRegions=Array.Empty<Rect>(),ConnectorPath=Array.Empty<Vector3>()};
                    if(accept==null||accept(p))candidates.Add(p);
                }
            }
            var random=new System.Random(seed);for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var c=candidates[i];candidates[i]=candidates[j];candidates[j]=c;}
            var result=new List<PlacedApartment>();foreach(var p in candidates)
            {
                if(result.Any(a=>a.SlotId==p.SlotId||a.Floor==p.Floor&&a.Regions.Any(r=>p.Regions.Any(o=>Crosses(r,o)))))continue;
                result.Add(p);if(result.Count==requestedCount)break;
            }
            if(result.Count==0)throw new InvalidOperationException("Целая квартира не помещается точно за фиксированными дверями без пересечения фасада/лестницы. Тамбуры, дополнительные стены и уменьшение запрещены.");
            return result;
        }        public static List<PlacedApartment> Plan(ApartmentPrefabLayout layout,int floors,int entrances,int seed,int requestedCount=3,float gridStep=.5f,Func<PlacedApartment,bool> accept=null,float sectionWidth=15f)
        {
            var errors=Validate(layout);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            if(floors<1||floors>16||entrances<1||entrances>6||requestedCount<1||requestedCount>3)throw new InvalidOperationException("Этажи1–16, подъезды1–6, квартир1–3.");
            if(!Finite(gridStep)||(Mathf.Abs(gridStep-.25f)>.0001f&&Mathf.Abs(gridStep-.5f)>.0001f))throw new InvalidOperationException("Шаг входов:0.25 или0.5м.");
            if(!Finite(sectionWidth)||(Mathf.Abs(sectionWidth-15f)>.0001f&&Mathf.Abs(sectionWidth-21f)>.0001f))throw new InvalidOperationException("Ширина секции планировщика:15 или21м.");
            var candidates=Candidates(layout,floors,entrances,gridStep,sectionWidth,accept,true);
            var random=new System.Random(seed);for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var c=candidates[i];candidates[i]=candidates[j];candidates[j]=c;}
            var placed=new List<PlacedApartment>();foreach(var candidate in candidates)
            {
                bool conflicts=placed.Where(p=>p.Floor==candidate.Floor).Any(p=>p.Regions.Any(a=>candidate.Regions.Any(b=>Overlap(a,b)))
                    ||p.Regions.Any(r=>Contains(r,new Vector2(candidate.ApproachPoint.x,candidate.ApproachPoint.z),.18f))
                    ||candidate.Regions.Any(r=>Contains(r,new Vector2(p.ApproachPoint.x,p.ApproachPoint.z),.18f)));
                if(conflicts)continue;
                var floorSet=placed.Where(p=>p.Floor==candidate.Floor).Concat(new[]{candidate}).ToArray();
                if(!ApproachesReachLobby(floorSet,entrances,sectionWidth))continue;
                placed.Add(candidate);if(placed.Count==requestedCount)break;
            }
            if(placed.Count==0)throw new InvalidOperationException("Готовая квартира не помещается без пересечения фасада, лестниц/коридора или собственного подхода. Масштаб и комнаты не изменялись.");
            return placed;
        }
        private static List<PlacedApartment> Candidates(ApartmentPrefabLayout layout,int floors,int entrances,float gridStep,float sectionWidth,Func<PlacedApartment,bool> accept,bool requireLobby)
        {
            var protectedRects=new List<Rect>();for(int e=0;e<entrances;e++)
            {float x=e*sectionWidth;protectedRects.Add(Rect.MinMaxRect(x-1.65f,4.2f,x+1.65f,9));protectedRects.Add(Rect.MinMaxRect(x-1.65f,-1.3f,x+1.65f,4.2f));}
            float minX=-sectionWidth*.5f,maxX=(entrances-1)*sectionWidth+sectionWidth*.5f;const float minZ=-1.5f,maxZ=10.5f;
            var candidates=new List<PlacedApartment>();var inward=new Vector2(layout.inwardDirection.x,layout.inwardDirection.z).normalized;
            int nx=Mathf.RoundToInt((maxX-minX)/gridStep),nz=Mathf.RoundToInt((maxZ-minZ)/gridStep);
            for(int floor=0;floor<Math.Min(floors,3);floor++)for(int q=0;q<4;q++)
            {
                var entryRotated=Rotate(new Vector2(layout.entryFloorPosition.x,layout.entryFloorPosition.z),q);var direction=Rotate(inward,q);
                var rotatedRegions=layout.footprintRegions.Select(r=>Turn(r,q,Vector2.zero)).ToArray();
                // Grid remains useful for variation; exact edge alignments are necessary for templates
                // whose full depth equals the facade span and therefore cannot fit at a rounded grid position.
                var xs=new List<float>();var zs=new List<float>();
                for(int ix=0;ix<=nx;ix++)xs.Add(minX+ix*gridStep-entryRotated.x);
                for(int iz=0;iz<=nz;iz++)zs.Add(minZ+iz*gridStep-entryRotated.y);
                foreach(var region in rotatedRegions)
                {
                    xs.Add(minX-region.xMin);xs.Add(maxX-region.xMax);
                    zs.Add(minZ-region.yMin);zs.Add(maxZ-region.yMax);
                    foreach(var obstacle in protectedRects)
                    {
                        // Five-centimetre clearance at occupied wall/circulation edges, never scale or trim the source.
                        xs.Add(obstacle.xMin-region.xMax-.05f);xs.Add(obstacle.xMax-region.xMin+.05f);
                        zs.Add(obstacle.yMin-region.yMax-.05f);zs.Add(obstacle.yMax-region.yMin+.05f);
                    }
                }
                var uniqueX=xs.Where(Finite).GroupBy(v=>Math.Round(v,5)).Select(g=>g.First()).OrderBy(v=>v).ToArray();
                var uniqueZ=zs.Where(Finite).GroupBy(v=>Math.Round(v,5)).Select(g=>g.First()).OrderBy(v=>v).ToArray();
                foreach(float x in uniqueX)foreach(float z in uniqueZ)
                {
                    var offset=new Vector2(x,z);var entry=offset+entryRotated;
                    var regions=layout.footprintRegions.Select(r=>Turn(r,q,offset)).ToArray();
                    if(regions.Any(r=>r.xMin<minX-.0001f||r.xMax>maxX+.0001f||r.yMin<minZ-.0001f||r.yMax>maxZ+.0001f||protectedRects.Any(o=>Overlap(r,o))))continue;
                    var approach=entry-direction*.8f;
                    if(approach.x<minX||approach.x>maxX||approach.y<minZ||approach.y>maxZ||regions.Any(r=>Contains(r,approach,.18f)))continue;
                    // An entry may meet the circulation zone, but its external approach must not sit in the stair shaft.
                    if(protectedRects.Where((r,index)=>index%2==0).Any(r=>Contains(r,approach,.18f)))continue;
                    var candidate=new PlacedApartment(layout,floor,q,new Vector3(offset.x,floor*3-layout.floorY,offset.y),regions,new Vector3(entry.x,floor*3,entry.y),new Vector3(approach.x,floor*3,approach.y));
                    if((accept==null||accept(candidate))&&(!requireLobby||ApproachesReachLobby(new[]{candidate},entrances,sectionWidth)))candidates.Add(candidate);
                }
            }
            return candidates;
        }
    }
}
