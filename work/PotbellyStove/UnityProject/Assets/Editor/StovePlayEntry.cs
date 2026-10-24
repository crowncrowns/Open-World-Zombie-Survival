using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class StovePlayEntry
{
    public static void Run()
    {
        StoveBuilder.Validate();
        EditorSceneManager.OpenScene(StoveBuilder.Root+"/Demo/PotbellyStove_Demo.unity");
        new GameObject("Isolated_Runtime_Probe").AddComponent<StoveRuntimeProbe>();
        EditorApplication.isPlaying=true;
    }
}
