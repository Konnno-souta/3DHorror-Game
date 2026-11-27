#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 血しぶきを、画像ではなく、立体のメッシュで作る。
/// ふくらんだ液体のような血だまりと、まわりに飛んだしずくを、1つのメッシュにまとめる。
///
/// メッシュは、Assets/Model/Horror/Blood に .asset で保存する(プレハブに入れても、消えないように)。
/// 形を作り直したいときは、そのフォルダを消してから、もう一度実行する。
///
/// メッシュの向き:
///   1辺1mの四角(-0.5 から 0.5)の中に、XZ平面で作る。厚みは +Y 方向。
///   壁に貼るときは、+Y を壁の外向き、+Z を上にする(したたりは -Z 方向へ垂れる)。
///   使うときは、Transform の Scale の X と Z で、大きさを決める(Y は 1 のまま)。
/// </summary>
public static class BloodMeshFactory
{
    public const string MeshFolder = "Assets/Model/Horror/Blood";

    // 種類の名前(Room5x5Builder から使う)
    public const string Splat01 = "Blood_Splat_01";
    public const string Splat02 = "Blood_Splat_02";
    public const string Splat03 = "Blood_Splat_03";
    public const string Drip01 = "Blood_Drip_01";
    public const string Drip02 = "Blood_Drip_02";
    public const string Smear01 = "Blood_Smear_01";
    public const string Hand01 = "Blood_Hand_01";

