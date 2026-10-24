using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdvancedGrassPainter.Editor
{
    public static class GrassSceneViewVerification
    {
        static GrassPainterWindow window;
        static SceneView sceneView;
        static GrassDataStorage layer;
        static double ready;
        public static void Run()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/work/AdvancedGrassPainter/"))throw new InvalidOperationException("Isolated project only");
            EditorSceneManager.OpenScene("Assets/Tools/AdvancedGrassPainter/Demo/AdvancedGrassPainter_Demo.unity");
            layer=new GameObject("SceneView brush verification").AddComponent<GrassDataStorage>();
            window=EditorWindow.GetWindow<GrassPainterWindow>();window.position=new Rect(0,0,500,700);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(GrassPainterWindow).GetField("layer",flags).SetValue(window,layer);
            typeof(GrassPainterWindow).GetField("painting",flags).SetValue(window,true);
            typeof(GrassPainterWindow).GetField("hints",flags).SetValue(window,false);
            typeof(GrassPainterWindow).GetField("settings",flags).SetValue(window,new GrassSettings{radius=1.5f,density=1,spacing=.15f,edgeFalloff=.2f});
            sceneView=EditorWindow.GetWindow<SceneView>();sceneView.position=new Rect(520,0,1000,800);
            sceneView.LookAt(new Vector3(8,0,-3),Quaternion.Euler(65,0,0),5,false,true);sceneView.Repaint();ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Tick;
        }
        static void Mouse(EventType type,Vector2 point,EventModifiers modifiers=EventModifiers.None){sceneView.SendEvent(new Event{type=type,button=0,mousePosition=point,modifiers=modifiers});}
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);Debug.Log("GRASS SCENEVIEW PASS "+message);}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=Tick;
            var report=new GrassVerification.Report{unity=Application.unityVersion,pipeline="URP SceneView scripted editor events"};
            try
            {
                sceneView.Focus();var center=new Vector2(500,400);Mouse(EventType.MouseDown,center);Mouse(EventType.MouseUp,center);int painted=layer.plants.Count;
                Require(painted>0,"SceneView MouseDown/MouseUp paints a stroke");report.checks.Add("PASS SceneView paint event creates "+painted+" plants");
                Mouse(EventType.MouseDown,center);Mouse(EventType.MouseDrag,center+new Vector2(130,0));Mouse(EventType.MouseDrag,center+new Vector2(230,0));Mouse(EventType.MouseUp,center+new Vector2(230,0));
                Require(layer.plants.Count>painted,"SceneView drag paints additional dabs");report.checks.Add("PASS SceneView drag creates continuous dabs");
                string before=GrassVerification.Hash(layer);int beforeCount=layer.plants.Count;
                Mouse(EventType.MouseDown,center,EventModifiers.Shift);Mouse(EventType.MouseUp,center,EventModifiers.Shift);Undo.FlushUndoRecordObjects();
                Require(layer.plants.Count<beforeCount,"Shift brush erases through SceneView events");report.checks.Add("PASS Shift erases through actual brush callback");
                Undo.PerformUndo();Require(GrassVerification.Hash(layer)==before,"SceneView Undo restores exact plant records");report.checks.Add("PASS SceneView stroke Undo exact hash");report.passed=true;
            }
            catch(Exception e){report.checks.Add(e.ToString());Debug.LogException(e);}
            finally{window.Close();sceneView.Close();UnityEngine.Object.DestroyImmediate(layer.gameObject);File.WriteAllText("Assets/Tools/AdvancedGrassPainter/Verification/SceneView_report.json",JsonUtility.ToJson(report,true));EditorApplication.Exit(report.passed?0:1);}
        }
    }
}
