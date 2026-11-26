using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SciFi Warehouse Kit で、3x3マス(18m x 18m、高さ9m)の部屋を組み立てる。
/// メニュー: Tools / Room / 3x3の部屋を作る
///
/// できるもの:
///   Assets/Prefab/Room_3x3.prefab を保存し、シーンにも1つ置く(Sceneビューの中心あたり)。
///
/// 部屋の向き(部屋の左下が原点。X が右、Z が奥):
///   南(Z=0)の中央  : 入口(Wall Door)
///   北(Z=18)の中央 : 鍵付きの扉(Assets/Prefab/KeyDoor_BayDoor.prefab があれば。なければ普通の壁)
///   東(X=18)の中央 : Wall Fan
/// </summary>
public static class Room3x3Builder
{
    // ---------------- 寸法(キットの規格) ----------------

    private const float Module = 6f;            // 壁・床・天井の1枚の長さ
    private const float CeilingPivotY = 9f;     // 天井タイルの基準点の高さ(タイルはその下に付く)
    private const float CeilingUnderside = 8.8f;
    private const float WallInner = 0.17f;      // 壁の内側の面(壁の厚み0.3の半分より少し内側)
    private const float RoomSize = Module * 3f; // 18m

    private const string KitFolderKeyword = "SciFi Warehouse Kit/Prefabs";
    private const string KeyDoorPrefabPath = "Assets/Prefab/KeyDoor_BayDoor.prefab";
    private const string OutputFolder = "Assets/Prefab";
    private const string OutputPath = "Assets/Prefab/Room_3x3.prefab";

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

    [MenuItem("Tools/Room/3x3の部屋を作る")]
    public static void Build()
    {
        kitCache.Clear();
        missing.Clear();

        GameObject root = new GameObject("Room_3x3");
        Undo.RegisterCreatedObjectUndo(root, "3x3の部屋を作る");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        structureRoot = CreateGroup(root.transform, "Structure");
        doorsRoot = CreateGroup(root.transform, "Doors");
        propsRoot = CreateGroup(root.transform, "Props");
        lightsRoot = CreateGroup(root.transform, "Lights");

        BuildFloorAndCeiling();
        BuildWalls();
        BuildProps();
        BuildLights();

        SetStatic(structureRoot);
        SetStatic(propsRoot);
        SetLayerDefault(root);

        string savedPath = SavePrefab(root);
        MoveToSceneView(root);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        string message = "3x3の部屋を作りました。\n\n"
            + "シーン : Room_3x3 (Sceneビューの中心あたりに置きました)\n"
            + "Prefab : " + savedPath;

        if (missing.Count > 0)
        {
            message += "\n\n見つからなかったPrefab(置かずに飛ばしました):\n - " + string.Join("\n - ", missing);
        }

        EditorUtility.DisplayDialog("Room 3x3", message, "OK");
    }

    // ---------------- 床と天井 ----------------

