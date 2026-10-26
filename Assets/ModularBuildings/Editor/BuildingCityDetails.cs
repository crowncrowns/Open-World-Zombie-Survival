using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZombieSurvival.ModularRoofKit;
using Object=UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
    public static class BuildingCityDetails
    {
        const string RoofRoot="Assets/Environment/ModularRoofKit/Prefabs/";
        const string BalconyRoot="Assets/Environment/PostApocalypse/Balconies/Prefabs/";
        public static BuildingSeriesProfile CreateProfile()
        {
            const string path=BuildingGenerator.Folder+"/CityProfileV6.asset";
            var old=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(path);if(old)return old;
            var p=Object.Instantiate(BuildingGenerator.CreateV5Profile());p.name="CityProfileV6";p.profileId="city-v6-15x12";p.extendedCityRules=true;
            p.evidence="Городской профиль V6: 1–16 этажей, 1–6 подъездов. 1–4 скатная крыша; 5+ почти плоская. Интерьеры до 4 этажа, квартиры до 3.";
            AssetDatabase.CreateAsset(p,path);return p;
        }
        static Transform Group(Transform parent,string name,Vector3 position)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        static GameObject Part(string path,Transform parent,Vector3 position,Quaternion rotation)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new InvalidOperationException("Не найден prefab: "+path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent.gameObject.scene);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;return go;
        }
        public static void Add(GeneratedBuilding b)
        {
            if(b.transform.Find("CityDetails_V6"))throw new InvalidOperationException("Правила V6 уже применены; не перестраивайте существующий дом.");
            var root=Group(b.transform,"CityDetails_V6",Vector3.zero);
            StairLimit(b,root);Balconies(b,root);Roof(b,root);Drains(b,root);
        }
        public static List<string> Validate(GeneratedBuilding b)
        {
            var errors=new List<string>();var root=b.transform.Find("CityDetails_V6");
            if(!root){errors.Add("Нет группы правил V6.");return errors;}
            var transforms=b.GetComponentsInChildren<Transform>(true);
            if(transforms.Count(t=>t.name=="StairwellModule")!=Mathf.Min(3,b.floors-1)*b.entrances)errors.Add("Лестницы должны заканчиваться на 4 этаже.");
            var blockers=b.GetComponentsInChildren<BuildingStairBlocker>(true);
            if(blockers.Length!=(b.floors>4?b.entrances:0)||blockers.Any(m=>!m.passageBlocker||!m.passageBlocker.enabled||!m.generatedVisual))errors.Add("Нарушена блокировка 4→5 этаж / модель завала.");
            if(root.Cast<Transform>().Any(t=>t.name.StartsWith("Скатная кровля"))!=(b.floors<5))errors.Add("Неверный тип кровли по этажности.");
            int balconyCount=b.GetComponentsInChildren<PostApocalypse.Balconies.BalconyKitModule>(true).Length;
            if(balconyCount!=(b.floors>5?0:Mathf.Max(1,b.floors-1)*b.entrances*4))errors.Add("Неверное число балконов по этажности.");
            if(transforms.Count(t=>t.name.StartsWith("VentilationStack_E"))!=b.entrances||transforms.Count(t=>t.name.StartsWith("VentilationDuct_E"))!=b.entrances)errors.Add("Нужны вентиляционные шахты и воздуховоды.");
            var outlets=transforms.Where(t=>t.name=="Нижний слив — ОТ дома").ToArray();if(outlets.Length!=4)errors.Add("Нужны четыре трассы водостока.");
            foreach(var end in outlets){var point=Snap(end.gameObject,"Out");var p=b.transform.InverseTransformPoint(point.transform.position);var direction=b.transform.InverseTransformDirection(point.transform.forward);if(p.z<4.5f?direction.z>-.99f:direction.z<.99f)errors.Add("Нижний слив направлен к стене.");}
            return errors;
        }
        static void StairLimit(GeneratedBuilding b,Transform root)
        {
            if(b.floors<=4)return;
            for(int e=0;e<b.entrances;e++)
            {
                // Block only the missing outgoing LEFT flight. A full-shaft box at Y9
                // would hit the head of a player still ascending the incoming RIGHT flight.
                var t=Group(root,"Завал — проход с 4 на 5 этаж — подъезд "+(e+1),new Vector3(e*15-.8f,9,6.6f));
                var marker=t.gameObject.AddComponent<BuildingStairBlocker>();marker.replacementPrefab=b.profile.stairBlockerPrefab;
                var c=t.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,1.15f,0);c.size=new Vector3(1.4f,2.3f,4.2f);marker.passageBlocker=c;
                var visual=Group(t,"Временный мусор — заменить префабом",Vector3.zero);marker.generatedVisual=visual.gameObject;
                for(int i=0;i<8;i++)
                {
                    var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Обломок-заглушка";cube.transform.SetParent(visual,false);
                    cube.transform.localPosition=new Vector3((i%2==0?-.2f:.2f),.25f+(i/2)*.42f,(i%3-1)*.65f);cube.transform.localRotation=Quaternion.Euler(i%3*5,i%3*8,i%2*12);
                    cube.transform.localScale=new Vector3(.7f,.45f,1.6f);cube.GetComponent<MeshRenderer>().sharedMaterial=b.profile.baseTrimMaterial?b.profile.baseTrimMaterial:b.profile.stairWallMaterial;
                    Object.DestroyImmediate(cube.GetComponent<Collider>());
                }
                if(marker.replacementPrefab)BuildingStairBlockerEditor.Apply(marker,false);
            }
        }
        static void Balconies(GeneratedBuilding b,Transform root)
        {
            if(b.floors>5)return;
            var balconyRoot=Group(root,"Балконы — минимум один промежуточный модуль от подъезда",Vector3.zero);
            for(int e=0;e<b.entrances;e++)for(int f=b.floors==1?0:1;f<b.floors;f++)foreach(bool rear in new[]{false,true})foreach(int bay in new[]{0,4})
            {
                var level=b.transform.Find($"Entrance_{e+1}/Floor_{f+1}");string module=(rear?"Rear_":"Front_")+bay;var wall=level.Find(module);
                if(!wall)throw new InvalidOperationException("Нет фасадного модуля для балкона "+module);
                var position=wall.localPosition;var rotation=wall.localRotation;
                Object.DestroyImmediate(wall.gameObject);
                var exit=Part("Assets/Maps/Buildings/Walls/Prefabs/Outside/BalconyExit/Wall_BalconyExit_Interactive.prefab",level,position,rotation);exit.name=module;
                foreach(var r in exit.GetComponentsInChildren<MeshRenderer>(true))
                    if(r.name.IndexOf("wall",StringComparison.OrdinalIgnoreCase)>=0||r.name.IndexOf("plaster",StringComparison.OrdinalIgnoreCase)>=0)r.sharedMaterial=b.profile.facadeMaterial;
                float x=e*15+(bay-2)*3;float z=rear?10.7f:-1.7f;
                var balcony=Part(BalconyRoot+"Balcony_01_Soviet/Balcony_01_Soviet_A.prefab",balconyRoot,new Vector3(x,f*3-.15f,z),Quaternion.Euler(0,rear?0:180,0));
                balcony.name=$"Балкон E{e+1} F{f+1} {(rear?"Rear":"Front")} Bay{bay}";
            }
        }
        static void Roof(GeneratedBuilding b,Transform root)
        {
            if(b.floors>=5)return;
            float left=-8,right=(b.entrances-1)*15+8,front=-2,back=11,centre=4.5f;
            float h=b.floors*3+.18f,rise=Mathf.Tan(b.profile.lowRiseRoofPitch*Mathf.Deg2Rad)*6.5f;
            var points=new[]{new Vector3(left,h,front),new Vector3(right,h,front),new Vector3(left,h+rise,centre),new Vector3(right,h+rise,centre),new Vector3(left,h,back),new Vector3(right,h,back)};
            string folder=BuildingGenerator.Folder+"/CityRoofMeshes";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(BuildingGenerator.Folder,"CityRoofMeshes");
            string path=$"{folder}/Gable_{b.entrances}_{b.floors}_{b.profile.lowRiseRoofPitch.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
                void Tri(int a,int c,int d){int n=vertices.Count;foreach(int k in new[]{a,c,d}){vertices.Add(points[k]);uv.Add(new Vector2(points[k].x/3,points[k].z/3));}indices.AddRange(new[]{n,n+1,n+2});}
                Tri(0,2,1);Tri(1,2,3);Tri(2,4,3);Tri(3,4,5);Tri(0,4,2);Tri(1,3,5);
                mesh=new Mesh{name="V6 pitched roof"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            var go=Group(root,"Скатная кровля "+b.profile.lowRiseRoofPitch+" градусов",Vector3.zero).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=b.profile.roofMaterial;go.AddComponent<MeshCollider>().sharedMesh=mesh;
            var roof=b.transform.Find("Roof_Flat_2Percent");
            foreach(Transform vent in roof)if(vent.name.StartsWith("Ventilation"))
            {var p=vent.localPosition;p.y+=.18f+Mathf.Max(0,6.5f-Mathf.Abs(p.z-centre))*Mathf.Tan(b.profile.lowRiseRoofPitch*Mathf.Deg2Rad);vent.localPosition=p;}
        }
        static RoofSnapPoint Snap(GameObject go,string name)=>go.GetComponentsInChildren<RoofSnapPoint>(true).Single(s=>s.name==name);
        static GameObject PipePart(string name,Transform parent,Vector3 inputPosition,float yaw)
        {
            var go=Part(RoofRoot+"Downpipes/"+name+".prefab",parent,Vector3.zero,Quaternion.Euler(0,yaw,0));
            go.transform.position+=parent.TransformPoint(inputPosition)-Snap(go,"In").transform.position;return go;
        }
        static void Drains(GeneratedBuilding b,Transform root)
        {
            var drains=Group(root,"Водостоки — замкнутые соединения и крепления",Vector3.zero);
            float left=-8,length=b.entrances*15+1,h=b.floors*3+.18f;
            foreach(bool rear in new[]{false,true})
            {
                float gutterZ=rear?11.15f:-2.15f;
                for(int i=0;i<Mathf.RoundToInt(length);i++)
                {
                    bool outlet=i==0||i==Mathf.RoundToInt(length)-1;
                    var gutter=Part(RoofRoot+"Gutters/"+(outlet?"Gutter_Outlet":"Gutter_Straight_1m")+".prefab",drains,new Vector3(left+i,h,gutterZ),Quaternion.identity);
                    Part(RoofRoot+"Gutters/Gutter_Bracket.prefab",drains,new Vector3(left+i+.5f,h,gutterZ),Quaternion.Euler(0,rear?180:0,0));
                    if(!outlet)continue;
                    var point=Snap(gutter,"Downpipe");
                    // Offset goes toward the wall, while the terminal shoe points away from it.
                    var offset=PipePart("Downpipe_Offset",drains,drains.InverseTransformPoint(point.transform.position),rear?180:0);
                    Vector3 current=drains.InverseTransformPoint(Snap(offset,"Out").transform.position);
                    float pipeX=current.x,pipeZ=current.z;
                    while(current.y>.7501f)
                    {
                        float remaining=current.y-.75f;
                        float segment=remaining>=3?3:remaining>=2?2:remaining>=1?1:.5f;
                        var pipe=PipePart("Downpipe_Straight_"+segment.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"m",drains,current,0);
                        if(remaining<segment)pipe.transform.localScale=new Vector3(1,remaining/segment,1);
                        current=drains.InverseTransformPoint(Snap(pipe,"Out").transform.position);
                    }
                    var end=PipePart("Downpipe_BottomDrain",drains,current,rear?0:180);end.name="Нижний слив — ОТ дома";
                    for(float y=1;y<h-.5f;y+=1.5f)
                    {
                        Part(RoofRoot+"Downpipes/Downpipe_Clamp.prefab",drains,new Vector3(pipeX,y,pipeZ),Quaternion.Euler(0,rear?180:0,0));
                        Part(RoofRoot+"Downpipes/Wall_BrickAnchor.prefab",drains,new Vector3(pipeX,y,rear?10.72f:-1.72f),Quaternion.Euler(0,rear?180:0,0));
                    }
                }
                Part(RoofRoot+"Gutters/Gutter_EndCap_Left.prefab",drains,new Vector3(left,h,gutterZ),Quaternion.identity);
                Part(RoofRoot+"Gutters/Gutter_EndCap_Right.prefab",drains,new Vector3(left+length,h,gutterZ),Quaternion.identity);
            }
        }
    }
}
