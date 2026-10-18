using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieSurvival.Combat;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Weapons;
using ZombieSurvival.Progression;

namespace ZombieSurvival.Persistence
{
    /// <summary>One synchronous scene-item transaction: validate + stage before any live state change.
    /// Does not reset enemies/doors or implement economy. Unknown schemas/catalogs fail closed.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonController),typeof(PlayerNeeds))]
    [RequireComponent(typeof(ItemStorage),typeof(PlayerStamina))]
    public sealed class PlayerSaveSystem : MonoBehaviour
    {
        [SerializeField, Tooltip("Имя одного файла в Application.persistentDataPath. Без папок; запись через временный файл и резервную .bak.")]
        private string fileName="character-stage11.json";
        [SerializeField, Tooltip("Каталог типов предметов этого сохранения. ID уникальны; добавляя тип, обновите каталог перед сборкой игры.")]
        private ItemDefinition[] itemCatalog;
        [SerializeField, Tooltip("Клавиша ручного сохранения. Доступно живым на земле, вне замаха, перезарядки и паркура.")]
        private InputAction saveInput=new InputAction("QuickSave",InputActionType.Button,"<Keyboard>/f5");
        [SerializeField, Tooltip("Клавиша загрузки. Работает также после смерти; незавершённые действия отменяются после проверки файла.")]
        private InputAction loadInput=new InputAction("QuickLoad",InputActionType.Button,"<Keyboard>/f9");
        [SerializeField, Tooltip("Принимать горячие клавиши только при фокусе приложения. API консолей не зависит от этого параметра.")]
        private bool requireApplicationFocus=true;
        [SerializeField, Tooltip("Слои стен, земли и актёров для проверки точки загрузки. Собственный персонаж и заменяемая добыча исключаются.")]
        private LayerMask obstructionMask=~0;
        [SerializeField, Min(.05f), Tooltip("Максимальный зазор до опоры под сохранённой позой, м. Слишком далёкая земля отменяет загрузку.")]
        private float supportDistance=.35f;
        [SerializeField, Tooltip("Показывать F5/F9 и результат операции на HUD.")]
        private bool showHud=true;
        private InputAction save,load;
        private FirstPersonController motor;
        private Health health;
        private PlayerNeeds needs;
        private bool working;
        public string Status { get; private set; }="F5 — сохранить · F9 — загрузить";
        public string SavePath => Path.Combine(Application.persistentDataPath,fileName);
        private void Awake() { motor=GetComponent<FirstPersonController>();health=GetComponent<Health>();needs=GetComponent<PlayerNeeds>(); save=saveInput.Clone();load=loadInput.Clone(); }
        private void OnEnable(){save?.Enable();load?.Enable();}
        private void OnDisable(){save?.Disable();load?.Disable();}
        private void OnDestroy(){save?.Dispose();load?.Dispose();}
        private void Update()
        {
            if(Time.timeScale<=0 || requireApplicationFocus && !Application.isFocused)return;
            if(save!=null && save.WasPressedThisFrame()) TrySave();
            else if(load!=null && load.WasPressedThisFrame()) TryLoad();
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        private static bool PositionValid(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z)&&v.sqrMagnitude<1e12f;
        private static string Hash(string s)
        { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-",""); }
        private void CheckContext()
        {
            if(!isActiveAndEnabled || !Application.isPlaying)throw new InvalidOperationException("Сохранение работает только в Play.");
            if(string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName)!=fileName)throw new InvalidOperationException("В имени сохранения запрещены пути.");
            // Intentional integration boundary: item rollback with a live unsaved wallet enables duplication.
            // The economy developer must add an atomic wallet/trader block + schema migration before enabling this.
            if(GetComponents<MonoBehaviour>().Any(c=>c!=null && c.GetType().FullName=="ZombieSurvival.Economy.Wallet"))
                throw new InvalidOperationException("Нужна интеграция Wallet в общий формат сохранения. См. Docs/CharacterSurvivalAndSaving.md.");
        }
        private Dictionary<string,ItemDefinition> Catalog()
        {
            var result=new Dictionary<string,ItemDefinition>(StringComparer.Ordinal);
            if(itemCatalog==null)throw new InvalidOperationException("Не назначен каталог предметов.");
            foreach(var item in itemCatalog)
                if(item==null || string.IsNullOrWhiteSpace(item.Id) || !result.TryAdd(item.Id,item))throw new InvalidOperationException("Пустой или повторяющийся ID в каталоге предметов.");
            return result;
        }
        private Dictionary<string,ItemStorage> Storages()
        {
            var result=new Dictionary<string,ItemStorage>(StringComparer.Ordinal);
            foreach(var storage in FindObjectsByType<ItemStorage>(FindObjectsInactive.Include).Where(s=>s.gameObject.scene==gameObject.scene))
            {
                var id=storage.GetComponent<SaveIdentity>();
                if(id==null || string.IsNullOrWhiteSpace(id.Id) || !result.TryAdd(id.Id,storage))throw new InvalidOperationException("Хранилищу нужен уникальный SaveIdentity: "+storage.name);
            }
            return result;
        }
        private WorldItem[] WorldItems()=>FindObjectsByType<WorldItem>(FindObjectsInactive.Include).Where(w=>w.gameObject.scene==gameObject.scene && w.Quantity>0).ToArray();
        private static SavedItem Capture(ItemStack s)=>s==null || s.IsEmpty ? new SavedItem() : new SavedItem{occupied=true,typeId=s.Definition.Id,instanceId=s.InstanceId,quantity=s.Quantity,durability=s.Durability,loadedRounds=s.LoadedRounds};
        private static ItemStack Decode(SavedItem s,Dictionary<string,ItemDefinition> catalog,HashSet<string> ids)
        {
            if(s==null || !s.occupied)return null;
            if(string.IsNullOrWhiteSpace(s.instanceId)||!ids.Add(s.instanceId)||!catalog.TryGetValue(s.typeId??"",out var item))throw new InvalidOperationException("Неизвестный тип или повтор ID предмета.");
            if(s.quantity<1 || s.quantity>item.MaxStack || !Finite(s.durability) || s.durability<0 || s.durability>item.MaximumDurability || s.loadedRounds<0
                || item.Weapon==null && s.loadedRounds!=0 || item.Weapon!=null && s.loadedRounds>item.Weapon.MagazineSize)
                throw new InvalidOperationException("Некорректное состояние предмета: "+s.typeId);
            return ItemStack.RestoreSaved(item,s.quantity,s.instanceId,s.durability,s.loadedRounds);
        }
        public bool TrySave()
        {
            if(working)return false; working=true;
            try
            {
                CheckContext(); var weapons=GetComponent<PlayerWeapons>();
                if(!motor.IsAlive || health.IsDead || !motor.IsGrounded || motor.Parkour!=null && motor.Parkour.OwnsMovement || weapons!=null && (weapons.IsAttacking || weapons.IsReloading))
                    throw new InvalidOperationException("Сохранение: встаньте на землю и завершите действие.");
                var data=new CharacterSaveData{scene=gameObject.scene.path,savedUtc=DateTime.UtcNow.ToString("O"),position=transform.position,yaw=transform.eulerAngles.y,pitch=motor.SavedViewPitch,
                    health=health.Current,stamina=motor.Stamina.Current,recoveryWait=motor.Stamina.RecoveryWait,exhausted=motor.Stamina.IsExhausted,food=needs.Food,water=needs.Water,
                    prone=motor.IsProne,crouched=motor.IsCrouching,equippedId=weapons!=null && weapons.Equipped!=null?weapons.Equipped.InstanceId:""};
                data.progression=GetComponent<PlayerProgression>()?.CaptureState();
                var hotbar=GetComponent<PlayerHotbar>();data.hotbar=hotbar?.CaptureAssignments();data.hotbarSelected=hotbar!=null?hotbar.SelectedIndex:0;
                foreach(var pair in Storages())
                {
                    var row=new SavedStorage{id=pair.Key};for(int i=0;i<pair.Value.SlotCount;i++)row.slots.Add(Capture(pair.Value.GetSlot(i)));data.storages.Add(row);
                }
                foreach(var w in WorldItems())data.world.Add(new SavedWorldItem{item=Capture(w.StateForSave()),position=w.transform.position,scale=w.transform.lossyScale,rotation=w.transform.rotation,active=w.gameObject.activeInHierarchy,enabled=w.enabled});
                ValidateData(data,Catalog(),Storages());
                var payload=JsonUtility.ToJson(data);var envelope=JsonUtility.ToJson(new SaveEnvelope{payload=payload,sha256=Hash(payload)},true);
                Directory.CreateDirectory(Application.persistentDataPath);string temp=SavePath+".tmp";
                File.WriteAllText(temp,envelope,Encoding.UTF8);
                if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
                Status="Сохранено · F9 — загрузить";return true;
            }
            catch(Exception e){Status="Не сохранено: "+e.Message;return false;}
            finally{working=false;}
        }
        private void ValidateData(CharacterSaveData data,Dictionary<string,ItemDefinition> catalog,Dictionary<string,ItemStorage> stores)
        {
            var prepared=PrepareProgression(data);
            float savedHealthMaximum=prepared!=null?Mathf.Max(1,prepared.Evaluate(StatId.HealthMaximum,health.BaseMaximum)):health.Maximum;
            float savedStaminaMaximum=prepared!=null?Mathf.Max(1,prepared.Evaluate(StatId.StaminaCapacity,motor.Stamina.BaseCapacity)):motor.Stamina.Capacity;
            if(data==null || data.version!=2 || data.scene!=gameObject.scene.path || !PositionValid(data.position) || !Finite(data.yaw)||!Finite(data.pitch)
                || !Finite(data.health)||data.health<=0||data.health>savedHealthMaximum || !Finite(data.stamina)||data.stamina<0||data.stamina>savedStaminaMaximum
                || !Finite(data.recoveryWait)||data.recoveryWait<0||data.recoveryWait>3600 || !Finite(data.food)||data.food<0||data.food>needs.FoodCapacity
                || !Finite(data.water)||data.water<0||data.water>needs.WaterCapacity || data.storages==null||data.world==null||data.world.Count>10000)
                throw new InvalidOperationException("Несовместимая сцена, версия или показатели сохранения.");
            var ids=new HashSet<string>();var storageIds=new HashSet<string>();
            foreach(var row in data.storages)
            {
                if(row==null || row.id==null || !storageIds.Add(row.id)||!stores.ContainsKey(row.id)||row.slots==null||row.slots.Count<1||row.slots.Count>4096)throw new InvalidOperationException("Набор хранилищ изменился или повреждён.");
                foreach(var item in row.slots)Decode(item,catalog,ids);
            }
            if(storageIds.Count!=stores.Count)throw new InvalidOperationException("Набор хранилищ сцены изменился; требуется миграция сохранения.");
            var hotbar=GetComponent<PlayerHotbar>();
            if(hotbar!=null)
            {
                string ownId=stores.FirstOrDefault(p=>p.Value==GetComponent<ItemStorage>()).Key;
                var owned=new HashSet<string>(data.storages.First(r=>r.id==ownId).slots.Where(s=>s!=null&&s.occupied).Select(s=>s.instanceId));
                if(!PlayerHotbar.ValidateSavedAssignments(data.hotbar,data.hotbarSelected,hotbar.SlotCount,owned,out var hotbarError))throw new InvalidOperationException(hotbarError);
            }
            else if(data.hotbar!=null&&data.hotbar.Any(id=>!string.IsNullOrEmpty(id)))throw new InvalidOperationException("Для этого файла нужен компонент хотбара.");
            foreach(var w in data.world)
            {
                if(w==null || w.item==null || !w.item.occupied || !PositionValid(w.position)||!PositionValid(w.scale)||w.scale.x<=0||w.scale.y<=0||w.scale.z<=0||w.scale.x>100||w.scale.y>100||w.scale.z>100||!Finite(w.rotation.x)||!Finite(w.rotation.y)||!Finite(w.rotation.z)||!Finite(w.rotation.w)
                    || Mathf.Abs(w.rotation.x*w.rotation.x+w.rotation.y*w.rotation.y+w.rotation.z*w.rotation.z+w.rotation.w*w.rotation.w-1)>.01f)
                    throw new InvalidOperationException("Повреждена позиция добычи.");
                var item=Decode(w.item,catalog,ids);
                if(item.Definition.WorldPrefab==null || item.Definition.WorldPrefab.GetComponent<WorldItem>()==null)throw new InvalidOperationException("Нет префаба добычи: "+item.Definition.Id);
            }
            if(!string.IsNullOrEmpty(data.equippedId))
            {
                var own=stores.FirstOrDefault(p=>p.Value==GetComponent<ItemStorage>()).Key;
                if(!data.storages.Any(r=>r.id==own && r.slots.Any(s=>s!=null && s.occupied && s.instanceId==data.equippedId && catalog[s.typeId].Weapon!=null)))throw new InvalidOperationException("Оружие не принадлежит игроку.");
            }
        }
        // Подготовка не изменяет живые ранги/пулы. Проверять HP нужно против СОХРАНЁННЫХ бонусов.
        private PreparedProgressionState PrepareProgression(CharacterSaveData data)
        {
            if(data==null)throw new InvalidOperationException("Пустой снимок.");
            var progression=GetComponent<PlayerProgression>();
            if(progression==null)
            {
                if(data.progression!=null)throw new InvalidOperationException("Для этого файла нужен компонент развития.");
                return null;
            }
            if(!progression.isActiveAndEnabled || GetComponent<PlayerStatModifiers>()?.isActiveAndEnabled!=true)
                throw new InvalidOperationException("Для сохранения развития включите PlayerProgression и PlayerStatModifiers.");
            if(data.progression==null)throw new InvalidOperationException("В файле нет развития персонажа.");
            if(!progression.TryPrepareRestore(data.progression,out var prepared,out var reason))throw new InvalidOperationException(reason);
            return prepared;
        }
        private void MigrateLegacy(CharacterSaveData data)
        {
            if(data==null||data.version!=1)return;
            data.progression=GetComponent<PlayerProgression>()?.CreateFreshState();
            var bar=GetComponent<PlayerHotbar>();
            if(bar!=null)
            {
                data.hotbar=new string[bar.SlotCount];data.hotbarSelected=0;
                // Старое оружие назначается по существующему ID: миграция не создаёт предметов.
                if(!string.IsNullOrEmpty(data.equippedId))data.hotbar[0]=data.equippedId;
            }
            data.version=2;
        }
        private bool FreePose(CharacterSaveData data,WorldItem[] old,GameObject staged)
        {
            var cc=GetComponent<CharacterController>();float radius=Mathf.Max(.01f,cc.radius-cc.skinWidth);
            Vector3 feet=data.position+Vector3.up*motor.BottomOffset;float height=motor.SavedPoseHeight(data.prone,data.crouched);
            Vector3 bottom=feet+Vector3.up*(radius+.035f),top=feet+Vector3.up*(height-radius);
            foreach(var c in Physics.OverlapCapsule(bottom,top,radius,obstructionMask,QueryTriggerInteraction.Ignore))
                if(!c.transform.IsChildOf(transform) && !old.Any(w=>c.transform.IsChildOf(w.transform)))return false;
            bool supported=false;
            foreach(var hit in Physics.RaycastAll(feet+Vector3.up*.08f,Vector3.down,supportDistance+.08f,obstructionMask,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!old.Any(w=>hit.transform.IsChildOf(w.transform))&&hit.normal.y>.6f)supported=true;
            // Staged objects are inactive, so queries cannot see them yet. Compare capsule geometry explicitly.
            var probe=staged.AddComponent<CapsuleCollider>();probe.radius=radius;probe.height=height;probe.center=Vector3.zero;
            foreach(var c in staged.GetComponentsInChildren<Collider>(true))
                if(c!=probe && c.enabled && !c.isTrigger && StagedActive(c.transform,staged.transform)
                    && Physics.ComputePenetration(probe,feet+Vector3.up*(height*.5f+.02f),Quaternion.identity,c,c.transform.position,c.transform.rotation,out _,out _))return false;
            return supported;
        }
        private static bool StagedActive(Transform child,Transform root)
        { for(var p=child;p!=null && p!=root;p=p.parent)if(!p.gameObject.activeSelf)return false; return true; }
        public bool TryLoad()
        {
            if(working)return false; working=true;GameObject stage=null;
            try
            {
                CheckContext();if(!File.Exists(SavePath))throw new InvalidOperationException("Файл ещё не создан. F5 — сохранить.");
                if(new FileInfo(SavePath).Length>16*1024*1024)throw new InvalidOperationException("Файл слишком большой.");
                var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(SavePath,Encoding.UTF8));
                if(envelope==null || string.IsNullOrEmpty(envelope.payload)||Hash(envelope.payload)!=envelope.sha256)throw new InvalidOperationException("Файл повреждён; текущая игра не изменена. Рядом может быть .bak.");
                var data=JsonUtility.FromJson<CharacterSaveData>(envelope.payload);MigrateLegacy(data);var catalog=Catalog();var stores=Storages();ValidateData(data,catalog,stores);
                var preparedProgression=PrepareProgression(data);
                var restored=new Dictionary<ItemStorage,List<ItemStack>>();var ids=new HashSet<string>();
                foreach(var row in data.storages)restored.Add(stores[row.id],row.slots.Select(s=>Decode(s,catalog,ids)).ToList());
                var old=WorldItems();stage=new GameObject("Save restore staging");stage.SetActive(false);
                var staged=new List<WorldItem>();
                foreach(var row in data.world)
                {
                    var state=Decode(row.item,catalog,ids);var go=Instantiate(state.Definition.WorldPrefab,stage.transform);
                    go.transform.SetPositionAndRotation(row.position,row.rotation);go.transform.localScale=row.scale;go.SetActive(row.active);
                    var world=go.GetComponent<WorldItem>();world.Initialize(state);world.enabled=row.enabled;staged.Add(world);
                }
                Physics.SyncTransforms();if(!FreePose(data,old,stage))throw new InvalidOperationException("Точка загрузки занята или потеряла опору. Освободите место.");
                // Commit section: everything above is non-destructive. No external observers until all slots/world are replaced.
                GetComponent<PlayerInventory>()?.Close();GetComponent<PlayerProgressionMenu>()?.Close();var weapons=GetComponent<PlayerWeapons>();weapons?.Unequip();
                foreach(var w in old){w.RetireForRestore();w.gameObject.SetActive(false);Destroy(w.gameObject);}
                foreach(var pair in restored)pair.Key.ReplaceSavedSlots(pair.Value);
                foreach(var w in staged)w.transform.SetParent(null,true);
                if(preparedProgression!=null)GetComponent<PlayerProgression>().ApplyPreparedRestore(preparedProgression);
                motor.RestoreSavedPose(data.position,Quaternion.Euler(0,data.yaw,0),data.pitch,data.prone,data.crouched);
                health.RestoreAlive(data.health);motor.Stamina.RestoreReserve(data.stamina,data.exhausted,data.recoveryWait);needs.RestoreValues(data.food,data.water);GetComponent<PlayerNoiseEmitter>()?.ResetAfterTeleport();
                foreach(var pair in restored)pair.Key.NotifySavedRestore();
                if(weapons!=null && !string.IsNullOrEmpty(data.equippedId))
                    for(int i=0;i<GetComponent<ItemStorage>().SlotCount;i++)if(GetComponent<ItemStorage>().GetSlot(i)?.InstanceId==data.equippedId){weapons.Equip(i);break;}
                GetComponent<PlayerHotbar>()?.RestoreAssignments(data.hotbar,data.hotbarSelected);
                Status="Загружено · F5 — сохранить";return true;
            }
            catch(Exception e){Status="Не загружено: "+e.Message;return false;}
            finally{if(stage!=null)Destroy(stage);working=false;}
        }
        private void OnGUI()
        {
            if(!showHud || GetComponent<PlayerInventory>()?.IsOpen == true || GetComponent<PlayerProgressionMenu>()?.IsOpen == true)return;float scale=Mathf.Min(Screen.width/1000f,Screen.height/700f);var matrix=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale;Color color=GUI.color;GUI.color=new Color(.055f,.075f,.095f,1);GUI.DrawTexture(new Rect(width/2-250,125,500,53),Texture2D.whiteTexture);GUI.color=color;
            GUI.Label(new Rect(width/2-242,128,484,48),Status,new GUIStyle(GUI.skin.label){fontSize=13,wordWrap=true});GUI.matrix=matrix;
        }
    }
}
