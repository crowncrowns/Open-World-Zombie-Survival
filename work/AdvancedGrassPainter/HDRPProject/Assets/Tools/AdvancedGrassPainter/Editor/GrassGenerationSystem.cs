using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AdvancedGrassPainter.Editor
{
    public sealed class GrassGenerationSystem
    {
        readonly GrassCollisionManager collisions;
        readonly Dictionary<Vector3Int,List<Vector3>> occupied = new Dictionary<Vector3Int,List<Vector3>>();
        float cell;
        public GrassGenerationSystem(GrassCollisionManager collisions) { this.collisions=collisions; }
        Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/cell),Mathf.FloorToInt(p.y/cell),Mathf.FloorToInt(p.z/cell));
        void Add(Vector3 p){var k=Key(p);if(!occupied.TryGetValue(k,out var list))occupied[k]=list=new List<Vector3>();list.Add(p);}
        bool HasNeighbour(Vector3 p,float distance)
        {
            var k=Key(p);int reach=Mathf.CeilToInt(distance/cell);
            for(int x=-reach;x<=reach;x++)for(int y=-reach;y<=reach;y++)for(int z=-reach;z<=reach;z++)
                if(occupied.TryGetValue(k+new Vector3Int(x,y,z),out var list))foreach(var other in list)if((other-p).sqrMagnitude<distance*distance)return true;
            return false;
        }
        public int Paint(GrassDataStorage data,Vector3 center,GrassSettings s,int sequence,bool refresh=true,Bounds? bounds=null)
        {
            s.Validate();var timer=Stopwatch.StartNew();if(refresh)collisions.Refresh();occupied.Clear();cell=Mathf.Max(.04f,s.spacing);
            foreach(var plant in data.plants)Add(plant.position);
            int before=data.plants.Count;
            int seed=GrassRandomizationSystem.Hash(s.globalSeed,s.brushSeed,sequence);
            var rng=new GrassRandomizationSystem(seed);
            // Random sequential inhibition is a Poisson-disk process; no sample lattice is rendered.
            int attempts=Mathf.CeilToInt(Mathf.PI*s.radius*s.radius/(s.spacing*s.spacing)*7);
            bool progress=!Application.isBatchMode&&attempts>50000;
            bool cancelled=false;
            try { for(int i=0;i<attempts;i++)
            {
                if(progress&&i%2048==0&&EditorUtility.DisplayCancelableProgressBar("Advanced Grass Painter","Распределение и проверка геометрии. Можно отменить операцию.",i/(float)attempts)){cancelled=true;break;}
                float angle=rng.Range(0,Mathf.PI*2),radius=s.radius*Mathf.Sqrt(rng.Value());
                Vector3 point=center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                float probability=rng.Value();
                if(bounds.HasValue&&(point.x<bounds.Value.min.x||point.x>bounds.Value.max.x||point.z<bounds.Value.min.z||point.z>bounds.Value.max.z))continue;
                if(!collisions.Project(point,s,out var hit)||HasNeighbour(hit.point,s.spacing))continue;
                Add(hit.point);
                if(probability>GrassDensityMask.Sample(point,center,s))continue;
                int chunkSeed=GrassRandomizationSystem.Hash(Mathf.FloorToInt(point.x/data.chunkSize),Mathf.FloorToInt(point.z/data.chunkSize),s.globalSeed);
                var p=CreatePlant(hit.point,hit.normal,s,GrassRandomizationSystem.Hash(seed,i,chunkSeed));
                if(rng.Value()>collisions.EdgeWeight(hit.point,hit,s)||!collisions.Accept(p,hit,s)){data.rejectedObstacles++;continue;}
                data.plants.Add(p);
            }} finally {if(progress)EditorUtility.ClearProgressBar();}
            if(cancelled)data.plants.RemoveRange(before,data.plants.Count-before);
            timer.Stop();data.generationMilliseconds=timer.Elapsed.TotalMilliseconds;data.Changed();return cancelled?-1:data.plants.Count-before;
        }
        public static GrassPlant CreatePlant(Vector3 point,Vector3 normal,GrassSettings s,int seed)
        {
            var r=new GrassRandomizationSystem(seed);GrassKind kind=s.kind;
            if(kind==GrassKind.Mixed)kind=(GrassKind)(r.Next()%4);
            float h=kind==GrassKind.Short?.38f:kind==GrassKind.Overgrown?1.65f:kind==GrassKind.Dry?1.1f:1;
            float scale=r.Range(1-s.randomScale*.65f,1+s.randomScale*.65f);
            float dry=kind==GrassKind.Dry?1:Mathf.SmoothStep(.52f,.76f,GrassDensityMask.Fractal(point+Vector3.one*17,s))*r.Range(.7f,1);
            float height=Mathf.Clamp(s.height*h*scale,.02f,2);
            return new GrassPlant{position=point,normal=normal,seed=seed,kind=kind,height=height,width=Mathf.Min(s.width*r.Range(.75f,1.2f),height*.5f),
                yaw=r.Range(0,s.randomRotation),clearance=s.clearance,wind=Mathf.Min(s.windEnvelope,height*.3f),root=s.rootColor*r.Range(.75f,1.15f),tip=Color.Lerp(s.tipColor,s.dryColor,dry)*r.Range(.8f,1.15f)};
        }
        public void Apply(GrassDataStorage data,Vector3 center,GrassSettings s,GrassBrushMode mode,Bounds? bounds=null)
        {
            Undo.RegisterCompleteObjectUndo(data,"Grass "+mode);collisions.Refresh();
            if(mode==GrassBrushMode.Paint||mode==GrassBrushMode.Scatter||mode==GrassBrushMode.Fill)
            {
                var copy=s.Copy();if(mode==GrassBrushMode.Scatter){copy.density*=.3f;copy.edgeFalloff=1;}
                int sequence=data.nextStroke++;if(Paint(data,center,copy,sequence,false,bounds)<0){data.nextStroke--;return;}data.strokes.Add(new GrassStroke{center=center,settings=copy,sequence=sequence,bounded=bounds.HasValue,bounds=bounds.GetValueOrDefault()});
            }
            else if(mode==GrassBrushMode.Erase)
            {
                // Destructive edits become the new baseline; do not resurrect erased plants on regeneration.
                data.plants.RemoveAll(p=>Inside(p.position,center,s.radius));data.strokes.Clear();
            }
            else if(mode==GrassBrushMode.Replace)
            {
                for(int i=data.plants.Count-1;i>=0;i--)
                {
                    var old=data.plants[i];if(old.kind!=s.replaceFrom||!Inside(old.position,center,s.radius))continue;
                    var p=CreatePlant(old.position,old.normal,s,old.seed);
                    if(collisions.Project(old.position,s,out var hit)&&collisions.Accept(p,hit,s))data.plants[i]=p;
                }
                data.strokes.Clear();
            }
            else if(mode==GrassBrushMode.CollisionCleanup) Cleanup(data,s,new Bounds(center,Vector3.one*s.radius*2));
            else if(mode==GrassBrushMode.Smooth)
            {
                // Remove crowding first, then refill gaps with a broad, weak mask. Positions remain on their surfaces.
                occupied.Clear();cell=Mathf.Max(.04f,s.spacing);var keep=new List<GrassPlant>();
                foreach(var p in data.plants){if(Inside(p.position,center,s.radius)&&HasNeighbour(p.position,s.spacing*1.15f))continue;keep.Add(p);Add(p.position);}
                data.plants=keep;var smooth=s.Copy();smooth.spacing*=1.15f;smooth.clumping*=.25f;
                Paint(data,center,smooth,data.nextStroke++,false);data.strokes.Clear();
            }
            data.Changed();EditorUtility.SetDirty(data);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(data.gameObject.scene);
        }
        public int Cleanup(GrassDataStorage data,GrassSettings s,Bounds? region=null)
        {
            collisions.Refresh();int before=data.plants.Count;
            data.plants.RemoveAll(p=>
            {
                if(region.HasValue&&!region.Value.Contains(p.position))return false;
                if(!collisions.Project(p.position,s,out var hit)||(hit.point-p.position).sqrMagnitude>.0009f)return true;
                return !collisions.Accept(p,hit,s);
            });
            if(data.plants.Count!=before)data.strokes.Clear();data.Changed();return before-data.plants.Count;
        }
        public void Regenerate(GrassDataStorage data,GrassSettings current,Bounds? region=null)
        {
            Undo.RegisterCompleteObjectUndo(data,"Regenerate grass");collisions.Refresh();
            if(data.strokes.Count>0 && !region.HasValue)
            {
                data.plants.Clear();foreach(var stroke in data.strokes){var s=stroke.settings.Copy();if(!current.lockSeed)s.globalSeed=current.globalSeed;Paint(data,stroke.center,s,stroke.sequence,false,stroke.bounded?stroke.bounds:(Bounds?)null);}
            }
            else
            {
                for(int i=data.plants.Count-1;i>=0;i--)
                {
                    var p=data.plants[i];if(region.HasValue&&!region.Value.Contains(p.position))continue;
                    var settings=current.Copy();settings.kind=p.kind;
                    int seed=current.lockSeed?p.seed:GrassRandomizationSystem.Hash(p.seed,current.globalSeed);
                    p=CreatePlant(p.position,p.normal,settings,seed);
                    if(collisions.Project(p.position,settings,out var hit)&&collisions.Accept(p,hit,settings))data.plants[i]=p;else data.plants.RemoveAt(i);
                }
            }
            data.Changed();EditorUtility.SetDirty(data);
        }
        static bool Inside(Vector3 p,Vector3 center,float r)=>new Vector2(p.x-center.x,p.z-center.z).sqrMagnitude<r*r&&Mathf.Abs(p.y-center.y)<r+2;
    }
}
