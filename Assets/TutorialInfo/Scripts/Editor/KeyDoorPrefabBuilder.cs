#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wall BayDoor(SciFi Warehouse Kit)から、鍵で開く扉のPrefabを自動で作るエディタ拡張。
/// メニュー: Tools > Door > Wall BayDoor から鍵付きドアPrefabを作る
/// このファイルは、Assets/Editor フォルダの中に置くこと。
/// </summary>
public static class KeyDoorPrefabBuilder
{
    private const string SourcePrefabName = "Wall BayDoor";
    private const string OutputFolder = "Assets/Prefab";
    private const string OutputName = "KeyDoor_BayDoor";

    // 扉の開口部(Wall BayDoorのローカル座標、単位はメートル)
    private const float OpeningZMin = 0.86f;
    private const float OpeningZMax = 5.14f;
    private const float OpeningTop = 5.62f;
    private const float WallHeight = 9f;
    private const float WallThickness = 0.3f;
    private const float WallWidth = 6f;

    [MenuItem("Tools/Door/Wall BayDoor から鍵付きドアPrefabを作る")]
    public static void BuildBayDoor()
    {
        string sourcePath = AssetDatabase.FindAssets(SourcePrefabName + " t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == SourcePrefabName);

        if (string.IsNullOrEmpty(sourcePath))
        {
            EditorUtility.DisplayDialog("見つかりません",
                "'" + SourcePrefabName + "' のPrefabがプロジェクトに見つかりませんでした。", "OK");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = OutputName;
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        Transform leaf = FindChild(root.transform, "LP_Bay_Door_snaps");
        Transform wall = FindChild(root.transform, "LP_Bay_Door_Wall_snaps");

        if (leaf == null || wall == null)
        {
            Object.DestroyImmediate(root);
            EditorUtility.DisplayDialog("構造が違います",
                "LP_Bay_Door_snaps または LP_Bay_Door_Wall_snaps が見つかりませんでした。", "OK");
            return;
        }

        FixWallColliders(wall);
        AddLeafCollider(leaf);
        AddTrigger(root);
        AddKeyDoor(root, leaf);

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefab");
        }

        string path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/" + OutputName + ".prefab");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        Debug.Log("[KeyDoorPrefabBuilder] 作成しました: " + path);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName);
    }

    /// <summary>
    /// 壁全体をふさぐ大きなBoxColliderを外し、扉の開口部だけあけた、3つのColliderに置き換える。
    /// </summary>
    private static void FixWallColliders(Transform wall)
    {
        foreach (BoxCollider box in wall.GetComponents<BoxCollider>())
        {
            bool isBigWall = box.size.y > 8f && box.size.z > 5f;
            if (isBigWall)
            {
                Object.DestroyImmediate(box, true);
            }
        }

        float sideWidth = OpeningZMin;

        // 左の壁
        AddBox(wall.gameObject,
            new Vector3(0f, WallHeight * 0.5f, sideWidth * 0.5f),
            new Vector3(WallThickness, WallHeight, sideWidth));

        // 右の壁
        AddBox(wall.gameObject,
            new Vector3(0f, WallHeight * 0.5f, WallWidth - (WallWidth - OpeningZMax) * 0.5f),
            new Vector3(WallThickness, WallHeight, WallWidth - OpeningZMax));

        // 開口部の上の壁
        float topHeight = WallHeight - OpeningTop;
        AddBox(wall.gameObject,
            new Vector3(0f, OpeningTop + topHeight * 0.5f, (OpeningZMin + OpeningZMax) * 0.5f),
            new Vector3(WallThickness, topHeight, OpeningZMax - OpeningZMin));
    }

    private static void AddBox(GameObject target, Vector3 center, Vector3 size)
    {
        BoxCollider box = target.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }

    /// <summary>
    /// 扉の板に、見た目の大きさに合わせたColliderを付ける。下は床まで伸ばして、隙間をなくす。
    /// </summary>
    private static void AddLeafCollider(Transform leaf)
    {
        Renderer[] renderers = leaf.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        Vector3 e = world.extents;
        for (int ix = -1; ix <= 1; ix += 2)
        for (int iy = -1; iy <= 1; iy += 2)
        for (int iz = -1; iz <= 1; iz += 2)
        {
            Vector3 corner = world.center + new Vector3(e.x * ix, e.y * iy, e.z * iz);
            Vector3 local = leaf.InverseTransformPoint(corner);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        min.y = Mathf.Min(min.y, 0f);

        BoxCollider box = leaf.gameObject.AddComponent<BoxCollider>();
        box.center = (min + max) * 0.5f;
        box.size = max - min;
    }

    /// <summary>
    /// プレイヤーが近づいたことを調べる、トリガー用のColliderを、ルートに付ける。
    /// KeyDoorがルートに付くので、OnTriggerEnterをそのまま受け取れる。
    /// </summary>
    private static void AddTrigger(GameObject root)
    {
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 2.5f, WallWidth * 0.5f);
        box.size = new Vector3(5f, 5f, WallWidth - 0.5f);
    }

    private static void AddKeyDoor(GameObject root, Transform leaf)
    {
        KeyDoor door = root.AddComponent<KeyDoor>();

        SerializedObject so = new SerializedObject(door);
        SetObject(so, "doorLeaf", leaf);
        SetBool(so, "hideLeafWhenOpen", true);

        // デバッグ用: 鍵(Tag = Key)が入ったアイテム図鑑を、自動で見つけて設定する
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDatabase"))
        {
            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (db != null && db.GetItemByTag("Key") != null)
            {
                SetObject(so, "debugItemDatabase", db);
                break;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject so, string field, bool value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.boolValue = value;
    }
}
#endif
