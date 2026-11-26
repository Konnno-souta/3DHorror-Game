using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 「C」と「Floors & Floor Props」を、1つの「C」にまとめて整理する。
///
/// 整理のあと:
///   C
///   ├ Wall_C         (そのまま)
///   ├ Floors_C       (Floor Tiles)
///   ├ Corridors_C    (そのまま)
///   ├ Ceiling_C      (そのまま)
///   ├ Structure_C    (Pillars, Catwalks)
///   └ Props_C        (Crates Barrels Pallets, Shelves, Misc Props)
///
/// やること:
///   1. 親(C)と、各グループの中心(Position)を、0 にそろえる。子の見た目の位置は動かさない。
///   2. 「Floors & Floor Props」の中身を、上の形に移す。見た目の位置は動かさない。
///   3. 名前が実物と違う物を、元のPrefab名に直す(例: 棚なのに "Pallet" など)。
///   4. 中身を、種類と位置の順に並べる。
///   5. 空になったグループを消す(Others_C、Floors & Floor Props)。
///
/// Ctrl+Z で、まとめて元に戻せる。保存(Ctrl+S)するまで、ファイルは変わらない。
/// メニュー: Tools / Scene / CとFloors & Floor Propsを整理してまとめる
/// </summary>
public static class SceneOrganizerC
{
    private const string RootCName = "C";
    private const string RootFloorsName = "Floors & Floor Props";

    private static readonly List<string> log = new List<string>();
    private static int movedCount;
    private static int renamedCount;
    private static int pivotCount;

