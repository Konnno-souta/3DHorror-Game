using UnityEngine;
using UnityEditor;

public static class FindBadSubmeshes
{
    [MenuItem("Tools/Fix Bad Submeshes (Trim Materials + Unstatic Empty)")]
    public static void Fix()
    {
        int trimmed = 0, unstatic = 0;
        foreach (var mr in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>();

            if (mf == null || mf.sharedMesh == null)
            {
                Undo.RecordObject(mr.gameObject, "Unstatic empty mesh");
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, 0);
                unstatic++;
                continue;
            }

            int sub = mf.sharedMesh.subMeshCount;
            var mats = mr.sharedMaterials;
            if (mats.Length > sub)
            {
                Undo.RecordObject(mr, "Trim materials");
                var trimmedMats = new Material[sub];
                for (int i = 0; i < sub; i++) trimmedMats[i] = mats[i];
                mr.sharedMaterials = trimmedMats;
                EditorUtility.SetDirty(mr);
                trimmed++;
            }
        }
        Debug.Log($"ƒ}ƒeƒŠƒAƒ‹íŒ¸: {trimmed} Œ / Static‰ðœ: {unstatic} Œ");
    }
}