using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonController),typeof(Health))]
    public sealed class PlayerNeeds : MonoBehaviour
    {
        [Header("Сытость и вода")]
        [SerializeField, Tooltip("Включить расход потребностей и урон от истощения. Значения HUD остаются доступны.")]
        private bool needsEnabled=true;
        [SerializeField, Min(1), Tooltip("Максимальная сытость, ед.")]
        private float foodCapacity=100;
        [SerializeField, Min(1), Tooltip("Максимальный запас воды, ед.")]
        private float waterCapacity=100;
        [SerializeField, Range(0,1), Tooltip("Начальная доля сытости в новой сессии, 0–1.")]
        private float startingFood=1;
        [SerializeField, Range(0,1), Tooltip("Начальная доля воды в новой сессии, 0–1.")]
        private float startingWater=1;
        [SerializeField, Min(0), Tooltip("Расход сытости за игровую минуту, ед/мин. Меню рюкзака мир не останавливает.")]
        private float foodPerMinute=.8f;
        [SerializeField, Min(0), Tooltip("Расход воды за игровую минуту, ед/мин.")]
        private float waterPerMinute=1.4f;
        [SerializeField, Min(1), Tooltip("Множитель расхода при фактическом беге, без изменения базовой скорости персонажа.")]
        private float sprintMultiplier=1.6f;
        [SerializeField, Min(0), Tooltip("Урон от каждого пустого показателя, HP/с. Голод и жажда суммируются; общий Health учитывает защиту.")]
        private float emptyDamagePerSecond=1;
        [SerializeField, Tooltip("Восстановить стартовые потребности при обычном возрождении R. Загрузка восстанавливает сохранённые значения.")]
        private bool resetOnRespawn=true;
        [SerializeField, Tooltip("Разрешённые эффекты еды, воды и медицины. Ссылки на существующие ItemDefinition; не отдельный инвентарь.")]
        private ConsumableDefinition[] consumables;
        [SerializeField, Tooltip("Показывать сытость и воду на HUD.")]
        private bool showHud=true;
        private FirstPersonController motor;
        private Health health;
        private PlayerLifeCycle life;
        private float food,water,damageClock;
        private bool initialized;
        public float Food => food;
        public float Water => water;
        public float FoodCapacity => Mathf.Max(1,foodCapacity);
        public float WaterCapacity => Mathf.Max(1,waterCapacity);
        private void OnEnable() => Initialize();
        private void Initialize(bool fresh=false)
        {
            if(life!=null) life.Respawned-=OnRespawn;
            motor=GetComponent<FirstPersonController>(); health=GetComponent<Health>(); life=GetComponent<PlayerLifeCycle>();
            if(life!=null) life.Respawned+=OnRespawn;
            if(fresh || !initialized) ResetNeeds(); initialized=true;
        }
        private void OnDisable() { if(life!=null) life.Respawned-=OnRespawn; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreForPlay() { foreach(var n in FindObjectsByType<PlayerNeeds>(FindObjectsInactive.Include)) if(n.isActiveAndEnabled)n.Initialize(true); }
        public void ResetNeeds() => RestoreValues(FoodCapacity*startingFood,WaterCapacity*startingWater);
        public void RestoreValues(float newFood,float newWater) { if(float.IsNaN(newFood)||float.IsInfinity(newFood)||float.IsNaN(newWater)||float.IsInfinity(newWater)) throw new System.ArgumentException("Потребности должны быть конечными числами."); food=Mathf.Clamp(newFood,0,FoodCapacity); water=Mathf.Clamp(newWater,0,WaterCapacity); damageClock=0; }
        private void OnRespawn() { if(resetOnRespawn)ResetNeeds(); }
        private void Update()
        {
            if(!needsEnabled || health.IsDead || Time.deltaTime<=0) return;
            float delta=Time.deltaTime*(motor.IsSprinting && motor.PlanarVelocity.sqrMagnitude>.1f ? sprintMultiplier : 1);
            food=Mathf.Max(0,food-foodPerMinute*delta/60); water=Mathf.Max(0,water-waterPerMinute*delta/60);
            if(food>0 && water>0) { damageClock=0; return; }
            damageClock+=Time.deltaTime;
            if(damageClock<1) return;
            float seconds=Mathf.Floor(damageClock); damageClock-=seconds;
            health.ApplyDamage(new DamageInfo(emptyDamagePerSecond*seconds*((food<=0?1:0)+(water<=0?1:0)),null,transform.position,DamageKind.Environment));
        }
        private ConsumableDefinition Effect(ItemDefinition item)
        { if(consumables!=null)foreach(var c in consumables)if(c!=null && c.Item==item)return c; return null; }
        public bool CanConsume(ItemDefinition item) => Effect(item)!=null;
        public bool TryConsume(int slot)
        {
            var storage=GetComponent<ItemStorage>(); var stack=storage!=null?storage.GetSlot(slot):null;
            var effect=stack!=null && !stack.IsEmpty ? Effect(stack.Definition) : null;
            if(health.IsDead || !isActiveAndEnabled || effect==null || stack.Definition.IsIndividual) return false;
            if(!(effect.Food>0 && food<FoodCapacity || effect.Water>0 && water<WaterCapacity || effect.Healing>0 && health.Current<health.Maximum))return false;
            // Commit effects and item count before observers see Changed; no world pause or second item state.
            food=Mathf.Min(FoodCapacity,food+effect.Food); water=Mathf.Min(WaterCapacity,water+effect.Water);
            storage.Remove(slot,1);
            // Сила лечения — общий модификатор: величину/процент дизайнер задаёт в узле дерева.
            float healing = effect.Healing;
            var modifiers = GetComponent<ZombieSurvival.Progression.PlayerStatModifiers>();
            if (modifiers != null) healing = modifiers.Evaluate(ZombieSurvival.Progression.StatId.HealingPower, healing);
            if(healing>0) health.Heal(healing); return true;
        }
        private void OnGUI()
        {
            if (ZombieSurvival.Presentation.SurvivorHUD.Owns(this)) return;
            if(!showHud || motor==null || !motor.IsAlive || !motor.GameplayInputEnabled)return;
            float scale=Mathf.Min(Screen.width/1000f,Screen.height/700f);var matrix=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float h=Screen.height/scale; Color old=GUI.color;GUI.color=new Color(.055f,.075f,.095f,1);GUI.DrawTexture(new Rect(18,h-245,250,64),Texture2D.whiteTexture);GUI.color=old;
            GUI.Label(new Rect(27,h-240,235,55),$"СЫТОСТЬ: {food:0} / {FoodCapacity:0}\nВОДА: {water:0} / {WaterCapacity:0}",new GUIStyle(GUI.skin.label){fontSize=15});GUI.matrix=matrix;
        }
    }
}
