using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hierarchy の「C」の建物の、中だけを作り直す。外側の大きさと、出入口の位置は、そのまま。
/// メニュー: Tools / Scene / Cの建物の中を作り直す
///
/// そのまま残す物:
///   外の壁、床、天井、梁、配管、壁の電灯、ダクト、スプリンクラー、
///   キャットウォークと階段(東の高い所の扉へ行く道)、廊下(Corridors_C)、東の別棟
///
/// しまう物(C_Old_Interior に移して、オフにする。消してはいない):
///   中の小物(Props_C の中身)、中の柱、つり下げ電灯とその光、南東の小部屋の仕切りの壁
///
/// 新しく作る物(C_Interior_New) … 案C「棚の迷路」:
///   西の倉庫(高さ9m)   : 南北に長い棚の列を4本。列の切れ目が南と北で交互なので、ジグザグに歩かないと東へ抜けられない。
///                         西の扉の内側に、パレットと木箱のバリケード(すき間から入る)。通路に倒れた棚と、散らかった物
///   東の吹き抜け(高さ18m): 南東の小部屋をなくして、木箱を高く積んだ山が並ぶ、コンテナ置き場のようにする
///   電灯                : ほとんど消えているか、ちらつく。代わりに、赤い非常灯がところどころ光る
///   血しぶき            : 床と壁に、立体の血。通路の1本に、引きずった跡
///
/// Ctrl+Z で、全部もとにもどせる。
/// </summary>
public static class CBuildingRebuilder
{
    private const string BuildingName = "C";
    private const string NewGroupName = "C_Interior_New";
    private const string OldGroupName = "C_Old_Interior";

    private const float WestCeiling = 8.8f;      // 西の倉庫の天井の下(m)
    private const float EastCeiling = 18.6f;     // 東の吹き抜けの天井の下(m)
    private const float LowCeiling = 7.8f;       // 西の倉庫の、低くなった天井の下(x=18から24、z=0.5から12.5)
    private const float WallInner = 0.17f;

    // 建物の外側(シーンの座標)
    private const float MinX = 0f;
    private const float MaxX = 42f;
    private const float MinZ = -5.53f;
    private const float MaxZ = 24.47f;

    private const string KitFolderKeyword = "SciFi Warehouse Kit/Prefabs";
    private const string BrokenLightPrefabPath = "Assets/Prefab/BrokenLight.prefab";

    private enum Anchor
    {
        Center,
        Min,
        Max
    }

    private static readonly Dictionary<string, GameObject> kitCache = new Dictionary<string, GameObject>();
    private static readonly List<string> missing = new List<string>();

    private static Transform structureRoot;
    private static Transform propsRoot;
    private static Transform lightsRoot;
    private static Transform bloodRoot;

    // ---------------- メニュー ----------------

