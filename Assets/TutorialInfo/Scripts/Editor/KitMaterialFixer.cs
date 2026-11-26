using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SciFi Warehouse Kit のマテリアルは、色の画像(Base Map)が空で、金属度だけが高い状態になっている。
/// そのため、床などが、模様のない茶色い金属のように見える。
/// このツールは、法線マップ(xxx_n.png)の名前から、同じ名前の色の画像(xxx_a.png)を探して、Base Map に入れる。
/// 金属度が高すぎるときは、下げる。
///
/// メニュー:
///   Tools / Room / 床のマテリアルを直す         (Floor Tile Mat だけ)
///   Tools / Room / キットのマテリアルを全部直す  (廊下の見た目も変わる)
/// </summary>
public static class KitMaterialFixer
{
    private const string MaterialFolderKeyword = "SciFi Warehouse Kit/Art/Materials";
    private const float MaxMetallic = 0.3f;
    private const float FixedSmoothness = 0.35f;

    [MenuItem("Tools/Room/床のマテリアルを直す")]
    public static void FixFloorOnly()
    {
        // 床のPrefab(Floor Tile 01 から 04)が、実際に使っているマテリアルを調べて、直す。
        // 名前や場所が違っていても、床が使っているものを直せる。
        string report = "";
        int fixedCount = 0;

        Texture2D albedo = FindTexture("floor_tile_a");
        if (albedo == null)
        {
            EditorUtility.DisplayDialog("Kit Material Fixer",
                "floor_tile_a.png が見つかりません。SciFi Warehouse Kit の Art/Textures を確認してください。", "OK");
            return;
        }

        List<Material> done = new List<Material>();
        for (int i = 1; i <= 4; i++)
        {
            GameObject prefab = FindKitPrefab("Floor Tile 0" + i);
            if (prefab == null) continue;

            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null || done.Contains(mat)) continue;
                    done.Add(mat);

                    string before = DescribeBaseMap(mat);
                    ApplyAlbedo(mat, albedo);
                    fixedCount++;

                    report += "\n - " + AssetDatabase.GetAssetPath(mat) + "\n     Base Map: " + before + " -> floor_tile_a";
                    Debug.Log("[KitMaterialFixer] " + AssetDatabase.GetAssetPath(mat) + " の Base Map: " + before + " -> floor_tile_a");
                }
            }
        }

        AssetDatabase.SaveAssets();

        if (fixedCount == 0)
        {
            EditorUtility.DisplayDialog("Kit Material Fixer", "床のPrefab(Floor Tile 01 から 04)が見つかりませんでした。", "OK");
            return;
        }

        EditorUtility.DisplayDialog("Kit Material Fixer",
            "床が使っているマテリアルを、" + fixedCount + "個直しました。" + report
            + "\n\nGameビューで、床の模様が出たか確認してください。", "OK");
    }

    private static string DescribeBaseMap(Material mat)
    {
        if (!mat.HasProperty("_BaseMap")) return "(_BaseMap なし。シェーダー: " + mat.shader.name + ")";
        Texture t = mat.GetTexture("_BaseMap");
        return t == null ? "空" : t.name;
    }

    private static void ApplyAlbedo(Material mat, Texture2D albedo)
    {
        Undo.RecordObject(mat, "キットのマテリアルを直す");

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > MaxMetallic)
        {
            mat.SetFloat("_Metallic", MaxMetallic);
        }
        if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", FixedSmoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", FixedSmoothness);

        EditorUtility.SetDirty(mat);
    }

    private static Texture2D FindTexture(string textureName)
    {
        string[] guids = AssetDatabase.FindAssets(textureName + " t:Texture2D");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("SciFi Warehouse Kit")) continue;
            if (Path.GetFileNameWithoutExtension(path) != textureName) continue;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }

    private static GameObject FindKitPrefab(string prefabName)
    {
        string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("SciFi Warehouse Kit")) continue;
            if (Path.GetFileNameWithoutExtension(path) != prefabName) continue;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    [MenuItem("Tools/Room/キットのマテリアルを全部直す")]
    public static void FixAll()
    {
        bool ok = EditorUtility.DisplayDialog(
            "キットのマテリアルを全部直す",
            "SciFi Warehouse Kit のマテリアルに、色の画像を入れます。\n"
            + "今ある廊下や部屋の見た目も、変わります。\n"
            + "(Gitで元に戻せます。コミットしてから実行するのがおすすめです)\n\n続けますか?",
            "続ける", "やめる");
        if (!ok) return;

        int fixedCount = FixMaterials(false);
        Report(fixedCount, "キットのマテリアル");
    }

    private static int FixMaterials(bool floorOnly)
    {
        int fixedCount = 0;
        string[] guids = AssetDatabase.FindAssets("t:Material");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains(MaterialFolderKeyword)) continue;

            string materialName = Path.GetFileNameWithoutExtension(path);
            if (floorOnly && materialName != "Floor Tile Mat") continue;

            // 透明、半透明、窓は、飛ばす
            if (materialName.Contains("Alpha") || materialName.Contains("Trans") || materialName.Contains("Windows")) continue;

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            if (!mat.HasProperty("_BaseMap") || !mat.HasProperty("_BumpMap")) continue;
            if (mat.GetTexture("_BaseMap") != null) continue;

            Texture bump = mat.GetTexture("_BumpMap");
            if (bump == null) continue;

            string bumpPath = AssetDatabase.GetAssetPath(bump);
            if (!bumpPath.EndsWith("_n.png")) continue;

            string albedoPath = bumpPath.Substring(0, bumpPath.Length - "_n.png".Length) + "_a.png";
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedo == null) continue;

            Undo.RecordObject(mat, "キットのマテリアルを直す");

            mat.SetTexture("_BaseMap", albedo);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            if (mat.HasProperty("_Metallic") && mat.GetFloat("_Metallic") > MaxMetallic)
            {
                mat.SetFloat("_Metallic", MaxMetallic);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", FixedSmoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", FixedSmoothness);

            EditorUtility.SetDirty(mat);
            fixedCount++;
            Debug.Log("[KitMaterialFixer] " + materialName + " に " + Path.GetFileName(albedoPath) + " を設定しました。");
        }

        AssetDatabase.SaveAssets();
        return fixedCount;
    }

    private static void Report(int fixedCount, string label)
    {
        string message = fixedCount > 0
            ? label + "を、" + fixedCount + "個直しました。"
            : "直すものは見つかりませんでした。(すでに直してあるか、キットのマテリアルが見つかりません)";

        EditorUtility.DisplayDialog("Kit Material Fixer", message, "OK");
    }
}