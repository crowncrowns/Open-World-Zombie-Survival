using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdvancedGrassPainter.Editor
{
    public static class GrassHDRPInstaller
    {
        const string Root="Assets/Tools/AdvancedGrassPainter";
        public static bool Available=>Type.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime")!=null;
        [MenuItem("Tools/Environment/Install Advanced Grass HDRP Shader")]
        public static void InstallMenu()=>Install();
        [MenuItem("Tools/Environment/Install Advanced Grass HDRP Shader",true)]
        public static bool CanInstall()=>Available;
        public static Material Install()
        {
            if(!Available)throw new InvalidOperationException("Установите HDRP 17.6 в вашем HDRP-проекте. Этот инструмент не изменяет Packages или настройки пайплайна.");
            string include=AssetDatabase.AssetPathToGUID(Root+"/Shaders/GrassGraphWind.hlsl");
            string graph=File.ReadAllText(Root+"/Shaders/HDRP~/GrassHDRP.shadergraph").Replace("__GRASS_INCLUDE_GUID__",include);
            string path=Root+"/Shaders/GrassHDRP.shadergraph";File.WriteAllText(path,graph);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);if(!shader)throw new InvalidOperationException("Не удалось импортировать HDRP Shader Graph.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Grass_HDRP.mat");
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Root+"/Materials/Grass_HDRP.mat");}
            material.shader=shader;EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();return material;
        }
    }
}
