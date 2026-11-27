using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 「遠くの部屋の作りが途中から消える」のを直す。メニュー: Tools/Optimize/遠くが消えるのを直す
///  1. カメラの見える距離 (Far) が 80m しかなく、大きい部屋の奥が切れていた → 150m に
///  2. 霧が濃すぎて、30m くらいで真っ黒になっていた → 少し薄く
///  3. 遠くのライトを消す距離 (LightCuller) が 45m で、奥の壁が真っ暗だった → 70m に
/// </summary>
public static class FarViewFixer
{
    private const float CameraFar = 150f;
    private const float FogDensity = 0.03f;
    private const float CullDistance = 70f;

    [MenuItem("Tools/Optimize/遠くが消えるのを直す")]
    public static void Fix()
    {
        string log = "";

        // 1. カメラ(インベントリ用の RenderTexture カメラ以外)
        int cams = 0;
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.targetTexture != null) continue;
            if (c.farClipPlane >= CameraFar) continue;
            Undo.RecordObject(c, "Far");
            c.farClipPlane = CameraFar;
            cams++;
        }
        log += "カメラの Far を " + CameraFar + "m に: " + cams + "個\n";

        // 2. 霧
        int fogs = 0;
        foreach (HorrorFog f in Object.FindObjectsByType<HorrorFog>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(f);
            SerializedProperty d = so.FindProperty("density");
            if (d != null && d.floatValue > FogDensity)
            {
                d.floatValue = FogDensity;
                so.ApplyModifiedProperties();
                fogs++;
            }
        }
        if (RenderSettings.fogDensity > FogDensity) RenderSettings.fogDensity = FogDensity;
        log += "霧の濃さを " + FogDensity + " に: " + fogs + "個\n";

        // 3. 遠くのライトを消す距離
        int cullers = 0;
        foreach (LightCuller lc in Object.FindObjectsByType<LightCuller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(lc);
            SerializedProperty m = so.FindProperty("maxDistance");
            if (m != null && m.floatValue < CullDistance)
            {
                m.floatValue = CullDistance;
                so.ApplyModifiedProperties();
                cullers++;
            }
        }
        log += "ライトを消す距離を " + CullDistance + "m に: " + cullers + "個";

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[FarView]\n" + log);
        EditorUtility.DisplayDialog("遠くが消えるのを直す", log + "\n\nシーンを保存して、プレイで確認してください。", "OK");
    }
}
