using UnityEditor;

namespace AdvancedCableTool.Editor
{
    public static class CableEditing
    {
        public sealed class Snapshot
        {
            internal string json;
            internal float length, slack;
        }
        public static Snapshot Capture(CableGenerator cable)
        {
            cable.CommitShape();
            return new Snapshot { json = EditorJsonUtility.ToJson(cable), length = cable.cableLength, slack = cable.slack };
        }
        public static bool Commit(CableGenerator cable, Snapshot before, out string message)
        {
            cable.Rebuild();
            bool valid = cable.ValidatePlacement(); message = valid ? null : cable.Status;
            if (valid) valid = CableSurfaceQuery.Validate(cable, out message);
            if (valid) { cable.CommitShape(); EditorUtility.SetDirty(cable); return true; }
            EditorJsonUtility.FromJsonOverwrite(before.json, cable);
            cable.cableLength = before.length; cable.slack = before.slack;
            cable.RestoreAuthoredShape(); EditorUtility.SetDirty(cable);
            return false;
        }
    }
}
