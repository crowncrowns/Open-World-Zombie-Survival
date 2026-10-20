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
    /// <summary>Единый снимок персонажа, предметов и зарегистрированной области мира.
    /// Валидация/подготовка предшествуют изменению состояния; деньги не загружаются отдельно от вещей.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonController),typeof(PlayerNeeds))]
    [RequireComponent(typeof(ItemStorage),typeof(PlayerStamina))]
    public sealed class PlayerSaveSystem : MonoBehaviour
    {
        [SerializeField, Tooltip("Имя одного файла в Application.persistentDataPath. Без папок; запись через временный файл и резервную .bak.")]
        private string fileName="character-stage11.json";
        [SerializeField, Tooltip("Каталог типов предметов этого сохранения. ID уникальны; добавляя тип, обновите каталог перед сборкой игры.")]
        private ItemDefinition[] itemCatalog;
        [SerializeField, InspectorName("Реестр области мира"), Tooltip("Реестр сохраняемых объектов этой сцены. Если ссылка не назначена, требуется ровно один WorldSaveRegistry в текущей сцене.")]
        private WorldSaveRegistry worldRegistry;
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
        public event Action SaveCompleted;
        public event Action LoadCompleted;
        public bool IsWorking => working;
        public string Status { get; private set; }="F5 — сохранить · F9 — загрузить";
        public string SavePath => Path.Combine(Application.persistentDataPath,fileName);
        private void Awake() { motor=GetComponent<FirstPersonController>();health=GetComponent<Health>();needs=GetComponent<PlayerNeeds>(); save=saveInput.Clone();load=loadInput.Clone(); }
        private void OnEnable(){save?.Enable();load?.Enable();}
        private void OnDisable(){save?.Disable();load?.Disable();}
        private void OnDestroy(){save?.Dispose();load?.Dispose();}
        private void Update()
        {
            if(Time.timeScale<=0 || requireApplicationFocus && !Application.isFocused || GetComponent<PlayerSaveMenu>()?.IsOpen == true)return;
            if(save!=null && save.WasPressedThisFrame()) TrySave();
            else if(load!=null && load.WasPressedThisFrame()) TryLoad();
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        private static bool PositionValid(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z)&&v.sqrMagnitude<1e12f;
        private static string Hash(string s)
        { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-",""); }
        private void CheckContext()
        {
            if (ZombieSurvival.Services.Trader.RestockIsBusy) throw new InvalidOperationException("Дождитесь завершения пополнения товаров.");
            if (ZombieSurvival.Services.RepairService.IsBusy) throw new InvalidOperationException("Дождитесь завершения ремонта.");
            if (PlayerWeapons.IsBusy || ItemStorage.IsWeaponUseBusy) throw new InvalidOperationException("Дождитесь завершения применения оружия.");
            if (ZombieSurvival.Services.TradeService.IsBusy) throw new InvalidOperationException("Дождитесь завершения сделки.");
            if(!isActiveAndEnabled || !Application.isPlaying)throw new InvalidOperationException("Сохранение работает только в Play.");
            if(string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName)!=fileName)throw new InvalidOperationException("В имени сохранения запрещены пути.");
            Registry().ValidateCoverage();
        }
        private WorldSaveRegistry Registry()
        {
            if(worldRegistry!=null)
            { if(worldRegistry.gameObject.scene!=gameObject.scene)throw new InvalidOperationException("Реестр сохранения находится в другой сцене."); return worldRegistry; }
            var registries=FindObjectsByType<WorldSaveRegistry>(FindObjectsInactive.Include).Where(r=>r.gameObject.scene==gameObject.scene).ToArray();
            if(registries.Length!=1)throw new InvalidOperationException("В сцене нужен один WorldSaveRegistry. См. Docs/WorldSaving.md.");
            return registries[0];
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
                var owner=storage.GetComponentInParent<WorldSaveObject>(true);if(owner!=null&&(owner.IsRetired||owner.IsSpawned&&owner.IsRemoved))continue;
                var id=storage.GetComponent<SaveIdentity>();
                if(id==null || string.IsNullOrWhiteSpace(id.Id) || !result.TryAdd(id.Id,storage))throw new InvalidOperationException("Хранилищу нужен уникальный SaveIdentity: "+storage.name);
            }
            return result;
        }
        private WorldItem[] WorldItems()=>FindObjectsByType<WorldItem>(FindObjectsInactive.Include).Where(w=>w.gameObject.scene==gameObject.scene && w.Quantity>0).ToArray();
        private static SavedItem Capture(ItemStack s)=>s==null || s.IsEmpty ? new SavedItem() : new SavedItem{occupied=true,typeId=s.Definition.Id,instanceId=s.InstanceId,quantity=s.Quantity,durability=s.Durability,loadedRounds=s.LoadedRounds};
        private static SavedWorldItem CaptureWorldItem(WorldItem item)
        {
            var body=item.GetComponent<Rigidbody>();
            if(item.GetComponentsInChildren<Rigidbody>(true).Length!=(body!=null?1:0))throw new InvalidOperationException("Составная физика добычи требует отдельного адаптера: "+item.name);
            return new SavedWorldItem{item=Capture(item.StateForSave()),position=item.transform.position,scale=item.transform.lossyScale,rotation=item.transform.rotation,active=item.gameObject.activeInHierarchy,enabled=item.enabled,
                hasBody=body!=null,body=body!=null?RigidbodyWorldState.Capture(body):null};
        }
        private static ItemStack Decode(SavedItem s,Dictionary<string,ItemDefinition> catalog,HashSet<string> ids)
        {
            if(s==null || !s.occupied)return null;
            if(string.IsNullOrWhiteSpace(s.instanceId)||!ids.Add(s.instanceId)||!catalog.TryGetValue(s.typeId??"",out var item))throw new InvalidOperationException("Неизвестный тип или повтор ID предмета.");
            if(s.quantity<1 || s.quantity>item.MaxStack || !Finite(s.durability) || s.durability<0 || s.durability>item.MaximumDurability || s.loadedRounds<0
                || item.Weapon==null && s.loadedRounds!=0 || item.Weapon!=null && s.loadedRounds>item.Weapon.MagazineSize)
                throw new InvalidOperationException("Некорректное состояние предмета: "+s.typeId);
            return ItemStack.RestoreSaved(item,s.quantity,s.instanceId,s.durability,s.loadedRounds);
        }
        /// <summary>Дешёвая проверка для планировщика. Полная проверка мира остаётся внутри TrySaveSlot.</summary>
        public bool CanSaveNow(out string reason)
        {
            if (ZombieSurvival.Services.Trader.RestockIsBusy) { reason = "Ожидание завершения пополнения товаров"; return false; }
            if (ZombieSurvival.Services.RepairService.IsBusy) { reason = "Ожидание завершения ремонта"; return false; }
            if (PlayerWeapons.IsBusy || ItemStorage.IsWeaponUseBusy) { reason = "Ожидание завершения применения оружия"; return false; }
            if (ZombieSurvival.Services.TradeService.IsBusy) { reason = "Ожидание завершения сделки"; return false; }
            var weapons=GetComponent<PlayerWeapons>();
            if(!isActiveAndEnabled || !Application.isPlaying || working) { reason="Система сохранения занята или отключена";return false; }
            if(Time.timeScale<=0) { reason="Игра на паузе";return false; }
            if(motor==null || health==null || !motor.IsAlive || health.IsDead) { reason="Персонаж мёртв";return false; }
            if(!motor.IsGrounded) { reason="Ожидание приземления";return false; }
            if(motor.Parkour!=null && motor.Parkour.OwnsMovement) { reason="Ожидание завершения паркура";return false; }
            if(weapons!=null && (weapons.IsAttacking || weapons.IsReloading)) { reason="Ожидание завершения атаки или перезарядки";return false; }
            reason="";return true;
        }
        public string SlotPath(SaveSlotKind kind, int index=0)
        {
            if(string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName)!=fileName || fileName.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
                throw new InvalidOperationException("В имени сохранения запрещены пути и недопустимые символы.");
            if(!Enum.IsDefined(typeof(SaveSlotKind),kind) || index<0 || index>99 || kind==SaveSlotKind.Quick && index!=0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if(kind==SaveSlotKind.Quick)return SavePath;
            return Path.Combine(Application.persistentDataPath,Path.GetFileNameWithoutExtension(fileName)+(kind==SaveSlotKind.Auto?"-auto-":"-manual-")+(index+1)+".json");
        }
        // Все способы записи проходят один валидатор; имя quick-save не меняется ради выбора слота.
        public bool TrySave()=>TrySaveSlot(SaveSlotKind.Quick);
        public bool TrySaveSlot(SaveSlotKind kind,int index=0)
        {
            if(!CanSaveNow(out var reason)){Status="Не сохранено: "+reason;return false;}
            if(working)return false; working=true;
            try
            {
                CheckContext();string path=SlotPath(kind,index); var weapons=GetComponent<PlayerWeapons>();
                if(!motor.IsAlive || health.IsDead || !motor.IsGrounded || motor.Parkour!=null && motor.Parkour.OwnsMovement || weapons!=null && (weapons.IsAttacking || weapons.IsReloading))
                    throw new InvalidOperationException("Сохранение: встаньте на землю и завершите действие.");
                var data=new CharacterSaveData{scene=gameObject.scene.path,savedUtc=DateTime.UtcNow.ToString("O"),position=transform.position,yaw=transform.eulerAngles.y,pitch=motor.SavedViewPitch,
                    health=health.Current,stamina=motor.Stamina.Current,recoveryWait=motor.Stamina.RecoveryWait,exhausted=motor.Stamina.IsExhausted,food=needs.Food,water=needs.Water,
                    prone=motor.IsProne,crouched=motor.IsCrouching,equippedId=weapons!=null && weapons.Equipped!=null?weapons.Equipped.InstanceId:""};
                data.progression=GetComponent<PlayerProgression>()?.CaptureState();
                var hotbar=GetComponent<PlayerHotbar>();data.hotbar=hotbar?.CaptureAssignments();data.hotbarSelected=hotbar!=null?hotbar.SelectedIndex:0;
                data.worldState=Registry().CaptureState();
                foreach(var pair in Storages())
                {
                    var row=new SavedStorage{id=pair.Key};for(int i=0;i<pair.Value.SlotCount;i++)row.slots.Add(Capture(pair.Value.GetSlot(i)));data.storages.Add(row);
                }
                foreach(var w in WorldItems())data.world.Add(CaptureWorldItem(w));
                ValidateData(data,Catalog(),Storages());
                var payload=JsonUtility.ToJson(data);var envelope=JsonUtility.ToJson(new SaveEnvelope{payload=payload,sha256=Hash(payload)},true);
                if(Encoding.UTF8.GetByteCount(envelope)>16*1024*1024)throw new InvalidOperationException("Снимок превысил допустимые 16 МиБ.");
                Directory.CreateDirectory(Application.persistentDataPath);string temp=path+".tmp";
                File.WriteAllText(temp,envelope,Encoding.UTF8);
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                Status="Сохранено · "+SaveSlotInfo.Label(kind,index);PublishSafely(()=>SaveCompleted?.Invoke());return true;
            }
            catch(Exception e){Status="Не сохранено: "+e.Message;return false;}
            finally{working=false;}
        }
        private void ValidateData(CharacterSaveData data,Dictionary<string,ItemDefinition> catalog,Dictionary<string,ItemStorage> stores)
        {
            var prepared=PrepareProgression(data);
            float savedHealthMaximum=prepared!=null?Mathf.Max(1,prepared.Evaluate(StatId.HealthMaximum,health.BaseMaximum)):health.Maximum;
            float savedStaminaMaximum=prepared!=null?Mathf.Max(1,prepared.Evaluate(StatId.StaminaCapacity,motor.Stamina.BaseCapacity)):motor.Stamina.Capacity;
            if(data==null || data.version!=3 || data.worldState==null || data.worldState.scope!=Registry().ScopeId || data.scene!=gameObject.scene.path || !PositionValid(data.position) || !Finite(data.yaw)||!Finite(data.pitch)
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
                // JsonUtility материализует null-вложенный DTO: наличие определяет hasBody, не сам объект body.
                if(w.hasBody&&w.body==null||item.Definition.WorldPrefab.GetComponentsInChildren<Rigidbody>(true).Length>(item.Definition.WorldPrefab.GetComponent<Rigidbody>()!=null?1:0))
                    throw new InvalidOperationException("Составная/повреждённая физика добычи: "+item.Definition.Id);
                if(w.hasBody)w.body.Validate();
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
            if(data==null)return;
            if(data.version==1)
            {
                data.progression=GetComponent<PlayerProgression>()?.CreateFreshState();
                var bar=GetComponent<PlayerHotbar>();
                if(bar!=null)
                {
                    data.hotbar=new string[bar.SlotCount];data.hotbarSelected=0;
                    if(!string.IsNullOrEmpty(data.equippedId))data.hotbar[0]=data.equippedId;
                }
                data.version=2;
            }
            if(data.version==2)
            {
                if(FindObjectsByType<ZombieSurvival.Economy.Wallet>(FindObjectsInactive.Include).Any(w=>w.gameObject.scene==gameObject.scene))
                    throw new InvalidOperationException("В старом файле нет денег мира. Создайте новое F5: откат вещей без кошельков запрещён.");
                // Старый снимок ничего не знал о мире: сохраняем его ТЕКУЩЕЕ состояние, не выдумываем прошлое.
                data.worldState=Registry().CaptureState();data.version=3;
                var catalog=Catalog();
                foreach(var row in data.world??new List<SavedWorldItem>())
                    if(row?.item!=null&&catalog.TryGetValue(row.item.typeId??"",out var type)&&type.WorldPrefab!=null)
                    {var body=type.WorldPrefab.GetComponent<Rigidbody>();row.hasBody=body!=null;row.body=body!=null?RigidbodyWorldState.Capture(body):null;}
            }
        }
        private bool FreePose(CharacterSaveData data,WorldItem[] old,GameObject staged,WorldRestorePlan worldPlan)
        {
            var replaced=worldPlan.ExistingRoots.ToArray();
            var cc=GetComponent<CharacterController>();float radius=Mathf.Max(.01f,cc.radius-cc.skinWidth);
            Vector3 feet=data.position+Vector3.up*motor.BottomOffset;float height=motor.SavedPoseHeight(data.prone,data.crouched);
            Vector3 bottom=feet+Vector3.up*(radius+.035f),top=feet+Vector3.up*(height-radius);
            foreach(var c in Physics.OverlapCapsule(bottom,top,radius,obstructionMask,QueryTriggerInteraction.Ignore))
                if(!c.transform.IsChildOf(transform) && !old.Any(w=>c.transform.IsChildOf(w.transform))&&!replaced.Any(r=>c.transform.IsChildOf(r)))return false;
            bool supported=false;
            foreach(var hit in Physics.RaycastAll(feet+Vector3.up*.08f,Vector3.down,supportDistance+.08f,obstructionMask,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!old.Any(w=>hit.transform.IsChildOf(w.transform))&&!replaced.Any(r=>hit.transform.IsChildOf(r))&&hit.normal.y>.6f)supported=true;
            using(var future=new ProspectiveWorldPhysics())
            {
                future.Add(staged.transform);future.Add(worldPlan.GeometryRoot);
                if(future.Blocks(bottom,top,radius,obstructionMask))return false;
                supported|=future.Supports(feet+Vector3.up*.08f,supportDistance+.08f,obstructionMask);
                foreach(var actor in worldPlan.NavigationGeometry)
                {
                    var body=actor.GetComponent<CapsuleCollider>();if(body==null||!body.enabled)continue;
                    if(body.direction!=1)throw new InvalidOperationException("Для навигационного актёра нужна вертикальная капсула.");
                    Vector3 scale=body.transform.lossyScale;float actualRadius=body.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));float actorRadius=actualRadius*.97f;
                    float half=Mathf.Max(0,body.height*Mathf.Abs(scale.y)*.5f-actualRadius);
                    Vector3 center=body.transform.TransformPoint(body.center),up=body.transform.up;
                    Vector3 a=center-up*half,b=center+up*half;
                    foreach(var collider in Physics.OverlapCapsule(a,b,actorRadius,obstructionMask,QueryTriggerInteraction.Ignore))
                        if(!old.Any(w=>collider.transform.IsChildOf(w.transform))&&!replaced.Any(r=>collider.transform.IsChildOf(r)))
                            throw new InvalidOperationException("Сохранённая точка NPC занята: "+actor.Id);
                    if(future.Blocks(a,b,actorRadius,obstructionMask,actor.Id))throw new InvalidOperationException("Объекты снимка перекрывают сохранённую капсулу NPC: "+actor.Id);
                }
            }
            return supported;
        }
        private static bool StagedActive(Transform child,Transform root)
        { for(var p=child;p!=null && p!=root;p=p.parent)if(!p.gameObject.activeSelf)return false; return true; }
        public bool TryLoad()=>TryLoadSlot(SaveSlotKind.Quick);
        public bool TryLoadSlot(SaveSlotKind kind,int index=0,bool backup=false)
        {
            if(working)return false; working=true;GameObject stage=null;WorldRestorePlan worldPlan=null;
            try
            {
                CheckContext();string path=SlotPath(kind,index)+(backup?".bak":"");if(!File.Exists(path))throw new InvalidOperationException("Файл ещё не создан. F5 — сохранить.");
                if(new FileInfo(path).Length>16*1024*1024)throw new InvalidOperationException("Файл слишком большой.");
                var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path,Encoding.UTF8));
                if(envelope==null || string.IsNullOrEmpty(envelope.payload)||Hash(envelope.payload)!=envelope.sha256)throw new InvalidOperationException("Файл повреждён; текущая игра не изменена. Рядом может быть .bak.");
                var data=JsonUtility.FromJson<CharacterSaveData>(envelope.payload);MigrateLegacy(data);var catalog=Catalog();
                if(data==null||data.version!=3)throw new InvalidOperationException("Неизвестная версия сохранения.");
                worldPlan=Registry().PrepareRestore(data.worldState);
                var stores=new Dictionary<string,ItemStorage>();
                foreach(var storage in worldPlan.Storages)
                {
                    var identity=storage.GetComponent<SaveIdentity>();
                    if(identity==null||string.IsNullOrWhiteSpace(identity.Id)||!stores.TryAdd(identity.Id,storage))throw new InvalidOperationException("Некорректные ID хранилищ будущего мира.");
                }
                ValidateData(data,catalog,stores);
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
                    var body=go.GetComponent<Rigidbody>();
                    if(row.hasBody){if(body==null)body=go.AddComponent<Rigidbody>();row.body.Prepare(body);}
                    else if(body!=null){body.isKinematic=true;body.detectCollisions=false;Destroy(body);}
                }
                Physics.SyncTransforms();if(!FreePose(data,old,stage,worldPlan))throw new InvalidOperationException("Точка загрузки занята или потеряла опору в сохранённом мире.");
                // Commit section: everything above is non-destructive. No external observers until all slots/world are replaced.
                // A successful restore starts a new interaction lifetime: release the service's input lock
                // before retiring its selected ItemStacks. Rejected loads leave the existing menu intact.
                GetComponent<ZombieSurvival.Services.PlayerServiceSession>()?.Close();
                GetComponent<PlayerInventory>()?.Close();GetComponent<PlayerProgressionMenu>()?.Close();GetComponent<PlayerSaveMenu>()?.Close();var weapons=GetComponent<PlayerWeapons>();weapons?.Unequip();
                worldPlan.CommitSilent();
                foreach(var w in old){w.RetireForRestore();w.gameObject.SetActive(false);Destroy(w.gameObject);}
                foreach(var pair in restored)pair.Key.ReplaceSavedSlots(pair.Value);
                for(int i=0;i<staged.Count;i++)
                {
                    var w=staged[i];w.transform.SetParent(null,true);var body=w.GetComponent<Rigidbody>();var row=data.world[i];
                    if(body!=null&&row.hasBody)row.body.Finish(body);
                }
                if(preparedProgression!=null)GetComponent<PlayerProgression>().ApplyPreparedRestore(preparedProgression,false);
                motor.RestoreSavedPose(data.position,Quaternion.Euler(0,data.yaw,0),data.pitch,data.prone,data.crouched);
                health.RestoreWorldStateSilently(data.health,false);motor.Stamina.RestoreReserve(data.stamina,data.exhausted,data.recoveryWait);needs.RestoreValues(data.food,data.water);GetComponent<PlayerNoiseEmitter>()?.ResetAfterTeleport();
                worldPlan.Publish();
                foreach(var pair in restored)PublishSafely(pair.Key.NotifySavedRestore);
                if(weapons!=null && !string.IsNullOrEmpty(data.equippedId))
                    for(int i=0;i<GetComponent<ItemStorage>().SlotCount;i++)if(GetComponent<ItemStorage>().GetSlot(i)?.InstanceId==data.equippedId){weapons.Equip(i);break;}
                GetComponent<PlayerHotbar>()?.RestoreAssignments(data.hotbar,data.hotbarSelected);
                PublishSafely(health.NotifyWorldRestore);
                var progression=GetComponent<PlayerProgression>();if(progression!=null)PublishSafely(progression.NotifySavedRestore);
                var modifiers=GetComponent<PlayerStatModifiers>();if(modifiers!=null)PublishSafely(modifiers.NotifySavedRestore);
                Status="Загружено · "+SaveSlotInfo.Label(kind,index)+(backup?" · резерв .bak":"");PublishSafely(()=>LoadCompleted?.Invoke());return true;
            }
            catch(Exception e){Status="Не загружено: "+e.Message;return false;}
            finally{worldPlan?.Dispose();if(stage!=null)Destroy(stage);working=false;}
        }
        private static void PublishSafely(Action action){try{action();}catch(Exception error){Debug.LogException(error);}}
        public SaveSlotInfo InspectSlot(SaveSlotKind kind,int index=0,bool backup=false)
        {
            var info=new SaveSlotInfo(kind,index,backup);
            try
            {
                string path=SlotPath(kind,index)+(backup?".bak":"");info.Exists=File.Exists(path);if(!info.Exists)return info;
                if(new FileInfo(path).Length>16*1024*1024)throw new InvalidOperationException("Файл больше 16 МиБ");
                var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path,Encoding.UTF8));
                if(envelope==null || string.IsNullOrEmpty(envelope.payload) || Hash(envelope.payload)!=envelope.sha256)throw new InvalidOperationException("Повреждён файл / контрольная сумма");
                var data=JsonUtility.FromJson<CharacterSaveData>(envelope.payload);
                if(data==null || data.version<1 || data.version>3)throw new InvalidOperationException("Неизвестный формат");
                info.Scene=data.scene;info.SavedUtc=DateTime.TryParse(data.savedUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out var utc)?utc.ToUniversalTime():File.GetLastWriteTimeUtc(path);
                if(data.scene!=gameObject.scene.path)throw new InvalidOperationException("Сохранение другой сцены");
                info.Readable=true;
            }
            catch(Exception error){info.Error=error.Message;}
            return info;
        }
        private void OnGUI()
        {
            if(!showHud || GetComponent<PlayerInventory>()?.IsOpen == true || GetComponent<PlayerProgressionMenu>()?.IsOpen == true || GetComponent<PlayerSaveMenu>()?.IsOpen == true || GetComponent<ZombieSurvival.Services.PlayerServiceSession>()?.IsOpen == true)return;float scale=Mathf.Min(Screen.width/1000f,Screen.height/700f);var matrix=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale;Color color=GUI.color;GUI.color=new Color(.055f,.075f,.095f,1);GUI.DrawTexture(new Rect(width/2-300,125,600,74),Texture2D.whiteTexture);GUI.color=color;
            GUI.Label(new Rect(width/2-292,128,584,68),Status+"\nF6 — меню · "+(GetComponent<PlayerSaveSlots>()?.AutoStatus??""),new GUIStyle(GUI.skin.label){fontSize=13,wordWrap=true});GUI.matrix=matrix;
        }
    }
}
