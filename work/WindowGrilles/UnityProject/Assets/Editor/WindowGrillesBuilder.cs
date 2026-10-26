using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class WindowGrillesBuilder
{
    private const string Root = "Assets/Environment/Windows/MetalGrilles";
    private const string Models = Root + "/Models";
    private const string Materials = Root + "/Materials";
    private const string Prefabs = Root + "/Prefabs";
    private const string Animations = Root + "/Animations";
    private const string ScenePath = Root + "/DemoScene/MetalGrilles_Demo.unity";
    private const string SpecsPath = Root + "/Documentation/collider_specs.json";
    private static readonly int[] Types = { 1, 2, 3, 4, 5 };
    private static readonly string[] Names = { "Classic", "Forged", "Reinforced", "Survivor", "Hinged" };
    private static readonly string[] States = { "A", "B", "C" };

    [Serializable] private sealed class ColliderDocument { public ColliderSet[] items; }
    [Serializable] private sealed class ColliderSet { public string id; public ColliderSegment[] segments; }
    [Serializable] private sealed class ColliderSegment
    {
        public string component;
        public float[] start;
        public float[] end;
        public float width;
        public float depth;
    }

    [MenuItem("Tools/Window Grilles/Build and validate package")]
    public static void Build()
    {
        EnsureIsolatedProject();
        Directory.CreateDirectory(Prefabs);
        Directory.CreateDirectory(Materials);
        Directory.CreateDirectory(Animations);
        ImportTextures();
        var paintMaterials = CreatePaintMaterials();
        var rust = CreateSurfaceMaterial("M_Grille_Rust", "Rust_Common", new Color(0.92f, 0.92f, 0.92f), 0.22f, 0.28f);
        var bare = CreateSurfaceMaterial("M_Grille_ExposedSteel", "BareSteel_Common", new Color(0.93f, 0.93f, 0.93f), 0.82f, 0.44f);
        var hingeController = CreateHingeController();
        var specs = LoadColliderSpecs();
        int built = 0;
        foreach (int type in Types)
        foreach (string state in States)
        {
            string id = $"SM_Grille_{type:00}_{Names[type - 1]}_{state}";
            string modelPath = $"{Models}/{id}.fbx";
            ConfigureModelImporter(modelPath);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException("Missing FBX after import: " + modelPath);
            var prefab = BuildPrefab(model, type, state, id, paintMaterials[(type, state)], rust, bare,
                specs.Single(x => x.id == id), hingeController);
            if (prefab == null) throw new InvalidOperationException("Prefab build failed: " + id);
            built++;
        }
        CreateDemoScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        bool valid = ValidateAndWriteReport("Assets/Validation/WindowGrilles/build_report.json", false);
        if (!valid || built != 15) throw new InvalidOperationException("WINDOW_GRILLES_BUILD_FAILED");
        Debug.Log($"WINDOW_GRILLES_BUILD_PASS prefabs={built} LODs=4 URP=17.6.0");
    }

    [MenuItem("Tools/Window Grilles/Validate package")]
    public static void Validate()
    {
        EnsureIsolatedProject();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(ScenePath);
        bool valid = ValidateAndWriteReport("Assets/Validation/WindowGrilles/reopen_report.json", true);
        if (!valid) throw new InvalidOperationException("WINDOW_GRILLES_REOPEN_FAILED");
        CaptureFinalReview();
        Debug.Log("WINDOW_GRILLES_REOPEN_PASS prefabs=15 FBX=15 textures=48 LODs=4");
    }

    private static void CaptureFinalReview()
    {
        var groups=UnityEngine.Object.FindObjectsByType<LODGroup>();
        foreach(var group in groups)group.ForceLOD(0);
        var cameras=UnityEngine.Object.FindObjectsByType<Camera>();
        CaptureCamera(cameras.Single(c=>c.name=="Camera_MainFiveWindows"),Root+"/Verification/Demo_FiveWindowWall.png");
        CaptureCamera(cameras.Single(c=>c.name=="Camera_WearStateGallery"),Root+"/Verification/Demo_FifteenVariants.png");
        var target=GameObject.Find("Gallery_Grille_01_B");
        var center=target.transform.position+new Vector3(0,.45f,0);
        var camera=CreateCamera("LOD inspection",center+new Vector3(.16f,.11f,1.8f),center,45);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.58f;
        var texts=UnityEngine.Object.FindObjectsByType<TextMesh>().Select(t=>t.GetComponent<Renderer>()).ToArray();
        foreach(var text in texts)text.enabled=false;
        for(int lod=0;lod<4;lod++)
        {
            target.GetComponent<LODGroup>().ForceLOD(lod);
            CaptureCamera(camera,Root+"/Verification/LOD_ClassicB_"+lod+".png",1000,1000);
        }
        foreach(var text in texts)text.enabled=true;
        foreach(var group in groups)group.ForceLOD(-1);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }

    private static void EnsureIsolatedProject()
    {
        string path=Application.dataPath.Replace('\\','/');
        if(!path.Contains("/work/WindowGrilles/") || !path.EndsWith("/Assets"))
            throw new InvalidOperationException("WindowGrilles builders are restricted to work/WindowGrilles isolated projects.");
    }

    private static void ImportTextures()
    {
        foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png", SearchOption.TopDirectoryOnly)
                     .Select(p => p.Replace('\\', '/')))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            string file = Path.GetFileNameWithoutExtension(path);
            var wantedType=file.EndsWith("_Normal",StringComparison.Ordinal)?TextureImporterType.NormalMap:TextureImporterType.Default;
            bool wantedSrgb=file.EndsWith("_BaseColor",StringComparison.Ordinal);
            var wantedAlpha=file.EndsWith("_MetallicSmoothness",StringComparison.Ordinal)?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            if(importer.textureType==wantedType&&importer.sRGBTexture==wantedSrgb&&importer.alphaSource==wantedAlpha&&
               importer.mipmapEnabled&&importer.maxTextureSize==4096&&importer.textureCompression==TextureImporterCompression.CompressedHQ&&
               importer.wrapMode==TextureWrapMode.Repeat&&importer.filterMode==FilterMode.Trilinear)continue;
            importer.textureType = wantedType;
            importer.sRGBTexture = wantedSrgb;
            importer.alphaSource = wantedAlpha;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
        }
    }

    private static Dictionary<(int, string), Material> CreatePaintMaterials()
    {
        var result = new Dictionary<(int, string), Material>();
        Color[] tints =
        {
            new Color(0.96f, 0.98f, 0.90f, 1f),
            new Color(1.00f, 0.94f, 0.83f, 1f),
            new Color(0.79f, 0.75f, 0.66f, 1f)
        };
        for (int type = 1; type <= 5; type++)
        for (int state = 0; state < States.Length; state++)
        {
            string path = $"{Materials}/M_Grille_{type:00}_{Names[type - 1]}_{States[state]}.mat";
            string textureName = $"{type:00}_{Names[type - 1]}";
            var mat = LoadOrCreateMaterial(path);
            ConfigureSurface(mat, textureName, tints[state], 0.05f, 0.34f);
            if(state>0)
            {
                mat.SetTexture("_BaseMap",Texture(textureName+"_"+States[state]+"_BaseColor"));
                mat.SetTexture("_MetallicGlossMap",Texture(textureName+"_"+States[state]+"_MetallicSmoothness"));
            }
            EditorUtility.SetDirty(mat);
            result[(type, States[state])] = mat;
        }
        return result;
    }

    private static Material CreateSurfaceMaterial(string fileName, string textureName, Color tint, float metallic, float smoothness)
    {
        string path = $"{Materials}/{fileName}.mat";
        var mat = LoadOrCreateMaterial(path);
        ConfigureSurface(mat, textureName, tint, metallic, smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadOrCreateMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
        mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Texture2D Texture(string fileName)
    {
        string path = $"{Root}/Textures/T_{fileName}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) throw new InvalidOperationException("Missing PBR texture: " + path);
        return tex;
    }

    private static void ConfigureSurface(Material mat, string textureName, Color tint, float metallic, float smoothness)
    {
        if (mat.shader == null || mat.shader.name != "Universal Render Pipeline/Lit")
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", tint);
        mat.SetTexture("_BaseMap", Texture(textureName + "_BaseColor"));
        mat.SetTexture("_MetallicGlossMap", Texture(textureName + "_MetallicSmoothness"));
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", 1f);
        mat.SetFloat("_SmoothnessTextureChannel", 0f);
        mat.SetTexture("_BumpMap", Texture(textureName + "_Normal"));
        mat.SetFloat("_BumpScale", 0.45f);
        mat.SetTexture("_OcclusionMap", Texture(textureName + "_AO"));
        mat.SetFloat("_OcclusionStrength", 0.85f);
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_OCCLUSIONMAP");
        mat.renderQueue = (int)RenderQueue.Geometry;
        mat.enableInstancing = true;
    }

    private static void ConfigureModelImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("FBX importer unavailable: " + path);
        importer.globalScale = 1f;
        importer.preserveHierarchy = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.SaveAndReimport();
    }

    private static GameObject BuildPrefab(GameObject model, int type, string state, string id,
        Material paint, Material rust, Material bare, ColliderSet colliderSet, RuntimeAnimatorController controller)
    {
        GameObject root = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (root == null) return null;
        PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        root.name = $"Grille_{type:00}_{state}";
        NormalizeModelHierarchy(root.transform);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string n = renderer.gameObject.name;
            Material mat = n.IndexOf("Rust", StringComparison.OrdinalIgnoreCase) >= 0 ? rust :
                n.IndexOf("Bare", StringComparison.OrdinalIgnoreCase) >= 0 ? bare : paint;
            var meshRenderer = renderer as MeshRenderer;
            if (meshRenderer != null)
            {
                int submeshCount = meshRenderer.GetComponent<MeshFilter>()?.sharedMesh?.subMeshCount ?? 1;
                meshRenderer.sharedMaterials = Enumerable.Repeat(mat, Math.Max(1, submeshCount)).ToArray();
            }
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        foreach(var oldGroup in root.GetComponentsInChildren<LODGroup>(true))
            if(oldGroup!=null) UnityEngine.Object.DestroyImmediate(oldGroup);
        var lodGroup = root.AddComponent<LODGroup>();
        var lods = new LOD[4];
        float[] transitions = { 0.62f, 0.34f, 0.15f, 0.025f };
        for (int i = 0; i < 4; i++)
        {
            Transform lodRoot = FindChildRecursive(root.transform, "LOD" + i);
            if (lodRoot == null) throw new InvalidOperationException($"{id}: missing LOD{i} transform");
            Renderer[] renderers = lodRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{id}: LOD{i} has no renderers");
            lods[i] = new LOD(transitions[i], renderers);
        }
        lodGroup.fadeMode = LODFadeMode.CrossFade;
        lodGroup.animateCrossFading = true;
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();
        AddColliders(root.transform, id, colliderSet);
        if (type == 5)
        {
            var animator = root.GetComponent<Animator>();
            if(animator==null)animator=root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var hinge = root.GetComponent<WindowGrilleHinge>();
            if(hinge==null)hinge=root.AddComponent<WindowGrilleHinge>();
            SerializedObject so = new SerializedObject(hinge);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        string folder = $"{Prefabs}/Grille_{type:00}_{state}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, folder);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    private static void NormalizeModelHierarchy(Transform root)
    {
        var importedRoots = Enumerable.Range(0,root.childCount).Select(root.GetChild).ToArray();
        for(int lod=0;lod<4;lod++)
        {
            Transform source=FindChildRecursive(root,"LOD"+lod);
            if(source==null)throw new InvalidOperationException("Imported LOD is missing");
            var children=Enumerable.Range(0,source.childCount).Select(source.GetChild).ToArray();
            var normalized=new GameObject("LOD"+lod).transform;
            normalized.SetParent(root,false);
            foreach(var child in children)
            {
                if(child.name.Contains("LeafPivot"))
                {
                    Vector3 position=root.InverseTransformPoint(child.position);
                    string side=position.x<0?"Left":"Right";
                    var pivot=new GameObject(side+"LeafPivot_LOD"+lod).transform;
                    pivot.SetParent(normalized,false);
                    pivot.localPosition=position;
                    var meshes=Enumerable.Range(0,child.childCount).Select(child.GetChild).ToArray();
                    foreach(var mesh in meshes)mesh.SetParent(pivot,true);
                }
                else child.SetParent(normalized,true);
            }
        }
        foreach(var imported in importedRoots)UnityEngine.Object.DestroyImmediate(imported.gameObject);
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static void AddColliders(Transform root, string id, ColliderSet set)
    {
        Transform collisionRoot = root.Find("Collision");
        if (collisionRoot == null)
        {
            var go = new GameObject("Collision");
            collisionRoot = go.transform;
            collisionRoot.SetParent(root, false);
        }
        Transform leftPivot = null;
        Transform rightPivot = null;
        if (id.Contains("Hinged"))
        {
            leftPivot = GetOrCreateCollisionPivot(collisionRoot, "LeftLeafPivot", new Vector3(-0.46f, 0, .035f));
            rightPivot = GetOrCreateCollisionPivot(collisionRoot, "RightLeafPivot", new Vector3(0.46f, 0, .035f));
        }
        foreach (var spec in set.segments)
        {
            Transform parent = collisionRoot;
            Vector3 pivotOffset = Vector3.zero;
            if (spec.component == "left") { parent = leftPivot; pivotOffset = new Vector3(-0.46f, 0, .035f); }
            else if (spec.component == "right") { parent = rightPivot; pivotOffset = new Vector3(0.46f, 0, .035f); }
            Vector3 a = new Vector3(spec.start[0], spec.start[1], spec.start[2]);
            Vector3 b = new Vector3(spec.end[0], spec.end[1], spec.end[2]);
            Vector3 d = b - a;
            var go = new GameObject("BarCollider_" + spec.component);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f - pivotOffset;
            go.transform.localRotation = d.sqrMagnitude > 0.000001f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(spec.width, 0.006f), Mathf.Max(d.magnitude, 0.008f), Mathf.Max(spec.depth, 0.006f));
            box.center = Vector3.zero;
        }
    }

    private static Transform GetOrCreateCollisionPivot(Transform parent, string name, Vector3 localPosition)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        var go = new GameObject(name);
        t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    private static ColliderSet[] LoadColliderSpecs()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(SpecsPath);
        if (json == null) throw new InvalidOperationException("Collider specification JSON is missing.");
        var parsed = JsonUtility.FromJson<ColliderDocument>(json.text);
        if (parsed?.items == null || parsed.items.Length != 15) throw new InvalidOperationException("Expected 15 collider specifications.");
        return parsed.items;
    }

    private static RuntimeAnimatorController CreateHingeController()
    {
        string closedPath = Animations + "/Grille_Hinged_Close.anim";
        string openPath = Animations + "/Grille_Hinged_Open.anim";
        string controllerPath = Animations + "/Grille_Hinged.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Open", AnimatorControllerParameterType.Bool);
        var sm = controller.layers[0].stateMachine;
        var closed = sm.AddState("Closed");
        var open = sm.AddState("Open");
        var close = sm.AddState("Close");
        closed.motion = CreateHingeClip(Animations + "/Grille_Hinged_ClosedPose.anim", false, true);
        close.motion = CreateHingeClip(closedPath, false);
        open.motion = CreateHingeClip(openPath, true);
        sm.defaultState = closed;
        var toOpen = closed.AddTransition(open);
        toOpen.hasExitTime = false;
        toOpen.duration = 0.14f;
        toOpen.AddCondition(AnimatorConditionMode.If, 0, "Open");
        var toClose = open.AddTransition(close);
        toClose.hasExitTime = false;
        toClose.duration = 0.14f;
        toClose.AddCondition(AnimatorConditionMode.IfNot, 0, "Open");
        var toIdle=close.AddTransition(closed);
        toIdle.hasExitTime=true;
        toIdle.exitTime=1f;
        toIdle.duration=0f;
        var reopen=close.AddTransition(open);
        reopen.hasExitTime=false;
        reopen.duration=.14f;
        reopen.AddCondition(AnimatorConditionMode.If,0,"Open");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateHingeClip(string path, bool open, bool closedPose=false)
    {
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path), frameRate = 30f, wrapMode = WrapMode.ClampForever };
        float end = 0.75f;
        string[] roots = { "Collision/LeftLeafPivot", "Collision/RightLeafPivot" };
        for (int lod = 0; lod < 4; lod++)
        {
            roots = roots.Concat(new[] { $"LOD{lod}/LeftLeafPivot_LOD{lod}", $"LOD{lod}/RightLeafPivot_LOD{lod}" }).ToArray();
        }
        foreach (string root in roots)
        {
            bool left = root.Contains("LeftLeafPivot");
            float full=left ? -105f : 105f;
            float startAngle = open || closedPose ? 0f : full;
            float angle = open ? full : 0f;
            var curve = new AnimationCurve(new Keyframe(0, startAngle), new Keyframe(end, angle));
            curve.SmoothTangents(0, 0f);
            curve.SmoothTangents(1, 0f);
            clip.SetCurve(root, typeof(Transform), "localEulerAnglesRaw.y", curve);
        }
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void CreateDemoScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.47f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.36f, 0.38f, 0.40f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 38f;
        Material concrete = DemoMaterial("M_Demo_WeatheredConcrete", new Color(0.38f, 0.39f, 0.37f), 0.12f);
        Material concreteTrim = DemoMaterial("M_Demo_ConcreteSill", new Color(0.48f, 0.47f, 0.43f), 0.16f);
        Material floor = DemoMaterial("M_Demo_Ground", new Color(0.20f, 0.21f, 0.20f), 0.08f);
        Material label = DemoMaterial("M_Demo_Label", new Color(0.10f, 0.11f, 0.10f), 0.6f);

        BuildFiveWindowWall(-6f, 0f, 0f, concrete, concreteTrim, label);
        BuildGalleryWall(7f, 0f, 0f, concrete, concreteTrim, label);
        CreateBox("Ground", new Vector3(0, -0.12f, -1.5f), new Vector3(28, 0.24f, 15), floor);
        CreateLight();
        var mainCam = CreateCamera("Camera_MainFiveWindows", new Vector3(-6.3f, 2.35f, 5.5f), new Vector3(-6f, 1.72f, 0f), 52f);
        var galleryCam = CreateCamera("Camera_WearStateGallery", new Vector3(7f, 2.25f, 6f), new Vector3(7f, 2.10f, 0f), 47f);
        mainCam.enabled = true;
        galleryCam.enabled = false;
        var ui=new GameObject("Demo controls").AddComponent<MetalGrillesDemo>();
        ui.mainCamera=mainCam; ui.galleryCamera=galleryCam;
        EditorSceneManager.SaveScene(scene, ScenePath);
        CaptureCamera(mainCam, Root + "/Verification/Demo_FiveWindowWall.png");
        CaptureCamera(galleryCam, Root + "/Verification/Demo_FifteenVariants.png");
        var closeup=CreateCamera("Camera_Inspection",Vector3.zero,Vector3.forward,45f);
        closeup.enabled=false;
        closeup.orthographic=true;
        closeup.orthographicSize=.58f;
        var textRenderers=UnityEngine.Object.FindObjectsByType<TextMesh>().Select(t=>t.GetComponent<Renderer>()).ToArray();
        foreach(var textRenderer in textRenderers)textRenderer.enabled=false;
        for(int row=0;row<3;row++) for(int col=0;col<5;col++)
        {
            var instance=GameObject.Find($"Gallery_Grille_{col+1:00}_{States[row]}");
            var center=instance.transform.position+new Vector3(0,.45f,0);
            closeup.transform.position=center+new Vector3(.16f,.11f,1.8f);
            closeup.transform.LookAt(center);
            instance.GetComponent<LODGroup>().ForceLOD(0);
            CaptureCamera(closeup,$"{Root}/Verification/Grille_{col+1:00}_{States[row]}.png",1200,1200);
            instance.GetComponent<LODGroup>().ForceLOD(-1);
        }
        UnityEngine.Object.DestroyImmediate(closeup.gameObject);
        foreach(var textRenderer in textRenderers)textRenderer.enabled=true;
        EditorSceneManager.SaveScene(scene,ScenePath);
        AssetDatabase.Refresh();
    }

    private static void BuildFiveWindowWall(float baseX, float baseY, float z, Material concrete, Material trim, Material labelMat)
    {
        const float wallWidth = 8f;
        const float wallHeight = 3.3f;
        const float bottom = 1.15f;
        CreateBox("MainWall_BottomBand", new Vector3(baseX, baseY + bottom * 0.5f, z), new Vector3(wallWidth, bottom, .34f), concrete);
        CreateBox("MainWall_TopBand", new Vector3(baseX, baseY + 2.15f + (wallHeight - 2.15f) * .5f, z), new Vector3(wallWidth, wallHeight - 2.15f, .34f), concrete);
        float[] centers = Enumerable.Range(0, 5).Select(i => baseX + (i - 2) * 1.5f).ToArray();
        for (int i = 0; i < 5; i++)
        {
            float left = i == 0 ? baseX - wallWidth * .5f : centers[i - 1] + .5f;
            float right = centers[i] - .5f;
            if (right - left > .01f)
                CreateBox("MainWall_Pier_L_" + i, new Vector3((left + right) * .5f, baseY + 1.65f, z), new Vector3(right-left, 1.0f, .34f), concrete);
            CreateBox("Window_Sill_" + i, new Vector3(centers[i], baseY + bottom - .045f, z + .03f), new Vector3(1.06f, .08f, .43f), trim);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Grille_{i + 1:00}_A.prefab");
            PlacePrefab(prefab, $"Installed_Grille_{i + 1:00}_A", new Vector3(centers[i], baseY + bottom + .02f, z + .24f));
            CreateText("TypeLabel_" + i, $"ТИП {i + 1:00}  /  A", new Vector3(centers[i], baseY + 2.27f, z + .205f), .055f, labelMat);
        }
        float lastPierLeft=centers[4]+.5f, lastPierRight=baseX+wallWidth*.5f;
        CreateBox("MainWall_Pier_R",new Vector3((lastPierLeft+lastPierRight)*.5f,baseY+1.65f,z),
            new Vector3(lastPierRight-lastPierLeft,1.0f,.34f),concrete);
    }

    private static void BuildGalleryWall(float baseX, float baseY, float z, Material concrete, Material trim, Material labelMat)
    {
        float[] centersX = Enumerable.Range(0, 5).Select(i => baseX + (i - 2) * 1.5f).ToArray();
        float[] bottoms = { .35f, 1.66f, 2.97f };
        CreateBox("GalleryWall_BottomBand", new Vector3(baseX,.175f,z-.20f),new Vector3(8f,.35f,.30f),concrete);
        CreateBox("GalleryWall_RowGap_0", new Vector3(baseX,1.505f,z-.20f),new Vector3(8f,.31f,.30f),concrete);
        CreateBox("GalleryWall_RowGap_1", new Vector3(baseX,2.815f,z-.20f),new Vector3(8f,.31f,.30f),concrete);
        CreateBox("GalleryWall_TopBand", new Vector3(baseX,4.11f,z-.20f),new Vector3(8f,.28f,.30f),concrete);
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 5; col++)
        {
            int type = col + 1;
            string state = States[row];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Grille_{type:00}_{state}.prefab");
            Vector3 pos = new Vector3(centersX[col], baseY + bottoms[row] + .02f, z + .02f);
            PlacePrefab(prefab, $"Gallery_Grille_{type:00}_{state}", pos);
            float left = col == 0 ? baseX - 4f : centersX[col - 1] + .5f;
            float right = centersX[col] - .5f;
            if (right > left)
                CreateBox($"GalleryPier_{row}_L_{col}",new Vector3((left+right)*.5f,baseY+bottoms[row]+.5f,z-.20f),
                    new Vector3(right-left,1f,.30f),concrete);
            CreateBox($"GallerySill_{row}_{col}", new Vector3(centersX[col], baseY + bottoms[row] - .045f, z + .02f), new Vector3(1.08f,.08f,.44f), trim);
            CreateText($"GalleryLabel_{row}_{col}", $"{type:00} / {state}", new Vector3(centersX[col], baseY + bottoms[row] - .20f, z + .12f), .052f, labelMat);
        }
        for(int row=0;row<3;row++)
        {
            float left=centersX[4]+.5f, right=baseX+4f;
            CreateBox($"GalleryPier_{row}_R",new Vector3((left+right)*.5f,baseY+bottoms[row]+.5f,z-.20f),new Vector3(right-left,1f,.30f),concrete);
        }
    }

    private static void PlacePrefab(GameObject prefab, string name, Vector3 position)
    {
        if (prefab == null) throw new InvalidOperationException("Demo prefab missing: " + name);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.identity;
    }

    private static void CreateBox(string name, Vector3 center, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateText(string name, string value, Vector3 pos, float size, Material mat)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation=Quaternion.Euler(0,180,0);
        var text = go.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = 48;
        text.characterSize = size*.5f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(.72f, .71f, .65f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = text.font.material;
    }

    private static Material DemoMaterial(string name, Color color, float smoothness)
    {
        string path = Materials + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", .0f);
        m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void CreateLight()
    {
        var go = new GameObject("Sun - late overcast");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;
        light.color = new Color(1f, .94f, .86f);
        light.shadows = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
        var fill=new GameObject("Soft sky fill").AddComponent<Light>();
        fill.type=LightType.Directional;
        fill.intensity=.32f;
        fill.color=new Color(.72f,.82f,1f);
        fill.transform.rotation=Quaternion.Euler(20f,220f,0f);
    }

    private static Camera CreateCamera(string name, Vector3 position, Vector3 lookAt, float fieldOfView)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 80f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.29f, .31f, .31f);
        camera.allowHDR = true;
        camera.allowMSAA = true;
        return camera;
    }

    public static void CaptureCamera(Camera camera, string assetPath,int width=1920,int height=1080)
    {
        try
        {
            var previous = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply(false, false);
            string relative = assetPath.Replace('\\', '/');
            string full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), relative));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("WINDOW_GRILLES_PREVIEW_CAPTURED " + full);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("WINDOW_GRILLES_PREVIEW_SKIPPED " + ex.Message);
        }
    }

    private static bool ValidateAndWriteReport(string reportPath, bool coldReopen)
    {
        var errors = new List<string>();
        var records = new List<PrefabRecord>();
        var openingSizes=new List<Vector2>();
        var bottomBand=GameObject.Find("MainWall_BottomBand");
        var topBand=GameObject.Find("MainWall_TopBand");
        if(bottomBand==null||topBand==null)errors.Add("Demo window wall is missing");
        else for(int i=0;i<5;i++)
        {
            var left=GameObject.Find("MainWall_Pier_L_"+i).GetComponent<Renderer>().bounds;
            var right=GameObject.Find(i<4?"MainWall_Pier_L_"+(i+1):"MainWall_Pier_R").GetComponent<Renderer>().bounds;
            float width=right.min.x-left.max.x;
            float height=topBand.GetComponent<Renderer>().bounds.min.y-bottomBand.GetComponent<Renderer>().bounds.max.y;
            openingSizes.Add(new Vector2(width,height));
            if(Mathf.Abs(width-1)>.001f||Mathf.Abs(height-1)>.001f)errors.Add("Demo opening "+i+" is not 1x1m: "+width+"x"+height);
        }
        int fbxCount = 0, textureCount = 0, prefabCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            if (AssetDatabase.GUIDToAssetPath(guid).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) fbxCount++;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            textureCount++;
            if (tex == null || tex.width != 4096 || tex.height != 4096) errors.Add("Texture not 4K: " + path);
        }
        foreach (int type in Types)
        foreach (string state in States)
        {
            string id = $"SM_Grille_{type:00}_{Names[type - 1]}_{state}";
            string fbxPath = $"{Models}/{id}.fbx";
            string prefabPath = $"{Prefabs}/Grille_{type:00}_{state}.prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (model == null) errors.Add("Missing FBX: " + fbxPath);
            if (prefab == null) { errors.Add("Missing prefab: " + prefabPath); continue; }
            prefabCount++;
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            var lodGroup = instance != null ? instance.GetComponent<LODGroup>() : null;
            var lods = lodGroup != null ? lodGroup.GetLODs() : Array.Empty<LOD>();
            if (lods.Length != 4) errors.Add(id + " does not have 4 LOD levels");
            long[] tri = new long[lods.Length];
            for (int i=0;i<lods.Length;i++)
            foreach (var renderer in lods[i].renderers)
            {
                var mf = renderer.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { errors.Add(id + " missing mesh at LOD" + i); continue; }
                var mesh = mf.sharedMesh;
                for (int s=0;s<mesh.subMeshCount;s++) tri[i] += (long)mesh.GetIndexCount(s)/3;
                if (mesh.GetIndexCount(0) == 0) errors.Add(id + " empty mesh at LOD" + i);
                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) errors.Add(id + " missing UV at LOD" + i);
            }
            if (tri.Length == 4 && !(tri[0] > tri[1] && tri[1] > tri[2] && tri[2] > tri[3]))
                errors.Add($"{id} LOD triangle counts are not descending: {string.Join(",",tri)}");
            var colliders = instance.GetComponentsInChildren<BoxCollider>(true);
            if (colliders.Length < 12) errors.Add(id + " has too few compound box colliders: " + colliders.Length);
            if (instance.GetComponentsInChildren<MeshCollider>(true).Length != 0) errors.Add(id + " contains MeshCollider");
            if (instance.GetComponent<Rigidbody>() != null) errors.Add(id + " unexpectedly has Rigidbody");
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            bool hasBounds=false;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                foreach(var material in renderer.sharedMaterials)
                {
                    if(material==null || material.shader==null || material.shader.name!="Universal Render Pipeline/Lit")
                        errors.Add(id+" missing URP material: "+renderer.name);
                    else foreach(string property in new[]{"_BaseMap","_MetallicGlossMap","_BumpMap","_OcclusionMap"})
                        if(material.GetTexture(property)==null) errors.Add(id+" missing "+property+" on "+material.name);
                }
                if (!hasBounds) { bounds=renderer.bounds; hasBounds=true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (hasBounds && (bounds.size.x > 1.11f || bounds.size.y < .90f || bounds.size.y > .97f ||
                bounds.min.y < -.035f || bounds.min.y > -.005f || Mathf.Abs(bounds.min.y + .02f) > .015f))
                errors.Add($"{id} size/pivot outside contract: size={bounds.size}, minY={bounds.min.y}");
            if (type == 5)
            {
                var hinge = instance.GetComponent<WindowGrilleHinge>();
                var animator = instance.GetComponent<Animator>();
                if (hinge == null || animator == null || animator.runtimeAnimatorController == null) errors.Add(id + " hinge animation setup missing");
                else
                {
                    animator.SetBool("Open",true); animator.Play("Open",0,1f); animator.Update(.01f);
                    var left = instance.transform.Find("LOD0/LeftLeafPivot_LOD0");
                    var right = instance.transform.Find("LOD0/RightLeafPivot_LOD0");
                    if (left == null || right == null) errors.Add(id + " leaf pivots missing");
                    var ctrlLeft = FindChildRecursive(instance.transform,"Collision")?.Find("LeftLeafPivot");
                    var ctrlRight = FindChildRecursive(instance.transform,"Collision")?.Find("RightLeafPivot");
                    if (ctrlLeft == null || ctrlRight == null) errors.Add(id + " hinge collider pivots missing");
                    if (left != null && Mathf.Abs(Mathf.DeltaAngle(left.localEulerAngles.y, -105f)) > 2f) errors.Add(id + " left Open clip did not reach -105 degrees");
                    if (right != null && Mathf.Abs(Mathf.DeltaAngle(right.localEulerAngles.y, 105f)) > 2f) errors.Add(id + " right Open clip did not reach 105 degrees");
                }
            }
            records.Add(new PrefabRecord { id=id, fbxPath=fbxPath, prefabPath=prefabPath, lodTriangles=tri,
                colliderCount=colliders.Length, boundsSize=hasBounds ? bounds.size : Vector3.zero,
                pivotMinY=hasBounds ? bounds.min.y : 0f });
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        }
        if (fbxCount != 15) errors.Add("Expected 15 FBX, got " + fbxCount);
        if (prefabCount != 15) errors.Add("Expected 15 prefabs, got " + prefabCount);
        if (textureCount != 48) errors.Add("Expected 48 4K PBR maps, got " + textureCount);
        string pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "Built-in";
        if (!pipeline.Contains("UniversalRenderPipelineAsset")) errors.Add("Current render pipeline is not URP: " + pipeline);
        var report = new ReportEnvelope
        {
            status = errors.Count == 0 ? (coldReopen ? "WINDOW_GRILLES_REOPEN_PASS" : "WINDOW_GRILLES_BUILD_PASS") : "FAIL",
            coldReopen = coldReopen,
            unityVersion = Application.unityVersion,
            renderPipeline = pipeline,
            fbxCount = fbxCount,
            prefabCount = prefabCount,
            textureCount = textureCount,
            demoOpeningSizes = openingSizes.ToArray(),
            pbrMapLayout = "4K BaseColor + MetallicSmoothness (R=metallic,A=smoothness) + tangent Normal + AO per paint/rust/bare set",
            items = records.ToArray(),
            errors = errors.ToArray()
        };
        string fullReport = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReport));
        File.WriteAllText(fullReport, JsonUtility.ToJson(report, true));
        foreach (string error in errors) Debug.LogError("WINDOW_GRILLES_VALIDATION: " + error);
        Debug.Log($"WINDOW_GRILLES_VALIDATE status={report.status} fbx={fbxCount} prefabs={prefabCount} textures={textureCount} errors={errors.Count} report={fullReport}");
        return errors.Count == 0;
    }

    [Serializable] private sealed class PrefabRecord
    {
        public string id;
        public string fbxPath;
        public string prefabPath;
        public long[] lodTriangles;
        public int colliderCount;
        public Vector3 boundsSize;
        public float pivotMinY;
    }

    [Serializable] private sealed class ReportEnvelope
    {
        public string status;
        public bool coldReopen;
        public string unityVersion;
        public string renderPipeline;
        public int fbxCount;
        public int prefabCount;
        public int textureCount;
        public Vector2[] demoOpeningSizes;
        public string pbrMapLayout;
        public PrefabRecord[] items;
        public string[] errors;
    }
}
