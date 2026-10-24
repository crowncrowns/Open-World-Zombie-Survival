using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ZombieSurvival.ModularBuildings.Editor
{
    // New derived assets only. Existing building, texture importers and materials are never rewritten.
    public static class DemoAssetSetup
    {
        public const string Library="Assets/ModularBuildings/LibraryV2";
        const string Textures="Assets/Maps/Buildings/Textures/";
        public const string Reference="Assets/Maps/Buildings/Buildings Prefabs/Building_Shk_68_FINISH.prefab";
        public static T Load<T>(string path) where T:Object =>AssetDatabase.LoadAssetAtPath<T>(path);
        public static Material Mat(string folder,string name)=>Load<Material>(Textures+folder+"/"+name+".mat");
        public static void Ensure(BuildingSeriesProfile p)
        {
            System.IO.Directory.CreateDirectory(Library);AssetDatabase.Refresh();
            var reference=Load<GameObject>(Reference);
            if(!reference)throw new InvalidOperationException("Нет здания-референса "+Reference);
            string[] names={"WoodSlats","RedTufted","DarkUpholstery"};
            p.apartmentDoors=new GameObject[3];
            for(int i=0;i<3;i++){
                string folder="ApartmentDoor_"+names[i]+"_01";
                var original=Mat(folder,"M_"+folder+"_URP");
                var material=DerivedMaterial(original,Library+"/Door_"+names[i]+".mat");
                var leaf=reference.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="Door_Leaf"&&r.sharedMaterial==original);
                if(!leaf)throw new InvalidOperationException("Нет дверного образца "+names[i]);
                p.apartmentDoors[i]=DoorVariant(p.door,leaf.transform.parent,material,Library+"/Door_"+names[i]+".prefab");
            }
            p.entranceDoor=DoorVariant(p.door,null,Mat("WeatheredEntranceDoor_01","M_WeatheredEntranceDoor_01_URP"),Library+"/EntranceDoor.prefab");
            foreach(var variant in p.apartmentDoors.Concat(new[]{p.entranceDoor}))UprightDoorUV(variant);
            EntranceHandleMaterial(p.entranceDoor);
            p.glassMaterial=Mat("DirtyHazyGlass_01","M_Glass_DirtyHazy");
            p.roofMaterial=Mat("RustyWindowSill_01","M_Roof_01_URP");
            p.stairWallMaterial=Mat("PeelingGreenStairwellWall_01","M_GreenStairwellWall_World");
            p.ceilingMaterial=Mat("WhiteStairwellCeiling_01","M_WhiteCeiling_World");
            // This is the same small metal cap and duct geometry used on the user's building.
            var cap=reference.transform.Cast<Transform>().First(t=>t.name=="Prism (7)");
            p.ventCap=NormalizedVisual(cap.gameObject,Library+"/MetalVentCap.prefab",new Vector3(1.5f,.18f,2.1f),p.roofMaterial);
            var duct=reference.transform.Cast<Transform>().First(t=>t.name=="Vent&Pipe");
            p.ventDuct=NormalizedVisual(duct.gameObject,Library+"/VentDuct.prefab",new Vector3(1.9f,1.03f,1.15f),null);
            p.roofMeshes=new Mesh[8];
            for(int e=1;e<=8;e++)p.roofMeshes[e-1]=MeshAsset(Library+"/FlatRoof_"+e+"E.asset",()=>Roof(e));
            p.chimneyCurb=MeshAsset(Library+"/ChimneyCurb.asset",()=>Curb(1.6f,2.2f,7.7f));
            p.ductCurb=MeshAsset(Library+"/DuctCurb.asset",()=>Curb(2.1f,1.35f,.8f));
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
        }
        public static void EnsureV3(BuildingSeriesProfile p)
        {
            Ensure(p);const string folder="Assets/ModularBuildings/LibraryV3";
            System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            p.ductCurb=MeshAsset(folder+"/FrontFacingDuctCurb.asset",()=>Curb(1.35f,2.1f,.8f));
            string path=folder+"/ClosedVentDuct.prefab";var existing=Load<GameObject>(path);
            if(existing)p.ventDuct=existing;
            else{
                var scene=EditorSceneManager.NewPreviewScene();
                try{
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(p.ventDuct,scene);
                    var body=go.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Cube");var material=body.sharedMaterial;body.enabled=false;
                    var fill=GameObject.CreatePrimitive(PrimitiveType.Cube);fill.name="ClosedDuctHousing";fill.transform.SetParent(go.transform,false);
                    fill.transform.localPosition=new Vector3(.035f,.515f,0);fill.transform.localScale=new Vector3(1.82f,1.02f,1.13f);fill.GetComponent<MeshRenderer>().sharedMaterial=material;Object.DestroyImmediate(fill.GetComponent<Collider>());
                    var facing=new GameObject("AirOutletDirection").transform;facing.SetParent(go.transform,false);facing.localRotation=Quaternion.Euler(0,-90,0);
                    p.ventDuct=PrefabUtility.SaveAsPrefabAsset(go,path);
                }finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
        }
        public static GameObject ReferenceEntrance()
        {
            var building=Load<GameObject>(Reference);
            var wall=building.transform.Cast<Transform>().First(t=>t.name=="Wall_Door_Metal");
            return wall.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Single_Metal").gameObject;
        }
        public static void EnsureV4(BuildingSeriesProfile p)
        {
            EnsureV3(p);const string folder="Assets/ModularBuildings/LibraryV4";
            System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            p.facadeMaterial=p.solidWall.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            p.approvedWorldFacadeShader=p.facadeMaterial.shader;
            p.interiorWindowLining=Load<GameObject>("Assets/Maps/Buildings/Walls/Prefabs/Outside/Wall_Window_Base.prefab");
            string path=folder+"/ReferenceEntranceDoor.prefab";var existing=Load<GameObject>(path);
            if(existing)p.entranceDoor=existing;
            else{
                var scene=EditorSceneManager.NewPreviewScene();
                try{
                    var holder=new GameObject("ReferenceDoorExtraction");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder,scene);
                    var copy=Object.Instantiate(ReferenceEntrance(),holder.transform,false);copy.name="ReferenceEntranceDoor";
                    copy.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Hinge").localRotation=Quaternion.identity;
                    p.entranceDoor=PrefabUtility.SaveAsPrefabAsset(copy,path);
                }finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
            if(FacadeDetailChecks.DoorSignature(p.entranceDoor)!=FacadeDetailChecks.DoorSignature(ReferenceEntrance()))throw new InvalidOperationException("Производная входная дверь не совпадает с Building_Shk_68_FINISH.");
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
        }
        public static void EnsureWideRoof(BuildingSeriesProfile p)
        {
            const string folder="Assets/ModularBuildings/LibraryV5";
            System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            p.roofMeshes=new Mesh[8];
            for(int e=1;e<=8;e++)p.roofMeshes[e-1]=MeshAsset(folder+"/FlatRoof15m_"+e+"E.asset",()=>Roof(e,p.section.sectionWidth));
            EditorUtility.SetDirty(p);
        }
        static Material DerivedMaterial(Material original,string path)
        {
            if(!original)throw new InvalidOperationException("Нет материала двери.");
            var existing=Load<Material>(path);if(existing)return existing;
            var copy=new Material(original){name=System.IO.Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(copy,path);return copy;
        }
        static void UprightDoorUV(GameObject prefab)
        {
            var leaf=prefab.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Door_Leaf");
            if(leaf.sharedMesh.name=="UprightDoorCube")return;
            var mesh=MeshAsset(Library+"/UprightDoorCube.asset",()=>{
                var copy=Object.Instantiate(leaf.sharedMesh);copy.name="UprightDoorCube";
                copy.uv=copy.uv.Select(uv=>new Vector2(uv.x,1-uv.y)).ToArray();return copy;
            });
            var path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try{root.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Door_Leaf").sharedMesh=mesh;PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void EntranceHandleMaterial(GameObject prefab)
        {
            var material=Mat("DarkDoorHandle_01","M_DarkDoorHandle_01_URP");
            if(prefab.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("EntranceHandle")).All(r=>r.sharedMaterial==material))return;
            var path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("EntranceHandle")))r.sharedMaterial=material;PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static GameObject DoorVariant(GameObject source,Transform referenceHinge,Material material,string path)
        {
            var existing=Load<GameObject>(path);if(existing)return existing;
            var scene=EditorSceneManager.NewPreviewScene();
            try{
                var go=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
                var hinge=go.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Door_Hinge");
                var leaf=hinge.Find("Door_Leaf");hinge.localRotation=Quaternion.identity;leaf.localRotation=Quaternion.identity;
                leaf.GetComponent<MeshRenderer>().sharedMaterial=material;
                foreach(var t in hinge.Cast<Transform>().Where(t=>t.name.StartsWith("Door_Handle")).ToArray())t.gameObject.SetActive(false);
                if(referenceHinge){
                    foreach(var t in referenceHinge.Cast<Transform>().Where(t=>t.name.StartsWith("Door_Handle")&&t.gameObject.activeSelf)){
                        var copy=Object.Instantiate(t.gameObject,hinge,false);copy.name=t.name+"_Reference";
                        copy.transform.localPosition=t.localPosition;copy.transform.localRotation=t.localRotation;copy.transform.localScale=t.localScale;
                        // The visual handle travels with the hinge. Small decorative pieces do not obstruct the opening.
                        foreach(var c in copy.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                    }
                }else{
                    // Measured entrance handle reference: 13 cm vertical pull, 1.055 m above the floor.
                    var handleMaterial=Mat("DarkDoorHandle_01","M_DarkDoorHandle_01_URP");
                    HandleBar(hinge,new Vector3(.755f,1.055f,.055f),handleMaterial);
                    HandleBar(hinge,new Vector3(.755f,1.055f,-.115f),handleMaterial);
                }
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void HandleBar(Transform parent,Vector3 position,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="EntranceHandle";go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=new Vector3(.025f,.13f,.025f);
            go.GetComponent<MeshRenderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());
            foreach(float y in new[]{-.045f,.045f}){
                var stem=GameObject.CreatePrimitive(PrimitiveType.Cube);stem.name="EntranceHandleMount";stem.transform.SetParent(parent,false);
                stem.transform.localPosition=position+new Vector3(0,y,position.z>0?-.02f:.02f);stem.transform.localScale=new Vector3(.02f,.02f,.04f);
                stem.GetComponent<MeshRenderer>().sharedMaterial=material;Object.DestroyImmediate(stem.GetComponent<Collider>());
            }
        }
        static GameObject NormalizedVisual(GameObject source,string path,Vector3 size,Material material)
        {
            var existing=Load<GameObject>(path);if(existing&&existing.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh&&m.sharedMesh.vertexCount>0))return existing;
            var scene=EditorSceneManager.NewPreviewScene();
            try{
                var root=new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var working=Object.Instantiate(source,root.transform,false);
                // ProBuilder stores authored topology in its component; prefab MeshFilter may be empty until rebuilt.
                foreach(var pb in working.GetComponentsInChildren<UnityEngine.ProBuilder.ProBuilderMesh>(true)){pb.ToMesh();pb.Refresh();}
                int index=0;
                foreach(var renderer in working.GetComponentsInChildren<MeshRenderer>(true)){
                    var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                    var child=new GameObject(renderer.name);child.transform.SetParent(root.transform,false);
                    var matrix=working.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                    child.transform.localPosition=matrix.GetColumn(3);child.transform.localRotation=matrix.rotation;child.transform.localScale=matrix.lossyScale;
                    var mesh=filter.sharedMesh;
                    if(!EditorUtility.IsPersistent(mesh))mesh=MeshAsset(Library+"/"+root.name+"_"+(index++)+".asset",()=>Object.Instantiate(filter.sharedMesh));
                    child.AddComponent<MeshFilter>().sharedMesh=mesh;
                    child.AddComponent<MeshRenderer>().sharedMaterials=material?Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray():renderer.sharedMaterials;
                }
                Object.DestroyImmediate(working);
                if(root.GetComponentsInChildren<MeshFilter>().Length==0)throw new InvalidOperationException("Нет геометрии производного пропса "+source.name);
                var bounds=BoundsOf(root);var scale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
                var normalized=new GameObject("Visual").transform;normalized.SetParent(root.transform,false);
                foreach(var t in root.transform.Cast<Transform>().Where(t=>t!=normalized).ToArray())t.SetParent(normalized,false);
                normalized.localScale=scale;normalized.localPosition=-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
                var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,size.y/2,0);collider.size=size;
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        public static Bounds BoundsOf(GameObject root)
        {
            bool first=true;var bounds=new Bounds();
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
                var b=r.localBounds;var m=root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
                for(int i=0;i<8;i++){
                    var p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }return bounds;
        }
        static Mesh MeshAsset(string path,Func<Mesh> create){var m=Load<Mesh>(path);if(m)return m;m=create();AssetDatabase.CreateAsset(m,path);return m;}
        public static float RoofHeight(float z)=>.08f+.02f*(6.2f-Mathf.Abs(z-4.5f));
        static Mesh Roof(int entrances,float sectionWidth=9)
        {
            float x0=-sectionWidth/2-.2f,x1=(entrances-1)*sectionWidth+sectionWidth/2+.2f;
            var mesh=new MeshData();
            foreach(var range in new[]{new Vector2(-1.7f,4.5f),new Vector2(4.5f,10.7f)}){
                var a=new Vector3(x0,RoofHeight(range.x),range.x);var b=new Vector3(x1,RoofHeight(range.x),range.x);
                var c=new Vector3(x1,RoofHeight(range.y),range.y);var d=new Vector3(x0,RoofHeight(range.y),range.y);
                mesh.Quad(a,d,c,b);
            }
            var p0=new Vector3(x0,-.08f,-1.7f);var p1=new Vector3(x1,-.08f,-1.7f);var p2=new Vector3(x1,-.08f,10.7f);var p3=new Vector3(x0,-.08f,10.7f);
            mesh.Quad(p0,p1,p2,p3);
            mesh.Quad(p0,new Vector3(x0,.08f,-1.7f),new Vector3(x1,.08f,-1.7f),p1);
            mesh.Quad(p3,p2,new Vector3(x1,.08f,10.7f),new Vector3(x0,.08f,10.7f));
            foreach(float x in new[]{x0,x1}){
                var a=new Vector3(x,-.08f,-1.7f);var b=new Vector3(x,-.08f,10.7f);
                var c=new Vector3(x,.08f,10.7f);var d=new Vector3(x,RoofHeight(4.5f),4.5f);var e=new Vector3(x,.08f,-1.7f);
                if(x==x0){mesh.Triangle(a,b,c);mesh.Triangle(a,c,d);mesh.Triangle(a,d,e);}else{mesh.Triangle(a,c,b);mesh.Triangle(a,d,c);mesh.Triangle(a,e,d);}
            }
            return mesh.Finish("FlatRoof2Percent_"+entrances+"E");
        }
        static Mesh Curb(float width,float depth,float z)
        {
            float x0=-width/2,x1=width/2,z0=-depth/2,z1=depth/2;
            float y0=RoofHeight(z+z0),y1=RoofHeight(z+z1),top=Mathf.Max(y0,y1)+.18f;
            var m=new MeshData();var a=new Vector3(x0,y0,z0);var b=new Vector3(x1,y0,z0);var c=new Vector3(x1,y1,z1);var d=new Vector3(x0,y1,z1);
            var e=new Vector3(x0,top,z0);var f=new Vector3(x1,top,z0);var g=new Vector3(x1,top,z1);var h=new Vector3(x0,top,z1);
            m.Quad(e,h,g,f);m.Quad(a,b,c,d);m.Quad(a,e,f,b);m.Quad(b,f,g,c);m.Quad(c,g,h,d);m.Quad(d,h,e,a);
            return m.Finish("RoofCurb");
        }
        sealed class MeshData
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector2> uvs=new List<Vector2>();readonly List<int> triangles=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.AddRange(new[]{a,b,c});triangles.AddRange(new[]{n,n+1,n+2});var normal=Vector3.Cross(b-a,c-a).normalized;foreach(var v in new[]{a,b,c})uvs.Add(Mathf.Abs(normal.y)>.5f?new Vector2(v.x,v.z):Mathf.Abs(normal.x)>.5f?new Vector2(v.z,v.y):new Vector2(v.x,v.y));}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
            public Mesh Finish(string name){var m=new Mesh{name=name};m.SetVertices(vertices);m.SetTriangles(triangles,0);m.SetUVs(0,uvs);m.RecalculateNormals();m.RecalculateBounds();return m;}
        }
    }
}
