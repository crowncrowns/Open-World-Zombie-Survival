using UnityEditor;
using UnityEngine;
namespace TerrainRoads.Editor {
// Capture native material drag-and-drop before a scene save, as well as before Rebuild.
public sealed class TerrainRoadSurfaceMapSave : AssetModificationProcessor {
 static string[] OnWillSaveAssets(string[] paths){foreach(var map in Object.FindObjectsByType<TerrainRoadSurfaceMap>(FindObjectsInactive.Include))map.CaptureMaterials();return paths;}
}
}

