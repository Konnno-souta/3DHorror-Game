using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SciFi Warehouse Kit で、5x5マス(30m x 30m、高さ9m)の部屋を組み立てる。
/// メニュー: Tools / Room / 5x5の部屋を作る
///
/// できるもの:
///   Assets/Prefab/Room_5x5.prefab を保存し、シーンにも1つ置く(Sceneビューの中心あたり)。
///
/// 部屋の向き(部屋の左下が原点。X が右、Z が奥):
///   南(Z=0)の中央  : 入口(Wall Door)
///   西(X=0)の中央  : 鍵付きの扉(Assets/Prefab/KeyDoor_BayDoor.prefab があれば。なければ普通の壁)
///   北(Z=30)の中央 : ヒューズボックス
///   通り道         : 入口から中央を通って、西の鍵付きの扉(と東のFan)へ。通り道には、物を置かない
///   東(X=30)の中央 : Wall Fan
///   中央           : 柱4本と、梁
///   電灯           : 8個。Assets/Prefab/BrokenLight.prefab があれば、1個を壊れかけの点滅ライトにする
/// </summary>
public static class Room5x5Builder
{
    // ---------------- 寸法(キットの規格) ----------------

    private const float Module = 6f;            // 壁・床・天井の1枚の長さ
    private const int Count = 5;                // 1辺のマス数
    private const float CeilingPivotY = 9f;     // 天井タイルの基準点の高さ(タイルはその下に付く)
    private const float CeilingUnderside = 8.8f;
    private const float WallInner = 0.17f;      // 壁の内側の面
    private const float RoomSize = Module * Count; // 30m

    private const string KitFolderKeyword = "SciFi Warehouse Kit/Prefabs";
    private const string KeyDoorPrefabPath = "Assets/Prefab/KeyDoor_BayDoor.prefab";
    private const string BrokenLightPrefabPath = "Assets/Prefab/BrokenLight.prefab";
    private const string OutputFolder = "Assets/Prefab";
    private const string OutputPath = "Assets/Prefab/Room_5x5.prefab";

    // ---------------- 作業用 ----------------

    private enum Anchor
    {
        Center,
        Min,
        Max
    }

    private static readonly Dictionary<string, GameObject> kitCache = new Dictionary<string, GameObject>();
    private static readonly List<string> missing = new List<string>();

    private static Transform structureRoot;
    private static Transform doorsRoot;
    private static Transform propsRoot;
    private static Transform lightsRoot;

    // ---------------- メニュー ----------------

    [MenuItem("Tools/Room/5x5の部屋を作る")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Room 5x5", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        kitCache.Clear();
        missing.Clear();

        GameObject root = new GameObject("Room_5x5");
        Undo.RegisterCreatedObjectUndo(root, "5x5の部屋を作る");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        structureRoot = CreateGroup(root.transform, "Structure");
        doorsRoot = CreateGroup(root.transform, "Doors");
        propsRoot = CreateGroup(root.transform, "Props");
        lightsRoot = CreateGroup(root.transform, "Lights");

        BuildFloorAndCeiling();
        BuildWalls();
        BuildColumns();
        BuildProps();
        BuildLights();

        SetStatic(structureRoot);
        SetStatic(propsRoot);
        SetLayerDefault(root);

        string savedPath = SavePrefab(root);
        MoveToSceneView(root);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        string message = "5x5の部屋を作りました。\n\n"
            + "シーン : Room_5x5 (Sceneビューの中心あたりに置きました)\n"
            + "Prefab : " + savedPath;

        if (missing.Count > 0)
        {
            message += "\n\n見つからなかったPrefab(置かずに飛ばしました):\n - " + string.Join("\n - ", missing);
        }

        EditorUtility.DisplayDialog("Room 5x5", message, "OK");
    }

    // ---------------- 床と天井 ----------------

    private static void BuildFloorAndCeiling()
    {
        // 床は、見た目に変化が出るよう、4種類を決まった順番で並べる
        string[] pattern = { "Floor Tile 01", "Floor Tile 01", "Floor Tile 02", "Floor Tile 01",
                             "Floor Tile 03", "Floor Tile 01", "Floor Tile 01", "Floor Tile 04" };
        int index = 0;

        for (int ix = 0; ix < Count; ix++)
        {
            for (int iz = 0; iz < Count; iz++)
            {
                Vector3 floorPos = new Vector3(ix * Module, 0f, iz * Module);
                Spawn(pattern[index % pattern.Length], structureRoot, floorPos, 0f);
                index++;

                Vector3 ceilingPos = new Vector3(ix * Module, CeilingPivotY, iz * Module);
                Spawn("Ceiling Closed", structureRoot, ceilingPos, 0f);
            }
        }
    }

