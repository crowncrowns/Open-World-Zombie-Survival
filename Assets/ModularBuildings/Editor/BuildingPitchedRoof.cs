using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
    // New-build geometry only. Never changes a shared source mesh or an existing scene building.
    public static class BuildingPitchedRoof
    {
        const string CornicePath="Assets/Maps/Buildings/Textures/SpalledCorniceBlocks_01/M_SpalledCorniceBlocks_World.mat";
        public static float Height(GeneratedBuilding b,float z)=>b.floors*3+.18f+Mathf.Max(0,6.5f-Mathf.Abs(z-4.5f))*Mathf.Tan(b.profile.lowRiseRoofPitch*Mathf.Deg2Rad);
        static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
        static string Key(GeneratedBuilding b)=>$"E{b.entrances}_F{b.floors}_W{BuildingFootprint.SectionWidth(b):0}_P{b.profile.lowRiseRoofPitch.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}";
        static Mesh Cached(string name,Func<Mesh> build)
        {
            string folder=BuildingGenerator.Folder+"/CityRoofMeshes";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(BuildingGenerator.Folder,"CityRoofMeshes");
            string path=folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old)return old;
            var mesh=build();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh Mesh(Vector3[] points,int[] faces)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int i=0;i<faces.Length;i+=3)
            {
                var a=points[faces[i]];var c=points[faces[i+1]];var d=points[faces[i+2]];
                var n=Vector3.Cross(c-a,d-a).normalized;
                foreach(var p in new[]{a,c,d}){tri.Add(v.Count);v.Add(p);uv.Add(Mathf.Abs(n.y)>.5f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.5f?new Vector2(p.z,p.y):new Vector2(p.x,p.y));}
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        static Mesh SafetyRail(float endRise)=>Cached("RoofSafetyRail_Rectangular_v2_Rise"+endRise.ToString("F6",System.Globalization.CultureInfo.InvariantCulture),()=>
        {
            var points=new List<Vector3>();var faces=new List<int>();
            void Box(float x0,float x1,float y0,float y1,float z0,float z1,bool sloped=false)
            {
                int n=points.Count;points.AddRange(new[]{new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),new Vector3(x1,y0,z1),new Vector3(x0,y0,z1),new Vector3(x0,y1,z0),new Vector3(x1,y1,z0),new Vector3(x1,y1,z1),new Vector3(x0,y1,z1)});
                if(sloped)for(int i=n;i<points.Count;i++){var p=points[i];p.y+=p.x*endRise;points[i]=p;}
                faces.AddRange(new[]{0,4,1,1,4,5,1,5,2,2,5,6,2,6,3,3,6,7,3,7,0,0,7,4,4,7,5,5,7,6,0,1,3,1,2,3}.Select(i=>i+n));
            }
            // One post belongs to the start of each segment; the next segment closes its end.
            Box(-.02f,.02f,-.02f,.44f,-.02f,.02f);Box(0,1,.4f,.44f,-.02f,.02f,true);Box(0,1,.19f,.23f,-.02f,.02f,true);Box(-.055f,.055f,-.015f,.005f,-.055f,.055f);
            return Mesh(points.ToArray(),faces.ToArray());
        });
        public static void Add(GeneratedBuilding b,Transform root)
        {
            if(b.floors>=5)return;
            float width=BuildingFootprint.SectionWidth(b),left=-width/2-.5f,right=(b.entrances-1)*width+width/2+.5f;
            float h=Height(b,-2),peak=Height(b,4.5f);
            var mesh=Cached("PitchedClosed_v2_"+Key(b),()=>Mesh(new[]{new Vector3(left,h,-2),new Vector3(right,h,-2),new Vector3(left,peak,4.5f),new Vector3(right,peak,4.5f),new Vector3(left,h,11),new Vector3(right,h,11)},new[]{0,2,1,1,2,3,2,4,3,3,4,5,0,4,2,1,3,5,0,1,4,1,5,4}));
            var roof=Group(root,"Скатная кровля "+b.profile.lowRiseRoofPitch+" градусов");roof.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;roof.gameObject.AddComponent<MeshRenderer>().sharedMaterial=b.profile.roofMaterial;roof.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
            var material=AssetDatabase.LoadAssetAtPath<Material>(CornicePath);if(!material)throw new InvalidOperationException("Не найден M_SpalledCorniceBlocks_World");
            var cornice=Group(root,"Карниз скатной крыши — замкнутый периметр");
            void Rim(string name,Vector3 position,Vector3 size)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(cornice,false);box.transform.localPosition=position;box.transform.localScale=size;box.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            float y=b.floors*3+.06f;
            Rim("Front",new Vector3((left+right)/2,y,-1.77f),new Vector3(right-left+.06f,.32f,.58f));
            Rim("Rear",new Vector3((left+right)/2,y,10.77f),new Vector3(right-left+.06f,.32f,.58f));
            Rim("Left",new Vector3(left+.26f,y,4.5f),new Vector3(.58f,.32f,11.96f));
            Rim("Right",new Vector3(right-.26f,y,4.5f),new Vector3(.58f,.32f,11.96f));
            SupportVents(b);
            var fences=Group(root,"Ограждение скатной крыши — опоры на кровле");
            var railMaterial=b.profile.entranceRailMaterial?b.profile.entranceRailMaterial:b.profile.roofMaterial;
            void Edge(Vector2 a,Vector2 c)
            {
                int count=Mathf.CeilToInt(Vector2.Distance(a,c));
                for(int i=0;i<count;i++)
                {
                    var p=Vector2.Lerp(a,c,(float)i/count);var q=Vector2.Lerp(a,c,(float)(i+1)/count);
                    var start=new Vector3(p.x,Height(b,p.y)-.025f,p.y);var end=new Vector3(q.x,Height(b,q.y)-.025f,q.y);var delta=end-start;
                    var railMesh=SafetyRail(delta.y);var part=Group(fences,"RoofSafetyRail_"+fences.childCount).gameObject;part.AddComponent<MeshFilter>().sharedMesh=railMesh;part.AddComponent<MeshRenderer>().sharedMaterial=railMaterial;part.AddComponent<MeshCollider>().sharedMesh=railMesh;part.transform.localPosition=start;
                    // Posts stay vertical. Only the bars follow the slope, so both runs join at the ridge.
                    var horizontal=new Vector3(delta.x,0,delta.z);part.transform.localRotation=Quaternion.LookRotation(Vector3.Cross(horizontal.normalized,Vector3.up),Vector3.up);
                    part.transform.localScale=new Vector3(horizontal.magnitude,1,1);
                }
            }
            float x0=left+.35f,x1=right-.35f,z0=-1.65f,z1=10.65f;
            Edge(new Vector2(x0,z0),new Vector2(x1,z0));Edge(new Vector2(x1,z0),new Vector2(x1,4.5f));Edge(new Vector2(x1,4.5f),new Vector2(x1,z1));
            Edge(new Vector2(x1,z1),new Vector2(x0,z1));Edge(new Vector2(x0,z1),new Vector2(x0,4.5f));Edge(new Vector2(x0,4.5f),new Vector2(x0,z0));
        }
        static void SupportVents(GeneratedBuilding b)
        {
            var oldRoof=b.transform.Find("Roof_Flat_2Percent");
            foreach(Transform vent in oldRoof)
            {
                if(!vent.name.StartsWith("Ventilation",StringComparison.Ordinal))continue;
                var curb=vent.Find("FlashedCurb");var old=curb.GetComponent<MeshFilter>().sharedMesh.bounds;var p=vent.localPosition;
                float highest=Mathf.Max(Height(b,p.z+old.min.z),Height(b,p.z+old.max.z));
                p.y=highest+.12f-old.max.y-oldRoof.localPosition.y;vent.localPosition=p;
                var points=new Vector3[8];
                for(int i=0;i<4;i++)
                {
                    float x=i==0||i==3?old.min.x:old.max.x,z=i<2?old.min.z:old.max.z;
                    points[i]=new Vector3(x,Height(b,p.z+z)-.04f-oldRoof.localPosition.y-p.y,z);points[i+4]=new Vector3(x,old.max.y,z);
                }
                var mesh=Cached("PitchedCurb_v2_"+Key(b)+"_"+vent.name,()=>Mesh(points,new[]{0,4,1,1,4,5,1,5,2,2,5,6,2,6,3,3,6,7,3,7,0,0,7,4,4,7,5,5,7,6,0,1,3,1,2,3}));
                curb.GetComponent<MeshFilter>().sharedMesh=mesh;curb.GetComponent<MeshCollider>().sharedMesh=mesh;
            }
        }
        public static List<string> Validate(GeneratedBuilding b)
        {
            var errors=new List<string>();var root=b.transform.Find("CityDetails_V6");var cornice=root.Find("Карниз скатной крыши — замкнутый периметр");var fence=root.Find("Ограждение скатной крыши — опоры на кровле");
            if(b.floors>=5){if(cornice||fence)errors.Add("Карниз/ограждение скатной крыши добавлены к плоской кровле.");return errors;}
            if(!cornice||cornice.childCount!=4)errors.Add("Нужен замкнутый карниз из четырёх сторон.");
            else foreach(var r in cornice.GetComponentsInChildren<MeshRenderer>())if(AssetDatabase.GetAssetPath(r.sharedMaterial)!=CornicePath)errors.Add("Неверный материал карниза.");
            if(!fence||fence.childCount<4)errors.Add("Нет ограждения по периметру скатной крыши.");
            else foreach(Transform rail in fence)
            {
                var p=rail.localPosition;if(Mathf.Abs(p.y-(Height(b,p.z)-.025f))>.002f)errors.Add("Опора ограждения не на крыше: "+rail.name);
                var vertices=rail.GetComponent<MeshFilter>().sharedMesh.vertices;var end=new Vector3(1,vertices.Where(v=>v.x>.999f).Max(v=>v.y),0);var endpoint=b.transform.InverseTransformPoint(rail.TransformPoint(end));
                if(Mathf.Abs(endpoint.y-(Height(b,endpoint.z)+.415f))>.003f)errors.Add("Перекладина ограждения не совпадает с концом соседней секции: "+rail.name);
            }
            foreach(Transform vent in b.transform.Find("Roof_Flat_2Percent"))
            {
                if(!vent.name.StartsWith("Ventilation",StringComparison.Ordinal))continue;var curb=vent.Find("FlashedCurb");var mesh=curb.GetComponent<MeshFilter>().sharedMesh;
                foreach(var p in mesh.vertices.Where(v=>v.y<mesh.bounds.max.y-.01f)){var point=b.transform.InverseTransformPoint(curb.TransformPoint(p));if(Mathf.Abs(point.y-(Height(b,point.z)-.04f))>.003f){errors.Add("Вентиляция не примыкает к скату: "+vent.name);break;}}
            }
            return errors;
        }
    }
}
