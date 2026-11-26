using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 「passage」の中を、種類ごとのグループに整理してまとめる。
///
/// 整理のあと:
///   passage
///   ├ Corridors_passage  (Corridor ... の廊下パーツ、Tunnel、Catwalk)
///   ├ Walls_passage      (Wall ... の壁パーツ)
///   ├ Doors_passage      (KeyDoor など、動く扉)
///   ├ Lights_passage     (Corridor ... Lights、Hanging Light、BrokenLight などの電灯)
///   ├ Props_passage      (木箱、棚、消火器などの小物)
///   └ Others_passage     (どれでもない物)
///
/// やること:
///   1. passage の中心(Position)を 0 にそろえる。子の見た目の位置は動かさない。
///   2. passage の直下にある物と、パーツの中に入ってしまっているパーツを、種類ごとのグループに移す。
///      (見た目の位置は動かさない)
///   3. 名前が、元のPrefab名と違う物を直す。
///   4. グループの中を、名前と番号の順に並べる。
///
/// 触らない物:
///   ・passage の中に、すでにあるグループ(普通のGameObject)の中身
///   ・パーツの内側の部品(Tunnel Floor など)
///
/// Ctrl+Z で、まとめて元に戻せる。保存(Ctrl+S)するまで、ファイルは変わらない。
/// メニュー: Tools / Scene / passageを整理してまとめる
/// </summary>
public static class SceneOrganizerPassage
{
    private const string RootName = "passage";

    private static readonly string[] GroupOrder =
    {
        "Corridors_passage", "Walls_passage", "Doors_passage",
        "Lights_passage", "Props_passage", "Others_passage"
    };

    private static readonly List<string> log = new List<string>();
    private static int renamedCount;
    private static bool pivotReset;