    [MenuItem("Tools/Scene/Cの建物の中を作り直す")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("C の建物", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        GameObject building = GameObject.Find(BuildingName);
        if (building == null)
        {
            EditorUtility.DisplayDialog("C の建物", "Hierarchy に「" + BuildingName + "」が見つかりません。", "OK");
            return;
        }

        if (building.transform.Find(NewGroupName) != null)
        {
            if (!EditorUtility.DisplayDialog("C の建物",
                "もう作り直してあります(" + NewGroupName + " があります)。\n古い " + NewGroupName + " を消して、作り直しますか?",
                "作り直す", "やめる"))
            {
                return;
            }
        }

        List<Transform> oldItems = CollectOldInterior(building.transform);

        if (!EditorUtility.DisplayDialog("C の建物",
            "C の建物の中を作り直します。\n\n"
            + "しまう物: " + oldItems.Count + " 個(" + OldGroupName + " に移して、オフにします。消しません)\n"
            + "残す物: 外の壁、床、天井、キャットウォーク、階段、廊下、東の別棟\n\n"
            + "Ctrl+Z で、もとにもどせます。続けますか?", "続ける", "やめる"))
        {
            return;
        }

        Undo.SetCurrentGroupName("Cの建物の中を作り直す");
        int undoGroup = Undo.GetCurrentGroup();

        kitCache.Clear();
        missing.Clear();

        // 前に作った新しい中身を消す
        Transform previous = building.transform.Find(NewGroupName);
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);

        // 古い中身をしまう
        Transform oldGroup = building.transform.Find(OldGroupName);
        if (oldGroup == null)
        {
            oldGroup = CreateWorldGroup(building.transform, OldGroupName);
        }
        foreach (Transform t in oldItems)
        {
            Undo.SetTransformParent(t, oldGroup, "Cの建物の中を作り直す");
        }
        Undo.RecordObject(oldGroup.gameObject, "Cの建物の中を作り直す");
        oldGroup.gameObject.SetActive(false);

        // 新しい中身
        Transform root = CreateWorldGroup(building.transform, NewGroupName);
        structureRoot = CreateGroup(root, "Structure");
        propsRoot = CreateGroup(root, "Props");
        lightsRoot = CreateGroup(root, "Lights");
        bloodRoot = CreateGroup(root, "Blood");

        BuildWestHall();
        BuildEastBay();
        BuildLights();
        BuildBlood();

        SetStatic(structureRoot);
        SetStatic(propsRoot);
        SetStatic(bloodRoot);
        SetLayerDefault(root.gameObject);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root) Undo.RegisterCreatedObjectUndo(t.gameObject, "Cの建物の中を作り直す");
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(building.scene);

        string message = "C の建物の中を作り直しました。\n\n"
            + "新しい中身: " + NewGroupName + "\n"
            + "古い中身: " + OldGroupName + "(オフ。オンにすると、前の物も見えます)\n\n"
            + "物の位置が変わったので、NavMesh を焼き直してください。";

        if (missing.Count > 0)
        {
            message += "\n\n見つからなかったPrefab(置かずに飛ばしました):\n - " + string.Join("\n - ", missing);
        }