    /// <summary>
    /// 血のメッシュを返す。保存したものがあれば、それを使う。
    /// thickness は、いちばん厚いところの厚み(m)。床は 0.012、壁は 0.005 くらい。
    /// </summary>
    public static Mesh Get(string kind, float thickness)
    {
        string meshName = kind + (thickness < 0.008f ? "_Wall" : "_Floor");
        string path = MeshFolder + "/" + meshName + ".asset";

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Builder b = new Builder(thickness);

        switch (kind)
        {
            case Splat01: BuildSplat(b, 11); break;
            case Splat02: BuildSplat(b, 23); break;
            case Splat03: BuildPool(b, 37); break;
            case Drip01: BuildDrip(b, 51); break;
            case Drip02: BuildDrip(b, 77); break;
            case Smear01: BuildSmear(b, 91); break;
            case Hand01: BuildHand(b, 5); break;
            default: BuildSplat(b, 3); break;
        }

        Mesh mesh = b.ToMesh(meshName);

        HorrorDecor.EnsureFolder(MeshFolder);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------------- 形 ----------------

    /// <summary>飛び散った血。真ん中のかたまりと、放射状のすじと、しずく。</summary>
    private static void BuildSplat(Builder b, int seed)
    {
        System.Random r = new System.Random(seed);

        b.AddBlob(new Vector2(R(r, -0.02f, 0.02f), R(r, -0.02f, 0.02f)), 0.19f, 0.19f, 0f, 0.28f, r, 1f, 40);

        int lumps = r.Next(2, 5);
        for (int i = 0; i < lumps; i++)
        {
            float a = R(r, 0f, Mathf.PI * 2f);
            float d = R(r, 0.1f, 0.18f);
            float size = R(r, 0.06f, 0.11f);
            b.AddBlob(Polar(a, d), size, size, 0f, 0.3f, r, 0.85f, 28);
        }

        int streaks = r.Next(6, 10);
        for (int i = 0; i < streaks; i++)
        {
            float a = R(r, 0f, Mathf.PI * 2f);
            float d = R(r, 0.2f, 0.3f);
            b.AddBlob(Polar(a, d), R(r, 0.07f, 0.13f), R(r, 0.018f, 0.032f), a, 0.12f, r, 0.6f, 20);
            // すじの先の、しずく
            float tip = d + R(r, 0.11f, 0.17f);
            float size = R(r, 0.012f, 0.025f);
            b.AddBlob(Polar(a, tip), size, size, 0f, 0.2f, r, 0.55f, 12);
        }

        AddDroplets(b, r, 30, 0.24f, 0.48f);
    }

    /// <summary>大きな血だまり。ふちがでこぼこで、まわりにしずく。</summary>
    private static void BuildPool(Builder b, int seed)
    {
        System.Random r = new System.Random(seed);

        b.AddBlob(Vector2.zero, 0.3f, 0.26f, R(r, 0f, 3f), 0.22f, r, 1.2f, 48);

        int lumps = r.Next(4, 7);
        for (int i = 0; i < lumps; i++)
        {
            float a = R(r, 0f, Mathf.PI * 2f);
            float size = R(r, 0.07f, 0.13f);
            b.AddBlob(Polar(a, R(r, 0.22f, 0.3f)), size, size * R(r, 0.7f, 1f), a, 0.3f, r, 0.9f, 24);
        }

        AddDroplets(b, r, 22, 0.36f, 0.48f);
    }

    /// <summary>壁の、上からしたたる血。</summary>
    private static void BuildDrip(Builder b, int seed)
    {
        System.Random r = new System.Random(seed);

        b.AddBlob(new Vector2(0f, 0.24f), 0.17f, 0.14f, 0f, 0.3f, r, 1f, 36);
        for (int i = 0; i < 3; i++)
        {
            float size = R(r, 0.05f, 0.09f);
            b.AddBlob(new Vector2(R(r, -0.18f, 0.18f), R(r, 0.16f, 0.34f)), size, size, 0f, 0.3f, r, 0.85f, 20);
        }

        int drips = r.Next(6, 10);
        for (int i = 0; i < drips; i++)
        {
            float x = R(r, -0.16f, 0.16f);
            float top = R(r, 0.2f, 0.26f);
            float length = R(r, 0.22f, 0.6f);
            float width = R(r, 0.01f, 0.022f);

            // 下へ垂れたすじと、先にたまった玉
            b.AddBlob(new Vector2(x, top - length * 0.5f), width, length * 0.5f, 0f, 0.05f, r, 0.7f, 20);
            float bulb = width * R(r, 1.4f, 1.9f);
            b.AddBlob(new Vector2(x, top - length), bulb, bulb * 1.2f, 0f, 0.1f, r, 0.9f, 14);
        }

        // 上のほうに飛んだしずく
        for (int i = 0; i < 24; i++)
        {
            float a = R(r, 0f, Mathf.PI);
            float d = R(r, 0.18f, 0.3f);
            Vector2 p = new Vector2(Mathf.Cos(a) * d, 0.24f + Mathf.Sin(a) * d * 0.8f);
            float size = R(r, 0.006f, 0.018f);
            b.AddBlob(p, size, size, 0f, 0.15f, r, 0.5f, 10);
        }
    }

    /// <summary>引きずった跡。-X 側が血だまりで、+X へ細くなる。</summary>
    private static void BuildSmear(Builder b, int seed)
    {
        System.Random r = new System.Random(seed);

        // 血だまり(横長のメッシュを、あとで横にのばすので、X は小さめ)
        b.AddBlob(new Vector2(-0.4f, 0f), 0.08f, 0.36f, 0f, 0.25f, r, 1f, 36);

        // 引きずったすじ
        int streaks = 9;
        for (int i = 0; i < streaks; i++)
        {
            float z = R(r, -0.28f, 0.28f);
            float start = R(r, -0.42f, -0.3f);
            float end = R(r, 0.05f, 0.48f) * (1f - Mathf.Abs(z) * 1.2f);
            float half = (end - start) * 0.5f;
            float width = R(r, 0.03f, 0.09f) * (1f - Mathf.Abs(z));
            b.AddBlob(new Vector2(start + half, z), half, width, 0f, 0.08f, r, 0.6f, 32);
        }

        // ところどころの、かたまり
        for (int i = 0; i < 10; i++)
        {
            float size = R(r, 0.01f, 0.03f);
            b.AddBlob(new Vector2(R(r, -0.3f, 0.35f), R(r, -0.25f, 0.25f)), size, size * 4f, 0f, 0.3f, r, 0.7f, 14);
        }
    }

    /// <summary>血の手形と、下へのしたたり。</summary>
    private static void BuildHand(Builder b, int seed)
    {
        System.Random r = new System.Random(seed);

        // 手のひら
        b.AddBlob(new Vector2(0f, -0.1f), 0.17f, 0.19f, 0f, 0.12f, r, 1f, 36);

        // 指(小指から人差し指)
        float[] xs = { -0.15f, -0.055f, 0.04f, 0.13f };
        float[] lengths = { 0.12f, 0.16f, 0.17f, 0.15f };
        float[] angles = { 0.25f, 0.08f, -0.05f, -0.2f };
        for (int i = 0; i < xs.Length; i++)
        {
            float a = angles[i] + Mathf.PI * 0.5f;
            Vector2 baseP = new Vector2(xs[i], 0.0f);
            Vector2 center = baseP + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * lengths[i] * 0.95f;
            b.AddBlob(center, lengths[i], 0.038f, a, 0.06f, r, 0.85f, 24);
        }

        // 親指
        b.AddBlob(new Vector2(0.2f, -0.08f), 0.1f, 0.042f, 0.75f, 0.06f, r, 0.85f, 20);

        // 下へのしたたり
        float[] dripXs = { -0.07f, 0.03f, 0.11f };
        foreach (float x in dripXs)
        {
            float length = R(r, 0.08f, 0.17f);
            b.AddBlob(new Vector2(x, -0.28f - length * 0.5f), 0.012f, length * 0.5f, 0f, 0.05f, r, 0.6f, 16);
            b.AddBlob(new Vector2(x, -0.28f - length), 0.02f, 0.024f, 0f, 0.1f, r, 0.8f, 12);
        }
    }

    private static void AddDroplets(Builder b, System.Random r, int count, float minDist, float maxDist)
    {
        for (int i = 0; i < count; i++)
        {
            float a = R(r, 0f, Mathf.PI * 2f);
            float size = R(r, 0.005f, 0.02f);
            b.AddBlob(Polar(a, R(r, minDist, maxDist)), size, size, 0f, 0.15f, r, 0.5f, 10);
        }
    }

    private static float R(System.Random r, float min, float max)
    {
        return min + (float)r.NextDouble() * (max - min);
    }

    private static Vector2 Polar(float angle, float distance)
    {
        return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
    }

    // ---------------- メッシュを組み立てる ----------------

    private class Builder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly float thickness;

        private const int Rings = 5;
        private const float Lift = 0.003f;   // 床や壁から、少しだけ浮かせる(ちらつき防止)

        public Builder(float thickness)
        {
            this.thickness = thickness;
        }

        /// <summary>
        /// ふくらんだ、しずくの形を足す。
        /// center : 真ん中の位置(XZ)。rx, rz : 半径。angle : 回転(ラジアン)。
        /// wobble : ふちのでこぼこ。height : 厚みの割合。segments : 周りの分割数。
        /// </summary>
        public void AddBlob(Vector2 center, float rx, float rz, float angle, float wobble, System.Random r,
            float height, int segments)
        {
            // ふちのでこぼこ(なめらかな波を3つ重ねる)
            float p1 = R(r, 0f, 6.28f), p2 = R(r, 0f, 6.28f), p3 = R(r, 0f, 6.28f);
            float a1 = R(r, 0.5f, 1f), a2 = R(r, 0.3f, 0.7f), a3 = R(r, 0.1f, 0.4f);

            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            // 小さいしずくほど、厚みはうすいが、丸くもり上がる
            float size = Mathf.Max(rx, rz);
            float peak = thickness * height * Mathf.Clamp(size * 6f, 0.35f, 1f);

            int centerIndex = vertices.Count;
            vertices.Add(new Vector3(center.x, Lift + peak, center.y));

            for (int ring = 1; ring <= Rings; ring++)
            {
                float t = (float)ring / Rings;
                // ふちほど急に下がる(液体のふくらみ)
                float h = peak * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * t * t));

                for (int i = 0; i < segments; i++)
                {
                    float theta = (float)i / segments * Mathf.PI * 2f;
                    float wave = a1 * Mathf.Sin(theta * 3f + p1) + a2 * Mathf.Sin(theta * 5f + p2)
                        + a3 * Mathf.Sin(theta * 9f + p3);
                    float radius = 1f + wobble * wave * 0.5f;

                    float lx = Mathf.Cos(theta) * rx * radius * t;
                    float lz = Mathf.Sin(theta) * rz * radius * t;

                    float x = center.x + lx * cosA - lz * sinA;
                    float z = center.y + lx * sinA + lz * cosA;

                    vertices.Add(new Vector3(x, Lift + h, z));
                }
            }

            // 真ん中とまわり(上から見て時計回り = 上向きの面)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(centerIndex);
                triangles.Add(centerIndex + 1 + next);
                triangles.Add(centerIndex + 1 + i);
            }

            // 輪と輪のあいだ
            for (int ring = 1; ring < Rings; ring++)
            {
                int inner = centerIndex + 1 + (ring - 1) * segments;
                int outer = centerIndex + 1 + ring * segments;

                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    triangles.Add(inner + i);
                    triangles.Add(inner + next);
                    triangles.Add(outer + i);

                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                    triangles.Add(outer + i);
                }
            }
        }

        public Mesh ToMesh(string meshName)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
#endif
