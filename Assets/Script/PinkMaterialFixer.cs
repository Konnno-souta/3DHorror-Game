using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ピンク色になったマテリアルを直す(今開いているシーンだけ)。
/// メニュー: Tools / Title / ピンクのマテリアルを直す(このシーン)
///
/// ピンクになる理由:
///   ・シェーダーが壊れている(エラーで読みこめない。Shader Graph など)
///   ・URP では使えない、古いシェーダー(Standard など)を使っている
///
/// 直し方:
///   元のマテリアルは変えずに、URP の Lit で作った「直したマテリアル」を Assets/Material/Fixed に作り、
///   シーンの中の物にだけ付けかえる。色の画像(テクスチャ)と、でこぼこの画像(ノーマル)は、元から写す。
///   Shadowlop(化け物)は、画像が見つからないときも、アセットのフォルダの画像を使う。
///
/// Ctrl+Z で、もとにもどせる。
/// </summary>
public static class PinkMaterialFixer
{
    private const string FixedFolder = "Assets/Material/Fixed";
    private const string ShadowlopTextureFolder = "Assets/Aseets_iroiro/Shadowlop/_Textures";

    private static readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();

    [MenuItem("Tools/Title/ピンクのマテリアルを直す(このシーン)")]
    public static void FixScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("ピンクのマテリアル", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        cache.Clear();
        Undo.SetCurrentGroupName("ピンクのマテリアルを直す");
        int group = Undo.GetCurrentGroup();

        int fixedRenderers = 0;
        List<string> names = new List<string>();

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (FixRenderer(r, names)) fixedRenderers++;
        }

        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();

        if (fixedRenderers > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        string message = fixedRenderers == 0
            ? "このシーンには、ピンクになるマテリアルは見つかりませんでした。"
            : fixedRenderers + " 個の物の、マテリアルを直しました。\n\n直したマテリアル:\n - " + string.Join("\n - ", names)
              + "\n\n直したマテリアルは " + FixedFolder + " にあります。Ctrl+S で保存してください。";

        EditorUtility.DisplayDialog("ピンクのマテリアル", message, "OK");
    }

    /// <summary>
    /// 1つの物の、ピンクになるマテリアルを、直したマテリアルに付けかえる。直したら true。
    /// </summary>
    public static bool FixRenderer(Renderer r, List<string> report)
    {
        if (r == null) return false;

        Material[] mats = r.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (!IsBroken(m)) continue;

            Material fixedMat = GetFixed(m, r);
            if (fixedMat == null) continue;

            mats[i] = fixedMat;
            changed = true;

            if (report != null && !report.Contains(fixedMat.name)) report.Add(fixedMat.name);
        }

        if (changed)
        {
            Undo.RecordObject(r, "ピンクのマテリアルを直す");
            r.sharedMaterials = mats;
        }
        return changed;
    }

    /// <summary>
    /// URP でピンクになるマテリアルか。マテリアルが空のときも、ピンクになるので true。
    /// </summary>
    public static bool IsBroken(Material m)
    {
        if (m == null) return true;

        Shader s = m.shader;
        if (s == null) return true;
        if (!s.isSupported) return true;

        string n = s.name;
        if (n == "Hidden/InternalErrorShader") return true;
        if (n == "Standard" || n == "Standard (Specular setup)") return true;
        if (n.StartsWith("Legacy Shaders/")) return true;
        if (n.StartsWith("Mobile/") || n.StartsWith("Nature/")) return true;
        if (n.StartsWith("Autodesk Interactive")) return true;

        return false;
    }

    private static Material GetFixed(Material source, Renderer r)
    {
        if (source != null && cache.TryGetValue(source, out Material done)) return done;

        string baseName = source != null ? source.name : r.gameObject.name;
        string path = FixedFolder + "/" + Sanitize(baseName) + "_URP.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;

            mat = new Material(lit);
            mat.name = Sanitize(baseName) + "_URP";

            Texture baseMap = FindTexture(source, "basemap", "maintex", "albedo", "basecolor", "diffuse", "color");
            Texture normal = FindTexture(source, "normal", "bump");

            // 化け物(Shadowlop)は、アセットのフォルダの画像を使う
            bool isShadowlop = baseName.ToLower().Contains("shadowlop") || HasParentNamed(r.transform, "shadowlop")
                || HasParentNamed(r.transform, "monster");
            if (isShadowlop)
            {
                if (baseMap == null) baseMap = AssetDatabase.LoadAssetAtPath<Texture>(ShadowlopTextureFolder + "/Shadowlop_BaseMap.png");
                if (normal == null) normal = AssetDatabase.LoadAssetAtPath<Texture>(ShadowlopTextureFolder + "/Shadowlop_Normal.png");
            }

            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                mat.mainTexture = baseMap;
            }

            Color color = Color.white;
            if (source != null)
            {
                if (source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
                else if (source.HasProperty("_Color")) color = source.GetColor("_Color");
            }
            if (baseMap == null && color == Color.white) color = new Color(0.35f, 0.35f, 0.37f);
            mat.SetColor("_BaseColor", color);

            if (normal != null)
            {
                MakeNormalMap(normal);
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", isShadowlop ? 0.35f : 0.3f);

            EnsureFolder(FixedFolder);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (source != null) cache[source] = mat;
        return mat;
    }

    /// <summary>
    /// 元のマテリアルの画像の中から、名前に keywords のどれかを含む物をさがす。
    /// </summary>
    private static Texture FindTexture(Material m, params string[] keywords)
    {
        if (m == null) return null;

        foreach (string prop in m.GetTexturePropertyNames())
        {
            Texture t = m.GetTexture(prop);
            if (t == null) continue;

            string lower = prop.ToLower() + " " + t.name.ToLower();
            foreach (string k in keywords)
            {
                if (lower.Contains(k)) return t;
            }
        }
        return null;
    }

    private static void MakeNormalMap(Texture t)
    {
        string path = AssetDatabase.GetAssetPath(t);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    private static bool HasParentNamed(Transform t, string part)
    {
        while (t != null)
        {
            if (t.name.ToLower().Contains(part)) return true;
            t = t.parent;
        }
        return false;
    }

    private static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
