using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
public static class RoadPackageSetup {
 static AddRequest request;
 public static void Install(){ request=Client.Add("com.unity.splines@2.9.1"); EditorApplication.update+=Poll; }
 static void Poll(){if(!request.IsCompleted)return; EditorApplication.update-=Poll; UnityEngine.Debug.Log(request.Status==StatusCode.Success?"ROAD_PACKAGE_OK":request.Error.message); EditorApplication.Exit(request.Status==StatusCode.Success?0:1);}
}