    [MenuItem("Tools/Scene/passageを整理してまとめる")]
    public static void Organize()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("整理", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject passage = FindRoot(scene, RootName);
        if (passage == null)
        {
            EditorUtility.DisplayDialog("整理", "シーンの一番上に「passage」が見つかりません。", "OK");
            return;
        }

        // 移す物を集めて、数を見せる
        List<Transform> pieces = new List<Transform>();
        CollectPieces(passage.transform, false, pieces);

        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (Transform piece in pieces)
        {
            string group = Classify(piece);
            int n;
            counts.TryGetValue(group, out n);
            counts[group] = n + 1;
        }

        StringBuilder preview = new StringBuilder();
        preview.AppendLine("「passage」を整理します。見た目の位置は動きません。");
        preview.AppendLine();
        preview.AppendLine("移す物: " + pieces.Count + " 個");
        foreach (string groupName in GroupOrder)
        {
            int n;
            if (counts.TryGetValue(groupName, out n)) preview.AppendLine("  " + groupName + ": " + n);
        }
        preview.AppendLine();
        preview.AppendLine("Ctrl+Z で、まとめて元に戻せます。保存はしません。続けますか?");

        if (pieces.Count == 0)
        {
            EditorUtility.DisplayDialog("整理", "整理する物が見つかりませんでした。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("整理", preview.ToString(), "続ける", "やめる")) return;

        log.Clear();
        renamedCount = 0;
        pivotReset = false;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("passageを整理する");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. 中心を、0にそろえる
        pivotReset = ResetPivot(passage.transform);

        // 2. グループを作って、移す
        Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        foreach (Transform piece in pieces)
        {
            string groupName = Classify(piece);
            Transform group;
            if (!groups.TryGetValue(groupName, out group))
            {
                group = GetOrCreateGroup(passage.transform, groupName);
                groups[groupName] = group;
            }

            Undo.SetTransformParent(piece, group, "passageを整理する");
        }

        // 3. 名前を直して、並べる
        foreach (KeyValuePair<string, Transform> pair in groups)
        {
            FixNames(pair.Value);
            SortChildren(pair.Value);
        }

        // 4. グループの並び順
        Undo.RegisterFullObjectHierarchyUndo(passage, "passageの並び順");
        int index = 0;
        foreach (string groupName in GroupOrder)
        {
            Transform t = FindChild(passage.transform, groupName);
            if (t == null) continue;
            t.SetSiblingIndex(index);
            index++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = passage;
        EditorGUIUtility.PingObject(passage);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("整理しました。");
        sb.AppendLine();
        sb.AppendLine("移した物: " + pieces.Count + " 個");
        sb.AppendLine("名前を直した物: " + renamedCount + " 個");
        sb.AppendLine(pivotReset ? "passage の中心を 0 にそろえました。" : "passage の中心は、そのままです。");
        sb.AppendLine();
        sb.AppendLine("Ctrl+Z で元に戻せます。問題なければ、Ctrl+S で保存してください。");

        Debug.Log("[SceneOrganizerPassage]\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("整理", sb.ToString(), "OK");
    }

    // ---------------- 集める ----------------

    /// <summary>
    /// 移す物(動かせるPrefabの親)を集める。
    ///   ・passage の直下にあるPrefab
    ///   ・Prefabの子として、入ってしまっているPrefab
    /// すでにある普通のグループ(GameObject)の中は、触らない。
    /// </summary>
    private static void CollectPieces(Transform parent, bool insidePlainGroup, List<Transform> result)
    {
        foreach (Transform child in parent)
        {
            GameObject go = child.gameObject;

            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                if (!insidePlainGroup) result.Add(child);

                // Prefabの中に入っているPrefabも、探す
                CollectPieces(child, insidePlainGroup, result);
            }
            else if (!PrefabUtility.IsPartOfPrefabInstance(go))
            {
                // 普通のGameObject(すでにあるグループ)。中は触らない
                log.Add("触らなかったグループ: " + go.name);
            }
            else
            {
                // Prefabの内側の部品。さらに中を探す
                CollectPieces(child, insidePlainGroup, result);
            }
        }
    }

    // ---------------- 種類わけ ----------------

    private static string Classify(Transform piece)
    {
        string name = GetSourceName(piece);
        string lower = name.ToLowerInvariant();

        if (lower.Contains("keydoor")) return "Doors_passage";
        if (lower.Contains("light")) return "Lights_passage";

        if (lower.StartsWith("corridor") || lower.Contains("tunnel") || lower.Contains("catwalk")
            || lower.Contains("incline") || lower.Contains("stairs"))
        {
            return "Corridors_passage";
        }

        if (lower.StartsWith("wall") || lower.Contains("door")) return "Walls_passage";

        string[] propKeywords =
        {
            "barrel", "crate", "pallet", "shelf", "shelves", "cart", "garbage", "extinguisher",
            "cam", "fusebox", "pipes", "sprinkler", "duct", "sign", "pillar", "support"
        };
        foreach (string keyword in propKeywords)
        {
            if (lower.Contains(keyword)) return "Props_passage";
        }

        return "Others_passage";
    }

    private static string GetSourceName(Transform piece)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(piece.gameObject);
        return source != null ? source.name : piece.name;
    }

    // ---------------- 部品 ----------------

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

    /// <summary>
    /// グループの中心(Position)を 0 にそろえる。子の見た目の位置は動かさない。
    /// </summary>
    private static bool ResetPivot(Transform group)
    {
        if (group.position.sqrMagnitude < 0.000001f) return false;

        if (group.localScale != Vector3.one || group.rotation != Quaternion.identity)
        {
            log.Add("回転か拡大があるので、中心はそのまま: " + group.name);
            return false;
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

            log.Add("中心を0にそろえた: " + group.name);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SceneOrganizerPassage] 中心をそろえられませんでした: " + group.name + "\n" + e.Message);
            return false;
        }
    }

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

    private static int SuffixNumber(string objectName)
    {
        string baseName = StripSuffix(objectName);
        if (baseName.Length == objectName.Length) return -1;

        string number = objectName.Substring(baseName.Length + 2, objectName.Length - baseName.Length - 3);
        int parsed;
        return int.TryParse(number, out parsed) ? parsed : -1;
    }

    /// <summary>
    /// 名前の順に並べる。同じ名前では、後ろの番号 (3) (28) の小さい順。
    /// </summary>
    private static void SortChildren(Transform group)
    {
        List<Transform> kids = new List<Transform>();
        foreach (Transform t in group) kids.Add(t);
        if (kids.Count < 2) return;

        Undo.RegisterFullObjectHierarchyUndo(group.gameObject, "並べ替える");

        kids.Sort((a, b) =>
        {
            int byName = string.CompareOrdinal(StripSuffix(a.name), StripSuffix(b.name));
            if (byName != 0) return byName;
            return SuffixNumber(a.name).CompareTo(SuffixNumber(b.name));
        });

        for (int i = 0; i < kids.Count; i++) kids[i].SetSiblingIndex(i);
    }
}
