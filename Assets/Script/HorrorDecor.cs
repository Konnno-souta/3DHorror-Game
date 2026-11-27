#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ホラー用の飾り(血しぶき、霧)の、テクスチャとマテリアルを用意する道具。
/// Room5x5Builder と HorrorFogSetup から使う。
///
/// テクスチャの置き場所 : Assets/Texture/Horror/  (Blood_Splat_01.png など。zip に入っている)
/// マテリアルの置き場所 : Assets/Material/Horror/ (なければ、自動で作る)
///
/// 血しぶきは、立体のメッシュ(BloodMeshFactory が作る)。画像はいらない。
/// 当たり判定はない。URP の Decal の設定をしなくても、そのまま見える。
/// </summary>
public static class HorrorDecor
{
    public const string TextureFolder = "Assets/Texture/Horror";
    public const string MaterialFolder = "Assets/Material/Horror";
    public const string FogTextureName = "Fog_Puff";

    // ---------------- テクスチャ ----------------

    /// <summary>
    /// テクスチャを読み込む。透明の扱いと、端をくり返さない設定に、そろえる。
    /// 見つからなければ null。
    /// </summary>
    public static Texture2D LoadTexture(string textureName)
    {
        string path = TextureFolder + "/" + textureName + ".png";

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        bool changed = false;
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
        if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------- 血のマテリアル ----------------

    /// <summary>
    /// 立体の血のマテリアル。画像は使わない。URP の Lit で、暗い赤。
    /// wet が true なら、ぬれて光る(床の血だまり)。false なら、少し乾いた感じ(壁)。
    /// </summary>
    public static Material GetBloodMaterial(bool wet)
    {
        string path = MaterialFolder + (wet ? "/Blood_3D_Wet.mat" : "/Blood_3D_Dry.mat");

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = wet ? "Blood_3D_Wet" : "Blood_3D_Dry";

        Color color = wet ? new Color(0.24f, 0.004f, 0.006f) : new Color(0.16f, 0.01f, 0.008f);
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", wet ? 0.9f : 0.55f);
        mat.SetFloat("_Glossiness", wet ? 0.9f : 0.55f);

        EnsureFolder(MaterialFolder);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------------- 霧のマテリアル ----------------

    /// <summary>
    /// 霧の、ふちがぼやけた丸い画像。なければ、ここで作って保存する(zip の画像を入れなくてもよい)。
    /// </summary>
    public static Texture2D GetFogTexture()
    {
        Texture2D texture = LoadTexture(FogTextureName);
        if (texture != null) return texture;

        const int size = 128;
        Texture2D made = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float seedX = Random.Range(0f, 50f);
        float seedY = Random.Range(0f, 50f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 真ん中が濃く、ふちで0になる。もやもやを少し混ぜる
                float falloff = Mathf.Clamp01(1f - d);
                falloff = falloff * falloff * (3f - 2f * falloff);
                float n = Mathf.PerlinNoise(seedX + x * 0.045f, seedY + y * 0.045f);
                float alpha = Mathf.Clamp01(falloff * (0.45f + n * 0.9f));

                made.SetPixel(x, y, new Color(0.93f, 0.94f, 0.95f, alpha));
            }
        }
        made.Apply();

        EnsureFolder(TextureFolder);
        string path = TextureFolder + "/" + FogTextureName + ".png";
        File.WriteAllBytes(path, made.EncodeToPNG());
        Object.DestroyImmediate(made);
        AssetDatabase.ImportAsset(path);

        return LoadTexture(FogTextureName);
    }

    /// <summary>
    /// 地面の霧のマテリアル。URP の Particles/Lit を、半透明で使う。
    /// 電灯の近くだけ明るく見え、暗い所では見えにくい。
    /// </summary>
    public static Material GetFogMaterial()
    {
        string path = MaterialFolder + "/Fog_Mist.mat";

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Texture2D texture = GetFogTexture();

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

        Material mat = new Material(shader);
        mat.name = "Fog_Mist";

        if (texture != null)
        {
            mat.SetTexture("_BaseMap", texture);
            mat.mainTexture = texture;
        }
        mat.SetColor("_BaseColor", Color.white);

        // 半透明(アルファブレンド)
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;

        EnsureFolder(MaterialFolder);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------------- 血しぶきを置く ----------------

    /// <summary>
    /// 床に、立体の血しぶきを置く。x, z は親から見た位置。floorY は床の高さ。
    /// width は横(x方向)、length は奥(z方向)の大きさ。yaw で回す。
    /// kind は BloodMeshFactory の名前(Blood_Splat_01 など)。
    /// </summary>
    public static GameObject SpawnBloodFloor(Transform parent, string kind, float x, float z, float floorY,
        float yaw, float width, float length)
    {
        Mesh mesh = BloodMeshFactory.Get(kind, 0.012f);
        Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up);
        Vector3 position = new Vector3(x, floorY, z);
        return SpawnBloodMesh(parent, kind, mesh, GetBloodMaterial(true), position, rotation, width, length);
    }

    /// <summary>
    /// 壁に、立体の血しぶきを置く。position は壁の表面の位置(親から見た位置)。
    /// normal は、壁から部屋の内側へ向く方向。roll で、壁の面の中で回す。
    /// </summary>
    public static GameObject SpawnBloodWall(Transform parent, string kind, Vector3 position, Vector3 normal,
        float width, float height, float roll)
    {
        Vector3 n = normal.normalized;
        Mesh mesh = BloodMeshFactory.Get(kind, 0.005f);

        // メッシュの +Y を壁の外向き、+Z を上に向ける(したたりが下へ垂れる)
        Quaternion rotation = Quaternion.AngleAxis(roll, n) * Quaternion.LookRotation(Vector3.up, n);
        return SpawnBloodMesh(parent, kind, mesh, GetBloodMaterial(false), position, rotation, width, height);
    }

    private static GameObject SpawnBloodMesh(Transform parent, string kind, Mesh mesh, Material mat,
        Vector3 localPosition, Quaternion localRotation, float width, float length)
    {
        if (mesh == null) return null;

        // 当たり判定はつけない(踏んでも、つまずかない)
        GameObject blood = new GameObject(kind);
        blood.transform.SetParent(parent, false);
        blood.transform.localPosition = localPosition;
        blood.transform.localRotation = localRotation;
        blood.transform.localScale = new Vector3(width, 1f, length);

        MeshFilter mf = blood.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = blood.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        return blood;
    }


    // ---------------- その他 ----------------

    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>
    /// 型を、名前でさがす(どのアセンブリに入っていても見つける)。
    /// HorrorFog.cs が別のフォルダや asmdef に入っていても、エラーにならないようにするため。
    /// </summary>
    public static System.Type FindType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type found = assembly.GetType(typeName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// シーンに、霧(HorrorFog)があるか。
    /// </summary>
    public static Component FindFogInScene()
    {
        System.Type fogType = FindType("HorrorFog");
        if (fogType == null) return null;
        return Object.FindFirstObjectByType(fogType, FindObjectsInactive.Include) as Component;
    }
}
#endif