    // ---------------- 壁 ----------------
    // 壁は、基準点から ローカル+Z方向 に6m伸びる。
    // ローカル+X側が部屋の内側になる(付属のデモシーンと同じ向き)。

    private static void BuildWalls()
    {
        // 四隅(Wall Corner は、両側に6mずつ伸びるL字)
        Spawn("Wall Corner", structureRoot, new Vector3(0f, 0f, 0f), 0f);
        Spawn("Wall Corner", structureRoot, new Vector3(0f, 0f, RoomSize), 90f);
        Spawn("Wall Corner", structureRoot, new Vector3(RoomSize, 0f, 0f), 270f);
        Spawn("Wall Corner", structureRoot, new Vector3(RoomSize, 0f, RoomSize), 180f);

        GameObject keyDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyDoorPrefabPath);

        // 各辺の、四隅の間の3枚
        for (int i = 1; i <= Count - 2; i++)
        {
            float offset = i * Module;

            // 西(X=0): +Z方向へ伸びる。中央(i=2)は、鍵付きの扉
            if (i == 2 && keyDoorPrefab != null)
            {
                SpawnPrefab(keyDoorPrefab, doorsRoot, new Vector3(0f, 0f, offset), 0f);
            }
            else
            {
                Spawn("Wall Plain", structureRoot, new Vector3(0f, 0f, offset), 0f);
            }

            // 東(X=30): -Z方向へ伸びる
            string eastName = (i == 2) ? "Wall Fan" : "Wall Plain";
            Spawn(eastName, structureRoot, new Vector3(RoomSize, 0f, offset + Module), 180f);

            // 南(Z=0): -X方向へ伸びる
            string southName = (i == 2) ? "Wall Door" : "Wall Plain";
            Spawn(southName, structureRoot, new Vector3(offset + Module, 0f, 0f), 270f);

            // 北(Z=30): +X方向へ伸びる
            Spawn("Wall Plain", structureRoot, new Vector3(offset, 0f, RoomSize), 90f);
        }
    }

    // ---------------- 柱と梁 ----------------

    private static void BuildColumns()
    {
        float low = Module * 2f;   // 12
        float high = Module * 3f;  // 18

        // 柱(基準点は、柱の底の真ん中。高さは9mで、天井まで届く)
        Spawn("Pillar", structureRoot, new Vector3(low, 0f, low), 0f);
        Spawn("Pillar", structureRoot, new Vector3(high, 0f, low), 0f);
        Spawn("Pillar", structureRoot, new Vector3(low, 0f, high), 0f);
        Spawn("Pillar", structureRoot, new Vector3(high, 0f, high), 0f);

        // 梁(基準点から、ローカル+X方向に6m。90度回すと、手前(-Z)へ伸びる)
        float beamY = 8.9f;
        Spawn("Ceiling Support", structureRoot, new Vector3(low, beamY, low), 0f);
        Spawn("Ceiling Support", structureRoot, new Vector3(low, beamY, high), 0f);
        Spawn("Ceiling Support", structureRoot, new Vector3(low, beamY, high), 90f);
        Spawn("Ceiling Support", structureRoot, new Vector3(high, beamY, high), 90f);
    }

    // ---------------- 小物 ----------------

    private static void BuildProps()
    {
        float center = RoomSize / 2f; // 15

        // 壁につける物(基準点が壁の中心線に合うように作られている。壁と同じ向きにする)
        float[] sideSpots = { 3f, 9f, 21f, 27f };
        foreach (float p in sideSpots)
        {
            Spawn("Wall Light", propsRoot, new Vector3(p, 5.6f, 0f), 270f);
            Spawn("Wall Light", propsRoot, new Vector3(p, 5.6f, RoomSize), 90f);
        }

        float[] westSpots = { 9f, 21f };
        foreach (float p in westSpots)
        {
            Spawn("Wall Light", propsRoot, new Vector3(0f, 5.6f, p), 0f);
        }

        Spawn("Wall Light", propsRoot, new Vector3(RoomSize, 5.6f, 9f), 180f);
        Spawn("Wall Light", propsRoot, new Vector3(RoomSize, 5.6f, 21f), 180f);

        Spawn("Exit Sign", propsRoot, new Vector3(center, 3f, 0f), 270f);
        Spawn("Fusebox 01", propsRoot, new Vector3(13.5f, 2f, RoomSize), 90f);
        Spawn("Fusebox 02", propsRoot, new Vector3(16.5f, 3.2f, RoomSize), 90f);
        Spawn("Wall Light", propsRoot, new Vector3(center, 5.6f, RoomSize), 90f);

        // 棚: 東の壁ぞい(真ん中のFanは、見えるようにあける)
        PlaceOnFloor("Shelf Variation 02", propsRoot, RoomSize - WallInner, 4.0f, 90f, Anchor.Max, Anchor.Center, 0f);
        PlaceOnFloor("Shelf Variation 04", propsRoot, RoomSize - WallInner, 26.0f, 90f, Anchor.Max, Anchor.Center, 0f);

        // 棚: 西の壁ぞい
        PlaceOnFloor("Shelf Variation 03", propsRoot, WallInner, 4.5f, 270f, Anchor.Min, Anchor.Center, 0f);
        PlaceOnFloor("Shelf Variation 06", propsRoot, WallInner, 25.5f, 270f, Anchor.Min, Anchor.Center, 0f);

        // 棚: 北の壁ぞい(鍵付きの扉の両わき)
        PlaceOnFloor("Shelf Variation 05", propsRoot, 6.5f, RoomSize - WallInner, 0f, Anchor.Center, Anchor.Max, 0f);
        PlaceOnFloor("Shelves Empty", propsRoot, 23.5f, RoomSize - WallInner, 0f, Anchor.Center, Anchor.Max, 0f);

        // 南西の角: 木箱を積む
        GameObject crateLong = PlaceOnFloor("Crate Long", propsRoot, 0.3f, 0.3f, 0f, Anchor.Min, Anchor.Min, 0f);
        PlaceOnFloor("Crate Short", propsRoot, 0.3f, 1.8f, 0f, Anchor.Min, Anchor.Min, 0f);
        StackOn(crateLong, "Crate Short", 12f);

        // 北東の角: ドラム缶
        PlaceOnFloor("Barrel", propsRoot, 28.9f, 28.9f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 28.0f, 28.8f, 35f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 28.5f, 27.8f, 80f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 27.2f, 28.9f, 10f, Anchor.Center, Anchor.Center, 0f);

        // 北西の角: ドラム缶
        PlaceOnFloor("Barrel", propsRoot, 1.0f, 28.9f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 1.9f, 28.8f, 55f, Anchor.Center, Anchor.Center, 0f);

        // 荷物を積んだパレット(隠れる場所)。通り道(入口から中央、中央から西と東)は、あけておく
        PlaceOnFloor("Pallet Variation 01", propsRoot, 7f, 9f, 10f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 05", propsRoot, 23f, 9f, 80f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 03", propsRoot, 8f, 22f, 190f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 07", propsRoot, 22f, 21f, 300f, Anchor.Center, Anchor.Center, 0f);

        // パレットと木箱
        GameObject pallet = PlaceOnFloor("Pallet", propsRoot, 4.5f, 20.5f, 20f, Anchor.Center, Anchor.Center, 0f);
        GameObject palletCrate = StackOn(pallet, "Crate Long", 110f);
        StackOn(palletCrate, "Crate Short", 15f);

        // カート
        PlaceOnFloor("Cart", propsRoot, 26.5f, 20.5f, 100f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Cart", propsRoot, 5f, 4.5f, 330f, Anchor.Center, Anchor.Center, 0f);

        // 入口のそば
        PlaceOnFloor("Garbage Bin", propsRoot, 11f, 0.4f, 0f, Anchor.Center, Anchor.Min, 0f);
        PlaceOnFloor("Fire Extinguisher", propsRoot, 21f, 0.25f, 0f, Anchor.Center, Anchor.Min, 1.0f);
    }

    /// <summary>
    /// 下の物の上に、真ん中をそろえて乗せる。
    /// </summary>
    private static GameObject StackOn(GameObject below, string kitName, float yaw)
    {
        if (below == null) return null;

        Bounds belowBounds;
        if (!TryGetBounds(below, out belowBounds)) return null;

        return PlaceOnFloor(kitName, propsRoot, belowBounds.center.x, belowBounds.center.z, yaw,
            Anchor.Center, Anchor.Center, belowBounds.max.y);
    }

    // ---------------- 照明 ----------------

    private static void BuildLights()
    {
        float[] positions = { 7.5f, 15f, 22.5f };
        GameObject brokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenLightPrefabPath);

        foreach (float x in positions)
        {
            foreach (float z in positions)
            {
                // 真ん中は、柱に囲まれているので、置かない
                if (Mathf.Approximately(x, 15f) && Mathf.Approximately(z, 15f)) continue;

                // 1個だけ、壊れかけの点滅ライトにする(Prefabがあれば)
                if (brokenPrefab != null && Mathf.Approximately(x, 7.5f) && Mathf.Approximately(z, 22.5f))
                {
                    SpawnPrefab(brokenPrefab, lightsRoot, new Vector3(x, CeilingUnderside, z), 0f);
                    continue;
                }

                AddHangingLight(x, z);
            }
        }
    }

    private static void AddHangingLight(float x, float z)
    {
        GameObject fixture = Spawn("Hanging Light", propsRoot, new Vector3(x, CeilingUnderside, z), 0f);

        // 電球のあたりの高さに、Lightを置く
        float lightY = 7.8f;
        Bounds fixtureBounds;
        if (fixture != null && TryGetBounds(fixture, out fixtureBounds))
        {
            lightY = fixtureBounds.min.y + 0.3f;
        }

        GameObject lightObject = new GameObject("RoomLight");
        lightObject.transform.SetParent(lightsRoot, false);
        lightObject.transform.position = new Vector3(x, lightY, z);

        Light roomLight = lightObject.AddComponent<Light>();
        roomLight.type = LightType.Point;
        roomLight.color = new Color(1f, 0.82f, 0.6f);
        roomLight.intensity = 3f;
        roomLight.range = 16f;
        roomLight.shadows = LightShadows.None;

        // ゆらぎのスクリプトがあれば付ける(なくても、部屋は作れる)
        System.Type flickerType = System.Type.GetType("RoomLightFlicker, Assembly-CSharp");
        if (flickerType != null)
        {
            lightObject.AddComponent(flickerType);
        }
    }

    // ---------------- 置く道具 ----------------

    private static Transform CreateGroup(Transform parent, string groupName)
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static GameObject LoadKit(string kitName)
    {
        GameObject cached;
        if (kitCache.TryGetValue(kitName, out cached)) return cached;

        GameObject found = null;
        string[] guids = AssetDatabase.FindAssets(kitName + " t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains(KitFolderKeyword)) continue;
            if (Path.GetFileNameWithoutExtension(path) != kitName) continue;

            found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            break;
        }

        if (found == null && !missing.Contains(kitName)) missing.Add(kitName);

        kitCache[kitName] = found;
        return found;
    }

    private static GameObject Spawn(string kitName, Transform parent, Vector3 position, float yaw)
    {
        GameObject prefab = LoadKit(kitName);
        if (prefab == null) return null;
        return SpawnPrefab(prefab, parent, position, yaw);
    }

    private static GameObject SpawnPrefab(GameObject prefab, Transform parent, Vector3 position, float yaw)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return instance;
    }

    /// <summary>
    /// 床に置く。基準点がどこにあっても、見た目の大きさ(Bounds)をもとに位置を合わせる。
    /// ax, az が Min のときは、その側の端を x, z に合わせる。Max なら反対の端。
    /// bottomY は、物の底の高さ。
    /// </summary>
    private static GameObject PlaceOnFloor(string kitName, Transform parent, float x, float z, float yaw,
        Anchor ax, Anchor az, float bottomY)
    {
        GameObject instance = Spawn(kitName, parent, Vector3.zero, yaw);
        if (instance == null) return null;

        Bounds b;
        if (!TryGetBounds(instance, out b)) return instance;

        Vector3 delta = Vector3.zero;
        delta.x = x - Pick(b.min.x, b.center.x, b.max.x, ax);
        delta.z = z - Pick(b.min.z, b.center.z, b.max.z, az);
        delta.y = bottomY - b.min.y;
        instance.transform.position += delta;

        return instance;
    }

    private static float Pick(float minValue, float centerValue, float maxValue, Anchor anchor)
    {
        if (anchor == Anchor.Min) return minValue;
        if (anchor == Anchor.Max) return maxValue;
        return centerValue;
    }

    private static bool TryGetBounds(GameObject target, out Bounds result)
    {
        result = new Bounds(target.transform.position, Vector3.zero);
        bool hasBounds = false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (!hasBounds)
            {
                result = r.bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(r.bounds);
            }
        }

        return hasBounds;
    }

    /// <summary>
    /// キットの床はレイヤー9に入っていて、Main Camera に映らない。全部 Default(0) にそろえる。
    /// </summary>
    private static void SetLayerDefault(GameObject root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            t.gameObject.layer = 0;
        }
    }

    private static void SetStatic(Transform group)
    {
        StaticEditorFlags flags = StaticEditorFlags.ContributeGI
            | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic
            | StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.ReflectionProbeStatic;

        Transform[] all = group.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }

    // ---------------- 保存と配置 ----------------

    private static string SavePrefab(GameObject root)
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefab");
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, OutputPath, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
        return OutputPath;
    }

    private static void MoveToSceneView(GameObject root)
    {
        Vector3 target = Vector3.zero;

        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            Vector3 pivot = view.pivot;
            target = new Vector3(
                Mathf.Round(pivot.x / Module) * Module,
                0f,
                Mathf.Round(pivot.z / Module) * Module);
        }

        root.transform.position = target;
    }
}