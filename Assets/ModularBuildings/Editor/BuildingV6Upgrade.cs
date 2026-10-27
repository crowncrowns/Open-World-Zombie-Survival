#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace ZombieSurvival.ModularBuildings.Editor
{
    /// <summary>Explicit generated-house upgrade. Manual houses, Terrain and source assets are not rebuilt.</summary>
    public static class BuildingV6Upgrade
    {
        public sealed class Row
        {
            public GeneratedBuilding building;public string oldId,newId;public bool rebuild;public string reason;
            internal string stamp;internal readonly Dictionary<string,Material> wallMaterials=new Dictionary<string,Material>();
            internal readonly Dictionary<string,Material[]> rendererMaterials=new Dictionary<string,Material[]>();
            internal readonly List<Transform> owned=new List<Transform>();
            public readonly List<string> preservedChildren=new List<string>();
        }
        public sealed class UpgradePlan
        {
            public Scene scene;public BuildingSeriesProfile profile;public readonly List<Row> rows=new List<Row>();public readonly List<string> errors=new List<string>();
            public string ToJson()=>Newtonsoft.Json.JsonConvert.SerializeObject(new{scene=scene.path,errors,rows=rows.Select(r=>new{root=r.building?r.building.name:null,r.oldId,r.newId,r.rebuild,r.reason,ownedChildren=r.owned.Select(t=>t.name),preservedChildren=r.preservedChildren,mappedWallModules=r.wallMaterials.Count,mappedRenderers=r.rendererMaterials.Count})},Newtonsoft.Json.Formatting.Indented);
        }
        sealed class Stage : IDisposable
        {
            public Scene scene;public GeneratedBuilding building;public Row row;
            public void Dispose(){if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
        }
        static bool Known(Transform t)=>t.name.StartsWith("Entrance_",StringComparison.Ordinal)&&int.TryParse(t.name.Substring(9),out _) || t.name=="TerrainFoundation"||t.name=="FacadeCornerClosures"||t.name=="BaseTrim"||t.name=="EntranceAccess"||t.name=="ContinuousFloorSlabs"||t.name=="CityDetails_V6"||t.name=="Roof_Flat_2Percent";
        static string RelativePath(Transform t,Transform root){var parts=new List<string>();while(t&&t!=root){parts.Add(t.name);t=t.parent;}parts.Reverse();return string.Join("/",parts);}
        static string RootStamp(GeneratedBuilding b)=>b.GetEntityId()+"|"+b.name+"|"+b.transform.position.ToString("R")+"|"+b.transform.rotation.ToString("R")+"|"+b.transform.localScale.ToString("R")+"|"+EditorJsonUtility.ToJson(b);
        static string StableSuffix(Object obj){string text=GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").Substring(0,12).ToLowerInvariant();}
        static bool Gameplay(Component c)=>c && (c.GetType().Name=="SaveIdentity"||c.GetType().Name=="WorldSaveObject"||c.GetType().Name=="WorldItem"||c.GetType().Name=="ActorIdentity"||c.GetType().Name=="Health"||c.GetType().Name=="EnemyBrain"||c.GetType().Name=="Trader"||c.GetType().Name=="PlayerInteractor"||c.GetType().Name=="ItemStorage");
        static string ModuleKey(Transform module,GeneratedBuilding b)=>RelativePath(module,b.transform);
        static void CaptureMaterials(Row row,List<string> errors)
        {
            var b=row.building;var wallSet=new HashSet<MeshRenderer>(BuildingExteriorMaterials.Walls(b));
            foreach(var floor in BuildingExteriorMaterials.Floors(b))foreach(Transform module in floor)
            {
                if(!(module.name.StartsWith("Front_",StringComparison.Ordinal)||module.name.StartsWith("Rear_",StringComparison.Ordinal)||module.name=="LeftEnd"||module.name=="RightEnd"))continue;
                var rs=module.GetComponentsInChildren<MeshRenderer>(true).Where(wallSet.Contains).ToArray();
                // End walls may deliberately vary per Window_n / Solid_n module.
                if(module.name=="LeftEnd"||module.name=="RightEnd")
                {foreach(Transform part in module)Capture(part);}
                else Capture(module);
                void Capture(Transform t)
                {
                    var mats=t.GetComponentsInChildren<MeshRenderer>(true).Where(wallSet.Contains).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                    if(mats.Length>1)errors.Add(b.name+": неоднозначный фасадный материал "+ModuleKey(t,b));
                    else if(mats.Length==1&&mats[0])row.wallMaterials[ModuleKey(t,b)]=mats[0];
                }
            }
            foreach(var r in b.GetComponentsInChildren<MeshRenderer>(true))if(row.owned.Any(t=>r.transform==t||r.transform.IsChildOf(t)))row.rendererMaterials[RelativePath(r.transform,b.transform)]=r.sharedMaterials.ToArray();
        }
        public static UpgradePlan Plan(Scene scene)
        {
            var plan=new UpgradePlan{scene=scene,profile=AssetDatabase.LoadAssetAtPath<BuildingSeriesProfile>(BuildingGenerator.Folder+"/CityProfileV6.asset")};
            if(Application.isPlaying||!scene.IsValid()||!scene.isLoaded||EditorSceneManager.IsPreviewScene(scene)||PrefabStageUtility.GetCurrentPrefabStage()!=null){plan.errors.Add("Нужна обычная загруженная сцена в Edit.");return plan;}
            if(!plan.profile||!plan.profile.extendedCityRules){plan.errors.Add("Нет исходного CityProfileV6.");return plan;}
            var buildings=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GeneratedBuilding>(true)).ToArray();var used=new HashSet<string>(StringComparer.Ordinal);
            foreach(var b in buildings)
            {
                if(b.name.IndexOf("Kurs",StringComparison.OrdinalIgnoreCase)>=0||b.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name=="EmptyBuildings")){plan.errors.Add("Неожиданный GeneratedBuilding в защищённой группе: "+b.name);continue;}
                var apartments=b.GetComponentsInChildren<GeneratedApartment>(true);bool v5=b.profile&&!b.profile.extendedCityRules;bool legacy=apartments.Any(a=>!a.prefabInstance||!a.sourcePrefab);
                var row=new Row{building=b,oldId=b.buildingId,newId=b.buildingId,rebuild=v5||legacy,reason=v5?"V5 to full V6":legacy?"Legacy malformed apartments replaced by explicit request":"Healthy V6 in place"};
                if(string.IsNullOrWhiteSpace(row.newId)){plan.errors.Add(b.name+": пустой ID.");continue;}
                if(!used.Add(row.newId)){row.newId=row.oldId+"-copy-"+StableSuffix(b);if(!used.Add(row.newId))plan.errors.Add(b.name+": конфликт стабильного ID.");if(!row.rebuild)plan.errors.Add(b.name+": дублированный ID здорового V6 требует отдельного согласования квартирных ID.");}
                if(!b.profile){plan.errors.Add(b.name+": нет профиля.");continue;}
                if(Vector3.Distance(b.transform.lossyScale,Vector3.one)>.001f||Vector3.Angle(b.transform.up,Vector3.up)>.01f)plan.errors.Add(b.name+": неподдерживаемый масштаб/наклон.");
                foreach(Transform child in b.transform)if(Known(child))row.owned.Add(child);else row.preservedChildren.Add(child.name);
                if(row.rebuild)
                {
                    foreach(var c in b.GetComponentsInChildren<Component>(true))if(!c)plan.errors.Add(b.name+": missing component.");else if(Gameplay(c))plan.errors.Add(b.name+": gameplay/SaveIdentity запрещает полную замену: "+c.GetType().Name);
                    foreach(var manual in b.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="ManualContent"&&t.childCount>0))
                    {
                        bool recognized=legacy&&manual.GetComponentsInChildren<GeneratedApartment>(true).Length>0;
                        if(!recognized)plan.errors.Add(b.name+": непустой пользовательский ManualContent "+RelativePath(manual,b.transform));
                    }
                    if(v5&&apartments.Length>0)plan.errors.Add(b.name+": V5 содержит квартиры, автоматическая замена запрещена.");
                    foreach(var c in row.owned.SelectMany(t=>t.GetComponentsInChildren<MonoBehaviour>(true)))if(c&&c.GetType().Namespace!=null&&!c.GetType().Namespace.StartsWith("ZombieSurvival",StringComparison.Ordinal))plan.errors.Add(b.name+": неизвестный script в заменяемой геометрии "+c.GetType().FullName);
                }
                CaptureMaterials(row,plan.errors);row.stamp=RootStamp(b);plan.rows.Add(row);
            }
            return plan;
        }
        static void RestoreMaterials(Row row,GeneratedBuilding target)
        {
            var wallSet=new HashSet<MeshRenderer>(BuildingExteriorMaterials.Walls(target));
            foreach(var pair in row.wallMaterials)
            {
                var module=target.transform.Find(pair.Key);
                if(!module && (pair.Key.Contains("/LeftEnd/")||pair.Key.Contains("/RightEnd/")))
                {
                    // V5 end cap was a single mesh; V6 splits the same end into window/solid modules.
                    int cut=pair.Key.IndexOf("/LeftEnd/",StringComparison.Ordinal);if(cut<0)cut=pair.Key.IndexOf("/RightEnd/",StringComparison.Ordinal);
                    string endPath=pair.Key.Substring(0,pair.Key.IndexOf('/',cut+1));
                    var endMaterials=row.wallMaterials.Where(x=>x.Key.StartsWith(endPath+"/",StringComparison.Ordinal)).Select(x=>x.Value).Distinct().ToArray();
                    if(endMaterials.Length==1)module=target.transform.Find(endPath);
                }
                if(!module)throw new InvalidOperationException("Не найден новый фасадный модуль "+pair.Key);
                foreach(var r in module.GetComponentsInChildren<MeshRenderer>(true).Where(wallSet.Contains))r.sharedMaterials=Enumerable.Repeat(pair.Value,r.sharedMaterials.Length).ToArray();
            }
            foreach(var r in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Semantic wall mapping above wins even if the new module has a different layout.
                if(wallSet.Contains(r))continue;
                if(row.rendererMaterials.TryGetValue(RelativePath(r.transform,target.transform),out var mats)&&mats.Length==r.sharedMaterials.Length)r.sharedMaterials=mats.ToArray();
            }
            foreach(string groupName in new[]{"TerrainFoundation","FacadeCornerClosures","BaseTrim"})
            {
                var group=target.transform.Find(groupName);if(!group)continue;
                foreach(var r in group.GetComponentsInChildren<MeshRenderer>(true))if(row.rendererMaterials.TryGetValue(RelativePath(r.transform,target.transform),out var mats)&&mats.Length==r.sharedMaterials.Length)r.sharedMaterials=mats.ToArray();
            }
        }
        static void CopyBuilding(GeneratedBuilding from,GeneratedBuilding to)
        {
            to.buildingId=from.buildingId;to.profile=from.profile;to.year=from.year;to.floors=from.floors;to.entrances=from.entrances;to.generatedSectionWidth=from.generatedSectionWidth;to.revision=from.revision;to.endWindowRuleVersion=from.endWindowRuleVersion;to.exteriorEnvelopeVersion=from.exteriorEnvelopeVersion;to.entranceAccessRuleVersion=from.entranceAccessRuleVersion;to.baseTrimMaterialOverride=from.baseTrimMaterialOverride;to.exteriorMaterialOverride=from.exteriorMaterialOverride;to.exteriorIncludesEntranceDoors=from.exteriorIncludesEntranceDoors;to.originalExteriorDoorMaterials=from.originalExteriorDoorMaterials;
        }
        static void RemapRootReferences(GameObject child,GeneratedBuilding staging,GeneratedBuilding original)
        {
            foreach(var c in child.GetComponentsInChildren<Component>(true))
            {
                if(!c)continue;var so=new SerializedObject(c);var p=so.GetIterator();bool changed=false;
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.propertyPath!="m_GameObject")
                {
                    var value=p.objectReferenceValue;Object replacement=value==staging?original:value==staging.gameObject?original.gameObject:value==staging.transform?original.transform:null;
                    if(replacement){p.objectReferenceValue=replacement;changed=true;}
                }
                if(changed)so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        public static string Apply(UpgradePlan plan)
        {
            if(plan==null||plan.errors.Count>0)throw new InvalidOperationException(plan==null?"Нет плана.":string.Join("\n",plan.errors));
            if(Application.isPlaying||!plan.scene.isLoaded||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Нужен Edit.");
            foreach(var row in plan.rows)if(!row.building||RootStamp(row.building)!=row.stamp)throw new InvalidOperationException("План устарел: "+row.oldId);
            var stages=new List<Stage>();var notes=new List<string>();int undo=-1;
            // Protect everything outside replaced subtrees, allowing only the known managed
            // healthy-V6 operations below. The caller additionally owns the full scene backup.
            var protectedRoots=plan.scene.GetRootGameObjects().Where(g=>!plan.rows.Any(r=>g==r.building.gameObject||r.building.transform.IsChildOf(g.transform))).ToArray();
            string Pose(Transform t)=>(t.parent?t.parent.GetEntityId().ToString():"root")+"|"+t.localPosition.ToString("R")+"|"+t.localRotation.ToString("R")+"|"+t.localScale.ToString("R")+"|"+t.gameObject.activeSelf;
            var protectedPoses=protectedRoots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t,t=>Pose(t));
            var protectedMats=protectedRoots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToDictionary(r=>r,r=>r.sharedMaterials.ToArray());
            var rootPoses=plan.rows.ToDictionary(r=>r.building,r=>Pose(r.building.transform));
            try
            {
                foreach(var row in plan.rows.Where(r=>r.rebuild))
                {
                    var stage=new Stage{scene=EditorSceneManager.NewPreviewScene(),row=row};stages.Add(stage);var old=row.building;
                    stage.building=BuildingGenerator.Build(plan.profile,old.year,old.floors,old.entrances,row.newId,stage.scene,false,old.exteriorMaterialOverride,old.exteriorIncludesEntranceDoors,old.baseTrimMaterialOverride);
                    RestoreMaterials(row,stage.building);
                    var errors=BuildingGenerator.Validate(stage.building);if(errors.Count>0)throw new InvalidOperationException(old.name+" staging Validate: "+string.Join(";",errors.Take(30)));
                    if(stage.building.GetComponentsInChildren<GeneratedApartment>(true).Length<1)throw new InvalidOperationException("Нет квартир в stage.");notes.Add(old.name+": staging FULL PASS");
                }
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Обновить готовые GeneratedBuilding до V6");
                foreach(var stage in stages)
                {
                    var row=stage.row;var old=row.building;var fresh=stage.building;Undo.RecordObject(old,"Метаданные V6");
                    var oldAccess=old.GetComponentInChildren<GeneratedEntranceAccess>(true);var newAccess=fresh.GetComponentInChildren<GeneratedEntranceAccess>(true);
                    if(oldAccess&&newAccess){newAccess.terrain=oldAccess.terrain;newAccess.autoFit=newAccess.uniformGeometry?false:oldAccess.autoFit;}
                    foreach(var child in row.owned)Undo.DestroyObjectImmediate(child.gameObject);
                    var children=fresh.transform.Cast<Transform>().ToArray();
                    foreach(var child in children)
                    {
                        var pos=child.localPosition;var rotation=child.localRotation;var scale=child.localScale;child.SetParent(null,false);SceneManager.MoveGameObjectToScene(child.gameObject,plan.scene);child.SetParent(old.transform,false);child.localPosition=pos;child.localRotation=rotation;child.localScale=scale;
                        RemapRootReferences(child.gameObject,fresh,old);Undo.RegisterCreatedObjectUndo(child.gameObject,"Новая геометрия V6");
                    }
                    CopyBuilding(fresh,old);EditorUtility.SetDirty(old);PrefabUtility.RecordPrefabInstancePropertyModifications(old);
                    var lot=old.GetComponent<ZombieSurvival.WorldAuthoring.CityGeneratedLot>();if(lot&&lot.buildingId==row.oldId&&row.newId!=row.oldId){Undo.RecordObject(lot,"Уникальный ID дома");lot.buildingId=row.newId;EditorUtility.SetDirty(lot);PrefabUtility.RecordPrefabInstancePropertyModifications(lot);}
                }
                foreach(var row in plan.rows.Where(r=>!r.rebuild))
                {
                    var b=row.building;
                    var doorObjects=b.GetComponentsInChildren<Component>(true).Where(c=>c is Transform || c is ZombieSurvival.Interaction.DoorInteractable).ToArray();Undo.RecordObjects(doorObjects.Cast<Object>().ToArray(),"Открытые входы V6");
                    typeof(BuildingApartments).GetMethod("ConfigureEntranceAccess",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{b});
                    BuildingApartmentDoorCleanup.Apply(b,true);BuildingInteriorLining.Apply(b,true);BuildingPublicFloorFinish.Apply(b,true);
                }
                Physics.SyncTransforms();
                foreach(var row in plan.rows)
                {
                    if(Pose(row.building.transform)!=rootPoses[row.building])throw new InvalidOperationException("Изменён корень дома.");
                    var errors=BuildingGenerator.Validate(row.building);if(errors.Count>0)throw new InvalidOperationException(row.building.name+" final Validate: "+string.Join(";",errors.Take(30)));
                    if(row.building.buildingId!=row.newId)throw new InvalidOperationException("ID не соответствует плану.");
                }
                if(plan.rows.Select(r=>r.building.buildingId).Distinct().Count()!=plan.rows.Count)throw new InvalidOperationException("Повтор ID после обновления.");
                foreach(var p in protectedPoses)if(!p.Key||Pose(p.Key)!=p.Value)throw new InvalidOperationException("Изменён защищённый объект.");
                foreach(var p in protectedMats)if(!p.Key||!p.Key.sharedMaterials.SequenceEqual(p.Value))throw new InvalidOperationException("Изменён материал вне GeneratedBuilding.");
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(plan.scene);
                return Newtonsoft.Json.JsonConvert.SerializeObject(new{pass=true,saved=false,total=plan.rows.Count,replaced=stages.Count,inPlace=plan.rows.Count-stages.Count,protectedTransforms=protectedPoses.Count,protectedRenderers=protectedMats.Count,notes},Newtonsoft.Json.Formatting.Indented);
            }
            catch{if(undo>=0)Undo.RevertAllDownToGroup(undo);throw;}
            finally{foreach(var stage in stages)stage.Dispose();}
        }
    }
}
#endif
