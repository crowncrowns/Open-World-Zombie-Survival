using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PostApocalypse.Graffiti.Editor
{
    public static class GraffitiPipeline
    {
        public static string Current
        {
            get
            {
                var p = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (!p) return "Built-in";
                var n = p.GetType().Name;
                return n.Contains("HDRender") ? "HDRP" : n.Contains("Universal") ? "URP" : n;
            }
        }
        public static bool HasDecals
        {
            get
            {
                var p = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (!p) return false;
                var so = new SerializedObject(p);
                if (Current == "HDRP")
                {
                    var enabled = so.FindProperty("m_RenderPipelineSettings.supportDecals");
                    return enabled != null && enabled.boolValue;
                }
                var list = so.FindProperty("m_RendererDataList");
                var index = so.FindProperty("m_DefaultRendererIndex");
                if (list == null || list.arraySize == 0) return false;
                var data = list.GetArrayElementAtIndex(Mathf.Clamp(index?.intValue ?? 0, 0, list.arraySize - 1)).objectReferenceValue;
                if (!data) return false;
                var features = new SerializedObject(data).FindProperty("m_RendererFeatures");
                if (features == null) return false;
                for (int i = 0; i < features.arraySize; i++)
                {
                    var feature = features.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (!feature || !feature.GetType().Name.Contains("DecalRendererFeature")) continue;
                    var active = new SerializedObject(feature).FindProperty("m_Active");
                    if (active == null || active.boolValue) return true;
                }
                return false;
            }
        }
        public static Material MakeMaterial(Texture texture)
        {
            var shader = Shader.Find(Current == "URP" ? "Universal Render Pipeline/Lit" : Current == "HDRP" ? "HDRP/Lit" : "Standard");
            if (!shader) throw new InvalidOperationException("Не найден Lit shader для " + Current);
            var mat = new Material(shader);
            mat.SetTexture(Current == "Built-in" ? "_MainTex" : Current == "HDRP" ? "_BaseColorMap" : "_BaseMap", texture);
            mat.SetColor(Current == "Built-in" ? "_Color" : "_BaseColor", Color.white);
            if (Current == "Built-in")
            {
                mat.SetFloat("_Mode", 2); mat.EnableKeyword("_ALPHABLEND_ON");
                mat.SetFloat("_Glossiness", 0);
            }
            else if (Current == "URP")
            {
                mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
                mat.SetFloat("_Smoothness", 0); mat.SetFloat("_Cull", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.SetFloat("_BlendModePreserveSpecular", 0);
                mat.SetFloat("_SpecularHighlights", 0); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.SetFloat("_EnvironmentReflections", 0); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            else
            {
                mat.SetFloat("_SurfaceType", 1); mat.SetFloat("_BlendMode", 0);
                mat.SetFloat("_DoubleSidedEnable", 1); mat.EnableKeyword("_DOUBLESIDED_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.EnableKeyword("_BLENDMODE_ALPHA");
                mat.SetFloat("_Smoothness", 0); mat.SetFloat("_AlphaDstBlend", 10);
                var t = Type.GetType("UnityEditor.Rendering.HighDefinition.HDMaterial, Unity.RenderPipelines.HighDefinition.Editor");
                t?.GetMethod("ValidateMaterial", new[]{typeof(Material)})?.Invoke(null, new object[]{mat});
            }
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0); mat.SetInt("_Cull", 0);
            mat.renderQueue = 3000;
            mat.SetShaderPassEnabled("ShadowCaster", false);
            return mat;
        }
        public static bool Project(GameObject go, GraffitiEntry entry, float width, float opacity, bool persist)
        {
            if (!HasDecals) return false;
            string typeName=Current=="URP" ? "UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime" : "UnityEngine.Rendering.HighDefinition.DecalProjector, Unity.RenderPipelines.HighDefinition.Runtime";
            var type=Type.GetType(typeName);if(type==null)return false;
            var shader=Current=="URP" ? AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Decal.shadergraph") : Shader.Find("HDRP/Decal");
            if(!shader)return false;
            string path=$"{GraffitiPainter.Root}/Materials/{entry.name}_{Current}_Projected.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat)
            {
                mat=new Material(shader);
                if(Current=="URP") {mat.SetTexture("Base_Map",entry.texture);mat.SetFloat("Normal_Blend",0);}
                else {mat.SetTexture("_BaseColorMap",entry.texture);mat.SetFloat("_AffectAlbedo",1);mat.SetFloat("_AffectNormal",0);mat.SetFloat("_AffectMetal",0);mat.SetFloat("_AffectAO",0);mat.SetFloat("_AffectSmoothness",0);}
                if(persist)AssetDatabase.CreateAsset(mat,path);else mat.hideFlags=HideFlags.HideAndDontSave;
            }
            var projector=go.GetComponent(type)??go.AddComponent(type);
            type.GetProperty("material")?.SetValue(projector,mat);
            type.GetProperty("size")?.SetValue(projector,new Vector3(width,width/entry.Aspect,.12f));
            type.GetProperty("pivot")?.SetValue(projector,Vector3.zero);
            type.GetProperty("fadeFactor")?.SetValue(projector,opacity);
            var scaleMode=type.GetProperty("scaleMode");
            if(scaleMode!=null && Enum.GetNames(scaleMode.PropertyType).Contains("InheritFromHierarchy"))scaleMode.SetValue(projector,Enum.Parse(scaleMode.PropertyType,"InheritFromHierarchy"));
            var marker=go.GetComponent<GraffitiPlacement>();marker.nativeProjector=projector;
            go.GetComponent<Renderer>().enabled=false;
            go.transform.localScale=Vector3.one;
            return true;
        }
        [MenuItem("Tools/Graffiti Pack/Rebuild materials for active pipeline")]
        public static void RebuildMaterials()
        {
            foreach (var id in AssetDatabase.FindAssets("t:GraffitiEntry", new[]{GraffitiPainter.Root}))
            {
                var entry = AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(id));
                if (!entry || !entry.prefab) continue;
                var old = entry.prefab.GetComponent<Renderer>().sharedMaterial;
                var next = MakeMaterial(entry.texture);
                Undo.RecordObject(old, "Graffiti pipeline material");
                old.shader = next.shader; old.CopyPropertiesFromMaterial(next);
                UnityEngine.Object.DestroyImmediate(next); EditorUtility.SetDirty(old);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
