using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PostApocalypse.Balconies;

namespace PostApocalypse.Balconies.Editor
{
    public static class BalconyKitBuilder
    {
        public const string Root = "Assets/Environment/PostApocalypse/Balconies";
        public const string Prefabs = Root + "/Prefabs";
        public const string Models = Root + "/Models/FBX";
        public const string Materials = Root + "/Materials";
        public const string Textures = Root + "/Textures";
        public const string DemoScene = Root + "/Demo/Balcony_Showcase.unity";

        static readonly string[] Types = {
            "Balcony_01_Soviet", "Balcony_02_Khrushchevka", "Balcony_03_GlazedSoviet",
            "Balcony_04_ModernLoggia", "Balcony_05_Wooden", "Balcony_06_Fortified",
            "Balcony_07_Industrial", "Balcony_08_Overgrown", "Balcony_09_Burned", "Balcony_10_Collapsed"
        };
        static readonly string[] States = { "A", "B", "C", "D" };

        sealed class MaterialSpec
        {
            public string Name;
            public string Family;
            public Color Tint;
            public float Metallic;
            public bool Transparent;
            public MaterialSpec(string name, string family, Color tint, float metallic = .0f, bool transparent = false)
            { Name = name; Family = family; Tint = tint; Metallic = metallic; Transparent = transparent; }
        }

        static readonly MaterialSpec[] SurfaceMaterials = {
            new MaterialSpec("M_Balcony_Concrete", "Concrete", new Color(.86f,.84f,.79f)),
            new MaterialSpec("M_Balcony_ConcreteDamaged", "Concrete", new Color(.70f,.67f,.61f)),
            new MaterialSpec("M_Balcony_PaintedSteel", "PaintedMetal", new Color(.78f,.77f,.70f), .48f),
            new MaterialSpec("M_Balcony_CorrodedSteel", "CorrodedMetal", new Color(.78f,.66f,.55f), .82f),
            new MaterialSpec("M_Balcony_Timber", "Timber", new Color(.85f,.76f,.64f)),
            new MaterialSpec("M_Balcony_RottenTimber", "Timber", new Color(.65f,.58f,.47f)),
            new MaterialSpec("M_Balcony_Glass", "Glazing", new Color(.72f,.82f,.81f,.48f), .0f, true),
            new MaterialSpec("M_Balcony_Aluminium", "PaintedMetal", new Color(.84f,.86f,.83f), .58f),
            new MaterialSpec("M_Balcony_Plastic", "PaintedMetal", new Color(.77f,.75f,.68f)),
            new MaterialSpec("M_Balcony_Canvas", "Organic", new Color(.79f,.73f,.62f)),
            new MaterialSpec("M_Balcony_Organic", "Organic", new Color(.77f,.78f,.65f)),
            new MaterialSpec("M_Balcony_Brick", "Concrete", new Color(.78f,.57f,.46f)),
            new MaterialSpec("M_Balcony_Burnt", "Timber", new Color(.42f,.39f,.35f)),
            new MaterialSpec("M_Balcony_Dirt", "Organic", new Color(.66f,.57f,.42f)),
            new MaterialSpec("M_Balcony_Ceramic", "Concrete", new Color(.79f,.74f,.64f))
        };

        static readonly Dictionary<string, string> MaterialBySource = new Dictionary<string, string> {
            {"BAL_Concrete","M_Balcony_Concrete"}, {"BAL_ConcreteDamage","M_Balcony_ConcreteDamaged"},
            {"BAL_PaintedSteel","M_Balcony_PaintedSteel"}, {"BAL_CorrodedSteel","M_Balcony_CorrodedSteel"},
            {"BAL_Timber","M_Balcony_Timber"}, {"BAL_RottenTimber","M_Balcony_RottenTimber"},
            {"BAL_Glass","M_Balcony_Glass"}, {"BAL_Aluminium","M_Balcony_Aluminium"},
            {"BAL_Plastic","M_Balcony_Plastic"}, {"BAL_Canvas","M_Balcony_Canvas"},
            {"BAL_Organic","M_Balcony_Organic"}, {"BAL_Brick","M_Balcony_Brick"},
            {"BAL_Burnt","M_Balcony_Burnt"}, {"BAL_Dirt","M_Balcony_Dirt"},
            {"BAL_Ceramic","M_Balcony_Ceramic"}
        };

