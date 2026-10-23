var a=UnityEditor.Selection.activeGameObject;
return new{selected=a?a.name:null,position=a?a.transform.position.ToString():null,renderers=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.sharedMaterials.Any(m=>m&&m.name=="M_GreenStairwellWall_World")).Take(12).Select(r=>new{name=r.name,min=r.bounds.min.ToString(),max=r.bounds.max.ToString()}).ToArray()};
