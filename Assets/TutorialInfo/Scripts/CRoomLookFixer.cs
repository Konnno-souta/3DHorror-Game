using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cの部屋の見た目を直すツール。メニュー: Tools/Scene/Cの部屋の見た目を直す
///
/// 直すこと:
///  1. 床の白い四角(三角のもよう) … 地面の霧(Fog_Mist)の混ぜ方がまちがっていて、
///     透明な所まで白く足されていた。ふつうの半透明にする。
///  2. 宙に浮いた白いかけら … 血がツヤツヤすぎて、ライトで白く光っていた。ツヤを落とす。
///     壁から離れて浮いている血は、壁にくっつける。くっつく壁が無い物は隠す。
///  3. 白く飛んだ壁 … ライトが明るすぎ・壁に近すぎ。暗くして、届く距離も短くする。
/// 何回押しても大丈夫(ライトは毎回暗くならないよう、最初の明るさを覚えておく)。
/// </summary>
public static class CRoomLookFixer
{
    private const string InteriorName = "C_Interior_New";
    private const string LightMarkKey = "CRoomFix_OriginalIntensity";

    // ライトの明るさの倍率
    private const float LightScale = 0.55f;
    // 壁にこれより近いライトは、さらに暗くする(m)
    private const float NearWallDistance = 1.8f;
    private const float NearWallScale = 0.6f;
    private const float MaxRange = 12f;

    [MenuItem("Tools/Scene/Cの部屋の見た目を直す")]
    public static void Fix()
    {
        List<string> log = new List<string>();

        log.Add("霧のマテリアル: " + FixMistMaterials() + "個 直した");
        log.Add("血のツヤ: " + FixBloodMaterials() + "個 直した");

        GameObject interior = FindInScene(InteriorName);
        if (interior == null)
        {
            log.Add(InteriorName + " が見つからないので、血とライトはそのまま");
        }
        else
        {
            int snapped, hidden;
            FixFloatingBlood(interior.transform, out snapped, out hidden);
            log.Add("血: 壁にくっつけた " + snapped + "個 / 浮いていたので隠した " + hidden + "個");
            log.Add("ライト: " + FixLights(interior.transform) + "個 暗くした");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        string msg = string.Join("\n", log);
        Debug.Log("[CRoom] Cの部屋の見た目を直しました\n" + msg);
        EditorUtility.DisplayDialog("Cの部屋", "見た目を直しました。\n\n" + msg + "\n\nシーンを保存して、プレイで確認してください。\n元に戻すときは Ctrl+Z。", "OK");
    }

    // ------------------------------------------------------------
    // 1. 霧
    private static int FixMistMaterials()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("Fog_Mist t:Material"))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;
            Undo.RecordObject(m, "Fix mist");

            // ふつうの半透明(Alpha)。Premultiply だと、透明な所の白が足されて四角く見える
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;

            // 白すぎないよう、少し暗い灰色に
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.5f, 0.52f, 0.56f, 0.6f));
            EditorUtility.SetDirty(m);
            n++;
        }

        // テクスチャの透明な所が、ちゃんと透明になるように
        foreach (string guid in AssetDatabase.FindAssets("Fog_Puff t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            if (!imp.alphaIsTransparency || imp.mipmapEnabled == false)
            {
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
        }
        return n;
    }

    // ------------------------------------------------------------
    // 2. 血のツヤ
    private static int FixBloodMaterials()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("Blood_3D t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) continue;
            Undo.RecordObject(m, "Fix blood");
            bool wet = path.Contains("Wet");
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", wet ? 0.35f : 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", wet ? 1f : 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            if (!wet) m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", wet ? new Color(0.22f, 0.012f, 0.01f, 1f) : new Color(0.14f, 0.012f, 0.008f, 1f));
            }
            EditorUtility.SetDirty(m);
            n++;
        }
        return n;
    }

    // 壁の血が、壁から離れて浮いていないか調べる
    private static void FixFloatingBlood(Transform interior, out int snapped, out int hidden)
    {
        snapped = 0; hidden = 0;
        Transform bloodRoot = interior.Find("Blood");
        if (bloodRoot == null) return;

        foreach (Transform b in bloodRoot)
        {
            if (!b.gameObject.activeSelf) continue;
            if (b.position.y < 0.3f) continue;   // 床の血はそのまま

            Vector3 n = b.up;                     // 血の表(壁から外向き)
            RaycastHit hit;
            bool found = Cast(b, b.position + n * 0.6f, -n, 2.0f, out hit);
            if (!found && Cast(b, b.position - n * 0.6f, n, 2.0f, out hit)) found = true;
            if (!found)
            {
                // 横のどこかに壁があるか
                foreach (Vector3 d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    if (Cast(b, b.position - d * 0.2f, d, 2.5f, out hit)) { found = true; break; }
                }
            }

            if (!found)
            {
                Undo.RecordObject(b.gameObject, "Hide blood");
                b.gameObject.SetActive(false);
                hidden++;
                continue;
            }

            Undo.RecordObject(b, "Snap blood");
            Quaternion turn = Quaternion.FromToRotation(b.up, hit.normal);
            b.rotation = turn * b.rotation;
            b.position = hit.point + hit.normal * 0.01f;
            snapped++;
        }
    }

    private static bool Cast(Transform self, Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
    {
        best = new RaycastHit();
        float nearest = float.MaxValue;
        bool any = false;
        foreach (RaycastHit h in Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(self)) continue;
            if (h.collider.name.StartsWith("Blood")) continue;
            if (h.distance < nearest) { nearest = h.distance; best = h; any = true; }
        }
        return any;
    }

    // ------------------------------------------------------------
    // 3. ライト
    private static int FixLights(Transform interior)
    {
        int n = 0;
        Vector3[] dirs =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            (Vector3.right + Vector3.forward).normalized, (Vector3.right + Vector3.back).normalized,
            (Vector3.left + Vector3.forward).normalized, (Vector3.left + Vector3.back).normalized
        };

        foreach (Light l in interior.GetComponentsInChildren<Light>(true))
        {
            if (l.type == LightType.Directional) continue;

            // 最初の明るさを覚えておく(何回押しても、暗くなりすぎないように)
            float original = GetOriginal(l);

            float nearest = float.MaxValue;
            foreach (Vector3 d in dirs)
            {
                RaycastHit h;
                if (Physics.Raycast(l.transform.position, d, out h, 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (l.transform.parent != null && h.collider.transform.IsChildOf(l.transform.parent)) continue;
                    nearest = Mathf.Min(nearest, h.distance);
                }
            }

            bool isRed = l.color.r > 0.6f && l.color.g < 0.4f;
            float scale = LightScale;
            if (nearest < NearWallDistance) scale *= NearWallScale;
            if (isRed) scale = Mathf.Max(scale, 0.7f);    // 赤い非常灯は、雰囲気のため少しだけ

            Undo.RecordObject(l, "Fix light");
            l.intensity = original * scale;
            if (l.range > MaxRange) l.range = MaxRange;
            l.shadows = LightShadows.None;
            n++;
        }
        return n;
    }

    private static float GetOriginal(Light l)
    {
        // 最初の明るさは、このパソコンの EditorPrefs に覚えておく(シーンには何も足さない)
        string key = LightMarkKey + "_" + GlobalObjectId.GetGlobalObjectIdSlow(l).ToString();
        if (EditorPrefs.HasKey(key)) return EditorPrefs.GetFloat(key);
        EditorPrefs.SetFloat(key, l.intensity);
        return l.intensity;
    }

    private static GameObject FindInScene(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name) return t.gameObject;
        }
        return null;
    }
}
