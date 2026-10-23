using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace AK74Reference.Editor
{
    public static class AK74ReferenceBuilder
    {
        public const string Root = "Assets/Weapons/AK74_Reference";
        public const string PrefabPath = Root + "/Prefabs/AK74_Reference.prefab";
        public const string ManifestPath = Root + "/Animations/AK74_AnimationManifest.json";
        public const string ReportDir = "Verification/AK74Reference";

        [Serializable] public sealed class Manifest { public ClipEntry[] clips; }
        [Serializable] public sealed class ClipEntry
        {
            public string name, file;
            public int frameStart, frameEnd;
            public float fps, durationSeconds;
            public bool loop;
            public ClipEvent[] events;
        }
        [Serializable] public sealed class ClipEvent
        {
            public int frame, intParameter;
            public float timeSeconds, floatParameter;
            public string functionName, stringParameter;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion, pipelineVersion, colorSpace, prefab, result;
            public string[] checks;
            public int[] lodTriangles;
            public int[] lodVisibleRestTriangles;
            public float[] dimensionsMetres;
            public string[] clips;
            public ClipProof[] animationClips;
            public ModelProof[] modelImports;
        }
        [Serializable] public sealed class ClipProof { public string name; public float durationSeconds, frameRate; public int curveBindings, eventCount; public float[] eventSeconds; }
        [Serializable] public sealed class ModelProof { public string file; public int meshes,triangles; }

        [MenuItem("Tools/AK74 Reference/Build and validate isolated asset")]
        public static void Build()
        {
            try
            {
                Directory.CreateDirectory(ReportDir);
                foreach (string dir in new[] { "Materials", "Prefabs", "Controller" }) Directory.CreateDirectory(Root + "/" + dir);
                AssetDatabase.Refresh();
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                if (manifest?.clips?.Length != 16) throw new Exception("Expected exactly 16 manifest clips.");
                ConfigureTextures();
                var material = BuildMaterial();
                string modelPath = Root + "/Models/AK74_GameReady.fbx";
                ConfigureModel(modelPath, false, null, null);
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (!modelAsset) throw new Exception("GameReady FBX did not import.");
                var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
                if (!avatar || !avatar.isValid) throw new Exception("Missing valid Generic avatar.");
                ConfigureModel(Root + "/Models/AK74_LODs.fbx", false, avatar, null);
                ConfigureModel(Root + "/Models/AK74_HighPoly.fbx", false, null, null);
                var clips = new Dictionary<string, AnimationClip>();
                foreach (var item in manifest.clips)
                {
                    string path = Root + "/Animations/" + item.file;
                    ConfigureModel(path, true, avatar, item);
                    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (!clip) throw new Exception("Missing animation: " + item.name);
                    clips.Add(item.name, clip);
                }
                var controller = BuildController(manifest, clips);
                BuildPrefab(modelAsset, material, controller);
                AssetDatabase.SaveAssets();
                Validate();
                if(File.Exists(ReportDir+"/build_failure.txt")) File.Delete(ReportDir+"/build_failure.txt");
                Debug.Log("AK74_REFERENCE_BUILD_PASS");
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(ReportDir);
                File.WriteAllText(ReportDir + "/build_failure.txt", ex.ToString());
                Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static void ConfigureTextures()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0;
                bool colour = path.IndexOf("BaseColor", StringComparison.OrdinalIgnoreCase) >= 0;
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = colour;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }
        }

        static Texture2D Texture(string suffix)
        {
            string path = AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material BuildMaterial()
        {
            string path = Root + "/Materials/AK74_Atlas.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_BaseMap", Texture("BaseColor"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", Texture("Normal"));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", Texture("MetallicSmoothness"));
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_OcclusionMap", Texture("AO"));
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ConfigureModel(string path, bool animationOnly, Avatar avatar, ClipEntry clip)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new Exception("Expected FBX: " + path);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = animationOnly;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = avatar ? ModelImporterAvatarSetup.CopyFromOther : ModelImporterAvatarSetup.CreateFromThisModel;
            if (avatar) importer.sourceAvatar = avatar;
            importer.motionNodeName = "WeaponRoot";
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            if (clip != null)
            {
                var defaults = importer.defaultClipAnimations;
                if (defaults.Length == 0) throw new Exception("No FBX take: " + path);
                var entry = defaults[0];
                entry.name = clip.name;
                entry.loopTime = clip.loop;
                entry.loopPose = false;
                entry.lockRootRotation = true;
                entry.lockRootHeightY = true;
                entry.lockRootPositionXZ = true;
                entry.keepOriginalOrientation = true;
                entry.keepOriginalPositionY = true;
                entry.keepOriginalPositionXZ = true;
                // ModelImporterClipAnimation stores event time normalized to the take, while the authored manifest uses seconds.
                entry.events = (clip.events ?? Array.Empty<ClipEvent>()).Select(e => new AnimationEvent {
                    time = Mathf.Clamp01(e.timeSeconds / clip.durationSeconds), functionName = e.functionName, intParameter = e.intParameter,
                    floatParameter = e.floatParameter, stringParameter = e.stringParameter ?? ""
                }).ToArray();
                importer.clipAnimations = new[] { entry };
            }
            importer.SaveAndReimport();
        }

        static AnimatorController BuildController(Manifest manifest, Dictionary<string, AnimationClip> clips)
        {
            string path = Root + "/Controller/AK74_Reference.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            // Rebuild only this generated controller while retaining its asset GUID for consumers.
            foreach(var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach(var child in machine.states) machine.RemoveState(child.state);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            string idleName = manifest.clips.FirstOrDefault(c => c.name.Equals("Idle", StringComparison.OrdinalIgnoreCase))?.name ?? manifest.clips[0].name;
            var states = new Dictionary<string, AnimatorState>();
            for (int i = 0; i < manifest.clips.Length; i++)
            {
                var item = manifest.clips[i];
                var state = machine.AddState(item.name, new Vector3(260 + i % 4 * 240, 60 + i / 4 * 90));
                state.motion = clips[item.name];
                state.writeDefaultValues = true;
                state.AddStateMachineBehaviour<AK74ResetPresentationOnExit>();
                states.Add(item.name, state);
                controller.AddParameter(item.name, AnimatorControllerParameterType.Trigger);
                var transition = machine.AddAnyStateTransition(state);
                transition.hasExitTime = false;
                transition.duration = .04f;
                transition.canTransitionToSelf = true;
                transition.AddCondition(AnimatorConditionMode.If, 0, item.name);
            }
            machine.defaultState = states[idleName];
            foreach (var item in manifest.clips.Where(c => !c.loop && c.name != "Aim_In" && c.name != "Unequip"))
            {
                var transition = states[item.name].AddTransition(states[idleName]);
                transition.hasExitTime = true;
                transition.exitTime = 1;
                transition.duration = .08f;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BuildPrefab(GameObject modelAsset, Material material, AnimatorController controller)
        {
            var root = new GameObject("AK74_Reference");
            try
            {
                var model = Object.Instantiate(modelAsset, root.transform);
                model.name = "Visual";
                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var presentation = model.AddComponent<AK74Presentation>();
                var transforms = model.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var boneNames = new[] { "WeaponRoot", "Magazine", "SpareMagazine", "Trigger", "Selector", "Bolt", "Stock" };
                foreach (string bone in boneNames) if (!transforms.ContainsKey(bone)) throw new Exception("Missing bone: " + bone);
                var lodsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/AK74_LODs.fbx");
                var lodSource = Object.Instantiate(lodsAsset);
                try
                {
                    foreach (var source in lodSource.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        int lod = source.name.EndsWith("_LOD1") ? 1 : source.name.EndsWith("_LOD2") ? 2 : -1;
                        if (lod < 0) throw new Exception("Expected _LOD1/_LOD2 suffix on " + source.name);
                        var go = new GameObject(source.name);
                        go.transform.SetParent(model.transform, false);
                        go.transform.localPosition = lodSource.transform.InverseTransformPoint(source.transform.position);
                        go.transform.localRotation = Quaternion.Inverse(lodSource.transform.rotation) * source.transform.rotation;
                        go.transform.localScale = source.transform.lossyScale;
                        var dest = go.AddComponent<SkinnedMeshRenderer>();
                        dest.sharedMesh = source.sharedMesh;
                        dest.bones = source.bones.Select(b => transforms.TryGetValue(b.name, out var mapped) ? mapped : throw new Exception("LOD unknown bone: " + b.name)).ToArray();
                        dest.rootBone = source.rootBone ? transforms[source.rootBone.name] : transforms["WeaponRoot"];
                        dest.localBounds = source.localBounds;
                        dest.updateWhenOffscreen = true;
                    }
                }
                finally { Object.DestroyImmediate(lodSource); }
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length == 0 ? 1 : renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                presentation.magazineRenderers = renderers.Where(r => r.name.IndexOf("Magazine", StringComparison.OrdinalIgnoreCase) >= 0 && r.name.IndexOf("Spare", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
                presentation.spareMagazineRenderers = renderers.Where(r => r.name.IndexOf("Spare", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                presentation.muzzle = Required(transforms, "Muzzle");
                presentation.eject = Required(transforms, "Eject");
                presentation.rightHand = Required(transforms, "Grip_R");
                presentation.leftHand = Required(transforms, "Grip_L");
                presentation.aim = Required(transforms, "Aim");
                foreach (var socket in new[] { presentation.muzzle, presentation.eject, presentation.rightHand, presentation.leftHand, presentation.aim })
                    socket.SetParent(transforms["WeaponRoot"], true);
                // Verified FBX import maps Blender (X,Y,Z) to Unity (X,Z,Y): bore is native -X and vertical is +Y.
                // The receiver pivot lies below the bore, so root-to-muzzle is unsuitable for deriving the bore axis.
                model.transform.localRotation = Quaternion.Euler(0, 90, 0);
                // Standard game wrapper: right-handed visual authored into Unity +Z muzzle / +Y up, one metre unit scale.
                foreach (var socket in new[] { presentation.muzzle, presentation.eject, presentation.rightHand, presentation.leftHand, presentation.aim }) socket.rotation = root.transform.rotation;
                presentation.ResetMagazineVisibility();
                var lodGroup = root.AddComponent<LODGroup>();
                var lods = new LOD[3];
                for (int i = 0; i < 3; i++)
                {
                    int level = i;
                    var members = renderers.Where(r => level == 0 ? !r.name.EndsWith("_LOD1") && !r.name.EndsWith("_LOD2") : r.name.EndsWith("_LOD" + level)).ToArray();
                    if (members.Length == 0) throw new Exception("Empty LOD " + i);
                    lods[i] = new LOD(new[] { .32f, .12f, .035f }[i], members);
                }
                lodGroup.SetLODs(lods);
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.RecalculateBounds();
                var collider = root.AddComponent<BoxCollider>();
                var bounds = ActiveBounds(root);
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform Required(Dictionary<string, Transform> names, string name)
            => names.TryGetValue(name, out var found) ? found : throw new Exception("Missing exported socket " + name);

        public static Bounds ActiveBounds(GameObject root)
        {
            var items = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (items.Length == 0) throw new Exception("No visible renderers.");
            var b = items[0].bounds;
            foreach (var renderer in items.Skip(1)) b.Encapsulate(renderer.bounds);
            return b;
        }

        [MenuItem("Tools/AK74 Reference/Validate asset")]
        public static void Validate()
        {
            var checks = new List<string>();
            if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new Exception("Validation project must match the target Linear colour space.");
            checks.Add("Linear colour space matches the target project");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!asset) throw new Exception("Prefab missing.");
            var modelProofs=new List<ModelProof>();
            foreach(var name in new[]{"AK74_GameReady.fbx","AK74_LODs.fbx","AK74_HighPoly.fbx"})
            {
                string modelPath=Root+"/Models/"+name;
                var meshes=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().ToArray();
                if(!AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)||meshes.Length==0)throw new Exception("Model did not import: "+name);
                int triangles=meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(s=>(int)m.GetIndexCount(s)/3));
                if(triangles<=0)throw new Exception("Empty model: "+name);
                modelProofs.Add(new ModelProof{file=name,meshes=meshes.Length,triangles=triangles});
            }
            checks.Add("GameReady, LOD and HighPoly FBX all import with nonempty mesh data");
            var instance = Object.Instantiate(asset);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                var relay = instance.GetComponentInChildren<AK74Presentation>();
                var group = instance.GetComponent<LODGroup>();
                if (!animator || !animator.avatar || !animator.avatar.isValid || animator.isHuman || animator.applyRootMotion) throw new Exception("Generic rig contract failed.");
                checks.Add("Valid Generic Avatar; root motion disabled");
                if (instance.transform.localScale != Vector3.one) throw new Exception("Non-unit prefab scale.");
                var bound = ActiveBounds(instance);
                if (bound.size.z < .8f || bound.size.z > 1.2f || bound.size.y < .2f || bound.size.y > .6f) throw new Exception("Unexpected metre bounds: " + bound);
                if (Vector3.Dot(relay.muzzle.position.normalized, Vector3.forward) < .9f || Vector3.Dot(relay.muzzle.forward, Vector3.forward) < .99f) throw new Exception("Muzzle is not +Z.");
                checks.Add("Metre bounds and +Z muzzle / +Y up wrapper");
                var lods = group.GetLODs();
                if (lods.Length != 3) throw new Exception("Expected three LODs.");
                var rig = instance.GetComponentsInChildren<Transform>(true).Where(t => t.name == "WeaponRoot").ToArray();
                if (rig.Length != 1) throw new Exception("Expected one common WeaponRoot.");
                foreach(var socket in new[]{relay.muzzle,relay.eject,relay.rightHand,relay.leftHand,relay.aim})
                    if(socket.parent!=rig[0]) throw new Exception("Socket does not follow animated WeaponRoot: "+socket.name);
                int[] tris = lods.Select(l => l.renderers.Sum(Triangles)).ToArray();
                if (!(tris[0] > tris[1] && tris[1] > tris[2] && tris[2] > 0)) throw new Exception("LODs are not decreasing: " + string.Join(",", tris));
                var lodBounds=lods.Select(l=>RendererBounds(l.renderers.Where(r=>r.name.IndexOf("Spare",StringComparison.OrdinalIgnoreCase)<0))).ToArray();
                foreach(var b in lodBounds.Skip(1))
                    if(Vector3.Distance(b.center,lodBounds[0].center)>.03f || Vector3.Distance(b.size,lodBounds[0].size)>.04f)
                        throw new Exception("LOD mesh bounds diverge from the shared rig rest pose: "+b);
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.sharedMesh || renderer.bones.Length == 0 || renderer.bones.Any(b => !b || !b.IsChildOf(animator.transform))) throw new Exception("Invalid common-rig skin " + renderer.name);
                    if (!renderer.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0)) throw new Exception("Missing UV0 " + renderer.name);
                    if (renderer.sharedMaterials.Any(m => !m || m.shader.name != "Universal Render Pipeline/Lit" || !m.GetTexture("_BaseMap") || !m.GetTexture("_BumpMap") || !m.GetTexture("_MetallicGlossMap"))) throw new Exception("Missing URP material/maps " + renderer.name);
                }
                checks.Add("Three decreasing LODs with matching rest bounds; every skinned renderer bound to the same rig; UV0 and URP maps resolve");
                if (relay.magazineRenderers.Length != 3 || relay.spareMagazineRenderers.Length != 3 || relay.spareMagazineRenderers.Any(r => r.enabled)) throw new Exception("Expected one magazine and spare renderer per LOD with spare hidden.");
                checks.Add("Main/spare magazine renderers in all three LODs; spare hidden at rest");
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller.layers[0].stateMachine.states.Length != 16) throw new Exception("Expected sixteen Animator states.");
                var clipProofs=new List<ClipProof>();
                foreach (var item in manifest.clips)
                {
                    var clip = controller.animationClips.FirstOrDefault(c => c.name == item.name);
                    if (!clip || clip.length < .03f || AnimationUtility.GetCurveBindings(clip).Length == 0) throw new Exception("Empty clip " + item.name);
                    if (Math.Abs(clip.length - item.durationSeconds) > .05f) throw new Exception("Duration mismatch " + item.name + ": " + clip.length + " vs " + item.durationSeconds);
                    if (Mathf.Abs(clip.frameRate-item.fps)>.01f)throw new Exception("Frame-rate mismatch "+item.name+": "+clip.frameRate);
                    var events = AnimationUtility.GetAnimationEvents(clip);
                    if (events.Length != (item.events?.Length ?? 0)) throw new Exception("Event mismatch " + item.name);
                    foreach (var evt in events) if (typeof(AK74Presentation).GetMethod(evt.functionName) == null) throw new Exception("Unknown visual event " + evt.functionName);
                    for(int evt=0;evt<events.Length;evt++)
                        if(events[evt].functionName!=item.events[evt].functionName || Mathf.Abs(events[evt].time-item.events[evt].timeSeconds)>.005f)
                            throw new Exception("Event time/function mismatch "+item.name+" at "+evt+": "+events[evt].time);
                    clipProofs.Add(new ClipProof{name=item.name,durationSeconds=clip.length,frameRate=clip.frameRate,curveBindings=AnimationUtility.GetCurveBindings(clip).Length,eventCount=events.Length,eventSeconds=events.Select(e=>e.time).ToArray()});
                }
                checks.Add("Sixteen nonempty imported clips, correct durations, states and supported visual event receivers");
                foreach (string suffix in new[] { "BaseColor", "Normal", "Metallic", "Roughness", "AO", "MetallicSmoothness", "Height" })
                {
                    var tex = Texture(suffix);
                    if (!tex || tex.width != 4096 || tex.height != 4096) throw new Exception("Expected 4K " + suffix);
                }
                checks.Add("Seven maps resolve at 4096 x 4096");
                var report = new Report { unityVersion = Application.unityVersion, pipelineVersion = "17.6.0", colorSpace=PlayerSettings.colorSpace.ToString(), prefab = PrefabPath, result = "PASS", checks = checks.ToArray(), lodTriangles = tris, lodVisibleRestTriangles=lods.Select(l=>l.renderers.Where(r=>r.name.IndexOf("Spare",StringComparison.OrdinalIgnoreCase)<0).Sum(Triangles)).ToArray(), dimensionsMetres = new[] { bound.size.x, bound.size.y, bound.size.z }, clips = manifest.clips.Select(c => c.name).ToArray(),animationClips=clipProofs.ToArray(),modelImports=modelProofs.ToArray() };
                Directory.CreateDirectory(ReportDir);
                File.WriteAllText(ReportDir + "/unity_validation.json", JsonUtility.ToJson(report, true));
                Debug.Log("AK74_REFERENCE_VALIDATION_PASS " + string.Join("/", tris));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        public static int Triangles(Renderer renderer)
        {
            var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh) return 0;
            int result = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) result += (int)mesh.GetIndexCount(sub) / 3;
            return result;
        }
        static Bounds RendererBounds(IEnumerable<Renderer> renderers)
        {
            var items=renderers.ToArray();
            if(items.Length==0)throw new Exception("Empty LOD renderer collection.");
            var result=items[0].bounds;
            foreach(var renderer in items.Skip(1))result.Encapsulate(renderer.bounds);
            return result;
        }

        public static void ExportPackage()
        {
            Validate();
            Directory.CreateDirectory(ReportDir);
            UnityEditor.AssetPackage.Package.Export(new UnityEditor.AssetPackage.ExportPackageParameters(
                Root, ReportDir + "/AK74_Reference.unitypackage", null, ExportPackageOptions.Recurse));
            Debug.Log("AK74_REFERENCE_PACKAGE_PASS");
        }
    }
}
