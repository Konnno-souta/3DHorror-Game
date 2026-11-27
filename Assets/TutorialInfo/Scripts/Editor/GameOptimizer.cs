using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ゲームを軽くする。
/// メニュー:
///   Tools / Optimize / 重さの原因を調べる   … 何も変えずに、数を数えて見せる
///   Tools / Optimize / ゲームを軽くする     … 下の内容を、まとめて行う(Ctrl+Z でもどせる)
///
/// 一番重いのは、電灯の影。点の電灯(Point Light)の影は、1つで6回分の絵を描くので、
/// 影つきの電灯が何十個もあると、それだけでとても重くなる。
///
/// ゲームを軽くする で変えること:
///   [今のシーン]
///   1. 点の電灯(Point)の影を、全部オフにする(電灯の光は、そのまま)
///   2. スポットライトの影は、カメラやプレイヤーに付いている物(懐中電灯)だけ残して、ほかはオフ
///   3. 太陽(Directional)の影をオフにする(屋内のゲームなので、ほぼ見えない)
///   4. LightCuller を置く(カメラから遠い電灯を、自動で消す)
///   5. Main Camera の、描く距離(Far)を 80m にする(霧の向こうは見えないので)
///   6. 小さな物(0.6m より小さい)の影を、オフにする
///   [URP の設定(Assets/Settings の PC_RPAsset など、今使っている物)]
///   7. 影の品質を下げる: やわらかい影は「低」、影の画像の大きさ 1024、段数 2、影を描く距離 30m
///   8. Opaque Texture(画面のコピー)をオフ
///   9. SSAO(すき間の暗さ)をオフ
/// </summary>
public static class GameOptimizer
{
    private const float SmallObjectSize = 0.6f;
    private const float CameraFar = 80f;

    // ---------------- 調べるだけ ----------------

    [MenuItem("Tools/Optimize/重さの原因を調べる", false, 1)]
    public static void Report()
    {
        EditorUtility.DisplayDialog("重さの原因", BuildReport(), "OK");
    }

    private static string BuildReport()
    {
        int point = 0, pointShadow = 0, spot = 0, spotShadow = 0, dir = 0, dirShadow = 0;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            bool shadow = l.shadows != LightShadows.None;
            switch (l.type)
            {
                case LightType.Point: point++; if (shadow) pointShadow++; break;
                case LightType.Spot: spot++; if (shadow) spotShadow++; break;
                case LightType.Directional: dir++; if (shadow) dirShadow++; break;
            }
        }

        int renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        int particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("今のシーン:");
        sb.AppendLine(" 点の電灯 " + point + " 個(影つき " + pointShadow + " 個) ← 影つきが一番重い");
        sb.AppendLine(" スポットライト " + spot + " 個(影つき " + spotShadow + " 個)");
        sb.AppendLine(" 太陽 " + dir + " 個(影つき " + dirShadow + " 個)");
        sb.AppendLine(" 見える物 " + renderers + " 個、パーティクル " + particles + " 個");

        UniversalRenderPipelineAsset urp = CurrentUrp();
        if (urp != null)
        {
            SerializedObject so = new SerializedObject(urp);
            sb.AppendLine();
            sb.AppendLine("URP の設定(" + urp.name + "):");
            sb.AppendLine(" やわらかい影の品質 " + QualityName(Int(so, "m_SoftShadowQuality")));
            sb.AppendLine(" 太陽の影の画像 " + Int(so, "m_MainLightShadowmapResolution") + "、段数 " + Int(so, "m_ShadowCascadeCount") + "、距離 " + Float(so, "m_ShadowDistance") + "m");
            sb.AppendLine(" 電灯の影の画像 " + Int(so, "m_AdditionalLightsShadowmapResolution"));
            sb.AppendLine(" Opaque Texture " + (Int(so, "m_RequireOpaqueTexture") == 1 ? "オン" : "オフ"));
            sb.AppendLine(" SSAO " + (IsSsaoOn(urp) ? "オン" : "オフ"));
        }

