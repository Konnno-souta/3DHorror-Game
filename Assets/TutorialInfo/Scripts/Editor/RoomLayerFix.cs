using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 部屋の床が、Gameビューに映らない問題を直す。
///
/// 原因: キットの床(Floor Tile)は、レイヤー9に入っている。
///       このプロジェクトでは、Main Camera が レイヤー9 を映さない設定なので、
///       Sceneビューには映るのに、Gameビューでは床が消える。
///
/// やること:
///   1. 部屋の中のオブジェクトのレイヤーを、全部 Default にする。
///   2. テストで付けたマテリアル(Room_Floor ...)を、元の Floor Tile Mat に戻す。
///   3. 部屋のPrefabにも反映する。
///
/// メニュー: Tools / Room / 部屋の床を直す(レイヤーを Default にする)
/// 再生を止めてから実行すること。
/// </summary>
public static class RoomLayerFix
{
    [MenuItem("Tools/Room/部屋の床を直す(レイヤーを Default にする)")]
    public static void Fix()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Room 3x3", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        GameObject room = GameObject.Find("Room_3x3");
        if (room == null)
        {
            EditorUtility.DisplayDialog("Room 3x3", "シーンに Room_3x3 が見つかりません。", "OK");
            return;
        }

        // 1. レイヤー
        int layerCount = 0;
        Transform[] all = room.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t.gameObject.layer == 0) continue;

            Undo.RecordObject(t.gameObject, "部屋のレイヤーを Default にする");
            t.gameObject.layer = 0;
            EditorUtility.SetDirty(t.gameObject);
            layerCount++;
        }

        // 2. テスト用マテリアルを、元に戻す
        Material original = FindFloorTileMat();
        int materialCount = 0;
        if (original != null)
        {
            MeshRenderer[] renderers = room.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in renderers)
            {
                Material current = r.sharedMaterial;
                if (current == null) continue;
                if (!current.name.StartsWith("Room_Floor")) continue;

                Undo.RecordObject(r, "床のマテリアルを戻す");
                r.sharedMaterial = original;
                EditorUtility.SetDirty(r);
                materialCount++;
            }
        }

        // 3. Prefabにも反映
        if (PrefabUtility.IsPartOfPrefabInstance(room))
        {
            GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(room);
            if (outer != null)
            {
                PrefabUtility.ApplyPrefabInstance(outer, InteractionMode.AutomatedAction);
            }
        }

        EditorSceneManager.MarkSceneDirty(room.scene);

        EditorUtility.DisplayDialog("Room 3x3",
            "レイヤーを Default にしたオブジェクト: " + layerCount + " 個\n"
            + "元のマテリアルに戻した床: " + materialCount + " 枚\n\n"
            + "再生して、Gameビューで床が見えるか確認してください。", "OK");
    }

    private static Material FindFloorTileMat()
    {
        string[] guids = AssetDatabase.FindAssets("Floor Tile Mat t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("SciFi Warehouse Kit")) continue;
            if (System.IO.Path.GetFileNameWithoutExtension(path) != "Floor Tile Mat") continue;
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        return null;
    }
}
