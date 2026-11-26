using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 敵が扉を通れるように、扉の板を、NavMeshの焼き込み(Bake)から外す。
///
/// 板をBakeに含めたままだと、扉の場所にNavMeshの穴ができて、敵が通れない。
/// 板を外しておけば、扉の開口部にもNavMeshができる。
/// 閉まっている間は、KeyDoor が NavMeshObstacle で、敵の経路をふさぐ。
///
/// メニュー: Tools / Door / ドアをNavMeshの焼き込みから外す
/// 実行したあと、NavMesh Surface を選んで、Bake し直すこと。
/// </summary>
public static class KeyDoorNavMeshSetup
{
    private const string KeyDoorPrefabPath = "Assets/Prefab/KeyDoor_BayDoor.prefab";

    [MenuItem("Tools/Door/ドアをNavMeshの焼き込みから外す")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Door", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        System.Type modifierType = System.Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
        if (modifierType == null)
        {
            EditorUtility.DisplayDialog("Door",
                "NavMeshModifier が見つかりません。\n"
                + "Window > Package Management > Package Manager で、AI Navigation パッケージを入れてください。\n\n"
                + "(古い方式で Navigation Static を使っているなら、扉の板の Static から Navigation Static を外してください)", "OK");
            return;
        }

        int prefabCount = 0;
        int sceneCount = 0;

        // 1. Prefab本体
        if (File.Exists(KeyDoorPrefabPath))
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(KeyDoorPrefabPath);
            try
            {
                prefabCount = SetupAll(contents, modifierType, false);
                PrefabUtility.SaveAsPrefabAsset(contents, KeyDoorPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // 2. シーンの中の扉
        KeyDoor[] doors = Object.FindObjectsByType<KeyDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (KeyDoor door in doors)
        {
            sceneCount += SetupDoor(door, modifierType, true);
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Door",
            "扉の板を、NavMeshの焼き込みから外しました。\n\n"
            + "Prefab: " + prefabCount + " 個\n"
            + "シーン: " + sceneCount + " 個\n\n"
            + "次に、NavMesh Surface を選んで、Bake し直してください。", "OK");
    }

    private static int SetupAll(GameObject root, System.Type modifierType, bool useUndo)
    {
        int count = 0;
        KeyDoor[] doors = root.GetComponentsInChildren<KeyDoor>(true);
        foreach (KeyDoor door in doors)
        {
            count += SetupDoor(door, modifierType, useUndo);
        }
        return count;
    }

    private static int SetupDoor(KeyDoor door, System.Type modifierType, bool useUndo)
    {
        SerializedObject so = new SerializedObject(door);
        SerializedProperty leafProperty = so.FindProperty("doorLeaf");
        Transform leaf = leafProperty != null ? leafProperty.objectReferenceValue as Transform : null;
        if (leaf == null) return 0;

        Component modifier = leaf.GetComponent(modifierType);
        if (modifier == null)
        {
            modifier = useUndo
                ? Undo.AddComponent(leaf.gameObject, modifierType)
                : leaf.gameObject.AddComponent(modifierType);
        }

        System.Reflection.PropertyInfo ignoreProperty = modifierType.GetProperty("ignoreFromBuild");
        if (ignoreProperty == null) return 0;

        ignoreProperty.SetValue(modifier, true);
        EditorUtility.SetDirty(modifier);
        return 1;
    }
}
