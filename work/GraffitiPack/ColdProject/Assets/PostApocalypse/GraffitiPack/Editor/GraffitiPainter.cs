using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PostApocalypse.Graffiti.Editor
{
    public sealed class GraffitiPainter : EditorWindow
    {
        public const string Root = "Assets/PostApocalypse/GraffitiPack";
        static readonly string[] Categories = { "Все", "Warnings", "Survivors", "Factions", "ZombieArt", "StreetArt", "Military2022" };
        GraffitiEntry[] entries = Array.Empty<GraffitiEntry>();
        int selected, category, seed = 17, stroke;
        bool painting, randomize, allowPartial, forceQuad;
        float width = 1.5f, opacity = 1, rotation, sizeSpread = .15f, angleSpread = 8, alphaSpread = .1f, offset = .003f;
        int layers = Physics.DefaultRaycastLayers;
        Vector2 scroll;
        GameObject ghost;
        Material ghostMaterial;
        [MenuItem("Tools/Graffiti Painter")]
        public static void Open() => GetWindow<GraffitiPainter>("Graffiti Painter");
        void OnEnable() { Reload(); SceneView.duringSceneGui += DuringSceneGUI; }
        void OnDisable() { SceneView.duringSceneGui -= DuringSceneGUI; ClearGhost(); }
        void Reload()
        {
            entries = AssetDatabase.FindAssets("t:GraffitiEntry", new[]{Root}).Select(x => AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(x))).Where(x=>x).OrderBy(x=>x.number).ToArray();
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Length-1));
        }
        void ClearGhost()
        {
            if (ghost) DestroyImmediate(ghost);
            if (ghostMaterial) DestroyImmediate(ghostMaterial);
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Graffiti Painter", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pipeline: " + GraffitiPipeline.Current + (GraffitiPipeline.HasDecals ? " · native decals доступны" : " · Lit Quad (decal feature отсутствует)"));
            EditorGUILayout.HelpBox("ЛКМ — поставить; Alt — навигация; Esc — выключить кисть. Стены определяются по видимому мешу, Collider и Static не обязательны. Красные точки показывают проёмы/рельеф и блокируют установку, пока не разрешены частичные опоры.", MessageType.Info);
            painting = GUILayout.Toggle(painting, "Размещение в Scene View", "Button");
            forceQuad=EditorGUILayout.Toggle("Всегда использовать Lit Quad",forceQuad);
            category = EditorGUILayout.Popup("Категория", category, Categories.Select(x=>x=="Military2022"?"Военная тематика · 2022":x).ToArray());
            var layerNames = Enumerable.Range(0,32).Where(i=>!string.IsNullOrEmpty(LayerMask.LayerToName(i))).ToArray();
            int compact=0; for(int i=0;i<layerNames.Length;i++) if((layers & (1<<layerNames[i]))!=0) compact|=1<<i;
            compact=EditorGUILayout.MaskField(new GUIContent("Слои стен", "Raycast только по выбранным слоям. Персонажи, анимированные и физические тела исключены дополнительно."),compact,layerNames.Select(LayerMask.LayerToName).ToArray());
            layers=0; for(int i=0;i<layerNames.Length;i++) if((compact&(1<<i))!=0) layers|=1<<layerNames[i];
            width=EditorGUILayout.Slider(new GUIContent("Ширина, м"),width,.3f,6);
            opacity=EditorGUILayout.Slider("Непрозрачность",opacity,.05f,1);
            rotation=EditorGUILayout.Slider("Поворот, °",rotation,-180,180);
            offset=EditorGUILayout.Slider("Отступ, м",offset,.001f,.02f);
            randomize=EditorGUILayout.Toggle("Случайный выбор в категории",randomize);
            seed=EditorGUILayout.IntField("Seed",seed);
            if(GUILayout.Button("Повторить последовательность")) stroke=0;
            sizeSpread=EditorGUILayout.Slider("Разброс размера, доля",sizeSpread,0,.5f);
            angleSpread=EditorGUILayout.Slider("Разброс поворота, °",angleSpread,0,90);
            alphaSpread=EditorGUILayout.Slider("Разброс прозрачности",alphaSpread,0,.5f);
            allowPartial=EditorGUILayout.Toggle("Разрешить проёмы/рельеф",allowPartial);
            if(GUILayout.Button("Обновить каталог / поверхности")) {Reload();GraffitiSurfaceQuery.Invalidate();}
            if(GUILayout.Button("Удалить граффити инструмента в активной сцене")) DeletePainted();
            scroll=EditorGUILayout.BeginScrollView(scroll);
            int cols=Mathf.Max(1,(int)(position.width/155)); int col=0;
            foreach(var entry in entries)
            {
                if(category>0 && entry.category!=Categories[category]) continue;
                if(col==0) EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.Width(145));
                var rect=GUILayoutUtility.GetRect(140,85);
                EditorGUI.DrawRect(rect,new Color(.24f,.24f,.24f));
                GUI.DrawTexture(rect,entry.texture,ScaleMode.ScaleToFit,true);
                if(GUI.Button(rect,GUIContent.none,GUIStyle.none)) { selected=Array.IndexOf(entries,entry); width=entry.width; stroke=0; Repaint(); }
                GUILayout.Label((Array.IndexOf(entries,entry)==selected?"▶ ":"")+entry.number.ToString("00")+" "+entry.title,EditorStyles.wordWrappedLabel,GUILayout.Width(140));
                GUILayout.Label($"{entry.width:F2} × {entry.width/entry.Aspect:F2} м · {entry.category}",EditorStyles.miniLabel,GUILayout.Width(140));
                EditorGUILayout.EndVertical(); col++;
                if(col>=cols) {EditorGUILayout.EndHorizontal();col=0;}
            }
            if(col!=0) EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            if(!painting) ClearGhost();
        }
        public static bool Eligible(Collider c, int mask)
        {
            if (!c || c.isTrigger || (mask & (1<<c.gameObject.layer))==0) return false;
            if(c.attachedRigidbody || c.GetComponentInParent<CharacterController>() || c.GetComponentInParent<Animator>() || c.GetComponentInParent<GraffitiPlacement>()) return false;
            if(c.GetComponentInParent<SkinnedMeshRenderer>()) return false;
            return true;
        }
        public static bool Hit(Ray ray,int mask,out GraffitiSurfaceHit result)
        {
            return GraffitiSurfaceQuery.Hit(ray,mask,out result);
        }
        public static Quaternion SurfaceRotation(Vector3 normal,float degrees)
        {
            Vector3 up=Vector3.ProjectOnPlane(Vector3.up,normal);
            if(up.sqrMagnitude<.001f) up=Vector3.ProjectOnPlane(Vector3.forward,normal);
            // Unity Quad faces local -Z.
            return Quaternion.LookRotation(-normal,up.normalized)*Quaternion.Euler(0,0,degrees);
        }
        public static int Unsupported(Vector3 center,Quaternion rot,float w,float h,int mask,bool draw)
        {
            int bad=0;var n=rot*Vector3.back;
            for(int y=0;y<7;y++) for(int x=0;x<7;x++)
            {
                var p=center+rot*new Vector3((x/6f-.5f)*w,(y/6f-.5f)*h,0);
                bool valid=Hit(new Ray(p+n*.08f,-n),mask,out var hit)&&Mathf.Abs(Vector3.Dot(hit.point-p,n))<.035f&&Vector3.Dot(hit.normal,n)>.94f;
                if(!valid)bad++;
                if(draw) {Handles.color=valid?new Color(0,1,0,.5f):Color.red;Handles.DotHandleCap(0,p,Quaternion.identity,HandleUtility.GetHandleSize(p)*.025f,EventType.Repaint);}
            }
            return bad;
        }
        void DuringSceneGUI(SceneView view)
        {
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape) {painting=false;ClearGhost();Repaint();return;}
            if(!painting||entries.Length==0||EditorApplication.isPlayingOrWillChangePlaymode||PrefabStageUtility.GetCurrentPrefabStage()!=null) {ClearGhost();return;}
            if(e.alt) {if(ghost)ghost.SetActive(false);return;}
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            if(!Hit(HandleUtility.GUIPointToWorldRay(e.mousePosition),layers,out var hit))
            {
                if(ghost)ghost.SetActive(false);
                Handles.BeginGUI();GUI.Label(new Rect(12,view.position.height-75,700,30),"Нет доступной поверхности. Проверьте «Слои стен», видимость меша и отсутствие Animator/Rigidbody.");Handles.EndGUI();
                if(e.type==EventType.MouseMove)view.Repaint();return;
            }
            var rng=new System.Random(unchecked(seed*397^stroke));
            var pool=entries.Where(x=>category==0||x.category==Categories[category]).ToArray();
            if(pool.Length==0)return;
            var entry=randomize?pool[rng.Next(pool.Length)]:entries[selected];
            float Next(float spread)=>(float)(rng.NextDouble()*2-1)*spread;
            float w=Mathf.Max(.1f,width*(1+Next(sizeSpread))), a=Mathf.Clamp01(opacity+Next(alphaSpread));
            var rot=SurfaceRotation(hit.normal,rotation+Next(angleSpread));var p=hit.point+hit.normal*offset;
            if(!ghost||ghost.name!=entry.name)
            {
                ClearGhost();ghost=Instantiate(entry.prefab);ghost.name=entry.name;
                ghost.hideFlags=HideFlags.HideAndDontSave;
                ghostMaterial=new Material(entry.prefab.GetComponent<Renderer>().sharedMaterial);
                ghost.GetComponent<Renderer>().sharedMaterial=ghostMaterial;
            }
            ghost.SetActive(true);ghost.transform.SetPositionAndRotation(p,rot);ghost.transform.localScale=new Vector3(w,w/entry.Aspect,1);
            ghost.GetComponent<GraffitiPlacement>().opacity=a;ghost.GetComponent<GraffitiPlacement>().ApplyOpacity();
            int bad=Unsupported(hit.point,rot,w,w/entry.Aspect,layers,e.type==EventType.Repaint);
            Handles.BeginGUI();GUI.Label(new Rect(12,view.position.height-75,600,30),$"{entry.number:00} {entry.title} · {w:F2} м · опоры {(49-bad)}/49"+(bad>0?" · ПРОЁМ/РЕЛЬЕФ":""));Handles.EndGUI();
            if(e.type==EventType.MouseDown&&e.button==0)
            {
                if(bad==0||allowPartial) {Place(entry,p,rot,w,a,hit.transform,!forceQuad);stroke++;}
                e.Use();
            }
            if(e.type==EventType.MouseMove) view.Repaint();
        }
        public static GameObject Place(GraffitiEntry entry,Vector3 position,Quaternion rotation,float width,float opacity,Transform surface,bool preferProjector=true)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(entry.prefab,surface.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go,"Нанести граффити");
            // Keep world proportions: transformed/nonuniform wall parents must not shear the quad.
            go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=new Vector3(width,width/entry.Aspect,1);
            var placement=go.GetComponent<GraffitiPlacement>();placement.paintedByTool=true;placement.opacity=opacity;placement.ApplyOpacity();
            if(preferProjector) GraffitiPipeline.Project(go,entry,width,opacity,true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(placement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<Renderer>());
            EditorUtility.SetDirty(placement);EditorSceneManager.MarkSceneDirty(go.scene);
            return go;
        }
        public static void DeletePainted()
        {
            int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Удалить граффити инструмента");
            foreach(var p in Resources.FindObjectsOfTypeAll<GraffitiPlacement>())
                if(p&&p.paintedByTool&&!EditorUtility.IsPersistent(p)&&p.gameObject.scene==SceneManager.GetActiveScene()&&(p.hideFlags&HideFlags.DontSave)==0) Undo.DestroyObjectImmediate(p.gameObject);
            Undo.CollapseUndoOperations(group);
        }
    }
}
