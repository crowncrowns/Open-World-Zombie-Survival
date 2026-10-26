using UnityEngine;

/// <summary>Small standalone presentation UI, used only by MetalGrilles_Demo.</summary>
public sealed class MetalGrillesDemo : MonoBehaviour
{
    public Camera mainCamera;
    public Camera galleryCamera;
    private WindowGrilleHinge[] hinges;

    private void Start() => hinges = FindObjectsByType<WindowGrilleHinge>();

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16,16,265,170),GUI.skin.box);
        GUILayout.Label("Оконные решётки 1 × 1 м");
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("5 окон")) SetView(false);
        if(GUILayout.Button("15 вариантов")) SetView(true);
        GUILayout.EndHorizontal();
        if(GUILayout.Button("Открыть обе створки")) SetHinges(true);
        if(GUILayout.Button("Закрыть обе створки")) SetHinges(false);
        GUILayout.Label("A — лёгкий износ   B — сильный\nC — повреждения и ремонт");
        GUILayout.EndArea();
    }

    private void SetView(bool gallery)
    {
        if(mainCamera!=null) mainCamera.enabled=!gallery;
        if(galleryCamera!=null) galleryCamera.enabled=gallery;
    }

    private void SetHinges(bool open)
    {
        if(hinges==null) hinges=FindObjectsByType<WindowGrilleHinge>();
        foreach(var hinge in hinges) hinge.SetOpen(open);
    }
}