        return sb.ToString();
    }

    // ---------------- 軽くする ----------------

    [MenuItem("Tools/Optimize/ゲームを軽くする", false, 2)]
    public static void Optimize()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("ゲームを軽くする", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("ゲームを軽くする",
            BuildReport() + "\nこれを、軽くします。\n"
            + "・電灯の影をオフ(懐中電灯は残す)、遠い電灯を自動で消す\n"
            + "・カメラの描く距離を " + CameraFar + "m、小さな物の影をオフ\n"
            + "・URP の影の品質を下げる、Opaque Texture と SSAO をオフ\n\n"
            + "電灯の光や見た目の明るさは、ほとんど変わりません。Ctrl+Z でもどせます。続けますか?",
            "続ける", "やめる"))
        {
            return;
        }

        Undo.SetCurrentGroupName("ゲームを軽くする");
        int group = Undo.GetCurrentGroup();

        List<string> done = new List<string>();

        // ---- 電灯 ----
        Camera cam = Camera.main;
        GameObject player = GameObject.FindWithTag("Player");
        int offPoint = 0, offSpot = 0, offDir = 0;

        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.shadows == LightShadows.None) continue;

            bool onPlayer = (cam != null && l.transform.IsChildOf(cam.transform))
                || (player != null && l.transform.IsChildOf(player.transform));

            if (l.type == LightType.Point) { SetNoShadow(l); offPoint++; }
            else if (l.type == LightType.Spot && !onPlayer) { SetNoShadow(l); offSpot++; }
            else if (l.type == LightType.Directional) { SetNoShadow(l); offDir++; }
            else if (l.type == LightType.Spot && onPlayer && l.shadows == LightShadows.Soft)
            {
                // 懐中電灯の影は残すが、かたい影にして軽くする
                Undo.RecordObject(l, "ゲームを軽くする");
                l.shadows = LightShadows.Hard;
            }
        }
        done.Add("電灯の影をオフ: 点 " + offPoint + "、スポット " + offSpot + "、太陽 " + offDir);

        // ---- LightCuller ----
        System.Type cullerType = FindType("LightCuller");
        if (cullerType != null)
        {
            if (Object.FindFirstObjectByType(cullerType) == null)
            {
                GameObject go = new GameObject("LightCuller");
                Undo.RegisterCreatedObjectUndo(go, "ゲームを軽くする");
                go.AddComponent(cullerType);
                done.Add("LightCuller を置いた(遠い電灯を自動で消す)");
            }
        }
        else
        {
            done.Add("LightCuller.cs が見つからないので、遠い電灯の自動オフは飛ばした");
        }

        // ---- カメラ ----
        if (cam != null && cam.farClipPlane > CameraFar)
        {
            Undo.RecordObject(cam, "ゲームを軽くする");
            cam.farClipPlane = CameraFar;
            done.Add("Main Camera の Far を " + CameraFar + "m に");
        }

        // ---- 小さな物の影 ----
        int smallOff = 0;
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
            Vector3 size = r.bounds.size;
            if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < SmallObjectSize)
            {
                Undo.RecordObject(r, "ゲームを軽くする");
                r.shadowCastingMode = ShadowCastingMode.Off;
                smallOff++;
            }
        }
        done.Add("小さな物の影をオフ: " + smallOff + " 個");

        // ---- URP の設定 ----
        UniversalRenderPipelineAsset urp = CurrentUrp();
        if (urp != null)
        {
            Undo.RecordObject(urp, "ゲームを軽くする");
            SerializedObject so = new SerializedObject(urp);
            SetInt(so, "m_SoftShadowQuality", 1);                 // 低
            SetMaxInt(so, "m_MainLightShadowmapResolution", 1024);
            SetMaxInt(so, "m_AdditionalLightsShadowmapResolution", 1024);
            SetMaxInt(so, "m_ShadowCascadeCount", 2);
            SetMaxFloat(so, "m_ShadowDistance", 30f);
            SetInt(so, "m_RequireOpaqueTexture", 0);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urp);
            done.Add("URP(" + urp.name + "): 影の品質を下げた、Opaque Texture をオフ");

            if (TurnOffSsao(urp)) done.Add("SSAO をオフ");
            AssetDatabase.SaveAssets();
        }
        else
        {
            done.Add("URP の設定が見つからないので、飛ばした");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("ゲームを軽くする",
            "軽くしました。\n\n - " + string.Join("\n - ", done)
            + "\n\nCtrl+S でシーンを保存してください。\n"
            + "ほかのシーン(タイトルなど)も、開いてから、もう一度実行してください。", "OK");
    }

    // ---------------- 道具 ----------------

    private static void SetNoShadow(Light l)
    {
        Undo.RecordObject(l, "ゲームを軽くする");
        l.shadows = LightShadows.None;
    }

    private static UniversalRenderPipelineAsset CurrentUrp()
    {
        RenderPipelineAsset asset = QualitySettings.renderPipeline;
        if (asset == null) asset = GraphicsSettings.defaultRenderPipeline;
        return asset as UniversalRenderPipelineAsset;
    }

    private static bool IsSsaoOn(UniversalRenderPipelineAsset urp)
    {
        foreach (ScriptableRendererFeature f in Features(urp))
        {
            if (f != null && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion") && f.isActive) return true;
        }
        return false;
    }

    private static bool TurnOffSsao(UniversalRenderPipelineAsset urp)
    {
        bool changed = false;
        foreach (ScriptableRendererFeature f in Features(urp))
        {
            if (f != null && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion") && f.isActive)
            {
                Undo.RecordObject(f, "ゲームを軽くする");
                f.SetActive(false);
                EditorUtility.SetDirty(f);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// URP の設定につながっている Renderer(PC_Renderer など)の、機能の一覧。
    /// </summary>
    private static List<ScriptableRendererFeature> Features(UniversalRenderPipelineAsset urp)
    {
        List<ScriptableRendererFeature> list = new List<ScriptableRendererFeature>();
        SerializedObject so = new SerializedObject(urp);
        SerializedProperty renderers = so.FindProperty("m_RendererDataList");
        if (renderers == null) return list;

        for (int i = 0; i < renderers.arraySize; i++)
        {
            ScriptableRendererData data = renderers.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (data == null) continue;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
            {
                if (f != null) list.Add(f);
            }
        }
        return list;
    }

    private static int Int(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return -1;
        return p.propertyType == SerializedPropertyType.Boolean ? (p.boolValue ? 1 : 0) : p.intValue;
    }

    private static float Float(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        return p != null ? p.floatValue : -1f;
    }

    private static void SetInt(SerializedObject so, string field, int value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
        else p.intValue = value;
    }

    private static void SetMaxInt(SerializedObject so, string field, int max)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && p.intValue > max) p.intValue = max;
    }

    private static void SetMaxFloat(SerializedObject so, string field, float max)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && p.floatValue > max) p.floatValue = max;
    }

    private static string QualityName(int q)
    {
        switch (q)
        {
            case 1: return "低";
            case 2: return "中";
            case 3: return "高";
            default: return q.ToString();
        }
    }

    private static System.Type FindType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type found = assembly.GetType(typeName);
            if (found != null) return found;
        }
        return null;
    }
}