        EditorUtility.DisplayDialog("C の建物", message, "OK");
    }

    // ---------------- しまう物を集める ----------------

    /// <summary>
    /// 作り直す物を集める。
    ///   Props_C の中身(グループごと)、Pillar、Hanging Light、Overhead Light Source、
    ///   南東の小部屋の仕切りの壁(位置で見分ける)
    /// </summary>
    private static List<Transform> CollectOldInterior(Transform building)
    {
        List<Transform> result = new List<Transform>();

        Transform props = building.Find("Props_C");
        if (props != null)
        {
            foreach (Transform child in props)
            {
                result.Add(child);
            }
        }

        // 南東の小部屋の仕切りの壁(シーンの座標と、元のプレハブの名前)
        Vector3[] partitionPositions =
        {
            new Vector3(24f, 0f, 6.47f),      // Wall Corner
            new Vector3(30f, 0f, 6.47f),      // Wall Door
            new Vector3(36f, 0f, 6.47f),      // Wall Plain
            new Vector3(23.89f, -0.75f, -5.41f) // Wall Corridor
        };
        string[] partitionKinds = { "Wall Corner", "Wall Door", "Wall Plain", "Wall Corridor" };

        foreach (Transform t in building.GetComponentsInChildren<Transform>(true))
        {
            if (t == building) continue;
            if (result.Contains(t) || IsInside(t, result)) continue;
            if (t.name == NewGroupName || t.name == OldGroupName || IsUnder(t, NewGroupName) || IsUnder(t, OldGroupName)) continue;

            string source = SourceName(t.gameObject);
            bool isPrefabRoot = PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject);

            if (isPrefabRoot && (source == "Pillar" || source == "Hanging Light"))
            {
                result.Add(t);
                continue;
            }

            if (t.name.StartsWith("Overhead Light Source"))
            {
                result.Add(t);
                continue;
            }

            if (isPrefabRoot)
            {
                for (int i = 0; i < partitionPositions.Length; i++)
                {
                    if (source == partitionKinds[i] && Vector3.Distance(t.position, partitionPositions[i]) < 0.6f)
                    {
                        result.Add(t);
                        break;
                    }
                }
            }
        }

        return result;
    }

    private static bool IsInside(Transform t, List<Transform> list)
    {
        foreach (Transform p in list)
        {
            if (t.IsChildOf(p)) return true;
        }
        return false;
    }

    private static bool IsUnder(Transform t, string groupName)
    {
        Transform p = t.parent;
        while (p != null)
        {
            if (p.name == groupName) return true;
            p = p.parent;
        }
        return false;
    }

    private static string SourceName(GameObject go)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
        return source != null ? source.name : go.name;
    }

    // ---------------- 西の倉庫(x=0から24、高さ9m): 棚の迷路 ----------------
    //
    //   棚の列(南北に長い)   x=4.6 : z=-5 から 14 (北が切れ目)
    //                        x=10.0: z= 5 から 24 (南が切れ目)
    //                        x=15.4: z=-5 から 14 (北が切れ目)
    //                        x=20.8: z= 5 から 24 (南が切れ目)
    //   西の扉(z=12.5から18.5)から入り、北、南、北、南と、ジグザグに歩いて東へ抜ける

    private static void BuildWestHall()
    {
        // 柱(天井の梁が交わる所。棚の列とぶつからない所)
        Spawn("Pillar", structureRoot, new Vector3(6f, 0f, 0.47f), 0f);
        Spawn("Pillar", structureRoot, new Vector3(18f, 0f, 0.47f), 0f);
        Spawn("Pillar", structureRoot, new Vector3(6f, 0f, 18.47f), 0f);
        Spawn("Pillar", structureRoot, new Vector3(18f, 0f, 18.47f), 0f);

        // 棚の列
        ShelfRowAlongZ(new[] { "Shelf Variation 02", "Shelf Variation 04", "Shelves Empty" }, 4.6f, -5.0f, 270f);
        ShelfRowAlongZ(new[] { "Shelf Variation 05", "Shelf Variation 03", "Shelf Variation 06" }, 10.0f, 5.0f, 90f);
        ShelfRowAlongZ(new[] { "Shelf Variation 06", "Shelf Variation 02", "Shelf Variation 05" }, 15.4f, -5.0f, 270f);
        ShelfRowAlongZ(new[] { "Shelf Variation 04", "Shelves Empty", "Shelf Variation 03" }, 20.8f, 5.0f, 90f);

        // 2本目と3本目のあいだの通路の北に、倒れた棚(行き止まりを、さらに狭くする)
        Quaternion fallen = Quaternion.AngleAxis(84f, Vector3.forward) * Quaternion.Euler(0f, 90f, 0f);
        GameObject fallenShelf = PlaceRotated("Shelf Variation 01", 12.6f, 21.2f, fallen, Anchor.Center, Anchor.Center);
        if (fallenShelf != null) fallenShelf.name = "Shelf Variation 01 (Fallen)";

        // 西の扉の内側のバリケード(真ん中の約2mのすき間から入る)
        GameObject palletA = PlaceOnFloor("Pallet", propsRoot, 2.2f, 13.6f, 5f, Anchor.Center, Anchor.Center, 0f);
        GameObject palletB = StackOn(palletA, "Pallet", 80f);
        StackOn(palletB, "Pallet Variation 07", 0f);
        GameObject crateA = PlaceOnFloor("Crate Long", propsRoot, 2.4f, 17.6f, 90f, Anchor.Center, Anchor.Center, 0f);
        GameObject crateB = StackOn(crateA, "Crate Long", 95f);
        StackOn(crateB, "Crate Short", 20f);
        PlaceRotated("Crate Short", 1.2f, 20.6f, Tipped(30f, 90f), Anchor.Center, Anchor.Center);

        // 通路に散らかった物
        PlaceRotated("Cart", 7.4f, 9.0f, Tipped(20f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Pallet Variation 06", propsRoot, 8.0f, -2.6f, 15f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 12.8f, 1.6f, Tipped(80f, 90f), Anchor.Center, Anchor.Center);
        PlaceRotated("Crate Short", 18.2f, 11.0f, Tipped(140f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Cart", propsRoot, 23.0f, -2.4f, 200f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 7.6f, 22.6f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 8.4f, 23.4f, 50f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Garbage Bin", 1.4f, 3.0f, Tipped(200f, 90f), Anchor.Center, Anchor.Center);
    }

    /// <summary>
    /// 棚を、南北(Z方向)に1列に並べる。x は列の真ん中、startZ は南の端。
    /// 1つずつ実際の大きさを測って、すきま 0.1m で北へつなげる。
    /// </summary>
    private static void ShelfRowAlongZ(string[] names, float x, float startZ, float yaw)
    {
        float cursor = startZ;
        foreach (string shelfName in names)
        {
            GameObject shelf = PlaceOnFloor(shelfName, propsRoot, x, cursor, yaw, Anchor.Center, Anchor.Min, 0f);

            Bounds b;
            if (shelf == null || !TryGetBounds(shelf, out b)) continue;
            cursor = b.max.z + 0.1f;
        }
    }

    // ---------------- 東の吹き抜け(x=24から42、高さ18m): コンテナ置き場 ----------------
    // キャットウォークの足と階段(x=29から37、z=19から24.5)と、南の廊下の入口(x=30から36)の前は、よける

    private static void BuildEastBay()
    {
        // 木箱を4段に積んだ山
        float[,] stacks =
        {
            { 26.2f, -3.4f }, { 28.8f, -3.4f }, { 39.6f, -3.4f }, { 39.6f, -0.6f },
            { 26.2f, 13.2f }, { 40.4f, 13.6f }, { 26.4f, 21.8f }, { 40.6f, 21.6f }
        };
        for (int i = 0; i < stacks.GetLength(0); i++)
        {
            CrateTower(stacks[i, 0], stacks[i, 1], 4, i * 37f);
        }

        PlaceOnFloor("Pallet Variation 01", propsRoot, 39.6f, 2.6f, 90f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Cart", 37.2f, 3.2f, Tipped(300f, 90f), Anchor.Center, Anchor.Center);
        PlaceRotated("Crate Short", 33.4f, 12.2f, Tipped(200f, 90f), Anchor.Center, Anchor.Center);

        PlaceOnFloor("Barrel", propsRoot, 41.0f, 8.4f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 38.4f, 19.6f, Tipped(30f, 90f), Anchor.Center, Anchor.Center);

        BuildLoadingArea();
    }

    /// <summary>
    /// 東の吹き抜けの南側(x=25から41、z=0から10)。南の廊下の入口から入ってすぐの場所。
    /// 東西に棚を置いて仕切り、真ん中のすき間(x=31から35)から北へ進む。
    /// 手前には、木箱の山をよけて通る、くねった道を作る。
    /// </summary>
    private static void BuildLoadingArea()
    {
        // 東西の仕切りの棚(前は南を向く)。真ん中に、約4mのすき間
        PlaceOnFloor("Shelf Variation 05", propsRoot, 24.6f, 6.0f, 0f, Anchor.Min, Anchor.Center, 0f);
        PlaceOnFloor("Shelf Variation 03", propsRoot, MaxX - WallInner, 6.0f, 0f, Anchor.Max, Anchor.Center, 0f);

        // 入口のすぐ先の、木箱の山(よけて通る)
        CrateTower(31.8f, 1.8f, 3, 15f);
        GameObject palletA = PlaceOnFloor("Pallet Variation 03", propsRoot, 35.6f, 3.6f, 70f, Anchor.Center, Anchor.Center, 0f);
        StackOn(palletA, "Crate Short", 10f);

        // 西のすみ: ドラム缶と、パレット
        PlaceOnFloor("Barrel", propsRoot, 27.8f, 2.0f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 28.7f, 2.6f, 40f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 27.9f, 3.1f, 80f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 29.6f, 4.0f, Tipped(110f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Pallet Variation 09", propsRoot, 25.8f, 3.8f, 0f, Anchor.Center, Anchor.Center, 0f);

        // 棚の北側(キャットウォークの下)
        PlaceRotated("Garbage Bin", 29.0f, 7.6f, Tipped(30f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Pallet Variation 02", propsRoot, 26.4f, 8.2f, 10f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Crate Short", 37.0f, 7.4f, Tipped(60f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Fire Extinguisher", propsRoot, 34.6f, 7.8f, 0f, Anchor.Center, Anchor.Center, 0f);
    }

    /// <summary>
    /// 木箱(Crate Long)を、少しずつ向きをずらして積み上げる。
    /// </summary>
    private static void CrateTower(float x, float z, int count, float baseYaw)
    {
        GameObject below = PlaceOnFloor("Crate Long", propsRoot, x, z, baseYaw, Anchor.Center, Anchor.Center, 0f);
        for (int i = 1; i < count && below != null; i++)
        {
            float yaw = baseYaw + ((i % 2 == 0) ? 0f : 90f) + Random.Range(-6f, 6f);
            below = StackOn(below, "Crate Long", yaw);
        }
    }

    /// <summary>
    /// 横に倒した向き。yaw で向きを決めてから、roll だけ横に倒す。
    /// </summary>
    private static Quaternion Tipped(float yaw, float roll)
    {
        return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(roll, Vector3.forward);
    }

    /// <summary>
    /// 好きな向きで、床に置く。見た目の大きさ(Bounds)で、位置と高さを合わせる。
    /// </summary>
    private static GameObject PlaceRotated(string kitName, float x, float z, Quaternion rotation, Anchor ax, Anchor az)
    {
        GameObject instance = Spawn(kitName, propsRoot, Vector3.zero, 0f);
        if (instance == null) return null;

        instance.transform.localRotation = rotation;

        Bounds b;
        if (!TryGetBounds(instance, out b)) return instance;

        Vector3 delta = Vector3.zero;
        delta.x = x - Pick(b.min.x, b.center.x, b.max.x, ax);
        delta.z = z - Pick(b.min.z, b.center.z, b.max.z, az);
        delta.y = 0f - b.min.y;
        instance.transform.position += delta;

        return instance;
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
    // 電灯の状態を、ばらばらにする。
    //   Steady  : 暗めで、ほぼ普通に光る
    //   Flicker : ちらちらして、よく消える
    //   Broken  : 壊れかけ(BrokenLight.prefab。火花と音つき)。プレハブがなければ、強い Flicker
    //   Dead    : 光っていない。少し傾いてぶら下がっている

    private enum LightState
    {
        Steady,
        Flicker,
        Broken,
        Dead
    }

    private static void BuildLights()
    {
        // 西の倉庫(ほとんど消えているか、ちらつく)
        AddCeilingLight(3f, -2.53f, LightState.Dead, 9f, WestCeiling, 1f);
        AddCeilingLight(15f, -2.53f, LightState.Steady, 0f, WestCeiling, 1f);
        AddCeilingLight(9f, 3.47f, LightState.Flicker, 0f, WestCeiling, 1f);
        AddCeilingLight(21f, 3.47f, LightState.Dead, -8f, LowCeiling, 1f);
        AddCeilingLight(15f, 9.47f, LightState.Dead, 11f, WestCeiling, 1f);
        AddCeilingLight(9f, 15.47f, LightState.Dead, -10f, WestCeiling, 1f);
        AddCeilingLight(21f, 15.47f, LightState.Flicker, 0f, WestCeiling, 1f);
        AddCeilingLight(3f, 21.47f, LightState.Broken, 0f, WestCeiling, 1f);
        AddCeilingLight(9f, 21.47f, LightState.Dead, 7f, WestCeiling, 1f);
        AddCeilingLight(21f, 21.47f, LightState.Steady, 0f, WestCeiling, 1f);

        // 東の吹き抜け(天井 18.6m。高いので、光を強く、遠くまで)
        AddCeilingLight(27f, 3.47f, LightState.Dead, 10f, EastCeiling, 2.4f);
        AddCeilingLight(39f, 3.47f, LightState.Flicker, 0f, EastCeiling, 2.4f);
        AddCeilingLight(33f, 9.47f, LightState.Dead, -9f, EastCeiling, 2.4f);
        AddCeilingLight(27f, 15.47f, LightState.Broken, 0f, EastCeiling, 2.4f);
        AddCeilingLight(39f, 15.47f, LightState.Dead, 8f, EastCeiling, 2.4f);
        AddCeilingLight(33f, 21.47f, LightState.Flicker, 0f, EastCeiling, 2.4f);

        // 落ちた電灯
        AddFallenLight(7.4f, 18.6f, 30f);
        AddFallenLight(34.4f, 15.6f, 120f);
        AddFallenLight(31.0f, 19.0f, 200f);

        // 赤い非常灯(棚の迷路の通路と、南の廊下の入口の上と、東の高い扉のそば)
        AddRedLight("EmergencyLight_Aisle1", new Vector3(7.3f, 6.0f, 6.0f));
        AddRedLight("EmergencyLight_Aisle3", new Vector3(18.1f, 6.0f, 12.0f));
        AddRedLight("EmergencyLight_South", new Vector3(33f, 6.0f, -4.4f));
        AddRedLight("EmergencyLight_UpperDoor", new Vector3(40.8f, 12.6f, 9.5f));
    }

    private static void AddRedLight(string lightName, Vector3 position)
    {
        GameObject warning = new GameObject(lightName);
        warning.transform.SetParent(lightsRoot, false);
        warning.transform.position = position;

        Light light = warning.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.15f, 0.1f);
        light.intensity = 2f;
        light.range = 10f;
        light.shadows = LightShadows.None;
    }

    /// <summary>
    /// 天井から下げる電灯。ceilingY は天井の下の高さ。scale は光の強さと届く距離の倍率(天井が高い所は大きく)。
    /// </summary>
    private static void AddCeilingLight(float x, float z, LightState state, float tilt, float ceilingY, float scale)
    {
        if (state == LightState.Broken)
        {
            GameObject brokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenLightPrefabPath);
            if (brokenPrefab != null)
            {
                GameObject broken = SpawnPrefab(brokenPrefab, lightsRoot, new Vector3(x, ceilingY, z), Random.Range(0f, 360f));
                foreach (Light l in broken.GetComponentsInChildren<Light>(true))
                {
                    l.range *= scale;
                }
                return;
            }

            if (!missing.Contains(BrokenLightPrefabPath)) missing.Add(BrokenLightPrefabPath);
        }

        GameObject fixture = Spawn("Hanging Light", propsRoot, new Vector3(x, ceilingY, z), 0f);

        if (state == LightState.Dead)
        {
            if (fixture != null)
            {
                fixture.name = "Hanging Light (Dead)";
                fixture.transform.localRotation = Quaternion.Euler(tilt, 20f, tilt * 0.5f);
            }
            return;
        }

        float lightY = ceilingY - 1f;
        Bounds fixtureBounds;
        if (fixture != null && TryGetBounds(fixture, out fixtureBounds))
        {
            lightY = fixtureBounds.min.y + 0.3f;
        }

        GameObject lightObject = new GameObject("RoomLight_" + state);
        lightObject.transform.SetParent(lightsRoot, false);
        lightObject.transform.position = new Vector3(x, lightY, z);

        Light roomLight = lightObject.AddComponent<Light>();
        roomLight.type = LightType.Point;
        roomLight.shadows = LightShadows.Soft;

        System.Type flickerType = System.Type.GetType("RoomLightFlicker, Assembly-CSharp");

        if (state == LightState.Steady)
        {
            roomLight.color = new Color(0.9f, 0.92f, 0.85f);
            roomLight.intensity = 2.2f * scale * scale;
            roomLight.range = 12f * scale;

            if (flickerType != null)
            {
                SerializedObject so = new SerializedObject(lightObject.AddComponent(flickerType));
                SetFloat(so, "noiseAmount", 0.08f);
                SetFloat(so, "noiseSpeed", 6f);
                SetBool(so, "blackouts", true);
                SetVector2(so, "blackoutInterval", new Vector2(15f, 40f));
                SetVector2(so, "blackoutDuration", new Vector2(0.1f, 0.3f));
                SetFloat(so, "blackoutChance", 0.4f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        else
        {
            bool strong = state == LightState.Broken;

            roomLight.color = new Color(0.85f, 0.88f, 0.9f);
            roomLight.intensity = (strong ? 2.6f : 2.0f) * scale * scale;
            roomLight.range = 11f * scale;

            if (flickerType != null)
            {
                SerializedObject so = new SerializedObject(lightObject.AddComponent(flickerType));
                SetFloat(so, "noiseAmount", strong ? 0.7f : 0.45f);
                SetFloat(so, "noiseSpeed", strong ? 22f : 14f);
                SetBool(so, "blackouts", true);
                SetVector2(so, "blackoutInterval", strong ? new Vector2(0.5f, 2.5f) : new Vector2(1.5f, 5f));
                SetVector2(so, "blackoutDuration", strong ? new Vector2(0.05f, 0.8f) : new Vector2(0.05f, 0.4f));
                SetFloat(so, "blackoutChance", strong ? 1f : 0.85f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    /// <summary>
    /// 天井から落ちて、床に転がっている電灯(光らない)。
    /// </summary>
    private static void AddFallenLight(float x, float z, float yaw)
    {
        GameObject fixture = Spawn("Hanging Light", propsRoot, Vector3.zero, 0f);
        if (fixture == null) return;

        fixture.name = "Hanging Light (Fallen)";
        fixture.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(82f, 0f, 6f);

        Bounds b;
        if (!TryGetBounds(fixture, out b)) return;

        fixture.transform.position += new Vector3(x - b.center.x, 0f - b.min.y, z - b.center.z);
    }

    private static void SetBool(SerializedObject so, string field, bool value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop != null) prop.boolValue = value;
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop != null) prop.floatValue = value;
    }

    private static void SetVector2(SerializedObject so, string field, Vector2 value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop != null) prop.vector2Value = value;
    }

    // ---------------- 血しぶき(立体) ----------------

    private static void BuildBlood()
    {
        // ---- 床 ----
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_03", 7.3f, 12.0f, 0f, 40f, 2.6f, 2.6f);
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_01", 18.0f, 3.0f, 0f, 160f, 2.0f, 2.0f);
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_02", 23.0f, 20.0f, 0f, 250f, 2.4f, 2.4f);
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_02", 33.5f, 6.5f, 0f, 20f, 2.2f, 2.2f);
        // 南の廊下の入口から、棚のすき間へ、引きずった跡
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Smear_01", 33.6f, 0.8f, 0.006f, -90f, 9.0f, 1.8f);
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_01", 27.0f, 8.8f, 0f, 70f, 2.0f, 2.0f);
        // 2本目と3本目の棚のあいだの通路を、南から北へ、引きずった跡(南の端が血だまり)
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Smear_01", 12.7f, 2.5f, 0.006f, -90f, 12.6f, 2.0f);

        // ---- 壁 ----
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Hand_01", new Vector3(MinX + WallInner, 1.5f, 11.6f), Vector3.right, 0.9f, 0.9f, -10f);
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Splat_01", new Vector3(MinX + WallInner, 4.5f, 20.0f), Vector3.right, 2.4f, 2.4f, 30f);
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Splat_01", new Vector3(MaxX - WallInner, 3.0f, 4.0f), Vector3.left, 2.4f, 2.4f, 0f);
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Drip_01", new Vector3(27.0f, 3.5f, MaxZ - WallInner), Vector3.back, 2.2f, 2.2f, 0f);
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Drip_02", new Vector3(39.0f, 3.0f, MinZ + WallInner), Vector3.forward, 2.0f, 2.0f, 0f);
    }

    // ---------------- グループ ----------------

    /// <summary>
    /// 建物の子に、シーンの原点にそろえたグループを作る(中の物は、シーンの座標でそのまま置ける)。
    /// </summary>
    private static Transform CreateWorldGroup(Transform building, string groupName)
    {
        GameObject group = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(group, "Cの建物の中を作り直す");
        group.transform.SetParent(building, true);
        group.transform.position = Vector3.zero;
        group.transform.rotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        return group.transform;
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
}