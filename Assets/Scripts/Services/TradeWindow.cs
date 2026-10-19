using System;
using System.Globalization;
using UnityEngine;
using ZombieSurvival.Economy;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Services
{
    /// <summary>IMGUI presentation only. Selection retains a live instance; commands go exclusively through TradeService.</summary>
    public sealed class TradeWindow : IDisposable
    {
        private PlayerServiceSession session;
        private Trader trader;
        private ItemStorage bag, stock, selectedSource;
        private ItemStack selected;
        private TradeDirection direction;
        private TradeQuote quote;
        private string quantityText = "1", quoteReason = "", message = "";
        private long sessionVersion, catalogVersion;
        private int selectedQuantity, selectedRounds;
        private float selectedDurability, selectedMaximum;
        private string selectedId;
        private Vector2 stockScroll, bagScroll, detailsScroll;
        private readonly ItemListPresentation stockList=new ItemListPresentation(), bagList=new ItemListPresentation();
        private GUIStyle title, heading, text, muted, price, row, selectedRow, button, primary, input, notice;
        private Texture2D rowTexture, hoverTexture, selectedTexture, buttonTexture, primaryTexture;
        private static readonly Color Ink = new Color(.9f, .94f, .95f);
        private static readonly Color Accent = new Color(.38f, .85f, .73f);

        public void Tick(PlayerServiceSession owner, Trader target)
        {
            if (owner == null || target == null || !owner.IsOpen) { Reset(); return; }
            if (session != owner || trader != target || sessionVersion != owner.SessionVersion)
            {
                Reset(); session = owner; trader = target; sessionVersion = owner.SessionVersion; catalogVersion = target.CatalogVersion;
                trader.CatalogChanged += CatalogChanged;
            }
            var nextBag = owner.GetComponent<ItemStorage>(); var nextStock = target.ActiveStock;
            if (bag != nextBag || stock != nextStock)
            {
                if (bag != null) bag.Changed -= InventoryChanged;
                if (stock != null) stock.Changed -= InventoryChanged;
                bag = nextBag; stock = nextStock;
                if (bag != null) bag.Changed += InventoryChanged;
                if (stock != null) stock.Changed += InventoryChanged;
                ClearSelection("");
            }
            if (catalogVersion != trader.CatalogVersion) { catalogVersion = trader.CatalogVersion; ClearSelection("Ассортимент обновился. Выберите вещь заново."); }
            if (selected != null && (selectedSource == null || !selectedSource.Contains(selected) || selected.IsEmpty || selected.InstanceId != selectedId
                || selected.Quantity != selectedQuantity || selected.LoadedRounds != selectedRounds || selected.Durability != selectedDurability || selected.Definition.MaximumDurability != selectedMaximum))
                ClearSelection("Состояние выбранной вещи изменилось. Выберите её заново.");
        }
        private void CatalogChanged() { catalogVersion = trader != null ? trader.CatalogVersion : 0; ClearSelection("Ассортимент обновился. Выберите вещь заново."); }
        private void InventoryChanged() => ClearSelection("Содержимое инвентаря изменилось. Выберите вещь заново.");
        private void ClearSelection(string status)
        { selected = null; selectedSource = null; selectedId = null; quote = null; quantityText = "1"; quoteReason = ""; message = status; detailsScroll = Vector2.zero; }
        public void Reset()
        {
            if (trader != null) trader.CatalogChanged -= CatalogChanged;
            if (bag != null) bag.Changed -= InventoryChanged;
            if (stock != null) stock.Changed -= InventoryChanged;
            session = null; trader = null; bag = stock = null; sessionVersion = catalogVersion = 0;
            stockScroll = bagScroll = Vector2.zero; stockList.Reset(); bagList.Reset(); ClearSelection("");
        }
        private void Select(ItemStorage source, ItemStack item, TradeDirection side)
        {
            selectedSource = source; selected = item; direction = side; selectedId = item.InstanceId;
            selectedQuantity = item.Quantity; selectedRounds = item.LoadedRounds; selectedDurability = item.Durability; selectedMaximum = item.Definition.MaximumDurability;
            quantityText = "1"; message = ""; detailsScroll = Vector2.zero; RefreshQuote(); GUI.FocusControl(null);
        }
        private void RefreshQuote(bool reportFailure=false)
        {
            quote = null; quoteReason = "";
            if (selected == null) return;
            if (!int.TryParse(quantityText, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count <= 0 || count > selected.Quantity)
            { quoteReason = "Введите количество от 1 до " + selected.Quantity + "."; if(reportFailure)session.ReportOperationResult(false); return; }
            bool success=TradeService.TryQuote(session, trader, direction, selected, count, out quote, out quoteReason);
            if(reportFailure&&!success)session.ReportOperationResult(false);
        }
        private void Quantity(int value)
        { if (selected == null) return; quantityText = Mathf.Clamp(value, 1, selected.Quantity).ToString(CultureInfo.InvariantCulture); RefreshQuote(); }

        public void Draw(PlayerServiceSession owner, Trader target)
        {
            Tick(owner, target); if (session == null || trader == null) return;
            EnsureStyles(); var oldMatrix = GUI.matrix; var oldColor = GUI.color; int oldDepth = GUI.depth; bool oldEnabled = GUI.enabled;
            try
            {
                GUI.depth = -100;
                float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 820f); if (scale <= 0) return;
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                float w = Screen.width / scale, h = Screen.height / scale;
                Fill(new Rect(0,0,w,h),new Color(.008f,.012f,.018f,.88f));
                var panel = new Rect((w-Mathf.Min(1240,w-32))/2,16,Mathf.Min(1240,w-32),h-32);
                Fill(panel,new Color(.035f,.048f,.061f)); Fill(new Rect(panel.x,panel.y,panel.width,3),Accent);
                GUI.Label(new Rect(panel.x+24,panel.y+17,panel.width-240,38),"ТОРГОВЛЯ",title);
                GUI.Label(new Rect(panel.x+24,panel.y+56,panel.width-240,24),trader.Profile != null ? trader.Profile.DisplayName : "Торговый профиль недоступен",muted);
                GUI.Label(new Rect(panel.x+24,panel.y+82,panel.width-48,23),EconomyCaption(),muted);
                if (GUI.Button(new Rect(panel.xMax-165,panel.y+24,140,38),"Закрыть  ×",button)) { session.Close(); return; }
                float contentY=panel.y+106, contentH=panel.height-164, gap=14, detailsWidth=330;
                float listWidth=(panel.width-48-detailsWidth-gap*2)/2;
                var left=new Rect(panel.x+24,contentY,listWidth,contentH);
                var middle=new Rect(left.xMax+gap,contentY,listWidth,contentH);
                var right=new Rect(middle.xMax+gap,contentY,detailsWidth,contentH);
                var budget=trader.GetComponent<TraderBudget>(); var wallet=session.GetComponent<Wallet>();
                DrawList(left,stock,TradeDirection.Buy,"ТОВАРЫ ТОРГОВЦА",budget==null?"Бюджет не задан":budget.IsInfinite?"Бюджет: бесконечный":"Бюджет: "+budget.Funds.FormattedBalance,ref stockScroll);
                DrawList(middle,bag,TradeDirection.Sell,"ВАШ РЮКЗАК",wallet!=null?"Ваши деньги: "+wallet.FormattedBalance:"Кошелёк отсутствует",ref bagScroll);
                DrawDetails(right);
                GUI.Label(new Rect(panel.x+24,panel.yMax-43,panel.width-48,28),"Выберите товар слева для покупки или вещь в рюкзаке для продажи.  Escape — закрыть",muted);
            }
            finally { GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;GUI.enabled=oldEnabled; }
        }
        private void DrawList(Rect area, ItemStorage storage, TradeDirection side, string caption, string funds, ref Vector2 scroll)
        {
            Fill(area,new Color(.065f,.083f,.101f));
            GUI.Label(new Rect(area.x+14,area.y+12,area.width-28,28),caption,heading);
            GUI.Label(new Rect(area.x+14,area.y+45,area.width-28,28),funds,price);
            GUILayout.BeginArea(new Rect(area.x+8,area.y+84,area.width-16,area.height-93));
            var presentation=side==TradeDirection.Buy?stockList:bagList;
            presentation.DrawControls(storage,true,button,ref scroll);
            scroll=GUILayout.BeginScrollView(scroll);
            bool any=false;
            foreach(var item in presentation.Items(storage,trader.Profile,side,true,null,trader))
            {
                any=true;
                string cost;
                if(side==TradeDirection.Sell&& !trader.Profile.Accepts(item.Definition)) cost="Не выкупается";
                else cost=TradeService.TryPreviewPrice(trader,side,item,1,out long unit,out _,out _)?Wallet.FormatRubles(unit)+" / шт.":"Цена не назначена";
                string state=item.Definition.HasDurability?"Прочность "+item.Durability.ToString("0.#")+" / "+item.Definition.MaximumDurability.ToString("0.#"):item.Definition.CategoryLabel;
                if(item.Definition.Weapon!=null&&item.Definition.Weapon.Ammo!=null)state+=" · магазин "+item.LoadedRounds;
                string label=item.Definition.DisplayName+"  ×"+item.Quantity+"\n"+cost+"\n"+state;
                bool active=selected==item&&direction==side;
                if(GUILayout.Button(label,active?selectedRow:row,GUILayout.MinHeight(92)))Select(storage,item,side);
                GUILayout.Space(5);
            }
            if(!any)GUILayout.Label(side==TradeDirection.Buy?"Товары закончились.":"Рюкзак пуст.\nСначала найдите вещи в мире.",muted);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void DrawDetails(Rect area)
        {
            Fill(area,new Color(.075f,.096f,.115f));
            GUILayout.BeginArea(new Rect(area.x+16,area.y+14,area.width-32,area.height-28));
            detailsScroll=GUILayout.BeginScrollView(detailsScroll);
            if(selected==null)
            {
                GUILayout.Label("ВЫБОР ПРЕДМЕТА",heading);GUILayout.Space(20);
                GUILayout.Label("Выберите конкретную вещь в одном из списков.\n\nЗдесь появятся состояние, количество и итоговая цена.",text);
                if(!string.IsNullOrEmpty(message)){GUILayout.Space(20);GUILayout.Label(message,notice);}
                GUILayout.EndScrollView();GUILayout.EndArea();return;
            }
            var item=selected;
            GUILayout.Label(direction==TradeDirection.Buy?"ПОКУПКА":"ПРОДАЖА",heading);GUILayout.Space(10);
            GUILayout.Label(item.Definition.DisplayName,title);GUILayout.Space(8);
            GUILayout.Label(item.Definition.CategoryLabel+" · "+item.Definition.SubcategoryLabel,muted);
            GUILayout.Label("Доступно: "+item.Quantity,text);
            if(item.Definition.HasDurability)GUILayout.Label("Прочность: "+item.Durability.ToString("0.#")+" / "+item.Definition.MaximumDurability.ToString("0.#"),text);
            if(item.Definition.Weapon!=null&&item.Definition.Weapon.Ammo!=null)GUILayout.Label("Магазин: "+item.LoadedRounds+" / "+item.Definition.Weapon.MagazineSize+" патр.",text);
            if(!string.IsNullOrWhiteSpace(item.Definition.Description)){GUILayout.Space(10);GUILayout.Label(item.Definition.Description,muted);}
            GUILayout.Space(20);GUILayout.Label("КОЛИЧЕСТВО",heading);GUILayout.Space(7);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("−",button,GUILayout.Width(37),GUILayout.Height(36)))Quantity(ParsedQuantity()-1);
            GUI.SetNextControlName("TradeQuantity");string next=GUILayout.TextField(quantityText,10,input,GUILayout.Height(36));
            if(next!=quantityText){quantityText=next;RefreshQuote();}
            if(GUILayout.Button("+",button,GUILayout.Width(37),GUILayout.Height(36)))Quantity(ParsedQuantity()==int.MaxValue?int.MaxValue:ParsedQuantity()+1);
            GUILayout.EndHorizontal();GUILayout.Space(5);
            GUILayout.BeginHorizontal();if(GUILayout.Button("1 шт.",button,GUILayout.Height(30)))Quantity(1);
            if(GUILayout.Button("Всё",button,GUILayout.Height(30)))Quantity(item.Quantity);GUILayout.EndHorizontal();
            GUILayout.Space(18);
            int count=ParsedQuantity();bool validQuantity=int.TryParse(quantityText,NumberStyles.None,CultureInfo.InvariantCulture,out count)&&count>0&&count<=item.Quantity;
            bool preview=TradeService.TryPreviewPrice(trader,direction,item,validQuantity?count:1,out long unit,out long total,out var previewReason);
            GUILayout.Label(preview?"За единицу: "+Wallet.FormatRubles(unit):"Цена недоступна",text);
            GUILayout.Label(preview&&validQuantity?"Итого: "+Wallet.FormatRubles(total):"Итого: —",price);
            bool eligible=false;string refusal=quoteReason;
            if(validQuantity)
            {
                bool available=TradeService.TryQuote(session,trader,direction,item,count,out _,out refusal);
                eligible=available&&quote!=null;
                if(available&&quote==null)refusal="Нажмите «Обновить предложение» для подтверждения.";
                if(quote!=null&&preview&&(quote.UnitPrice!=unit||quote.TotalPrice!=total))
                {eligible=false;refusal="Цена изменилась. Обновите предложение.";}
            }
            if(!validQuantity)refusal="Введите количество от 1 до "+item.Quantity+".";
            else if(!preview)refusal=previewReason;
            else if(quote==null&&string.IsNullOrEmpty(refusal))refusal="Обновите предложение перед подтверждением.";
            if(!string.IsNullOrEmpty(refusal)){GUILayout.Space(10);GUILayout.Label(refusal,notice);}
            if(!string.IsNullOrEmpty(message)){GUILayout.Space(8);GUILayout.Label(message,text);}
            GUILayout.Space(15);bool enabled=GUI.enabled;GUI.enabled=enabled&&eligible&&validQuantity&&quote!=null;
            bool execute=GUILayout.Button(direction==TradeDirection.Buy?"Купить":"Продать",primary,GUILayout.Height(43));GUI.enabled=enabled;
            bool refresh=GUILayout.Button("Обновить предложение",button,GUILayout.Height(34));
            ItemListPresentation.DrawComparison(item,bag,trader.Profile,null,heading,muted,trader);
            GUILayout.EndScrollView();GUILayout.EndArea();
            if(execute)
            {
                var submitted=quote;var operationSession=session;bool success=TradeService.TryExecute(submitted,out var reason);
                if(operationSession!=null)operationSession.ReportOperationResult(success);
                ClearSelection(success?(direction==TradeDirection.Buy?"Покупка выполнена.":"Продажа выполнена."):reason);
            }
            else if(refresh)RefreshQuote(true);
        }
        private int ParsedQuantity()=>int.TryParse(quantityText,NumberStyles.None,CultureInfo.InvariantCulture,out int n)&&n>0?n:1;
        private string EconomyCaption()
        {
            string schedule="Пополнение выключено";
            if(trader.RestockEnabled)
            {
                double remaining=trader.GetRestockRemainingSeconds(trader.Profile);
                if(!double.IsNaN(remaining)&&!double.IsInfinity(remaining)&&remaining>=0&&remaining<=1e12)
                {
                    long seconds=(long)Math.Ceiling(remaining);
                    schedule="Пополнение через "+(seconds/60)+":"+(seconds%60).ToString("00")+" игрового времени";
                }
                else schedule="Пополнение временно недоступно";
            }
            return schedule+"  ·  Наценка +"+trader.SaleMarkupPercent+"%  ·  Скидка "+trader.SaleDiscountPercent+"%";
        }
        private static void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        private static Texture2D Solid(Color color){var t=new Texture2D(1,1){hideFlags=HideFlags.HideAndDontSave};t.SetPixel(0,0,color);t.Apply();return t;}
        private static GUIStyle Label(int size,Color color,bool bold=false)=>new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,wordWrap=true,normal={textColor=color},padding=new RectOffset(0,0,2,2)};
        private void EnsureStyles()
        {
            if(title!=null)return;
            title=Label(25,Ink,true);heading=Label(16,Ink,true);text=Label(17,Ink);muted=Label(15,new Color(.65f,.73f,.78f));price=Label(19,Accent,true);notice=Label(16,new Color(1,.74f,.45f));
            rowTexture=Solid(new Color(.09f,.12f,.145f));hoverTexture=Solid(new Color(.13f,.19f,.22f));selectedTexture=Solid(new Color(.1f,.28f,.27f));buttonTexture=Solid(new Color(.17f,.23f,.27f));primaryTexture=Solid(new Color(.13f,.42f,.34f));
            row=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,fontSize=17,wordWrap=true,padding=new RectOffset(12,12,10,10),normal={background=rowTexture,textColor=Ink},hover={background=hoverTexture,textColor=Color.white},active={background=selectedTexture,textColor=Color.white},focused={background=hoverTexture,textColor=Ink}};
            selectedRow=new GUIStyle(row);selectedRow.normal.background=selectedTexture;
            button=new GUIStyle(GUI.skin.button){fontSize=16,padding=new RectOffset(9,9,5,5),normal={background=buttonTexture,textColor=Ink},hover={background=hoverTexture,textColor=Color.white},active={background=selectedTexture,textColor=Color.white}};
            primary=new GUIStyle(button){fontStyle=FontStyle.Bold,fontSize=18};primary.normal.background=primaryTexture;
            input=new GUIStyle(GUI.skin.textField){fontSize=18,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(6,6,5,5)};
        }
        public void Dispose()
        {
            Reset();foreach(var texture in new[]{rowTexture,hoverTexture,selectedTexture,buttonTexture,primaryTexture})if(texture!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}
            title=null;rowTexture=hoverTexture=selectedTexture=buttonTexture=primaryTexture=null;
        }
    }
}
