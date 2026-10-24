using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PostApocalypse.Graffiti;
using PostApocalypse.Graffiti.Editor;

public static class GraffitiSceneViewVerify
{
    static GraffitiPainter painter;
    static SceneView view;
    static int step,before;
    static bool military;
    static double due;
    static string Out=>Path.GetFullPath(Application.dataPath+"/../../../../Verification/GraffitiPack");
    public static void Run()=>Begin(false);
    public static void RunMilitary(){GraffitiMilitaryVerify.RefreshGallery();Begin(true);}
    static void Begin(bool isMilitary)
    {
        military=isMilitary;
        if(!Application.dataPath.Replace('\\','/').Contains("/work/GraffitiPack/"))throw new Exception("Isolation required");
        EditorSceneManager.OpenScene(GraffitiPainter.Root+"/Demo/GraffitiGallery.unity");
        var wall=GameObject.Find(military?"Wall_51_3x3m":"Wall_01_3x3m");wall.isStatic=false;UnityEngine.Object.DestroyImmediate(wall.GetComponent<Collider>());GraffitiSurfaceQuery.Invalidate();
        painter=EditorWindow.GetWindow<GraffitiPainter>();painter.Show();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(GraffitiPainter).GetField("painting",flags).SetValue(painter,true);
        if(military){typeof(GraffitiPainter).GetField("selected",flags).SetValue(painter,50);typeof(GraffitiPainter).GetField("category",flags).SetValue(painter,6);}
        typeof(GraffitiPainter).GetField("width",flags).SetValue(painter,.8f);
        typeof(GraffitiPainter).GetField("sizeSpread",flags).SetValue(painter,0f);
        typeof(GraffitiPainter).GetField("angleSpread",flags).SetValue(painter,0f);
        typeof(GraffitiPainter).GetField("alphaSpread",flags).SetValue(painter,0f);
        view=EditorWindow.GetWindow<SceneView>();view.Show();view.Focus();
        view.pivot=wall.transform.position;view.rotation=Quaternion.identity;view.size=2;view.orthographic=true;
        Physics.SyncTransforms();before=Count();step=0;due=EditorApplication.timeSinceStartup+2;
        EditorApplication.update+=Tick;
    }
    static int Count()=>UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Count(x=>x.paintedByTool);
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<due)return;
        try
        {
            Vector2 center=new Vector2(view.position.width*.5f,view.position.height*.5f);
            if(step==0)
            {
                view.SendEvent(new Event{type=EventType.MouseMove,mousePosition=center});view.Repaint();step++;due=EditorApplication.timeSinceStartup+1;return;
            }
            if(step==1)
            {
                view.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=center});
                view.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=center});step++;due=EditorApplication.timeSinceStartup+1;return;
            }
            if(step==2)
            {
                if(Count()!=before+1)throw new Exception("SceneView mouse event did not place one graffiti: "+Count()+" / "+before);
                if(military&&!UnityEngine.Object.FindObjectsByType<GraffitiPlacement>(FindObjectsSortMode.None).Any(p=>p.paintedByTool&&p.entry.number==51))throw new Exception("Military selection was not placed");
                Undo.PerformUndo();if(Count()!=before)throw new Exception("SceneView Undo");
                Undo.PerformRedo();if(Count()!=before+1)throw new Exception("SceneView Redo");
                File.WriteAllText(Out+(military?"/sceneview_military_checks.json":"/sceneview_building_fix.json"),"{\"nonStaticWallWithoutCollider\":true,\"editorMouseEventPlacement\":true,\"undo\":true,\"redo\":true,\"nativeOSMouse\":false}");
                painter.Close();EditorApplication.update-=Tick;Debug.Log("GRAFFITI_SCENEVIEW_PASS");EditorApplication.Exit(0);
            }
        }
        catch(Exception ex){EditorApplication.update-=Tick;Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
