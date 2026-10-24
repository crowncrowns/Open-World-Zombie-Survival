using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    public static class GrassGraphAuthoring
    {
        public static void Generate()
        {
            const string root="Assets/Tools/AdvancedGrassPainter";
            var types=TypeCache.GetTypesDerivedFrom<Target>();var type=types.First(t=>t.Name=="HDTarget");var target=(Target)Activator.CreateInstance(type);
            var sub=TypeCache.GetTypesDerivedFrom<SubTarget>().First(t=>t.Name=="HDLitSubTarget");type.GetMethod("TrySetActiveSubTarget").Invoke(target,new object[]{sub});
            var datas=type.GetField("m_Datas",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target) as System.Collections.IEnumerable;
            foreach(var data in datas)
            {
                var value=data.GetType().GetProperty("value").GetValue(data);
                if(value.GetType().Name=="SystemData")
                {
                    var prop=value.GetType().GetProperty("doubleSidedMode");prop.SetValue(value,Enum.Parse(prop.PropertyType,"FlippedNormals"));
                    value.GetType().GetProperty("alphaTest").SetValue(value,true);
                }
            }
            var graph=new GraphData();graph.AddContexts();graph.InitializeOutputs(new[]{target},new[]{BlockFields.VertexDescription.Position,BlockFields.VertexDescription.Normal,BlockFields.VertexDescription.Tangent,BlockFields.SurfaceDescription.BaseColor,BlockFields.SurfaceDescription.NormalTS,BlockFields.SurfaceDescription.Metallic,BlockFields.SurfaceDescription.Smoothness,BlockFields.SurfaceDescription.Occlusion,BlockFields.SurfaceDescription.Alpha,BlockFields.SurfaceDescription.AlphaClipThreshold});
            var position=new PositionNode();typeof(GeometryNode).GetField("m_Space",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(position,CoordinateSpace.Object);var uv=new UVNode{uvChannel=UVChannel.UV0};var reserve=new UVNode{uvChannel=UVChannel.UV1};var color=new VertexColorNode();
            var wind=new CustomFunctionNode{sourceType=HlslSourceType.File,functionName="AdvancedGrassWind",functionSource=AssetDatabase.AssetPathToGUID(root+"/Shaders/GrassGraphWind.hlsl")};
            wind.AddSlot(new Vector3MaterialSlot(0,"Position","Position",SlotType.Input,Vector3.zero));wind.AddSlot(new Vector4MaterialSlot(1,"UV","UV",SlotType.Input,Vector4.zero));wind.AddSlot(new Vector4MaterialSlot(2,"Reserve","Reserve",SlotType.Input,Vector4.zero));wind.AddSlot(new Vector3MaterialSlot(3,"Deformed","Deformed",SlotType.Output,Vector3.zero));
            foreach(var node in new AbstractMaterialNode[]{position,uv,reserve,color,wind})graph.AddNode(node);
            var texture=new Texture2DShaderProperty{displayName="Blade texture (optional alpha)",overrideReferenceName="_BaseMap"};graph.AddGraphInput(texture);
            var property=new PropertyNode{property=texture};var sample=new SampleTexture2DNode();
            var surface=new CustomFunctionNode{sourceType=HlslSourceType.File,functionName="AdvancedGrassSurface",functionSource=wind.functionSource};
            surface.AddSlot(new Vector4MaterialSlot(0,"UV","UV",SlotType.Input,Vector4.zero));surface.AddSlot(new Vector4MaterialSlot(1,"Vertex","Vertex",SlotType.Input,Vector4.one));surface.AddSlot(new Vector4MaterialSlot(2,"Texture","Texture",SlotType.Input,Vector4.one));surface.AddSlot(new Vector3MaterialSlot(3,"Color","Color",SlotType.Output,Vector3.one));surface.AddSlot(new Vector1MaterialSlot(4,"Alpha","Alpha",SlotType.Output,1));surface.AddSlot(new Vector1MaterialSlot(5,"AO","AO",SlotType.Output,1));
            foreach(var node in new AbstractMaterialNode[]{property,sample,surface})graph.AddNode(node);
            graph.Connect(property.GetSlotReference(0),sample.GetSlotReference(1));graph.Connect(uv.GetSlotReference(0),sample.GetSlotReference(2));graph.Connect(uv.GetSlotReference(0),surface.GetSlotReference(0));graph.Connect(color.GetSlotReference(0),surface.GetSlotReference(1));graph.Connect(sample.GetSlotReference(0),surface.GetSlotReference(2));
            graph.Connect(position.GetSlotReference(0),wind.GetSlotReference(0));graph.Connect(uv.GetSlotReference(0),wind.GetSlotReference(1));graph.Connect(reserve.GetSlotReference(0),wind.GetSlotReference(2));
            foreach(var block in graph.GetNodes<BlockNode>())
            {
                if(block.descriptor==BlockFields.VertexDescription.Position)graph.Connect(wind.GetSlotReference(3),block.GetSlotReference(0));
                if(block.descriptor==BlockFields.SurfaceDescription.BaseColor)graph.Connect(surface.GetSlotReference(3),block.GetSlotReference(0));
                if(block.descriptor==BlockFields.SurfaceDescription.Alpha)graph.Connect(surface.GetSlotReference(4),block.GetSlotReference(0));
                if(block.descriptor==BlockFields.SurfaceDescription.Occlusion)graph.Connect(surface.GetSlotReference(5),block.GetSlotReference(0));
                if(block.descriptor==BlockFields.SurfaceDescription.Smoothness)block.FindSlot<Vector1MaterialSlot>(0).value=.17f;
            }
            graph.ValidateGraph();File.WriteAllText(root+"/Shaders/GrassHDRP.shadergraph",MultiJson.Serialize(graph));AssetDatabase.Refresh();
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(root+"/Shaders/GrassHDRP.shadergraph");if(!shader)throw new Exception("HDRP graph import failed");
            var material=AssetDatabase.LoadAssetAtPath<Material>(root+"/Materials/Grass_HDRP.mat");if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,root+"/Materials/Grass_HDRP.mat");}AssetDatabase.SaveAssets();Debug.Log("GRASS_HDRP_GRAPH_AUTHORED");
        }
    }
}