    private static void BuildFloorAndCeiling()
    {
        // 床は、見た目に変化が出るよう、4種類を決まった順番で並べる
        string[] floorNames = { "Floor Tile 01", "Floor Tile 01", "Floor Tile 02",
                                "Floor Tile 01", "Floor Tile 03", "Floor Tile 01",
                                "Floor Tile 04", "Floor Tile 01", "Floor Tile 01" };
        int index = 0;

        for (int ix = 0; ix < 3; ix++)
        {
            for (int iz = 0; iz < 3; iz++)
            {
                Vector3 floorPos = new Vector3(ix * Module, 0f, iz * Module);
                Spawn(floorNames[index], structureRoot, floorPos, 0f);
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

        // 西(X=0)の中央
        Spawn("Wall Plain", structureRoot, new Vector3(0f, 0f, Module), 0f);

        // 東(X=18)の中央
        Spawn("Wall Fan", structureRoot, new Vector3(RoomSize, 0f, Module * 2f), 180f);

        // 南(Z=0)の中央: 入口
        Spawn("Wall Door", structureRoot, new Vector3(Module * 2f, 0f, 0f), 270f);

        // 北(Z=18)の中央: 鍵付きの扉
        GameObject keyDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyDoorPrefabPath);
        if (keyDoorPrefab != null)
        {
            SpawnPrefab(keyDoorPrefab, doorsRoot, new Vector3(Module, 0f, RoomSize), 90f);
        }
        else
        {
            Spawn("Wall Plain", structureRoot, new Vector3(Module, 0f, RoomSize), 90f);
        }
    }

    // ---------------- 小物 ----------------

    private static void BuildProps()
    {
        // 壁につける物(基準点が壁の中心線に合うように作られている。壁と同じ向きにする)
        Spawn("Wall Light", propsRoot, new Vector3(0f, 5.6f, 9f), 0f);
        Spawn("Wall Light", propsRoot, new Vector3(RoomSize, 5.6f, 9f), 180f);
        Spawn("Wall Light", propsRoot, new Vector3(3f, 5.6f, 0f), 270f);
        Spawn("Wall Light", propsRoot, new Vector3(15f, 5.6f, 0f), 270f);
        Spawn("Wall Light", propsRoot, new Vector3(3f, 5.6f, RoomSize), 90f);
        Spawn("Wall Light", propsRoot, new Vector3(15f, 5.6f, RoomSize), 90f);

        Spawn("Exit Sign", propsRoot, new Vector3(9f, 3f, 0f), 270f);
        Spawn("Fusebox 01", propsRoot, new Vector3(0f, 2f, 7.2f), 0f);
        Spawn("Fusebox 02", propsRoot, new Vector3(0f, 3.2f, 10.2f), 0f);

        // 東の壁ぞいの棚(入口から奥へ。真ん中のFanは見えるよう、間をあける)
        PlaceOnFloor("Shelf Variation 02", propsRoot, RoomSize - WallInner, 4.2f, 90f, Anchor.Max, Anchor.Center, 0f);
        PlaceOnFloor("Shelf Variation 04", propsRoot, RoomSize - WallInner, 13.8f, 90f, Anchor.Max, Anchor.Center, 0f);

        // 南西の角: 木箱を積む
        GameObject crateLong = PlaceOnFloor("Crate Long", propsRoot, 0.3f, 0.3f, 0f, Anchor.Min, Anchor.Min, 0f);
        PlaceOnFloor("Crate Short", propsRoot, 0.3f, 1.8f, 0f, Anchor.Min, Anchor.Min, 0f);
        StackOn(crateLong, "Crate Short", 12f);

        // 北西の角: ドラム缶
        PlaceOnFloor("Barrel", propsRoot, 0.9f, 17.0f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 1.8f, 17.1f, 35f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 1.3f, 16.1f, 80f, Anchor.Center, Anchor.Center, 0f);

        // 部屋の左寄り: パレットと木箱(まっすぐ進む通り道は、あけておく)
        GameObject pallet = PlaceOnFloor("Pallet", propsRoot, 3.5f, 9f, 20f, Anchor.Center, Anchor.Center, 0f);
        GameObject palletCrate = StackOn(pallet, "Crate Long", 110f);
        StackOn(palletCrate, "Crate Short", 15f);

        // 部屋の右寄り: カート
        PlaceOnFloor("Cart", propsRoot, 14f, 9.5f, 300f, Anchor.Center, Anchor.Center, 0f);

        // 入口のそば
        PlaceOnFloor("Garbage Bin", propsRoot, 4.2f, 0.4f, 0f, Anchor.Center, Anchor.Min, 0f);
        PlaceOnFloor("Fire Extinguisher", propsRoot, 15f, 0.25f, 0f, Anchor.Center, Anchor.Min, 1.0f);
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
        float[] positions = { 4.5f, 13.5f };
        foreach (float x in positions)
        {
            foreach (float z in positions)
            {
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
        roomLight.intensity = 2.5f;
        roomLight.range = 14f;
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