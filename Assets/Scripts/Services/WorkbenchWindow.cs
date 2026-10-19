using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Inventory;

namespace ZombieSurvival.Services
{
    /// <summary>Presentation only: explicit quotes and confirmation over the common repair/session contracts.</summary>
    public sealed class WorkbenchWindow : IDisposable
    {
        private PlayerServiceSession session;
        private RepairWorkbench workbench;
        private ItemStorage storage;
        private ItemStack selected;
        private RepairQuote quote;
        private long sessionVersion;
        private RepairPolicy policy;
        private string policyStamp, feedback="";
        private float condition, maximum;
        private int rounds, quantity, filter;
        private string itemId, profileId;
        private ItemDefinition itemType;
        private bool repairable, executing;
        private Vector2 listScroll, detailsScroll;
        private readonly ItemListPresentation itemList=new ItemListPresentation();
        private GUIStyle title,heading,text,muted,value,notice,row,selectedRow,button,primary,activeButton;
        private Texture2D rowTexture,hoverTexture,selectedTexture,buttonTexture,primaryTexture;
        private static readonly Color Ink=new Color(.9f,.94f,.95f), Accent=new Color(.38f,.85f,.73f);
        public void Tick(PlayerServiceSession owner,RepairWorkbench target)
        {
            if(owner==null||!owner.IsOpen){Reset();return;}
            if(target==null||!target.isActiveAndEnabled){owner.Close();return;}
            if(session!=owner||workbench!=target||sessionVersion!=owner.SessionVersion)
            {
                Reset();session=owner;workbench=target;sessionVersion=owner.SessionVersion;storage=owner.GetComponent<ItemStorage>();
                if(storage!=null)storage.Changed+=InventoryChanged;
                policy=target.Policy;policyStamp=policy!=null?policy.ConfigurationStamp():null;
            }
            var currentPolicy=target.Policy;string stamp=currentPolicy!=null?currentPolicy.ConfigurationStamp():null;
            if(currentPolicy!=policy||stamp!=policyStamp){policy=currentPolicy;policyStamp=stamp;Invalidate("Правила ремонта изменились. Получите новое предложение.");}
            if(selected!=null)
            {
                if(storage==null||!storage.Contains(selected)||selected.IsEmpty){selected=null;quote=null;feedback="Вещь больше не находится в вашем рюкзаке.";return;}
                if(selected.InstanceId!=itemId||selected.Definition!=itemType||selected.Durability!=condition||selected.Definition.MaximumDurability!=maximum||selected.LoadedRounds!=rounds
                    ||selected.Quantity!=quantity||selected.Definition.Repairable!=repairable||selected.Definition.RepairProfileId!=profileId)
                {Invalidate("Состояние вещи изменилось. Получите новое предложение.");CaptureItem();}
            }
        }
        private void CaptureItem()
        {
            if(selected==null||selected.IsEmpty||selected.Definition==null)return;itemId=selected.InstanceId;itemType=selected.Definition;condition=selected.Durability;maximum=itemType.MaximumDurability;
            rounds=selected.LoadedRounds;quantity=selected.Quantity;repairable=itemType.Repairable;profileId=itemType.RepairProfileId;
        }
        private void Invalidate(string reason){quote=null;if(!executing)feedback=reason;}
        private void InventoryChanged(){Invalidate("Рюкзак изменился. Получите новое предложение.");CaptureItem();}
        private void Select(ItemStack item){selected=item;quote=null;feedback="";detailsScroll=Vector2.zero;CaptureItem();GUI.FocusControl(null);}
        public void Reset()
        {
            if(storage!=null)storage.Changed-=InventoryChanged;session=null;workbench=null;storage=null;selected=null;quote=null;policy=null;policyStamp=null;sessionVersion=0;
            feedback="";listScroll=detailsScroll=Vector2.zero;filter=0;itemList.Reset();executing=false;
        }
        public void Draw(PlayerServiceSession owner,RepairWorkbench target)
        {
            Tick(owner,target);if(session==null||!session.IsOpen||workbench==null)return;EnsureStyles();
            var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;bool oldEnabled=GUI.enabled;
            try
            {
                GUI.depth=-100;float scale=Mathf.Min(Screen.width/1280f,Screen.height/820f);if(scale<=0)return;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
                float width=Screen.width/scale,height=Screen.height/scale;Fill(new Rect(0,0,width,height),new Color(.008f,.012f,.018f,.88f));
                var panel=new Rect((width-Mathf.Min(1240,width-32))/2,16,Mathf.Min(1240,width-32),height-32);Fill(panel,new Color(.035f,.048f,.061f));Fill(new Rect(panel.x,panel.y,panel.width,3),Accent);
                GUI.Label(new Rect(panel.x+24,panel.y+17,panel.width-220,38),"ВЕРСТАК",title);
                GUI.Label(new Rect(panel.x+24,panel.y+59,panel.width-220,28),"Полный ремонт за материалы из вашего рюкзака",muted);
                if(GUI.Button(new Rect(panel.xMax-165,panel.y+24,140,38),"Закрыть  ×",button)){session.Close();return;}
                float leftWidth=(panel.width-62)*.4f,contentY=panel.y+106,contentH=panel.height-190;
                var left=new Rect(panel.x+24,contentY,leftWidth,contentH);var right=new Rect(left.xMax+14,contentY,panel.width-62-leftWidth,contentH);
                DrawList(left);DrawDetails(right);
                GUI.Label(new Rect(panel.x+24,panel.yMax-73,panel.width-48,43),string.IsNullOrEmpty(feedback)?"Выберите вещь → получите предложение → подтвердите ремонт. Рубли не расходуются.":feedback,string.IsNullOrEmpty(feedback)?muted:notice);
                GUI.Label(new Rect(panel.x+24,panel.yMax-29,panel.width-48,22),"Escape — закрыть. Мир продолжает работать.",muted);
            }
            finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;GUI.enabled=oldEnabled;}
        }
        private void DrawList(Rect area)
        {
            Fill(area,new Color(.065f,.083f,.101f));GUI.Label(new Rect(area.x+14,area.y+12,area.width-28,28),"ВАШИ ВЕЩИ",heading);
            GUILayout.BeginArea(new Rect(area.x+10,area.y+50,area.width-20,area.height-60));
            GUILayout.BeginHorizontal();string[] captions={"Все","Оружие","Снаряжение"};
            for(int i=0;i<captions.Length;i++)if(GUILayout.Button(captions[i],filter==i?activeButton:button,GUILayout.Height(34))){filter=i;listScroll=Vector2.zero;}
            GUILayout.EndHorizontal();GUILayout.Space(5);itemList.DrawControls(storage,false,button,ref listScroll,false);listScroll=GUILayout.BeginScrollView(listScroll);bool any=false;
            foreach(var item in itemList.Items(storage,null,TradeDirection.Sell,false,i=>i.Definition.HasDurability))
            {
                var category=item.Definition.Category;if(filter==1&&category!=ItemCategory.Weapon||filter==2&&category!=ItemCategory.Equipment)continue;any=true;
                float max=item.Definition.MaximumDurability,current=item.Durability;
                string state=current>=max?"Исправно":current<=0?"Сломано":"Повреждено";
                if(!item.Definition.Repairable)state="Ремонт не предусмотрен";
                string label=item.Definition.DisplayName+"\n"+item.Definition.CategoryLabel+" · "+current.ToString("0.#")+" / "+max.ToString("0.#")+"\n"+state;
                if(GUILayout.Button(label,selected==item?selectedRow:row,GUILayout.MinHeight(91)))Select(item);
                GUILayout.Space(5);
            }
            if(!any)GUILayout.Label(filter==0?"В рюкзаке нет вещей с прочностью.\nОружие и снаряжение появятся здесь после получения.":"В этой категории вещей пока нет.",muted);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void DrawDetails(Rect area)
        {
            Fill(area,new Color(.075f,.096f,.115f));GUILayout.BeginArea(new Rect(area.x+18,area.y+14,area.width-36,area.height-28));detailsScroll=GUILayout.BeginScrollView(detailsScroll);
            if(selected==null){GUILayout.Label("РЕМОНТ ПРЕДМЕТА",heading);GUILayout.Space(18);GUILayout.Label("Выберите конкретную вещь слева. Здесь появятся её состояние, нужные материалы и результат ремонта.",text);GUILayout.EndScrollView();GUILayout.EndArea();return;}
            var item=selected;var type=item.Definition;
            GUILayout.Label(type.DisplayName,title);GUILayout.Label(type.CategoryLabel+" · "+type.SubcategoryLabel,muted);GUILayout.Space(9);
            GUILayout.Label("Прочность: "+item.Durability.ToString("0.##")+" / "+type.MaximumDurability.ToString("0.##"),value);
            var bar=GUILayoutUtility.GetRect(1,23,GUILayout.ExpandWidth(true));Fill(bar,new Color(.025f,.04f,.05f));Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(item.Durability/type.MaximumDurability),bar.height),Accent);
            if(type.Weapon!=null&&type.Weapon.Ammo!=null)GUILayout.Label("Магазин: "+item.LoadedRounds+" / "+type.Weapon.MagazineSize+" патр. — сохраняется при ремонте",muted);
            if(!string.IsNullOrWhiteSpace(type.Description)){GUILayout.Space(8);GUILayout.Label(type.Description,muted);}
            GUILayout.Space(17);GUILayout.Label("МАТЕРИАЛЫ",heading);GUILayout.Space(8);
            bool preview=RepairService.TryPreviewCost(workbench.Policy,item,out var costs,out var previewReason);bool enough=preview;
            if(preview)
            {
                foreach(var cost in costs)
                {
                    int have=storage.Count(cost.Material),missing=Mathf.Max(0,cost.Amount-have);if(missing>0)enough=false;
                    GUILayout.Label(cost.Material.DisplayName,text);
                    GUILayout.Label("Нужно: "+cost.Amount+"   ·   Есть: "+have+(missing>0?"   ·   Не хватает: "+missing:"   ·   Достаточно"),missing>0?notice:muted);GUILayout.Space(5);
                }
                GUILayout.Space(10);GUILayout.Label("Результат: "+item.Durability.ToString("0.##")+" → "+type.MaximumDurability.ToString("0.##")+" (полное восстановление)",value);
                GUILayout.Label("Ремонт не меняет предмет, его магазин и назначение в хотбаре. Оплата только материалами.",muted);
            }
            else GUILayout.Label(previewReason,notice);
            bool available=false;string refusal="";
            if(preview&&enough)available=RepairService.TryQuote(session,workbench,item,out _,out refusal);
            if(preview&&!enough)refusal="Для полного ремонта не хватает материалов. Ничего не будет списано.";
            if(!string.IsNullOrEmpty(refusal)){GUILayout.Space(8);GUILayout.Label(refusal,notice);}
            GUILayout.Space(15);
            bool oldEnabled=GUI.enabled;GUI.enabled=oldEnabled&&available;
            bool request=GUILayout.Button(quote==null?"Получить предложение":"Обновить предложение",button,GUILayout.Height(38));
            GUI.enabled=oldEnabled&&available&&quote!=null;
            bool confirm=GUILayout.Button("Подтвердить ремонт",primary,GUILayout.Height(43));GUI.enabled=oldEnabled;
            if(quote!=null)GUILayout.Label("Предложение получено. При подтверждении будут списаны перечисленные материалы.",muted);
            else if(available)GUILayout.Label("Сначала получите предложение, затем подтвердите ремонт.",muted);
            GUILayout.Space(12);GUI.enabled=false;GUILayout.Button("Улучшить",button,GUILayout.Height(34));GUI.enabled=oldEnabled;
            GUILayout.Label("Улучшения пока недоступны",muted);
            ItemListPresentation.DrawComparison(item,storage,null,workbench.Policy,heading,muted);
            GUILayout.EndScrollView();GUILayout.EndArea();
            // Keep every GUI control present on each event; execute only the previously issued quote.
            if(confirm)
            {
                var submitted=quote;quote=null;executing=true;
                var operationSession=session;
                try{bool success=RepairService.TryExecute(submitted,out var reason);if(operationSession!=null)operationSession.ReportOperationResult(success);feedback=success?"Предмет полностью отремонтирован. Материалы списаны.":reason;CaptureItem();}
                finally{executing=false;}
            }
            else if(request){bool success=RepairService.TryQuote(session,workbench,item,out quote,out var reason);if(!success)session.ReportOperationResult(false);feedback=success?"Предложение получено. Проверьте расход и подтвердите ремонт.":reason;}
        }
        private static void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        private static Texture2D Solid(Color color){var texture=new Texture2D(1,1){hideFlags=HideFlags.HideAndDontSave};texture.SetPixel(0,0,color);texture.Apply();return texture;}
        private static GUIStyle Label(int size,Color color,bool bold=false)=>new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,wordWrap=true,normal={textColor=color},padding=new RectOffset(0,0,2,2)};
        private void EnsureStyles()
        {
            if(title!=null)return;title=Label(25,Ink,true);heading=Label(16,Ink,true);text=Label(17,Ink);muted=Label(15,new Color(.65f,.73f,.78f));value=Label(19,Accent,true);notice=Label(16,new Color(1,.74f,.45f));
            rowTexture=Solid(new Color(.09f,.12f,.145f));hoverTexture=Solid(new Color(.13f,.19f,.22f));selectedTexture=Solid(new Color(.1f,.28f,.27f));buttonTexture=Solid(new Color(.17f,.23f,.27f));primaryTexture=Solid(new Color(.13f,.42f,.34f));
            row=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,fontSize=17,wordWrap=true,padding=new RectOffset(12,12,10,10),normal={background=rowTexture,textColor=Ink},hover={background=hoverTexture,textColor=Color.white},active={background=selectedTexture,textColor=Color.white}};
            selectedRow=new GUIStyle(row);selectedRow.normal.background=selectedTexture;
            button=new GUIStyle(GUI.skin.button){fontSize=16,wordWrap=true,padding=new RectOffset(9,9,5,5),normal={background=buttonTexture,textColor=Ink},hover={background=hoverTexture,textColor=Color.white},active={background=selectedTexture,textColor=Color.white}};
            activeButton=new GUIStyle(button);activeButton.normal.background=selectedTexture;
            primary=new GUIStyle(button){fontSize=18,fontStyle=FontStyle.Bold};primary.normal.background=primaryTexture;
        }
        public void Dispose()
        {
            Reset();foreach(var texture in new[]{rowTexture,hoverTexture,selectedTexture,buttonTexture,primaryTexture})if(texture!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}
            title=null;rowTexture=hoverTexture=selectedTexture=buttonTexture=primaryTexture=null;
        }
    }
}
