using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// メニュー: Tools/Inventory/3D表示にライトを付ける
/// InventoryUI の itemDisplayCamera を探して、ItemDisplayLighting を付ける。
/// </summary>
public static class ItemDisplayLightingSetup
{
    [MenuItem("Tools/Inventory/3D表示にライトを付ける")]
    public static void Setup()
    {
        Camera cam = null;
        foreach (InventoryUI ui in Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedProperty p = new SerializedObject(ui).FindProperty("itemDisplayCamera");
            if (p != null && p.objectReferenceValue is Camera c) { cam = c; break; }
        }
        if (cam == null)
        {
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.name.Contains("ItemDisplay")) { cam = c; break; }
            }
        }
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Inventory", "ItemDisplayCamera が見つかりませんでした。", "OK");
            return;
        }

        if (cam.GetComponent<ItemDisplayLighting>() == null) Undo.AddComponent<ItemDisplayLighting>(cam.gameObject);
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Selection.activeGameObject = cam.gameObject;
        EditorUtility.DisplayDialog("Inventory", "「" + cam.name + "」に 3D表示用のライトを付けました。\nシーンを保存して、プレイで確認してください。", "OK");
    }
}
