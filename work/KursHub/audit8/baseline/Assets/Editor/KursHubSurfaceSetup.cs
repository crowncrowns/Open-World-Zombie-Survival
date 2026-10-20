using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Authoring-only PBR setup. An authored material's existing map wins.</summary>
public static class KursHubSurfaceSetup
{
    public const string TextureFolder = "Assets/Environment/KursHub/Textures";
    private const string AppliedTag = "KursSurfaceVersion";

    [Serializable] private sealed class AuditSurfaceManifest { public AuditSurface[] families; }
    [Serializable] private sealed class AuditSurface
    {
        public string name;
        public int resolution;
        public float tiling;
        public float normalStrength;
    }

    private static AuditSurface AuditSurfaceFor(string family)
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(TextureFolder + "/Audit7PBRManifest.json");
        if (text == null) return null;
        var manifest = JsonUtility.FromJson<AuditSurfaceManifest>(text.text);
        if (manifest?.families == null) return null;
        foreach (var item in manifest.families) if (item.name == family) return item;
        return null;
    }

    public static void Apply(Dictionary<string, Material> materials)
    {
        foreach (var pair in materials)
        {
            var material = pair.Value;
            if (material == null) throw new InvalidOperationException("Нет материала: " + pair.Key);
            if (pair.Key == "LampGlass" || pair.Key == "Ember")
            {
                if (material.GetTag(AppliedTag, false, "") != "") continue;
                // Emissive glass is presentation only, without added real-time lights.
                if (!material.IsKeywordEnabled("_EMISSION") && material.GetColor("_EmissionColor").maxColorComponent <= 0)
                {
                    material.SetColor("_EmissionColor", new Color(1, .56f, .21f) * 2);
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                material.SetOverrideTag(AppliedTag, "1");
                EditorUtility.SetDirty(material);
                continue;
            }
            var family = FamilyFor(pair.Key);
            if ((pair.Key == "LeafGreen" || pair.Key == "FirNeedles") && material.GetTexture("_BaseMap") == null
                && material.GetTag(AppliedTag, false, "") == "" && AuditSurfaceFor(pair.Key) != null)
            {
                material.SetTexture("_BaseMap", Texture(pair.Key + "_BaseColor.png", false, true, pair.Key == "FirNeedles"));
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetColor("_BaseColor", Color.white);
                material.SetOverrideTag(AppliedTag, "1");
            }
            if (pair.Key == "FirNeedles")
            {
                material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .4f);
                material.SetFloat("_Surface", 0); material.SetFloat("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON"); material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = 2450;
            }
            if (family != null && ApplyAuditSurface(material, family)) continue;
            // Replace the first prototype's wall-streak map on horizontal floors.
            // This migration recognizes only that generated map, never artist maps.
            if (pair.Key == "Floor" && AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) == TextureFolder + "/CreamConcrete_BaseColor.png")
            {
                material.SetTexture("_BaseMap", null);
                material.SetOverrideTag(AppliedTag, "");
            }
            if (family == null || material.GetTag(AppliedTag, false, "") != "") continue;
            var current = material.GetTexture("_BaseMap");
            // A project asset assigned by an artist is not a generated default.
            if (current != null && AssetDatabase.GetAssetPath(current).StartsWith("Assets/", StringComparison.Ordinal)) continue;
            var transparent = family == "GlassGrime" || family == "DirtStreaks";
            material.SetTexture("_BaseMap", Texture(family + "_BaseColor.png", false, true, transparent));
            material.SetTextureScale("_BaseMap", family == "DirtStreaks" || family == "CeramicTile" || family.StartsWith("Rug", StringComparison.Ordinal) ? Vector2.one : Vector2.one * .5f);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", pair.Key == "Floor" ? new Color(1.15f, 1.15f, 1.15f, 1)
                : family == "GlassGrime" ? new Color(1, 1, 1, .5f)
                : family == "DirtStreaks" ? new Color(1, 1, 1, .7f) : Color.white);
            if (!transparent)
            {
                material.SetTexture("_BumpMap", Texture(family + "_Normal.png", true, false, false));
                material.SetTexture("_MetallicGlossMap", Texture(family + "_MetallicSmoothness.png", false, false, false));
                material.SetFloat("_BumpScale", .35f);
                material.SetFloat("_Metallic", 1);
                material.SetFloat("_Smoothness", 1);
                material.SetFloat("_SmoothnessTextureChannel", 0);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            }
            else
            {
                Transparent(material);
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_Smoothness", family == "GlassGrime" ? .55f : .08f);
                material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            }
            material.enableInstancing = true;
            if (pair.Key == "ClothInk")
            {
                material.SetColor("_EmissionColor", new Color(.12f,.11f,.085f));
                material.EnableKeyword("_EMISSION");
            }
            material.SetOverrideTag(AppliedTag, "1");
            EditorUtility.SetDirty(material);
        }
    }

    /// <summary>
    /// Upgrade only the project generator's own known maps. Material GUIDs,
    /// artist textures, tint, custom UV transforms and render state survive.
    /// Physical normal strength replaces only the original generated .35 value.
    /// </summary>
    private static bool ApplyAuditSurface(Material material, string family)
    {
        var spec = AuditSurfaceFor(family);
        if (spec == null) return false;
        var basePath = TextureFolder + "/" + family + "_BaseColor.png";
        var current = material.GetTexture("_BaseMap");
        if (current == null || AssetDatabase.GetAssetPath(current) != basePath) return false;
        var version = material.GetTag(AppliedTag, false, "");
        if (version != "1" && version != "7") return false;
        var transparent = family == "GlassGrime";
        var hasAlpha = transparent || family == "FirNeedles";
        material.SetTexture("_BaseMap", Texture(family + "_BaseColor.png", false, true, hasAlpha));
        var normalPath = TextureFolder + "/" + family + "_Normal.png";
        var oldNormal = material.GetTexture("_BumpMap");
        if (oldNormal == null || AssetDatabase.GetAssetPath(oldNormal) == normalPath)
        {
            material.SetTexture("_BumpMap", Texture(family + "_Normal.png", true, false, false));
            if (version == "1" && Mathf.Approximately(material.GetFloat("_BumpScale"), .35f))
                material.SetFloat("_BumpScale", spec.normalStrength);
            material.EnableKeyword("_NORMALMAP");
        }
        var packedPath = TextureFolder + "/" + family + "_MetallicSmoothness.png";
        var oldPacked = material.GetTexture("_MetallicGlossMap");
        if (oldPacked == null || AssetDatabase.GetAssetPath(oldPacked) == packedPath)
        {
            material.SetTexture("_MetallicGlossMap", Texture(family + "_MetallicSmoothness.png", false, false, false));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            // Glass previously had scalar metallic = 0. The generated map is
            // also zero; URP uses its alpha for the new nonuniform roughness.
            if (transparent) { material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); }
        }
        var occlusionPath = TextureFolder + "/" + family + "_Occlusion.png";
        var oldOcclusion = material.GetTexture("_OcclusionMap");
        if (oldOcclusion == null || AssetDatabase.GetAssetPath(oldOcclusion) == occlusionPath)
        {
            material.SetTexture("_OcclusionMap", Texture(family + "_Occlusion.png", false, false, false));
            material.EnableKeyword("_OCCLUSIONMAP");
        }
        // Exchange scalar maps stay linear even when not sampled by URP Lit.
        foreach (var suffix in new[] { "Roughness", "Metallic", "Height" })
            Texture(family + "_" + suffix + ".png", false, false, false);
        if (version == "1" && family == "CeramicTile" && material.GetTextureScale("_BaseMap") == Vector2.one
            && material.GetTextureOffset("_BaseMap") == Vector2.zero)
            material.SetTextureScale("_BaseMap", Vector2.one * spec.tiling);
        if (version == "1" && (family == "WornWood" || family == "TimberRaw")
            && material.GetTextureScale("_BaseMap") == Vector2.one * .5f
            && material.GetTextureOffset("_BaseMap") == Vector2.zero)
            material.SetTextureScale("_BaseMap", Vector2.one * spec.tiling);
        // Stage 6 used weak self-illumination to compensate for dark cloth
        // lettering. Paint is reflective; remove only that known generated
        // value, preserving an artist's independently chosen emission.
        if (version == "1" && family == "ClothInk")
        {
            var emission = material.GetColor("_EmissionColor");
            if (Mathf.Abs(emission.r - .12f) < .0001f && Mathf.Abs(emission.g - .11f) < .0001f
                && Mathf.Abs(emission.b - .085f) < .0001f)
            {
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
        material.SetOverrideTag(AppliedTag, "7");
        EditorUtility.SetDirty(material);
        return true;
    }

    private static string FamilyFor(string name)
    {
        switch (name)
        {
            case "BlueCladding": case "MallBlue": return "BluePaint";
            case "Concrete": case "CreamPanel": return "CreamConcrete";
            case "Floor": return "FloorConcrete";
            case "Frame": case "SteelOxide": return "RustedSteel";
            case "Roof": return "RoofMembrane";
            case "Glass": return "GlassGrime";
            case "TarpGreen": case "SandCanvas": case "WornWood": return name;
            case "DirtStain": return "DirtStreaks";
            case "TimberRaw": case "CanvasBlue": case "CanvasBeige": case "CanvasRed": case "CanvasDark":
            case "PlasticDark": case "Enamel": case "Copper": case "Paper":
            case "RugRed": case "RugBlue": case "RugWoven": case "CeramicTile": return name;
            case "ClothInk": case "InkDark": case "LeafGreen": case "FirNeedles": return name;
            default: return null;
        }
    }

    private static Texture2D Texture(string filename, bool normal, bool srgb, bool transparent)
    {
        var path = TextureFolder + "/" + filename;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Нет PBR-карты: " + path + ". Запустите Tools/KursHub/build_surface_textures.py.");
        var changed = false;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type) { importer.textureType = type; changed = true; }
        if (importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }
        if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
        if (filename == "FirNeedles_BaseColor.png")
        {
            // Keep the real cutout coverage when thin needles enter lower
            // mip levels; this atlas is opaque alpha-test foliage, not glass.
            if (!importer.mipMapsPreserveCoverage) { importer.mipMapsPreserveCoverage = true; changed = true; }
            if (!Mathf.Approximately(importer.alphaTestReferenceValue, .4f)) { importer.alphaTestReferenceValue = .4f; changed = true; }
        }
        var separator = filename.LastIndexOf('_');
        var family = separator < 0 ? filename : filename.Substring(0, separator);
        var maximum = AuditSurfaceFor(family)?.resolution ?? 1024;
        if (importer.maxTextureSize != maximum) { importer.maxTextureSize = maximum; changed = true; }
        if (maximum > 1024 && importer.filterMode == FilterMode.Bilinear) { importer.filterMode = FilterMode.Trilinear; changed = true; }
        if (maximum > 1024 && importer.anisoLevel <= 1) { importer.anisoLevel = family == "CeramicTile" ? 8 : 4; changed = true; }
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
        if (importer.alphaIsTransparency != transparent) { importer.alphaIsTransparency = transparent; changed = true; }
        if (importer.wrapModeU != TextureWrapMode.Repeat) { importer.wrapModeU = TextureWrapMode.Repeat; changed = true; }
        var wrapV = filename == "DirtStreaks_BaseColor.png" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        if (importer.wrapModeV != wrapV) { importer.wrapModeV = wrapV; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.CompressedHQ) { importer.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
        if (normal && importer.convertToNormalmap) { importer.convertToNormalmap = false; changed = true; }
        if (normal && importer.flipGreenChannel) { importer.flipGreenChannel = false; changed = true; }
        if (changed) importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException("Не импортирована PBR-карта: " + path);
        return texture;
    }

    private static void Transparent(Material material)
    {
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 0);
        material.SetFloat("_AlphaClip", 0);
        material.SetFloat("_ZWrite", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
