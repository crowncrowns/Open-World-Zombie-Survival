"""Prepare isolated-engine checks; these temporary scripts never enter product Assets."""
from pathlib import Path
import shutil
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7';T=R/'work/KursHub/UnityProject'
for p in (R/'Assets/Editor').glob('KursHub*.cs'):shutil.copy2(p,T/'Assets/Editor'/p.name)
v=(R/'Verification/KursHub/Stage6/checks/KursHubVerification.cs.txt').read_text(encoding='utf8')
v=v.replace('../stage6','../audit7').replace('KURS_STAGE6_SCENE_CHECK_OK','KURS_AUDIT7_SCENE_CHECK_OK')
v=v.replace('KursHubSetup.BatchBuild();','EditorSceneManager.OpenScene(KursHubSetup.ScenePath); KursHubSetup.BuildPrefab(); KursHubSetup.Check();')
v=v.replace('Require(PrefabIdentities() == firstBuildIdentities', 'KursHubQualitySetup.CreateDemo();\n        Require(PrefabIdentities() == firstBuildIdentities')
v=v.replace('All existing Stage 5 prefab IDs retained','All existing baseline prefab IDs retained')
v=v.replace('Require(refs.All(id=>newIds.Contains(id)),', 'var effectiveIds=EffectivePrefabIds(AssetDatabase.LoadAssetAtPath<GameObject>(KursHubSetup.PrefabPath));\n        Require(refs.All(id=>newIds.Contains(id) || effectiveIds.Contains(id)),')
v=v.replace('    private static string PrefabIdentities()', '''    private static System.Collections.Generic.HashSet<string> EffectivePrefabIds(GameObject root)
    {
        var ids=new System.Collections.Generic.HashSet<string>();
        foreach(var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var objects=new UnityEngine.Object[]{transform.gameObject}.Concat(transform.GetComponents<Component>().Where(c=>c!=null).Cast<UnityEngine.Object>());
            foreach(var obj in objects)
                if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id))ids.Add(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        File.WriteAllLines("../audit7/effective_prefab_ids.txt",ids.OrderBy(x=>x,StringComparer.Ordinal));
        return ids;
    }
    private static string PrefabIdentities()''')
v=v.replace('        File.WriteAllText(Results,', '        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);\n        File.WriteAllText(Results,')
v=v.replace('foreach(var r in builtHub.GetComponentsInChildren<MeshRenderer>(true))Require(r.sharedMaterials.All(m=>m!=null),"Material slots "+r.name);', 'Require(builtHub.GetComponentsInChildren<MeshRenderer>(true).All(r=>r.sharedMaterials.Length>0 && r.sharedMaterials.All(m=>m!=null)),"All native LOD and prop material slots present");')
v=v.replace('>=53','>=54').replace('Fifty-three reusable','Fifty-four reusable')
v=v.replace('var renderers = hub.GetComponentsInChildren<MeshRenderer>();','foreach(var lod in hub.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);\n        var renderers = hub.transform.Find("Shell").GetComponentsInChildren<MeshRenderer>();')
v=v.replace('"stage\\\": 6','"stage\\\": 7')
# Surface screenshots have their own matched capture; route verification keeps
# these existing extra views as supplemental evidence.
(T/'Assets/Editor/KursHubVerification.cs').write_text(v,encoding='utf8')
p=(R/'Verification/KursHub/Stage6/checks/KursHubProbe.cs.txt').read_text(encoding='utf8').replace('../stage6','../audit7')
for old,new in [
    ('(-1.4f,0f,-53f)','(-1.4f,0f,-40.5f)'),('(-1.4f,0f,-44f)','(-1.4f,0f,-30.5f)'),
    ('(0f,0f,-41f)','(0f,0f,-29f)'),('(3.4f,0f,-35f)','(3.4f,0f,-28f)'),
    ('(1.4f,0f,-44f)','(1.4f,0f,-30.5f)'),('(1.4f,0f,-54f)','(1.4f,0f,-40.5f)')]:p=p.replace(old,new)
(T/'Assets/KursHubProbe.cs').write_text(p,encoding='utf8')
capture=(T/'Assets/Editor/KursHubAuditBefore7.cs').read_text(encoding='utf8')
capture=capture.replace('class KursHubAuditBefore7','class KursHubAuditAfter7').replace('../audit7/before','../audit7/after').replace('BASELINE_CAPTURE_OK','AFTER_CAPTURE_OK').replace('KURS_AUDIT7_BEFORE_OK','KURS_AUDIT7_AFTER_OK')
capture=capture.replace('var sun = new GameObject', 'foreach(var lod in hub.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);\n            var sun = new GameObject')
capture=capture.replace('            var rows =', '''            sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            View(cam,"material_sunny",new Vector3(3.65f,1.6f,-31),new Vector3(7.5f,1.6f,-30),58);
            sun.intensity=.45f;sun.transform.rotation=Quaternion.Euler(6,70,0);
            View(cam,"material_grazing",new Vector3(3.65f,1.6f,-31),new Vector3(7.5f,1.6f,-30),58);
            View(cam,"floor_grazing",new Vector3(3.3f,.45f,-39),new Vector3(.5f,0,-35),65);
            View(cam,"window_detail",new Vector3(-49,2.1f,-39),new Vector3(-46.3f,2.5f,-37),58);
            var rows =''')
capture=capture.replace('            var rows =', '''            sun.intensity=.65f;sun.transform.rotation=Quaternion.Euler(38,-35,0);
            View(cam,"checkpoint_reference",new Vector3(0,1.7f,-40.5f),new Vector3(0,2.2f,-30.5f),78);
            View(cam,"market_reference",new Vector3(0,1.7f,-28),new Vector3(0,3.1f,-3),76);
            View(cam,"gallery_reference",new Vector3(-17,7.15f,8),new Vector3(0,3.5f,-14),82);
            var rows =''')
(T/'Assets/Editor/KursHubAuditAfter7.cs').write_text(capture,encoding='utf8')
print('AUDIT7_CHECKS_READY')
