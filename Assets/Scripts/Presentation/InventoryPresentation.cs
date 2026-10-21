using System;
using UnityEngine;
using ZombieSurvival.Combat;
using ZombieSurvival.Economy;
using ZombieSurvival.Inventory;
using ZombieSurvival.Player;
using ZombieSurvival.Progression;

namespace ZombieSurvival.Presentation
{
    /// <summary>Read-only layout and explicit commands to the existing inventory owner.</summary>
    internal sealed class InventoryPresentation : IDisposable
    {
        private static readonly Color Paper = new Color(.85f, .83f, .75f);
        private static readonly Color Muted = new Color(.53f, .56f, .52f);
        private static readonly Color Amber = new Color(.83f, .62f, .32f);
        private static readonly Color Back = new Color(.055f, .065f, .058f, .94f);
        private readonly PlayerInventory owner;
        private ModelPreview portrait;
        private GameObject shownModel;
        private GUIStyle small, body, title, number, wrap, button;
        private Vector2 ownScroll, containerScroll;
        internal InventoryPresentation(PlayerInventory owner) { this.owner = owner; }
        internal void Tick()
        {
            var visual = owner.GetComponent<PlayerVisual>(); var model = visual != null ? visual.ModelPrefab : null;
            if (model != shownModel)
            {
                portrait?.Dispose(); portrait = null; shownModel = model;
                if (model != null) portrait = new ModelPreview(model, 640, 896, owner.InventoryIdle, true);
            }
            portrait?.Render(Time.unscaledDeltaTime);
            Warm(owner.Storage); if (owner.Container != null) Warm(owner.Container);
        }
        private static void Warm(ItemStorage storage)
        {
            for (int i = 0; i < storage.SlotCount; i++)
            { var item = storage.GetSlot(i); if (item != null && !item.IsEmpty) ItemModelIcons.Get(item.Definition); }
        }
        private void Styles()
        {
            if (body != null) return;
            body = Style(18, Paper); small = Style(13, Muted); title = Style(34, Paper); number = Style(25, Paper);
            wrap = Style(15, Paper); wrap.wordWrap = true;
            button = Style(15, Paper); button.alignment = TextAnchor.MiddleCenter;
        }
        private static GUIStyle Style(int size, Color color)
        {
            var result = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) };
            result.normal.textColor = color; result.hover.textColor = color; result.active.textColor = color;
            result.richText = false; return result;
        }
        internal void Draw()
        {
            Styles(); var matrix = GUI.matrix; var color = GUI.color;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); GUI.color = Color.white;
            Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, .24f));
            // Fit a fixed, legible composition to the right; the left stays an open view of the live world.
            float offset = width - 1920;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset * scale, (height - 1080) * .5f * scale, 0), Quaternion.identity, Vector3.one * scale);
            DrawCharacter(); DrawBackpack(); DrawStats();
            if (owner.Container != null) DrawContainer();
            GUI.color = color; GUI.matrix = matrix;
        }
        private void DrawCharacter()
        {
            Text(new Rect(556, 79, 490, 23), "ПОДГОТОВКА К ВЫЛАЗКЕ", small);
            Text(new Rect(552, 105, 490, 48), "ВЫЖИВШИЙ", title);
            Fill(new Rect(555, 164, 42, 2), Amber);
            if (portrait?.Texture != null) GUI.DrawTexture(new Rect(484, 172, 590, 688), portrait.Texture, ScaleMode.ScaleToFit, true);
            var needs = owner.GetComponent<PlayerNeeds>();
            if (needs != null) Text(new Rect(555, 856, 500, 24), "СЫТОСТЬ  " + Mathf.RoundToInt(needs.Food / needs.FoodCapacity * 100) + "%    /    ВОДА  " + Mathf.RoundToInt(needs.Water / needs.WaterCapacity * 100) + "%", small);
            Text(new Rect(555, 886, 500, 26), "[TAB / ESC]  ВЕРНУТЬСЯ К ВЫЛАЗКЕ", small);
        }
        private void DrawBackpack()
        {
            Fill(new Rect(1118, 66, 746, 824), Back);
            Fill(new Rect(1118, 66, 3, 45), Amber);
            Text(new Rect(1148, 82, 510, 46), "РЮКЗАК", title);
            Text(new Rect(1148, 127, 440, 22), "СНАРЯЖЕНИЕ  /  ПРИПАСЫ", small);
            if (Button(new Rect(1812, 84, 32, 32), "×")) owner.Close();
            var wallet = owner.GetComponent<Wallet>();
            Text(new Rect(1148, 164, 310, 20), "БАЛАНС", small);
            Text(new Rect(1148, 188, 320, 36), wallet != null ? wallet.FormattedBalance : "— ₽", number);
            Text(new Rect(1515, 164, 295, 20), "ВЕС ИНВЕНТАРЯ", small);
            Text(new Rect(1515, 188, 120, 36), "— кг", number);
            Text(new Rect(1630, 196, 205, 22), "НЕ УЧИТЫВАЕТСЯ", small);
            Fill(new Rect(1148, 233, 686, 1), new Color(.3f, .33f, .28f, .6f));
            Grid(owner.Storage, new Rect(1148, 254, 455, 348), false, ref ownScroll);
            DrawDetails(new Rect(1629, 254, 205, 348));
            DrawActions(); DrawAssignments();
            Text(new Rect(1148, 856, 686, 27), owner.Feedback, small);
        }
        private void Grid(ItemStorage storage, Rect viewport, bool container, ref Vector2 scroll)
        {
            const int columns = 4; float cell = (viewport.width - 30) / columns, pitch = cell + 10;
            float contentHeight = Mathf.Ceil(storage.SlotCount / (float)columns) * pitch - 10;
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, viewport.width - (contentHeight > viewport.height ? 18 : 0), contentHeight), false, false);
            if (contentHeight > viewport.height) cell -= 4.5f;
            pitch = cell + 10;
            for (int i = 0; i < storage.SlotCount; i++)
            {
                var rect = new Rect(i % columns * pitch, i / columns * pitch, cell, cell);
                bool selected = owner.SelectedIndex == i && owner.SelectedContainer == container;
                var stack = storage.GetSlot(i);
                if (Slot(rect, stack, (i + 1).ToString("00"), selected)) owner.SelectSlot(i, container);
            }
            GUI.EndScrollView();
        }
        private bool Slot(Rect rect, ItemStack stack, string key, bool selected)
        {
            Fill(rect, selected ? new Color(.16f, .18f, .14f, .98f) : new Color(.095f, .11f, .095f, .86f));
            Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), selected ? Amber : new Color(.3f, .34f, .29f, .6f));
            bool clicked = GUI.Button(rect, new GUIContent("", stack != null ? stack.Definition.DisplayName : "Свободный слот"), GUIStyle.none);
            Text(new Rect(rect.x + 8, rect.y + 4, 35, 20), key, small);
            if (stack != null && !stack.IsEmpty)
            {
                var texture = ItemModelIcons.Get(stack.Definition);
                if (texture != null) GUI.DrawTexture(new Rect(rect.x + 11, rect.y + 15, rect.width - 22, rect.height - 32), texture, ScaleMode.ScaleToFit, true);
                var count = new Rect(rect.xMax - 59, rect.yMax - 28, 52, 24);
                Fill(count, new Color(.04f, .05f, .04f, .85f));
                var style = new GUIStyle(body) { alignment = TextAnchor.MiddleRight };
                Text(count, "×" + stack.Quantity, style);
                if (stack.Definition.HasDurability)
                {
                    float fraction = Mathf.Clamp01(stack.Durability / stack.Definition.MaximumDurability);
                    Fill(new Rect(rect.x + 7, rect.yMax - 5, (rect.width - 14) * fraction, 2), fraction < .25f ? new Color(.78f, .27f, .23f) : Muted);
                }
            }
            return clicked;
        }
        private void DrawDetails(Rect rect)
        {
            var stack = owner.SelectedStack;
            if (stack == null || stack.IsEmpty)
            {
                Text(new Rect(rect.x, rect.y + 4, rect.width, 55), "ВЫБЕРИТЕ\nПРЕДМЕТ", wrap);
                Text(new Rect(rect.x, rect.y + 70, rect.width, 90), "Запасы, состояние и быстрые слоты — всё для следующей вылазки.", wrap); return;
            }
            var texture = ItemModelIcons.Get(stack.Definition);
            if (texture != null) GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 142), texture, ScaleMode.ScaleToFit, true);
            Text(new Rect(rect.x, rect.y + 153, rect.width, 48), stack.Definition.DisplayName, wrap);
            Text(new Rect(rect.x, rect.y + 204, rect.width, 40), stack.Definition.CategoryLabel, small);
            string details = "Количество  " + stack.Quantity;
            if (stack.Definition.HasDurability) details += "\nПрочность  " + stack.Durability.ToString("0.#") + " / " + stack.Definition.MaximumDurability.ToString("0.#");
            if (stack.Definition.Weapon != null && stack.Definition.Weapon.kind == Weapons.WeaponKind.Firearm) details += "\nВ магазине  " + stack.LoadedRounds;
            Text(new Rect(rect.x, rect.y + 247, rect.width, 90), details, wrap);
        }
        private void DrawActions()
        {
            var stack = owner.SelectedStack; bool valid = stack != null && !stack.IsEmpty;
            bool mine = valid && !owner.SelectedContainer;
            var needs = owner.GetComponent<PlayerNeeds>();
            if (owner.Container != null)
            {
                if (Button(new Rect(1148, 619, 218, 34), owner.SelectedContainer ? "В РЮКЗАК · ВСЁ" : "В ЯЩИК · ВСЁ", valid)) owner.TransferSelected(int.MaxValue);
                if (Button(new Rect(1376, 619, 227, 34), "ПЕРЕНЕСТИ 1", valid)) owner.TransferSelected(1);
            }
            else
            {
                if (Button(new Rect(1148, 619, 218, 34), "УПОТРЕБИТЬ 1", mine && needs != null && needs.CanConsume(stack.Definition))) owner.ConsumeSelected();
                if (Button(new Rect(1376, 619, 227, 34), "ЭКИПИРОВАТЬ", mine && stack.Definition.Weapon != null)) owner.EquipSelected();
            }
            if (Button(new Rect(1148, 665, 218, 34), "ВЫБРОСИТЬ 1", mine)) owner.DropSelected(false);
            if (Button(new Rect(1376, 665, 227, 34), "ВЫБРОСИТЬ ВСЁ", mine)) owner.DropSelected(true);
        }
        private void DrawAssignments()
        {
            var hotbar = owner.GetComponent<PlayerHotbar>(); if (hotbar == null) return;
            Text(new Rect(1148, 714, 680, 23), "БЫСТРЫЙ ДОСТУП", small);
            int columns = Mathf.Min(5, hotbar.SlotCount); float width = (686 - (columns - 1) * 8) / columns;
            int rows = Mathf.CeilToInt(hotbar.SlotCount / 5f); float height = rows > 1 ? 51 : 96;
            var selected = owner.SelectedStack;
            for (int i = 0; i < hotbar.SlotCount; i++)
            {
                var rect = new Rect(1148 + i % columns * (width + 8), 742 + i / columns * (height + 5), width, height);
                var assigned = hotbar.GetAssignedItem(i);
                Fill(rect, new Color(.1f, .12f, .1f));
                Text(new Rect(rect.x + 7, rect.y + 2, 25, 18), (i + 1).ToString(), small);
                var texture = assigned != null ? ItemModelIcons.Get(assigned.Definition) : null;
                float iconHeight = height - 25;
                if (texture != null) GUI.DrawTexture(new Rect(rect.x + 25, rect.y + 2, width - 49, iconHeight), texture, ScaleMode.ScaleToFit, true);
                if (GUI.Button(new Rect(rect.x, rect.y, width - 20, iconHeight), GUIContent.none, GUIStyle.none)) hotbar.Select(i);
                if (Button(new Rect(rect.xMax - 20, rect.y, 20, 20), "×")) hotbar.Clear(i);
                bool canAssign = !owner.SelectedContainer && selected != null && !selected.IsEmpty && selected.Definition.AllowInHotbar;
                if (Button(new Rect(rect.x + 3, rect.yMax - 24, width - 6, 21), "НАЗНАЧИТЬ", canAssign)) owner.AssignSelected(i);
            }
        }
        private void DrawContainer()
        {
            Fill(new Rect(40, 238, 430, 480), new Color(Back.r, Back.g, Back.b, .86f));
            Text(new Rect(62, 255, 380, 35), "КОНТЕЙНЕР", number);
            Grid(owner.Container, new Rect(62, 309, 388, 381), true, ref containerScroll);
        }
        private void DrawStats()
        {
            var health = owner.GetComponent<Health>(); var stamina = owner.GetComponent<PlayerStamina>();
            var motor = owner.GetComponent<FirstPersonController>(); var progression = owner.GetComponent<PlayerProgression>();
            var modifiers = owner.GetComponent<PlayerStatModifiers>();
            Text(new Rect(554, 929, 1200, 22), "СОСТОЯНИЕ И ХАРАКТЕРИСТИКИ", small);
            string[] labels = { "ЗДОРОВЬЕ", "ВЫНОСЛИВОСТЬ", "ШАГ / БЕГ", "ПРЫЖОК", "ЛЕЧЕНИЕ", "УРОВЕНЬ" };
            string[] values = {
                health != null ? Mathf.CeilToInt(health.Current)+" / "+Mathf.CeilToInt(health.Maximum) : "—",
                stamina != null ? Mathf.CeilToInt(stamina.Current)+" / "+Mathf.CeilToInt(stamina.Capacity) : "—",
                motor != null ? motor.EffectiveWalkSpeed.ToString("0.#")+" / "+motor.EffectiveRunSpeed.ToString("0.#")+" м/с" : "—",
                motor != null ? motor.EffectiveJumpHeight.ToString("0.#")+" м" : "—",
                (modifiers != null ? modifiers.Evaluate(StatId.HealingPower,100) : 100).ToString("0")+"%",
                progression != null ? progression.Level+" · "+progression.AvailablePoints+" очк." : "—"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = 554 + i * 218;
                Fill(new Rect(x, 961, 202, 80), new Color(.055f, .065f, .058f, .85f));
                Text(new Rect(x + 13, 968, 185, 22), labels[i], small);
                Text(new Rect(x + 13, 995, 185, 33), values[i], number);
            }
        }
        private bool Button(Rect rect, string text, bool enabled = true)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            Fill(rect, enabled && rect.Contains(Event.current.mousePosition) ? new Color(.25f, .27f, .21f) : new Color(.13f, .15f, .12f));
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), enabled ? Muted : new Color(.2f, .22f, .19f));
            bool clicked = GUI.Button(rect, text, button); GUI.enabled = previous; return clicked;
        }
        private static void Text(Rect rect, string text, GUIStyle style) => GUI.Label(rect, text, style);
        private static void Fill(Rect rect, Color color)
        { var previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }
        public void Dispose() { portrait?.Dispose(); portrait = null; shownModel = null; }
    }
}
