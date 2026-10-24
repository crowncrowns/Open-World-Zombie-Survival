using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PostApocalypse.Graffiti.Editor
{
    public static class GraffitiAtlasBuilder
    {
        [MenuItem("Tools/Graffiti Pack/Build optional Quad atlases")]
        public static void Build()
        {
            BuildTo(GraffitiPainter.Root+"/Atlases");
        }
        public static void BuildTo(string folder)
        {
            if(!folder.StartsWith("Assets/"))throw new System.ArgumentException("Atlas destination must be inside Assets");
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var entries=AssetDatabase.FindAssets("t:GraffitiEntry",new[]{GraffitiPainter.Root}).Select(x=>AssetDatabase.LoadAssetAtPath<GraffitiEntry>(AssetDatabase.GUIDToAssetPath(x))).OrderBy(x=>x.number).ToArray();
            for(int start=0;start<entries.Length;start+=4)
            {
                var group=entries.Skip(start).Take(4).ToArray();
                var copies=group.Select(e=>Readable(e.texture)).ToArray();
                var atlas=new Texture2D(4096,4096,TextureFormat.RGBA32,true,false);
                var rects=atlas.PackTextures(copies,32,4096,false);
                string png=$"{folder}/Atlas_{start/4:00}.png";File.WriteAllBytes(png,atlas.EncodeToPNG());
                foreach(var c in copies)Object.DestroyImmediate(c);Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(png);
                var importer=(TextureImporter)AssetImporter.GetAtPath(png);importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(png);
                string matPath=$"{folder}/Atlas_{start/4:00}.mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!mat){mat=GraffitiPipeline.MakeMaterial(texture);AssetDatabase.CreateAsset(mat,matPath);}
                else mat.SetTexture(GraffitiPipeline.Current=="Built-in"?"_MainTex":GraffitiPipeline.Current=="HDRP"?"_BaseColorMap":"_BaseMap",texture);
                for(int j=0;j<group.Length;j++)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(group[j].prefab);
                    var source=go.GetComponent<MeshFilter>().sharedMesh;var uv=source.uv;
                    for(int k=0;k<uv.Length;k++)uv[k]=rects[j].position+Vector2.Scale(uv[k],rects[j].size);
                    string meshPath=$"{folder}/{group[j].name}_UV.asset";
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(!mesh){mesh=Object.Instantiate(source);mesh.uv=uv;AssetDatabase.CreateAsset(mesh,meshPath);}else {mesh.uv=uv;EditorUtility.SetDirty(mesh);}
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=mat;
                    PrefabUtility.SaveAsPrefabAsset(go,$"{folder}/{group[j].name}_Atlas.prefab");Object.DestroyImmediate(go);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Graffiti optional atlases built. PackTextures may downsample; originals remain intact. Atlas prefabs share one material per four entries. Profile actual draw calls in target scene.");
        }
        static Texture2D Readable(Texture2D source)
        {
            var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            Graphics.Blit(source,rt);var previous=RenderTexture.active;RenderTexture.active=rt;
            var result=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,false);
            result.ReadPixels(new Rect(0,0,source.width,source.height),0,0);result.Apply();RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);return result;
        }
    }
}