        [MenuItem("Tools/Zombie Survival/Post-Apocalypse Balconies/Build Prefabs and Showcase")]
        public static void BuildPackage()
        {
            EnsureFolders();
            ConfigureTextures();
            ConfigureModelImporters();
            CreateMaterials();
            int built = 0;
            foreach (string type in Types)
            foreach (string state in States)
            {
                string name = type + "_" + state;
                string model = Models + "/Balconies/" + type + "/" + name + ".fbx";
                string prefab = Prefabs + "/" + type + "/" + name + ".prefab";
                BuildPrefab(name, model, prefab, type, state);
                built++;
            }

            foreach (string path in FindFbx("Props"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                BuildPrefab(name, path, Prefabs + "/Props/" + name + ".prefab", "Prop", "A");
            }
            foreach (string path in FindFbx("Accessories"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                BuildPrefab(name, path, Prefabs + "/Accessories/" + name + ".prefab", "Accessory", "A");
            }

            AssetDatabase.SaveAssets();
            BuildShowcase();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"BALCONY_KIT_BUILD_PASS {built} balcony prefabs; {FindFbx("Props").Count} props; {FindFbx("Accessories").Count} door accessories.");
        }

        public static void BuildAndValidate()
        {
            BuildPackage();
            BalconyKitValidation.Run();
        }

        static void EnsureFolders()
        {
            foreach (string p in new[] { Materials, Prefabs, Prefabs + "/Props", Prefabs + "/Accessories", Root + "/Demo" })
                EnsureAssetFolder(p);
        }

        static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void ConfigureTextures()
        {
            foreach (string family in new[] {"Concrete","PaintedMetal","CorrodedMetal","Timber","Glazing","Organic"})
            foreach (string kind in new[] {"BaseColor","Normal","Mask"})
            {
                string path = Textures + "/" + family + "_" + kind + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool changed = false;
                bool normal = kind == "Normal";
                bool linear = kind == "Mask" || normal;
                if (importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)) { importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; changed = true; }
                if (importer.sRGBTexture == linear) { importer.sRGBTexture = !linear; changed = true; }
                if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; changed = true; }
                if (importer.mipmapEnabled != true) { importer.mipmapEnabled = true; changed = true; }
                if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
        }

        static void ConfigureModelImporters()
        {
            var paths=AssetDatabase.FindAssets("t:Model",new[]{Models}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach(string path in paths)
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)continue;
                bool changed=false;
                if(importer.globalScale!=1f){importer.globalScale=1f;changed=true;}
                if(!importer.bakeAxisConversion){importer.bakeAxisConversion=true;changed=true;}
                if(importer.materialImportMode==ModelImporterMaterialImportMode.None){importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;changed=true;}
                if(importer.importCameras){importer.importCameras=false;changed=true;}
                if(importer.importLights){importer.importLights=false;changed=true;}
                if(importer.importAnimation){importer.importAnimation=false;changed=true;}
                if(importer.isReadable){importer.isReadable=false;changed=true;}
                if(!importer.generateSecondaryUV){importer.generateSecondaryUV=true;changed=true;}
                if(importer.meshCompression!=ModelImporterMeshCompression.Off){importer.meshCompression=ModelImporterMeshCompression.Off;changed=true;}
                if(changed)importer.SaveAndReimport();
            }
        }

