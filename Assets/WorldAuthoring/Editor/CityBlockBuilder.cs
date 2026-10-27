using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZombieSurvival.ModularBuildings;
using ZombieSurvival.ModularBuildings.Editor;
using Object=UnityEngine.Object;

namespace ZombieSurvival.WorldAuthoring.Editor
{
    public sealed class CityLotPreview : IDisposable
    {
        public Scene Scene { get; private set; }
        public GameObject Root { get; private set; }
        internal CityLotPreview(Scene scene,GameObject root){Scene=scene;Root=root;}
        public void Dispose(){if(Scene.IsValid())EditorSceneManager.ClosePreviewScene(Scene);Scene=default;Root=null;}
    }
    /// <summary>Explicit one-lot authoring. Validate is read-only; profile Build may create the existing immutable derived-mesh cache.</summary>
    public static class CityBlockBuilder
    {
        public static string BuildingId(CityBlockPlan plan,CityLot lot)=>"city-"+plan.planId+"-"+lot.id;
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        private static bool Finite(Vector2 v)=>Finite(v.x)&&Finite(v.y);
        private static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        private static string Label(CityLot lot,int index)=>"Участок "+(index+1)+" «"+(lot?.title??"null")+"»";
        private static IEnumerable<T> Loaded<T>() where T:Component
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(!scene.IsValid()||!scene.isLoaded||EditorSceneManager.IsPreviewScene(scene))continue;
                foreach(var root in scene.GetRootGameObjects())foreach(var item in root.GetComponentsInChildren<T>(true))yield return item;
            }
        }
        public static Vector2 PlannedFootprint(CityBuildingEntry entry,CityLot lot)=>entry.kind==CityBuildingKind.Profile
            ?new Vector2(BuildingFootprint.SectionWidth(entry.profile,lot.entrances)*lot.entrances,entry.profile.section.sectionDepth):entry.prefabFootprint;
        public static List<string> Validate(CityBlockPlan plan,Scene target)
        {
            var errors=new List<string>();
            if(EditorApplication.isPlayingOrWillChangePlaymode)errors.Add("Авторинг разрешён только в Edit Mode.");
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null)errors.Add("Закройте Prefab Stage перед авторингом квартала.");
            if(!target.IsValid()||!target.isLoaded||EditorSceneManager.IsPreviewScene(target))errors.Add("Нужна загруженная обычная целевая сцена, не Preview.");
            if(plan==null){errors.Add("Не назначен план.");return errors;}
            if(string.IsNullOrWhiteSpace(plan.planId))errors.Add("Пустой постоянный ID плана.");
            if(plan.catalog==null||plan.catalog.entries==null){errors.Add("Не назначен каталог или список типов.");return errors;}
            if(plan.lots==null){errors.Add("Нет списка участков.");return errors;}
            var entries=new Dictionary<string,CityBuildingEntry>(StringComparer.Ordinal);
            foreach(var e in plan.catalog.entries)
            {
                if(e==null){errors.Add("В каталоге есть пустая запись.");continue;}
                if(string.IsNullOrWhiteSpace(e.key)||entries.ContainsKey(e.key)){errors.Add("Пустой или повторный ключ каталога: "+e.key);continue;}
                entries.Add(e.key,e);
                if(e.kind==CityBuildingKind.Profile)
                {
                    if(e.profile==null||!EditorUtility.IsPersistent(e.profile))errors.Add("Тип «"+e.key+"»: нужен сохранённый BuildingSeriesProfile asset.");
                }
                else if(e.kind==CityBuildingKind.Prefab)
                {
                    if(e.prefab==null||!EditorUtility.IsPersistent(e.prefab)||!PrefabUtility.IsPartOfPrefabAsset(e.prefab)||!AssetDatabase.IsMainAsset(e.prefab))
                        errors.Add("Тип «"+e.key+"»: нужен корень prefab asset из Project, не сценовый объект.");
                    if(e.prefab!=null)
                    {
                        if(e.prefab.GetComponentsInChildren<CityGeneratedLot>(true).Length>0)errors.Add("Тип «"+e.key+"»: prefab содержит маркер участка; выберите исходную статическую геометрию.");
                        var gameplay=new HashSet<string>(new[]{"SaveIdentity","WorldSaveObject","WorldItem","Trader","EnemyBrain"},StringComparer.Ordinal);
                        foreach(var component in e.prefab.GetComponentsInChildren<MonoBehaviour>(true))
                            if(component!=null&&gameplay.Contains(component.GetType().Name))errors.Add("Тип «"+e.key+"»: игровой компонент "+component.GetType().Name+" не поддержан. Этот этап создаёт только статические здания, не NPC/лут/сохраняемые объекты.");
                        foreach(var child in e.prefab.GetComponentsInChildren<Transform>(true))
                            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)>0)errors.Add("Тип «"+e.key+"»: Missing Script в prefab на «"+child.name+"».");
                    }
                    if(e.prefab!=null&&e.prefab.GetComponentsInChildren<GeneratedBuilding>(true).Length>0)errors.Add("Тип «"+e.key+"»: prefab содержит GeneratedBuilding; используйте профиль для уникальных авторских ID.");
                    if(!Finite(e.prefabFootprint)||e.prefabFootprint.x<=0||e.prefabFootprint.y<=0||!Finite(e.prefabPivotOffset))errors.Add("Тип «"+e.key+"»: недопустимые размер корпуса/смещение pivot.");
                }
                else errors.Add("Тип «"+e.key+"»: неизвестный способ создания.");
            }
            var active=new List<CityLot>();var ids=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<plan.lots.Count;i++)
            {
                var lot=plan.lots[i];string label=Label(lot,i);
                if(lot==null){errors.Add(label+": пустая запись.");continue;}
                if(!lot.enabled)continue;
                if(string.IsNullOrWhiteSpace(lot.id)||!ids.Add(lot.id))errors.Add(label+": пустой или повторный ID.");
                if(!Finite(lot.position)||!Finite(lot.yaw)||!Finite(lot.size)||lot.size.x<=0||lot.size.y<=0)errors.Add(label+": нужны конечная поза и положительный размер.");
                if(!entries.TryGetValue(lot.entryKey??"",out var entry)){errors.Add(label+": тип не найден: "+lot.entryKey);continue;}
                if(entry.kind==CityBuildingKind.Profile)
                {
                    try{BuildingGenerator.Preflight(entry.profile,lot.year,lot.floors,lot.entrances);}
                    catch(Exception e){errors.Add(label+": "+e.Message);continue;}
                }
                if(entry.kind==CityBuildingKind.Profile&&entry.profile!=null&&entry.profile.section!=null||entry.kind==CityBuildingKind.Prefab)
                {
                    var minimum=PlannedFootprint(entry,lot);
                    if(Finite(minimum)&&(lot.size.x<minimum.x||lot.size.y<minimum.y))errors.Add(label+": участок меньше корпуса "+minimum+" м; нужен дополнительный запас для полной геометрии.");
                }
                active.Add(lot);
            }
            for(int i=0;i<active.Count;i++)for(int j=i+1;j<active.Count;j++)
                if(Finite(active[i].position)&&Finite(active[j].position)&&Finite(active[i].yaw)&&Finite(active[j].yaw)&&Finite(active[i].size)&&Finite(active[j].size)&&Overlap(active[i],active[j]))
                    errors.Add("Участки «"+active[i].title+"» и «"+active[j].title+"» пересекаются в XZ.");
            // Invalid input must report immediately without scanning a large city hierarchy.
            if(errors.Count>0)return errors;
            var markers=Loaded<CityGeneratedLot>().ToArray();var buildings=Loaded<GeneratedBuilding>().ToArray();
            // A detailed house may contain thousands of renderers. Measure once per validation,
            // rather than rescanning every existing house for every proposed lot.
            var rectangles=new Dictionary<GeneratedBuilding,CityLot>();
            foreach(var building in buildings)
                if(TryExistingRectangle(building,out var rectangle))rectangles.Add(building,rectangle);
            foreach(var lot in active)
            {
                string id=BuildingId(plan,lot);
                var owners=markers.Where(m=>m.planId==plan.planId&&m.lotId==lot.id).ToArray();
                CityGeneratedLot owner=owners.Length==1?owners[0]:null;
                if(owners.Length>1)errors.Add("Участок «"+lot.title+"»: несколько уже размещённых экземпляров.");
                if(owner!=null)
                {
                    if(owner.buildingId!=id||owner.transform.parent!=null||owner.transform.localScale!=Vector3.one)errors.Add("Участок «"+lot.title+"»: нарушен контракт маркера/корня размещённого здания.");
                    var expected=entries[lot.entryKey];var children=owner.GetComponentsInChildren<GeneratedBuilding>(true);
                    if(expected.kind==CityBuildingKind.Profile&&(children.Length!=1||children[0].buildingId!=id))errors.Add("Участок «"+lot.title+"»: маркер не соответствует единственному GeneratedBuilding.");
                }
                foreach(var marker in markers.Where(m=>m.buildingId==id&&m!=owner))errors.Add("Занят buildingId другим маркером: "+id+" в "+marker.gameObject.scene.name);
                foreach(var existing in buildings)
                {
                    if(owner!=null&&existing.GetComponentInParent<CityGeneratedLot>()==owner)continue;
                    if(!rectangles.TryGetValue(existing,out var rect))continue;
                    if(Overlap(lot,rect))errors.Add("Участок «"+lot.title+"» пересекает консервативный XZ bounds существующего дома «"+existing.name+"» ("+existing.gameObject.scene.name+"). Исходный дом не перемещается.");
                }
                foreach(var b in buildings.Where(b=>b.buildingId==id))
                    if(owner==null||b.GetComponentInParent<CityGeneratedLot>()!=owner)errors.Add("Занят buildingId посторонним зданием: "+id+" в "+b.gameObject.scene.name);
            }
            return errors;
        }
        private static bool TryExistingRectangle(GeneratedBuilding building,out CityLot rectangle)
        {
            bool found=false;Bounds bounds=default;
            foreach(var renderer in building.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                var local=renderer.localBounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var point=renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z)));
                    if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                }
            }
            rectangle=found?new CityLot{position=bounds.center,size=new Vector2(bounds.size.x,bounds.size.z),yaw=0}:null;
            return found;
        }
        private static bool Overlap(CityLot a,CityLot b)
        {
            Vector2 Axis(float yaw)=>new Vector2(Mathf.Cos(yaw*Mathf.Deg2Rad),-Mathf.Sin(yaw*Mathf.Deg2Rad));
            var ax=Axis(a.yaw);var az=new Vector2(-ax.y,ax.x);var bx=Axis(b.yaw);var bz=new Vector2(-bx.y,bx.x);
            var delta=new Vector2(b.position.x-a.position.x,b.position.z-a.position.z);
            foreach(var n in new[]{ax,az,bx,bz})
            {
                float ra=Mathf.Abs(Vector2.Dot(n,ax))*a.size.x*.5f+Mathf.Abs(Vector2.Dot(n,az))*a.size.y*.5f;
                float rb=Mathf.Abs(Vector2.Dot(n,bx))*b.size.x*.5f+Mathf.Abs(Vector2.Dot(n,bz))*b.size.y*.5f;
                if(Mathf.Abs(Vector2.Dot(delta,n))>=ra+rb)return false; // Exact touching is allowed.
            }
            return true;
        }
        private static CityLot Selected(CityBlockPlan plan,int index,Scene target)
        {
            var errors=Validate(plan,target);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            if(index<0||index>=plan.lots.Count||!plan.lots[index].enabled)throw new InvalidOperationException("Выберите включённый участок.");
            return plan.lots[index];
        }
        public static CityLotPreview PreviewLot(CityBlockPlan plan,int index,Scene targetScene)
        {
            var lot=Selected(plan,index,targetScene);var scene=EditorSceneManager.NewPreviewScene();
            try{return new CityLotPreview(scene,Build(plan,lot,scene));}
            catch{EditorSceneManager.ClosePreviewScene(scene);throw;}
        }
        public static GameObject GenerateSelected(CityBlockPlan plan,int index,Scene targetScene)
        {
            var lot=Selected(plan,index,targetScene);
            if(Loaded<CityGeneratedLot>().Any(m=>m.planId==plan.planId&&m.lotId==lot.id))throw new InvalidOperationException("Этот участок уже размещён. Замена/удаление существующего здания запрещены.");
            using(var staged=PreviewLot(plan,index,targetScene))
            {
                var root=staged.Root;
                // Source assets remain unchanged; only the successfully bounded new root enters the scene.
                SceneManager.MoveGameObjectToScene(root,targetScene);
                try{Undo.RegisterCreatedObjectUndo(root,"Создать здание квартала");EditorSceneManager.MarkSceneDirty(targetScene);return root;}
                catch{Object.DestroyImmediate(root);throw;}
            }
        }
        private static GameObject Build(CityBlockPlan plan,CityLot lot,Scene scene)
        {
            var entry=plan.catalog.entries.Single(e=>e.key==lot.entryKey);
            var wrapper=new GameObject("CityLot_"+lot.title);SceneManager.MoveGameObjectToScene(wrapper,scene);
            try
            {
                var marker=wrapper.AddComponent<CityGeneratedLot>();marker.planId=plan.planId;marker.lotId=lot.id;marker.buildingId=BuildingId(plan,lot);
                GameObject content;Vector2 center;
                if(entry.kind==CityBuildingKind.Profile)
                {
                    content=BuildingGenerator.Build(entry.profile,lot.year,lot.floors,lot.entrances,marker.buildingId,scene).gameObject;
                    if(entry.profile.extendedCityRules)
                    {
                        var geometryErrors=BuildingGenerator.Validate(content.GetComponent<GeneratedBuilding>());
                        if(geometryErrors.Count>0){Object.DestroyImmediate(content);throw new InvalidOperationException(string.Join("\n",geometryErrors));}
                    }
                    center=new Vector2((lot.entrances-1)*BuildingFootprint.SectionWidth(entry.profile,lot.entrances)*.5f,4.5f);
                }
                else
                {
                    content=(GameObject)PrefabUtility.InstantiatePrefab(entry.prefab,scene);content.transform.SetParent(wrapper.transform,false);center=entry.prefabPivotOffset;
                    // A baked modular building must use Profile, otherwise its internal authoring IDs would be copied unchanged.
                    if(content.GetComponentsInChildren<GeneratedBuilding>(true).Length>0)throw new InvalidOperationException("Prefab содержит GeneratedBuilding. Используйте профиль генератора для новых авторских ID.");
                }
                content.transform.SetParent(wrapper.transform,false);content.transform.localPosition=new Vector3(-center.x,0,-center.y);
                wrapper.transform.SetPositionAndRotation(lot.position,Quaternion.Euler(0,lot.yaw,0));wrapper.transform.localScale=Vector3.one;
                CheckBounds(wrapper,lot);return wrapper;
            }
            catch{Object.DestroyImmediate(wrapper);throw;}
        }
        private static void CheckBounds(GameObject wrapper,CityLot lot)
        {
            bool found=false;float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minZ=float.PositiveInfinity,maxZ=float.NegativeInfinity;
            void Add(Transform space,Bounds b)
            {
                found=true;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var p=wrapper.transform.InverseTransformPoint(space.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                    if(!Finite(p))throw new InvalidOperationException("Геометрия содержит неконечные координаты.");
                    minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);
                }
            }
            foreach(var r in wrapper.GetComponentsInChildren<Renderer>(true))if(r.enabled&&r.gameObject.activeInHierarchy)Add(r.transform,r.localBounds);
            foreach(var c in wrapper.GetComponentsInChildren<Collider>(true))
            {
                if(!c.enabled||!c.gameObject.activeInHierarchy||c.isTrigger)continue;
                if(c is BoxCollider box)Add(c.transform,new Bounds(box.center,box.size));
                else if(c is MeshCollider mesh&&mesh.sharedMesh!=null)Add(c.transform,mesh.sharedMesh.bounds);
                else if(c is SphereCollider sphere)Add(c.transform,new Bounds(sphere.center,Vector3.one*sphere.radius*2));
                else if(c is CapsuleCollider capsule){var size=Vector3.one*capsule.radius*2;size[capsule.direction]=Mathf.Max(capsule.height,capsule.radius*2);Add(c.transform,new Bounds(capsule.center,size));}
                else throw new InvalidOperationException("Неподдерживаемый физический объём: "+c.GetType().Name+". Нужна явная геометрия участка.");
            }
            if(!found)throw new InvalidOperationException("У здания нет видимой геометрии/коллайдеров для проверки участка.");
            const float tolerance=.001f;
            if(minX<-lot.size.x*.5f-tolerance||maxX>lot.size.x*.5f+tolerance||minZ<-lot.size.y*.5f-tolerance||maxZ>lot.size.y*.5f+tolerance)
                throw new InvalidOperationException("Полная геометрия выходит за участок «"+lot.title+"»: X["+minX.ToString("F2")+";"+maxX.ToString("F2")+"] Z["+minZ.ToString("F2")+";"+maxZ.ToString("F2")+"], участок "+lot.size+" м. Увеличьте запас/уточните pivot; существующие объекты не изменены.");
        }
    }
}