    [MenuItem("Tools/Scene/CとFloors & Floor Propsを整理してまとめる")]
    public static void Organize()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("整理", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject c = FindRoot(scene, RootCName);
        GameObject floors = FindRoot(scene, RootFloorsName);

        if (c == null)
        {
            EditorUtility.DisplayDialog("整理", "シーンの一番上に「C」が見つかりません。", "OK");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("整理",
            "「C」と「Floors & Floor Props」を、1つの「C」にまとめます。\n"
            + "見た目の位置は動きません。\n\n"
            + "Ctrl+Z で、まとめて元に戻せます。保存はしません。\n続けますか?",
            "続ける", "やめる");
        if (!ok) return;

        log.Clear();
        movedCount = 0;
        renamedCount = 0;
        pivotCount = 0;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("CとFloors & Floor Propsを整理する");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. 親の中心を、0にそろえる
        ResetPivot(c.transform);

        // 2. 新しいグループを作って、移す
        Transform floorsC = GetOrCreateGroup(c.transform, "Floors_C");
        Transform structureC = GetOrCreateGroup(c.transform, "Structure_C");
        Transform propsC = GetOrCreateGroup(c.transform, "Props_C");

        if (floors != null)
        {
            MoveChild(floors.transform, "Floor Tiles", floorsC);
            MoveChild(floors.transform, "Pillars", structureC);
            MoveChild(floors.transform, "Catwalks", structureC);
            MoveChild(floors.transform, "Crates Barrels Pallets", propsC);
            MoveChild(floors.transform, "Shelves", propsC);
            MoveChild(floors.transform, "Misc Props", propsC);

            // 上のどれでもない物は、Props_C に入れる(消えないように)
            List<Transform> leftovers = new List<Transform>();
            foreach (Transform t in floors.transform) leftovers.Add(t);
            foreach (Transform t in leftovers)
            {
                Undo.SetTransformParent(t, propsC, "残りをPropsに移す");
                log.Add("残りの物を Props_C に移した: " + t.name);
                movedCount++;
            }
        }
        else
        {
            log.Add("「Floors & Floor Props」が見つからないので、Cの整理だけ行った。");
        }

        // 3. グループの中心を、0にそろえる
        foreach (Transform group in new[] { floorsC, structureC, propsC })
        {
            List<Transform> groups = new List<Transform>();
            foreach (Transform t in group) groups.Add(t);
            foreach (Transform g in groups) ResetPivot(g);
        }

        // 4. 名前を直して、並べる
        foreach (Transform group in new[] { floorsC, structureC, propsC })
        {
            foreach (Transform sub in group)
            {
                FixNames(sub);
                SortChildren(sub, sub.name == "Floor Tiles");
            }
        }

        // 5. 空のグループを消す
        RemoveIfEmpty(FindChild(c.transform, "Others_C"));
        if (floors != null) RemoveIfEmpty(floors.transform);

        // Cの中の並び順
        string[] order = { "Wall_C", "Floors_C", "Corridors_C", "Ceiling_C", "Structure_C", "Props_C" };
        Undo.RegisterFullObjectHierarchyUndo(c, "Cの並び順");
        int index = 0;
        foreach (string groupName in order)
        {
            Transform t = FindChild(c.transform, groupName);
            if (t == null) continue;
            t.SetSiblingIndex(index);
            index++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = c;
        EditorGUIUtility.PingObject(c);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("整理しました。");
        sb.AppendLine();
        sb.AppendLine("移したグループ・物: " + movedCount + " 個");
        sb.AppendLine("中心を0にそろえた物: " + pivotCount + " 個");
        sb.AppendLine("名前を直した物: " + renamedCount + " 個");
        sb.AppendLine();
        sb.AppendLine("Ctrl+Z で元に戻せます。問題なければ、Ctrl+S で保存してください。");

        Debug.Log("[SceneOrganizerC]\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("整理", sb.ToString(), "OK");
    }

    // ---------------- 探す ----------------

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == rootName) return go;
        }
        return null;
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform t in parent)
        {
            if (t.name == childName) return t;
        }
        return null;
    }

    private static Transform GetOrCreateGroup(Transform parent, string groupName)
    {
        Transform existing = FindChild(parent, groupName);
        if (existing != null) return existing;

        GameObject go = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(go, "グループを作る");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        log.Add("グループを作った: " + groupName);
        return go.transform;
    }

    // ---------------- 移す ----------------

    private static void MoveChild(Transform from, string childName, Transform destination)
    {
        Transform child = FindChild(from, childName);
        if (child == null)
        {
            log.Add("見つからなかった(飛ばした): " + childName);
            return;
        }

        Undo.SetTransformParent(child, destination, "グループを移す");
        movedCount++;
        log.Add(childName + " を " + destination.name + " に移した");
    }

    /// <summary>
    /// グループの中心(Position)を 0 にそろえる。子の見た目の位置は動かさない。
    /// </summary>
    private static void ResetPivot(Transform group)
    {
        if (group.position.sqrMagnitude < 0.000001f) return;

        // 回転や拡大がある親では、子の形がゆがむことがあるので、飛ばす
        if (group.localScale != Vector3.one || group.rotation != Quaternion.identity)
        {
            log.Add("回転か拡大があるので、中心はそのまま: " + group.name);
            return;
        }

        List<Transform> kids = new List<Transform>();
        foreach (Transform t in group) kids.Add(t);

        try
        {
            foreach (Transform kid in kids) Undo.SetTransformParent(kid, null, "中心をそろえる");

            Undo.RecordObject(group, "中心をそろえる");
            group.position = Vector3.zero;

            for (int i = 0; i < kids.Count; i++)
            {
                Undo.SetTransformParent(kids[i], group, "中心をそろえる");
                kids[i].SetSiblingIndex(i);
            }

            pivotCount++;
            log.Add("中心を0にそろえた: " + group.name);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneOrganizerC] 中心をそろえられませんでした: " + group.name + "\n" + e.Message);
        }
    }

    // ---------------- 名前と並び順 ----------------

    /// <summary>
    /// 名前が、元のPrefab名と違う物を、直す。後ろの " (12)" のような番号は、そのまま残す。
    /// </summary>
    private static void FixNames(Transform group)
    {
        foreach (Transform child in group)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            if (source == null) continue;

            string currentName = child.name;
            string currentBase = StripSuffix(currentName);
            if (currentBase == source.name) continue;

            string suffix = currentName.Substring(currentBase.Length);
            Undo.RecordObject(child.gameObject, "名前を直す");
            child.gameObject.name = source.name + suffix;
            renamedCount++;
        }
    }

    private static string StripSuffix(string objectName)
    {
        // "Barrel (12)" -> "Barrel"
        if (objectName.EndsWith(")"))
        {
            int open = objectName.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0)
            {
                string number = objectName.Substring(open + 2, objectName.Length - open - 3);
                int parsed;
                if (int.TryParse(number, out parsed)) return objectName.Substring(0, open);
            }
        }
        return objectName;
    }

    private static void SortChildren(Transform group, bool byPositionOnly)
    {
        List<Transform> kids = new List<Transform>();
        foreach (Transform t in group) kids.Add(t);
        if (kids.Count < 2) return;

        Undo.RegisterFullObjectHierarchyUndo(group.gameObject, "並べ替える");

        kids.Sort((a, b) =>
        {
            if (!byPositionOnly)
            {
                int byName = string.CompareOrdinal(StripSuffix(a.name), StripSuffix(b.name));
                if (byName != 0) return byName;
            }

            Vector3 pa = a.position;
            Vector3 pb = b.position;

            int byY = Mathf.Round(pa.y * 10f).CompareTo(Mathf.Round(pb.y * 10f));
            if (byY != 0) return byY;
            int byZ = Mathf.Round(pa.z * 10f).CompareTo(Mathf.Round(pb.z * 10f));
            if (byZ != 0) return byZ;
            return Mathf.Round(pa.x * 10f).CompareTo(Mathf.Round(pb.x * 10f));
        });

        for (int i = 0; i < kids.Count; i++) kids[i].SetSiblingIndex(i);
    }

    // ---------------- 空のグループ ----------------

    private static void RemoveIfEmpty(Transform group)
    {
        if (group == null) return;
        if (group.childCount > 0) return;
        if (group.GetComponents<Component>().Length > 1) return;

        log.Add("空のグループを消した: " + group.name);
        Undo.DestroyObjectImmediate(group.gameObject);
    }
}
