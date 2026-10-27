using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdvancedCableTool.Editor
{
    public sealed class CableToolWindow : EditorWindow
    {
        CableType type; CableWear wear = CableWear.Used;
        float diameter = .035f, slack = 1, length = 8, wind = 1, damping = 1, stiffness = .9f, tiling = 5;
        Vector3 gravity = new Vector3(0,-9.81f,0), windDirection = Vector3.right;
        int segments = 64, parallel = 1; float spacing = .12f, randomSlack = .2f;
        bool physicsEnabled, collision = true, drawing, reattach, intermediate, previewPhysics;
        CableGenerator pending, settingsSource;
        Vector2 scroll;
        double lastTick;
        string feedback;
        UnityEditor.Editor selectionEditor;
        readonly Vector3[] previewPoints = new Vector3[65];
        readonly List<CableAnchor> draftSupports = new List<CableAnchor>();
        CableGenerator draftCable;
        [MenuItem("Tools/Environment/Cable Tool")]
        public static void Open() { GetWindow<CableToolWindow>("Advanced Cable Tool").Show(); }
        public void BeginDrawing() { drawing = true; pending = null; draftSupports.Clear(); draftCable = null; }
        public void SetBundle(int count, float separation, float variation)
        { parallel = Mathf.Clamp(count, 1, 16); spacing = separation; randomSlack = Mathf.Max(0, variation); }
        public void BeginIntermediate()
        { if (draftCable != Selected) { draftSupports.Clear(); draftCable = Selected; } intermediate = true; reattach = drawing = false; }
        public void BeginReattach() { reattach = true; intermediate = drawing = false; }
        void OnEnable() { minSize = new Vector2(420, 540); SceneView.duringSceneGui += SceneGUI; Undo.undoRedoPerformed += UndoChanged; EditorApplication.update += Tick; Selection.selectionChanged += Repaint; }
        void OnDisable()
        {
            SceneView.duringSceneGui -= SceneGUI; Undo.undoRedoPerformed -= UndoChanged; EditorApplication.update -= Tick; Selection.selectionChanged -= Repaint;
            StopPreview(); if (selectionEditor) DestroyImmediate(selectionEditor);
        }
        CableGenerator Selected => Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<CableGenerator>() : null;
        void UndoChanged()
        {
            foreach (var cable in FindObjectsByType<CableGenerator>()) cable.Rebuild();
            Repaint(); SceneView.RepaintAll();
        }
        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (previewPhysics && !Application.isPlaying && now - lastTick > 1.0 / 50)
            {
                var c = Selected;
                if (c) { c.editorSimulation = true; c.PreviewStep(.02f); SceneView.RepaintAll(); }
                lastTick = now;
            }
        }
        void StopPreview()
        {
            previewPhysics = false;
            foreach (var c in FindObjectsByType<CableGenerator>())
                if (c.editorSimulation) { c.editorSimulation = false; c.Rebuild(); }
        }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Advanced Cable Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Рисование: первый клик создаёт свисающий кабель, второй закрепляет B. Esc оставляет свободный конец. Опоры следуют Transform поверхности. Alt — навигация.", MessageType.Info);
            type = (CableType)EditorGUILayout.EnumPopup("Cable Type", type);
            wear = (CableWear)EditorGUILayout.EnumPopup("Wear Level", wear);
            diameter = Mathf.Max(.002f, EditorGUILayout.FloatField("Cable Diameter, м", diameter));
            length = Mathf.Max(.05f, EditorGUILayout.FloatField("Cable Length, м", length));
            slack = Mathf.Max(0, EditorGUILayout.FloatField("Slack при создании, м", slack));
            gravity = EditorGUILayout.Vector3Field("Gravity", gravity);
            wind = Mathf.Max(0, EditorGUILayout.FloatField("Wind Strength", wind));
            windDirection = EditorGUILayout.Vector3Field("Wind Direction", windDirection);
            stiffness = EditorGUILayout.Slider("Stiffness", stiffness, 0, 1);
            damping = EditorGUILayout.Slider("Damping", damping, 0, 10);
            segments = EditorGUILayout.IntSlider("Segment Count", segments, 8, 256);
            physicsEnabled = EditorGUILayout.Toggle("Physics Enabled", physicsEnabled);
            collision = EditorGUILayout.Toggle("Collision Enabled", collision);
            tiling = Mathf.Max(.01f, EditorGUILayout.FloatField("UV Tiling / метр", tiling));
            parallel = EditorGUILayout.IntSlider("Параллельных проводов", parallel, 1, 16);
            spacing = EditorGUILayout.FloatField("Шаг пучка, м", spacing);
            randomSlack = EditorGUILayout.Slider("Разброс провисания, м", randomSlack, 0, 2);
            bool nextDrawing = GUILayout.Toggle(drawing, "Прокладывать кабель в Scene View", "Button");
            if (nextDrawing != drawing) { drawing = nextDrawing; pending = null; draftSupports.Clear(); draftCable = null; reattach = intermediate = false; SceneView.RepaintAll(); }
            CableGenerator selected = Selected;
            if (!string.IsNullOrEmpty(feedback)) EditorGUILayout.HelpBox(feedback, MessageType.Info);
            if (selected)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Выбран: " + selected.name, EditorStyles.boldLabel);
                if (GUILayout.Button("Следующим кликом закрепить B")) { reattach = true; drawing = false; intermediate = false; }
                if (GUILayout.Button("Следующим кликом добавить промежуточную опору")) BeginIntermediate();
                if (draftSupports.Count > 0 && GUILayout.Button("Отменить промежуточные точки предпросмотра")) { draftSupports.Clear(); intermediate = false; }
                if (GUILayout.Button("Применить настройки окна"))
                { var before = CableEditing.Capture(selected); Undo.RecordObject(selected, "Cable settings"); Apply(selected); CableEditing.Commit(selected, before, out feedback); }
                settingsSource = (CableGenerator)EditorGUILayout.ObjectField("Копировать настройки из", settingsSource, typeof(CableGenerator), true);
                if (settingsSource && GUILayout.Button("Копировать настройки"))
                { var before = CableEditing.Capture(selected); Undo.RecordObject(selected, "Copy cable settings"); selected.CopySettingsFrom(settingsSource); CableEditing.Commit(selected, before, out feedback); }
                bool preview = GUILayout.Toggle(previewPhysics, "Физический предпросмотр выбранного", "Button");
                if (preview != previewPhysics)
                {
                    if (preview) { Undo.RecordObject(selected, "Cable simulation preview"); selected.CommitShape(); selected.physicsEnabled = true; selected.editorSimulation = true; selected.Rebuild(); previewPhysics = true; }
                    else StopPreview();
                }
                if (GUILayout.Button("Зафиксировать текущую форму / Static"))
                {
                    Undo.RecordObject(selected, "Freeze cable"); selected.CommitShape(); selected.physicsEnabled = false;
                    selected.editorSimulation = false; previewPhysics = false; selected.Rebuild(); EditorUtility.SetDirty(selected);
                }
                if (GUILayout.Button("Сохранить Prefab с Mesh-ассетами")) SavePrefab(selected);
                if (GUILayout.Button("Соединить B первого с A второго (выделите 2)")) JoinSelection();
                if (GUILayout.Button("Объединить выбранные статические кабели")) BatchSelection();
                UnityEditor.Editor.CreateCachedEditor(selected, null, ref selectionEditor);
                selectionEditor.OnInspectorGUI();
            }
            EditorGUILayout.EndScrollView();
        }
        void Apply(CableGenerator c)
        {
            c.cableType = type; c.wearLevel = wear; c.diameter = diameter; c.cableLength = length;
            c.slack = slack; c.gravity = gravity; c.windStrength = wind; c.windDirection = windDirection;
            c.stiffness = stiffness; c.damping = damping; c.segmentCount = segments; c.physicsEnabled = physicsEnabled;
            c.collisionEnabled = collision; c.uvTiling = tiling;
            c.material = LoadMaterial(type, wear);
            c.materialLibrary = AssetDatabase.LoadAssetAtPath<CableMaterialLibrary>("Assets/AdvancedCableTool/Materials/CableLibrary.asset");
        }
        public static Material LoadMaterial(CableType t, CableWear w) => AssetDatabase.LoadAssetAtPath<Material>("Assets/AdvancedCableTool/Materials/" + t + "_" + w + ".mat");
        public static CableGenerator CreateCable(Vector3 a, Transform targetA, Vector3? b, Transform targetB,
            float length, CableType type = CableType.Electric, CableWear wear = CableWear.Used)
        {
            var go = new GameObject("Cable " + type); Undo.RegisterCreatedObjectUndo(go, "Create cable");
            go.transform.position = a;
            var cable = go.AddComponent<CableGenerator>();
            cable.anchors.Clear(); var first = new CableAnchor(); first.Attach(a, targetA); cable.anchors.Add(first);
            var last = new CableAnchor(); if (b.HasValue) last.Attach(b.Value, targetB); else { last.attached = false; last.worldPoint = a + Vector3.down * length; }
            cable.anchors.Add(last); cable.cableLength = length; cable.cableType = type; cable.wearLevel = wear;
            cable.material = LoadMaterial(type, wear); cable.Rebuild();
            cable.materialLibrary = AssetDatabase.LoadAssetAtPath<CableMaterialLibrary>("Assets/AdvancedCableTool/Materials/CableLibrary.asset"); cable.Rebuild();
            EditorSceneManager.MarkSceneDirty(go.scene); return cable;
        }
        public bool PlaceHit(RaycastHit hit)
        {
            Vector3 p = hit.point + hit.normal * (diameter * .5f + .015f);
            Transform support = CableSurfaceQuery.SourceTransform(hit);
            if (reattach && Selected)
            {
                var c = Selected; var before = CableEditing.Capture(c); Undo.RecordObject(c, "Attach cable B"); c.AttachEndB(p, support);
                bool valid = CableEditing.Commit(c, before, out feedback); if (valid) reattach = false; return valid;
            }
            if (intermediate && Selected)
            {
                var c = Selected;
                if (draftCable != c) { draftSupports.Clear(); draftCable = c; }
                var before = CableEditing.Capture(c); Undo.RecordObject(c, "Add cable support"); var a = new CableAnchor(); a.Attach(p, support);
                draftSupports.Add(a);
                foreach (var anchor in draftSupports)
                { var copy = new CableAnchor(); copy.Attach(anchor.Resolve(), anchor.target); c.anchors.Insert(Mathf.Max(1, c.anchors.Count - 1), copy); }
                bool valid = CableEditing.Commit(c, before, out feedback);
                if (valid) { intermediate = false; draftSupports.Clear(); }
                else feedback += " Точка оставлена в предпросмотре: добавьте следующую опору для обхода стены.";
                return valid;
            }
            if (!drawing) return false;
            if (!pending)
            {
                pending = CreateCable(p, support, null, null, length, type, wear); Apply(pending); pending.Rebuild();
                bool valid = pending.ValidatePlacement(); feedback = valid ? null : pending.Status;
                if (!valid || !CableSurfaceQuery.Validate(pending, out feedback))
                {
                    if (string.IsNullOrEmpty(feedback)) feedback = pending.Status;
                    Undo.DestroyObjectImmediate(pending.gameObject); pending = null; return false;
                }
                Selection.activeGameObject = pending.gameObject;
            }
            else
            {
                Vector3 start = pending.anchors[0].Resolve(); Transform supportA = pending.anchors[0].target;
                var before = CableEditing.Capture(pending);
                Undo.RecordObject(pending, "Attach cable B");
                pending.cableLength = Mathf.Max(length, Vector3.Distance(start, p) + slack);
                pending.AttachEndB(p, support);
                if (!CableEditing.Commit(pending, before, out feedback))
                {
                    feedback += " Предыдущая форма восстановлена. Выберите другую точку B или промежуточную опору.";
                    return false;
                }
                else feedback = "Кабель создан. Длину и опоры можно редактировать.";
                Vector3 lateral = Vector3.Cross((p - start).normalized, Vector3.up).normalized;
                // Offset a facade bundle along its mounting surface, not into the wall.
                lateral = Vector3.ProjectOnPlane(lateral, hit.normal).normalized;
                if (lateral.sqrMagnitude < .001f) lateral = Vector3.Cross(hit.normal, (p - start).normalized).normalized;
                for (int i = 1; i < parallel; ++i)
                {
                    Vector3 offset = lateral * spacing * i;
                    float jitter = Mathf.Sin(i * 17.731f) * randomSlack;
                    var c = CreateCable(start + offset, supportA, p + offset, support, pending.cableLength + Mathf.Abs(jitter), type, wear);
                    c.CopySettingsFrom(pending); c.cableLength = pending.cableLength + Mathf.Abs(jitter); c.Rebuild();
                    if (!c.ValidatePlacement() || !CableSurfaceQuery.Validate(c, out _))
                    { Undo.DestroyObjectImmediate(c.gameObject); feedback = "Часть пучка пересекает препятствие и не создана. Измените шаг или опоры."; }
                }
                EditorUtility.SetDirty(pending); pending = null;
            }
            return true;
        }
        void SceneGUI(SceneView view)
        {
            if (Application.isPlaying) return;
            if (!drawing && !reattach && !intermediate) return;
            Event e = Event.current;
            if (e.keyCode == KeyCode.Escape && e.type == EventType.KeyDown) { pending = null; draftSupports.Clear(); drawing = reattach = intermediate = false; e.Use(); Repaint(); return; }
            if (e.alt) return;
            Handles.color = Color.yellow;
            foreach (var anchor in draftSupports) { var point = anchor.Resolve(); Handles.DrawWireCube(point, Vector3.one * .12f); Handles.Label(point, "Промежуточная опора · предпросмотр"); }
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!CableSurfaceQuery.Raycast(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ray, out RaycastHit hit)) return;
            Vector3 p = hit.point + hit.normal * (diameter * .5f + .015f);
            Handles.color = Color.cyan; Handles.DrawWireDisc(p, hit.normal, .08f);
            if (pending)
            {
                Vector3 a = pending.anchors[0].Resolve();
                float l = Mathf.Max(length, Vector3.Distance(a, p) + slack);
                for (int i = 0; i <= 64; ++i) previewPoints[i] = CableCatenary.Point(a, p, l, gravity, i / 64f);
                bool clear = !collision || CableSurfaceQuery.ValidatePath(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), previewPoints,
                    diameter * .535f, ~(1 << 2), out _);
                Handles.color = clear ? Color.cyan : Color.red;
                for (int i = 1; i <= 64; ++i)
                    Handles.DrawLine(previewPoints[i - 1], previewPoints[i], 3);
                Handles.Label(p, clear ? "B · " + l.ToString("F2") + " м" : "Стена на пути — добавьте промежуточную опору");
            }
            if (e.type == EventType.MouseDown && e.button == 0) { PlaceHit(hit); e.Use(); Repaint(); }
            if (e.type == EventType.MouseMove) view.Repaint();
        }
        public static void SavePrefab(CableGenerator c)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save cable prefab", c.name, "prefab", "Выберите файл Prefab");
            if (string.IsNullOrEmpty(path)) return;
            SavePrefabAt(c, path);
        }
        public static void SavePrefabAt(CableGenerator c, string path)
        {
            c.CommitShape();
            string folder = Path.ChangeExtension(path, null) + "_Meshes";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var clone = Instantiate(c.gameObject); var generator = clone.GetComponent<CableGenerator>();
            generator.enabled = false;
            // Anchors bound to scene objects become self-contained world anchors in a prefab.
            foreach (var a in generator.anchors) { Vector3 p = a.Resolve(); bool attached = a.attached; a.Attach(p, generator.transform); a.attached = attached; }
            generator.transform.position = c.transform.position;
            int i = 0;
            foreach (var mf in clone.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                Mesh mesh = Instantiate(mf.sharedMesh); mesh.hideFlags = HideFlags.None;
                string meshPath = folder + "/Mesh_" + i++ + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing) { EditorUtility.CopySerialized(mesh, existing); DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                mf.sharedMesh = mesh;
            }
            generator.enabled = true;
            PrefabUtility.SaveAsPrefabAsset(clone, path); DestroyImmediate(clone); AssetDatabase.SaveAssets();
        }
        public static void JoinSelection()
        {
            var list = new List<CableGenerator>();
            foreach (var go in Selection.gameObjects) { var c = go.GetComponent<CableGenerator>(); if (c) list.Add(c); }
            if (list.Count != 2) return;
            var junction = new GameObject("Cable junction"); junction.transform.position = list[0].anchors[list[0].anchors.Count - 1].Resolve();
            Undo.RegisterCreatedObjectUndo(junction, "Cable junction");
            foreach (var c in list) Undo.RecordObject(c, "Join cables");
            list[0].AttachEndB(junction.transform.position, junction.transform);
            list[1].anchors[0].Attach(junction.transform.position, junction.transform);
            foreach (var c in list) { c.Rebuild(); EditorUtility.SetDirty(c); }
        }
        static void BatchSelection()
        {
            string path = EditorUtility.SaveFilePanelInProject("Static batch", "CableBatch", "asset", "Сохранить объединённый меш");
            if (string.IsNullOrEmpty(path)) return;
            BatchSelectionAt(path);
        }
        public static void BatchSelectionAt(string path)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>(); var sources = new List<CableGenerator>();
            foreach (var go in Selection.gameObjects)
            {
                var c = go.GetComponent<CableGenerator>(); if (!c || c.physicsEnabled || !c.material) continue;
                c.Rebuild();
                foreach (var mf in c.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.name.StartsWith("Cable_LOD") && mf.name != "Cable_LOD0") continue;
                    var renderer = mf.GetComponent<MeshRenderer>(); if (!renderer || !mf.sharedMesh) continue;
                    Material[] slots = renderer.sharedMaterials;
                    for (int sub = 0; sub < mf.sharedMesh.subMeshCount; ++sub)
                    {
                        Material mat = slots[Mathf.Min(sub, slots.Length - 1)]; if (!mat) continue;
                        if (!byMaterial.ContainsKey(mat)) byMaterial.Add(mat, new List<CombineInstance>());
                        byMaterial[mat].Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sub, transform = mf.transform.localToWorldMatrix });
                    }
                }
                sources.Add(c);
            }
            if (sources.Count == 0) return;
            int undoGroup = Undo.GetCurrentGroup(); var root = new GameObject("Static Cable Batch"); Undo.RegisterCreatedObjectUndo(root, "Batch cables");
            int index = 0;
            foreach (var pair in byMaterial)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray());
                AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, null) + "_" + index++ + ".asset"));
                var child = new GameObject(pair.Key.name); child.transform.SetParent(root.transform);
                child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>().sharedMaterial = pair.Key; child.isStatic = true;
            }
            foreach (var c in sources) { Undo.RecordObject(c.gameObject, "Disable batched original"); c.gameObject.SetActive(false); }
            Undo.CollapseUndoOperations(undoGroup); AssetDatabase.SaveAssets(); Selection.activeGameObject = root;
        }
    }
}
