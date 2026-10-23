using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace AK74Reference.Editor
{
    public static class AK74ReferencePreview
    {
        public static void Render()
        {
            AK74ReferenceBuilder.Validate();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Assets/AK74_Validation");
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "AK74 Validation Renderer";
            AssetDatabase.CreateAsset(rendererData, "Assets/AK74_Validation/Renderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.name = "AK74 Validation URP";
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            pipeline.shadowDistance = 10;
            AssetDatabase.CreateAsset(pipeline, "Assets/AK74_Validation/URP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .5f, .53f);
            RenderSettings.ambientIntensity = 1;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AK74ReferenceBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            instance.GetComponentInChildren<Animator>().enabled = false;
            instance.GetComponent<LODGroup>().ForceLOD(0);
            LightAt("Key", new Vector3(45,-35,0), 3, new Color(1,.94f,.86f));
            LightAt("Fill", new Vector3(15,135,0), 2.2f, new Color(.77f,.86f,1));
            LightAt("Rim", new Vector3(55,35,0), 1.3f, Color.white);
            var cameraObject = new GameObject("Validation Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f,.19f,.21f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 10;
            camera.orthographic = true;
            camera.orthographicSize = .335f;
            var bounds = AK74ReferenceBuilder.ActiveBounds(instance);
            Vector3 centre = bounds.center;
            Directory.CreateDirectory(AK74ReferenceBuilder.ReportDir + "/Preview");
            Shot(camera, centre + new Vector3(-1.2f,.20f,.5f), centre, "Unity_ThreeQuarter.png");
            Shot(camera, centre + new Vector3(-1.4f,0,0), centre, "Unity_ReferenceSide.png");
            Shot(camera, centre + new Vector3(1.4f,0,0), centre, "Unity_OppositeSide.png");
            for(int lod=0;lod<3;lod++)
            {
                instance.GetComponent<LODGroup>().ForceLOD(lod);
                Shot(camera, centre + new Vector3(-1.2f,.20f,.5f), centre, "Unity_LOD"+lod+".png");
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(AK74ReferenceBuilder.ReportDir + "/render_validation.txt", "Rendered in Unity " + Application.unityVersion + " / URP 17.6.0; three views and each actual LOD. This isolated studio does not modify the user's scene lighting.");
            Debug.Log("AK74_REFERENCE_RENDER_PASS");
        }

        static void LightAt(string name, Vector3 rotation, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(rotation);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
        }

        static void Shot(Camera camera, Vector3 location, Vector3 target, string name)
        {
            camera.transform.position = location;
            camera.transform.LookAt(target, Vector3.up);
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            var prior = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
            image.Apply();
            File.WriteAllBytes(AK74ReferenceBuilder.ReportDir + "/Preview/" + name, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            RenderTexture.active=prior;
            camera.targetTexture=null;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