        static void CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("URP Lit shader was not found.");
            foreach (var spec in SurfaceMaterials)
            {
                string path = Materials + "/" + spec.Name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = spec.Name };
                    AssetDatabase.CreateAsset(material, path);
                }
                else if (material.shader != shader) material.shader = shader;
                var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/" + spec.Family + "_BaseColor.png");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/" + spec.Family + "_Normal.png");
                var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/" + spec.Family + "_Mask.png");
                material.SetColor("_BaseColor", spec.Tint);
                material.enableInstancing = true;
                material.SetFloat("_Metallic", spec.Metallic);
                material.SetFloat("_Smoothness", 1f);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseMap);
                if (material.HasProperty("_BumpMap")) { material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .42f); }
                if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", mask);
                if (material.HasProperty("_OcclusionMap")) { material.SetTexture("_OcclusionMap", mask); material.SetFloat("_OcclusionStrength", .72f); }
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", spec.Transparent ? 1f : 0f);
                if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
                if (spec.Transparent)
                {
                    material.SetColor("_BaseColor", spec.Tint);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                {
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = (int)RenderQueue.Geometry;
                }
                if (normal != null) material.EnableKeyword("_NORMALMAP");
                if (mask != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(material);
            }
        }

        static List<string> FindFbx(string category)
        {
            string dir = Models + "/" + category;
            if (!AssetDatabase.IsValidFolder(dir)) return new List<string>();
            return AssetDatabase.FindAssets("t:Model", new[] {dir})
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        static void BuildPrefab(string name, string modelPath, string prefabPath, string type, string state)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new FileNotFoundException("Missing FBX model: " + modelPath);
            string directory = Path.GetDirectoryName(prefabPath).Replace('\\','/');
            EnsureAssetFolder(directory);
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) { UnityEngine.Object.DestroyImmediate(root); throw new InvalidOperationException("Could not instantiate FBX: " + modelPath); }
            instance.name = "Geometry";
            instance.transform.SetParent(root.transform, false);
            AssignMaterials(instance);
            var levels = new List<LOD>();
            for (int i = 0; i < 4; i++)
            {
                var group = FindNamed(instance.transform, "LOD" + i);
                if (group == null) { UnityEngine.Object.DestroyImmediate(root); throw new InvalidDataException("Missing LOD" + i + " in " + modelPath); }
                var renderers = group.GetComponentsInChildren<Renderer>(true).Where(r => r.name != "COLLISION").ToArray();
                if (renderers.Length == 0) { UnityEngine.Object.DestroyImmediate(root); throw new InvalidDataException("Empty LOD" + i + " in " + modelPath); }
                levels.Add(new LOD(new[] { .62f, .34f, .14f, .025f }[i], renderers));
            }
            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(levels.ToArray());
            lodGroup.RecalculateBounds();
            var collision = FindNamed(instance.transform, "COLLISION");
            if (collision == null) { UnityEngine.Object.DestroyImmediate(root); throw new InvalidDataException("Missing collision mesh in " + modelPath); }
            var collisionFilter = collision.GetComponent<MeshFilter>();
            if (collisionFilter == null || collisionFilter.sharedMesh == null) { UnityEngine.Object.DestroyImmediate(root); throw new InvalidDataException("Missing collision MeshFilter in " + modelPath); }
            var collisionRenderer = collision.GetComponent<Renderer>();
            if (collisionRenderer != null) collisionRenderer.enabled = false;
            var collider = collision.GetComponent<MeshCollider>();
            if (!collider) collider = collision.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = collisionFilter.sharedMesh;
            collider.convex = false;
            collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices;
            collider.sharedMaterial = GetPhysicsMaterial();

            var mounts = new GameObject("MountPoints"); mounts.transform.SetParent(root.transform, false);
            float depth = type == "Balcony_04_ModernLoggia" ? 1.5f : type == "Balcony_05_Wooden" ? 1.4f : 1.2f;
            AddMount(mounts.transform,"WallAttachment_Left",new Vector3(-1.35f,0f,0f));
            AddMount(mounts.transform,"WallAttachment_Right",new Vector3(1.35f,0f,0f));
            AddMount(mounts.transform,"DoorAnchor",new Vector3(0f,.15f,0f));
            AddMount(mounts.transform,"ThresholdAnchor",new Vector3(0f,.15f,.09f));
            AddMount(mounts.transform,"BalconyFront",new Vector3(0f,.15f,depth));
            AddMount(mounts.transform,"DoorClearance_Left",new Vector3(-.45f,.15f,0f));
            AddMount(mounts.transform,"DoorClearance_Right",new Vector3(.45f,.15f,0f));
            if (type.StartsWith("Balcony_", StringComparison.Ordinal))
            {
                var module = root.AddComponent<BalconyKitModule>();
                module.balconyType = type; module.wearState = state; module.balconyDepth = depth;
                module.randomSeed = 2401024 + Math.Abs(name.GetHashCode());
                module.ApplySeed(module.randomSeed);
            }
            root.isStatic = true;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) throw new IOException("Failed to save prefab: " + prefabPath);
        }

        static PhysicsMaterial GetPhysicsMaterial()
        {
            const string path = Materials + "/PM_Balcony_Concrete.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material != null) return material;
            material = new PhysicsMaterial("PM_Balcony_Concrete") { dynamicFriction = .72f, staticFriction = .82f, bounciness = .02f, frictionCombine = PhysicsMaterialCombine.Average };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void AssignMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "COLLISION") continue;
                var source = renderer.sharedMaterials;
                var replacement = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    string key = source[i] == null ? "BAL_Concrete" : source[i].name;
                    if (!MaterialBySource.TryGetValue(key, out string target)) target = "M_Balcony_Concrete";
                    replacement[i] = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + target + ".mat");
                }
                renderer.sharedMaterials = replacement;
            }
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindNamed(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static void AddMount(Transform parent, string name, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false); point.localPosition = localPosition;
            point.localRotation = name.Contains("WallAttachment") ? Quaternion.Euler(0,180,0) : Quaternion.identity;
        }

        [MenuItem("Tools/Zombie Survival/Post-Apocalypse Balconies/Create Showcase Scene")]
        public static void BuildShowcase()
        {
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.43f,.46f,.50f);
            var lightObject = new GameObject("Balcony Showcase Key Light");
            var light = lightObject.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.25f; light.color=new Color(1f,.91f,.78f);
            lightObject.transform.rotation=Quaternion.Euler(48,-32,0);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name="Showcase Ground";
            ground.transform.position=new Vector3(4,-.38f,5);ground.transform.localScale=new Vector3(76,.6f,68);ground.isStatic=true;
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/M_Balcony_Concrete.mat");

            // Catalog gallery: every one of the forty distinct models is displayed once.
            int index=0;
            for (int row=0;row<5;row++)
            for (int col=0;col<8;col++)
            {
                string type=Types[index/4],state=States[index%4],name=type+"_"+state;
                string path=Prefabs+"/"+type+"/"+name+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab==null) { index++; continue; }
                float x=(col-3.5f)*3.35f-5.0f; float z=(row-2f)*3.25f-7f;
                var plinth=GameObject.CreatePrimitive(PrimitiveType.Cube);plinth.name="Display plinth "+name;
                plinth.transform.position=new Vector3(x,.06f,z+.82f);plinth.transform.localScale=new Vector3(3.05f,.12f,2.0f);plinth.isStatic=true;
                plinth.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/M_Balcony_ConcreteDamaged.mat");
                var instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.name="Gallery "+name;instance.transform.SetPositionAndRotation(new Vector3(x,.12f,z),Quaternion.identity);
                var labelObject=new GameObject("Label "+name);labelObject.transform.position=new Vector3(x,.18f,z+1.78f);labelObject.transform.rotation=Quaternion.Euler(90,0,0);
                var label=labelObject.AddComponent<TextMesh>();label.text=type.Replace("Balcony_","")+" "+state;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.characterSize=.15f;label.fontSize=32;label.color=new Color(.11f,.11f,.1f);
                index++;
            }
            BuildHouseFragment();
            var cameraObject=new GameObject("Balcony Showcase Camera");var camera=cameraObject.AddComponent<Camera>();
            camera.transform.position=new Vector3(26,25,47);camera.transform.LookAt(new Vector3(7,4,10));camera.orthographic=true;camera.orthographicSize=23;camera.clearFlags=CameraClearFlags.Skybox;
            cameraObject.tag="MainCamera";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene,DemoScene);
            Debug.Log("BALCONY_SHOWCASE_CREATED " + DemoScene);
        }

        static void BuildHouseFragment()
        {
            var wallMat=AssetDatabase.LoadAssetAtPath<Material>(Materials+"/M_Balcony_Concrete.mat");
            float xOrigin=22f, zWall=25f; int columns=4, floors=5;
            for (int floor=0;floor<floors;floor++)
            for (int col=0;col<columns;col++)
            {
                float x=xOrigin+col*3f, y=floor*3f;
                CreateWallBox("Facade left",new Vector3(x-.975f,y+1.575f,zWall-.16f),new Vector3(1.05f,2.85f,.32f),wallMat);
                CreateWallBox("Facade right",new Vector3(x+.975f,y+1.575f,zWall-.16f),new Vector3(1.05f,2.85f,.32f),wallMat);
                CreateWallBox("Facade lintel",new Vector3(x,y+2.625f,zWall-.16f),new Vector3(.90f,.75f,.32f),wallMat);
                CreateWallBox("Facade sill",new Vector3(x,y+.075f,zWall-.16f),new Vector3(.90f,.15f,.32f),wallMat);
                if ((floor+col)%3!=1)
                {
                    string type=Types[(floor*columns+col)%Types.Length];string state=States[(floor+col)%States.Length];
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+type+"/"+type+"_"+state+".prefab");
                    if (prefab!=null)
                    {
                        var balcony=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                        balcony.name="Building balcony "+type+" "+state;balcony.transform.SetPositionAndRotation(new Vector3(x,y,zWall),Quaternion.identity);
                        balcony.GetComponent<BalconyKitModule>()?.ApplySeed(2401024+floor*29+col*7);
                    }
                }
                else
                {
                    CreateWallBox("Window recess",new Vector3(x,y+1.4f,zWall+.015f),new Vector3(1.35f,1.55f,.04f),AssetDatabase.LoadAssetAtPath<Material>(Materials+"/M_Balcony_Glass.mat"));
                    CreateWallBox("Window lintel",new Vector3(x,y+2.29f,zWall+.02f),new Vector3(1.55f,.08f,.08f),AssetDatabase.LoadAssetAtPath<Material>(Materials+"/M_Balcony_Aluminium.mat"));
                    CreateWallBox("Window sill",new Vector3(x,y+.58f,zWall+.02f),new Vector3(1.6f,.10f,.12f),AssetDatabase.LoadAssetAtPath<Material>(Materials+"/M_Balcony_Aluminium.mat"));
                }
            }
            CreateWallBox("House base",new Vector3(xOrigin+4.5f,-.12f,zWall-.17f),new Vector3(15.2f,.24f,.42f),wallMat);
        }

        static void CreateWallBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;go.isStatic=true;
            var renderer=go.GetComponent<Renderer>();if(material!=null)renderer.sharedMaterial=material;
        }
    }
}
