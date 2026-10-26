using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings.Editor
{
    /// <summary>One continuous slab per storey. Shared grid edges, only intentional stair openings.</summary>
    public static class BuildingFloorShell
    {
        public static void Add(GeneratedBuilding building)
        {
            var group=new GameObject("ContinuousFloorSlabs").transform;group.SetParent(building.transform,false);
            var floorMaterial=building.profile.floor.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            for(int f=0;f<building.floors;f++)
            {
                bool openings=f>0&&f<4;
                var go=new GameObject("FloorSlab_"+(f+1));go.transform.SetParent(group,false);go.transform.localPosition=Vector3.up*(f*3);
                var mesh=Mesh(building.entrances,openings,BuildingFootprint.SectionWidth(building));go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials=new[]{floorMaterial,building.profile.ceilingMaterial?building.profile.ceilingMaterial:floorMaterial};
                go.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
        }
        static Mesh Mesh(int entrances,bool openings,float width)
        {
            string folder=BuildingGenerator.Folder+"/ContinuousFloors";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(BuildingGenerator.Folder,"ContinuousFloors");
            string path=folder+"/Slab_v1_E"+entrances+(openings?"_Stairs":"_Solid")+(width==21?"_W21":"")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing)return existing;
            float left=-width/2+.2f,right=(entrances-1)*width+width/2-.2f;
            var holes=new List<Rect>();if(openings)for(int e=0;e<entrances;e++)holes.Add(Rect.MinMaxRect(e*width-1.55f,4.5f,e*width+1.55f,8.7f));
            var xs=new List<float>{left,right};foreach(var h in holes){xs.Add(h.xMin);xs.Add(h.xMax);}xs=xs.Distinct().OrderBy(x=>x).ToList();
            var zs=openings?new[]{-1.3f,4.5f,8.7f,10.3f}:new[]{-1.3f,10.3f};
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var top=new List<int>();var underside=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,bool ceiling=false)
            {
                int first=vertices.Count;var points=new[]{a,b,c,d};
                foreach(var p in points){vertices.Add(p);normals.Add(normal);uv.Add(normal.y==0?new Vector2(p.x+p.z,p.y):new Vector2(p.x/3,p.z/3));}
                var indices=ceiling?underside:top;
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0)indices.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                else indices.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
            }
            for(int ix=0;ix<xs.Count-1;ix++)for(int iz=0;iz<zs.Length-1;iz++)
            {
                float x0=xs[ix],x1=xs[ix+1],z0=zs[iz],z1=zs[iz+1];
                if(holes.Any(h=>h.Contains(new Vector2((x0+x1)*.5f,(z0+z1)*.5f))))continue;
                Quad(new Vector3(x0,0,z0),new Vector3(x0,0,z1),new Vector3(x1,0,z1),new Vector3(x1,0,z0),Vector3.up);
                Quad(new Vector3(x0,-.2f,z0),new Vector3(x0,-.2f,z1),new Vector3(x1,-.2f,z1),new Vector3(x1,-.2f,z0),Vector3.down,true);
            }
            void Edge(Vector2 a,Vector2 b,Vector3 outward)=>Quad(new Vector3(a.x,0,a.y),new Vector3(b.x,0,b.y),new Vector3(b.x,-.2f,b.y),new Vector3(a.x,-.2f,a.y),outward);
            void Rim(Rect rect,bool hole)
            {
                float sign=hole?-1:1;
                Edge(new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),Vector3.back*sign);
                Edge(new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),Vector3.right*sign);
                Edge(new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax),Vector3.forward*sign);
                Edge(new Vector2(rect.xMin,rect.yMax),new Vector2(rect.xMin,rect.yMin),Vector3.left*sign);
            }
            Rim(Rect.MinMaxRect(left,-1.3f,right,10.3f),false);foreach(var h in holes)Rim(h,true);
            var mesh=new Mesh{name="Continuous storey slab"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=2;mesh.SetTriangles(top,0);mesh.SetTriangles(underside,1);mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        public static List<string> Validate(GeneratedBuilding building)
        {
            var errors=new List<string>();var group=building.transform.Find("ContinuousFloorSlabs");
            if(!group||group.childCount!=building.floors){errors.Add("Нет непрерывного перекрытия каждого этажа.");return errors;}
            float width=BuildingFootprint.SectionWidth(building);
            foreach(Transform floor in group)
            {
                var collider=floor.GetComponent<MeshCollider>();if(!collider||!collider.sharedMesh){errors.Add(floor.name+": нет коллизии перекрытия.");continue;}
                int index=Mathf.RoundToInt(floor.localPosition.y/3);
                bool Supported(float x,float z)=>collider.Raycast(new Ray(floor.TransformPoint(new Vector3(x,.3f,z)),Vector3.down),out _,.6f);
                for(int e=0;e<building.entrances;e++)
                {
                    foreach(float side in new[]{-3f,3f})foreach(float z in new[]{4.49f,4.51f,8.69f,8.725f,8.76f})
                        if(!Supported(e*width+side,z))errors.Add(floor.name+": щель у бывшего стыка плит.");
                    if(Supported(e*width,6.6f)!=(index==0||index>=4))errors.Add(floor.name+": неверный проём лестничной шахты.");
                }
                for(int e=0;e<building.entrances-1;e++)foreach(float dx in new[]{-.001f,0,.001f})
                    if(!Supported(e*width+width/2+dx,2))errors.Add(floor.name+": щель между подъездами.");
            }
            return errors;
        }
    }
}
