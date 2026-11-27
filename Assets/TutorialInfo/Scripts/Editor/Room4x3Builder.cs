using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SciFi Warehouse Kit で、4x3マス(24m x 18m、高さ9m)の「棚の迷路の倉庫」を組み立てる。
/// メニュー: Tools / Room / 4x3の倉庫の部屋を作る
///
/// 3x3、5x5の部屋との違い:
///   横に長い部屋で、長い棚の列が2本あり、通路がジグザグになる(隠れる場所と、曲がり角が多い)。
///
/// 部屋の向き(部屋の左下が原点。X が右(24m)、Z が奥(18m)):
///   南(Z=0)   : 入口。x=6から12の間。動く扉(Assets/Prefab/KeyDoor_BayDoor.prefab)を、
///               鍵なしの自動ドア(前に立つと開いて、離れると閉まる)にして置く。
///               プレハブがなければ、普通の入口(Wall Door)にする
///   棚の列1   : z=6。西の壁から東へ並べる。東の端(x=19.4より先)が通れる
///   棚の列2   : z=12。東の壁から西へ並べる。西の端(x=4.5より手前)が通れる
///   北(Z=18)  : 扉なし(普通の壁)
///   北東の奥  : アイテム(パレットの上)。通り道のゴール
///   西(X=0)   : ヒューズボックス(中央の通路)
///   東(X=24)  : Wall Fan(中央の通路)
///   通り道    : 入口 → 南の通路を東へ → 東の端から中央の通路へ → 西へ → 西の端から奥の通路へ → 東へ → アイテム
///   電灯      : 7個。ホラー向けに、まともな電灯はなし。
///               暗め2、ちらつく2、壊れかけ(火花)2、切れている1(Assets/Prefab/BrokenLight.prefab を使う)
///
/// アイテムの置き方:
///   実行する前に、置きたいアイテム(ItemPickup が付いた物)を、Hierarchy か Project で選んでおく。
///     Project のプレハブを選んだとき : そのプレハブを置く
///     Hierarchy の物を選んだとき    : その物をコピーして置く(元の物は、そのまま残る)
///   何も選んでいないとき : 目印の「ItemSpot」だけを置く。あとでアイテムを、そこへ動かす。
///   アイテムは、部屋のプレハブの外(シーンの直下)に置く。シーンのUIへの参照が、切れないようにするため。
/// </summary>
public static class Room4x3Builder
{
    // ---------------- 寸法(キットの規格) ----------------

    private const float Module = 6f;            // 壁・床・天井の1枚の長さ
    private const int CountX = 4;               // 横のマス数
    private const int CountZ = 3;               // 奥行きのマス数
    private const float CeilingPivotY = 9f;     // 天井タイルの基準点の高さ(タイルはその下に付く)
    private const float CeilingUnderside = 8.8f;
    private const float WallInner = 0.17f;      // 壁の内側の面
    private const float RoomX = Module * CountX; // 24m
    private const float RoomZ = Module * CountZ; // 18m
    private const float ShelfGap = 0.1f;        // 棚と棚のすきま

    private const string KitFolderKeyword = "SciFi Warehouse Kit/Prefabs";
    private const string MovingDoorPrefabPath = "Assets/Prefab/KeyDoor_BayDoor.prefab";
    private const string BrokenLightPrefabPath = "Assets/Prefab/BrokenLight.prefab";
    private const string OutputFolder = "Assets/Prefab";
    private const string OutputPath = "Assets/Prefab/Room_4x3.prefab";

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
    private static Transform doorsRoot;           // 動く扉は Static にしない
    private static Vector3 itemSpot;            // アイテムを置く場所(部屋の中の座標。底の高さ)
    private static Transform propsRoot;
    private static Transform lightsRoot;

    // ---------------- メニュー ----------------

    [MenuItem("Tools/Room/4x3の倉庫の部屋を作る")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Room 4x3", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        // 選んでいるアイテムを、先に覚えておく(部屋を作ると、選択が変わるため)
        GameObject itemSource = Selection.activeGameObject;

        kitCache.Clear();
        missing.Clear();
        itemSpot = new Vector3(21f, 0f, 16.3f);

        GameObject root = new GameObject("Room_4x3");
        Undo.RegisterCreatedObjectUndo(root, "4x3の倉庫の部屋を作る");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        structureRoot = CreateGroup(root.transform, "Structure");
        doorsRoot = CreateGroup(root.transform, "Doors");
        propsRoot = CreateGroup(root.transform, "Props");
        lightsRoot = CreateGroup(root.transform, "Lights");

        BuildFloorAndCeiling();
        BuildWalls();
        BuildBeams();
        BuildShelfRows();
        BuildProps();
        BuildLights();

        SetStatic(structureRoot);
        SetStatic(propsRoot);
        SetLayerDefault(root);

        string savedPath = SavePrefab(root);
        MoveToSceneView(root);

        string itemMessage = PlaceItem(root, itemSource);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        string message = "4x3の倉庫の部屋を作りました。\n\n"
            + "シーン : Room_4x3 (Sceneビューの中心あたりに置きました)\n"
            + "Prefab : " + savedPath + "\n"
            + "アイテム : " + itemMessage;

        if (missing.Count > 0)
        {
            message += "\n\n見つからなかったPrefab(置かずに飛ばしました):\n - " + string.Join("\n - ", missing);
        }

        EditorUtility.DisplayDialog("Room 4x3", message, "OK");
    }

    // ---------------- 床と天井 ----------------

    private static void BuildFloorAndCeiling()
    {
        // 床は、見た目に変化が出るよう、4種類を決まった順番で並べる(3x3、5x5とは違う並び)
        string[] pattern = { "Floor Tile 01", "Floor Tile 02", "Floor Tile 01", "Floor Tile 01",
                             "Floor Tile 03", "Floor Tile 01", "Floor Tile 04", "Floor Tile 01",
                             "Floor Tile 01" };
        int index = 0;

        for (int ix = 0; ix < CountX; ix++)
        {
            for (int iz = 0; iz < CountZ; iz++)
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
        Spawn("Wall Corner", structureRoot, new Vector3(0f, 0f, RoomZ), 90f);
        Spawn("Wall Corner", structureRoot, new Vector3(RoomX, 0f, 0f), 270f);
        Spawn("Wall Corner", structureRoot, new Vector3(RoomX, 0f, RoomZ), 180f);

        GameObject movingDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MovingDoorPrefabPath);

        // 南(Z=0): -X方向へ伸びる。i=1(x=6から12)が入口
        for (int i = 1; i <= CountX - 2; i++)
        {
            float offset = i * Module;
            Vector3 pos = new Vector3(offset + Module, 0f, 0f);

            if (i == 1 && movingDoorPrefab != null)
            {
                GameObject door = SpawnPrefab(movingDoorPrefab, doorsRoot, pos, 270f);
                door.name = "EntranceDoor";
                SetupAutoDoor(door);
            }
            else
            {
                string southName = (i == 1) ? "Wall Door" : "Wall Plain";
                if (i == 1 && !missing.Contains(MovingDoorPrefabPath)) missing.Add(MovingDoorPrefabPath);
                Spawn(southName, structureRoot, pos, 270f);
            }
        }

        // 北(Z=18): +X方向へ伸びる。扉はなく、全部普通の壁
        for (int i = 1; i <= CountX - 2; i++)
        {
            float offset = i * Module;
            Spawn("Wall Plain", structureRoot, new Vector3(offset, 0f, RoomZ), 90f);
        }

        // 西(X=0): +Z方向へ伸びる。真ん中(z=6から12)は普通の壁(ヒューズボックスをつける)
        for (int i = 1; i <= CountZ - 2; i++)
        {
            float offset = i * Module;
            Spawn("Wall Plain", structureRoot, new Vector3(0f, 0f, offset), 0f);
        }

        // 東(X=24): -Z方向へ伸びる。真ん中(z=6から12)は Wall Fan
        for (int i = 1; i <= CountZ - 2; i++)
        {
            float offset = i * Module;
            Spawn("Wall Fan", structureRoot, new Vector3(RoomX, 0f, offset + Module), 180f);
        }
    }

    /// <summary>
    /// 動く扉(KeyDoor)を、鍵なしの自動ドアにする。プレハブ本体は変えず、この部屋の扉だけを変える。
    /// </summary>
    private static void SetupAutoDoor(GameObject door)
    {
        System.Type keyDoorType = System.Type.GetType("KeyDoor, Assembly-CSharp");
        if (keyDoorType == null) return;

        Component keyDoor = door.GetComponentInChildren(keyDoorType, true);
        if (keyDoor == null) return;

        SerializedObject so = new SerializedObject(keyDoor);
        SetBool(so, "startLocked", false);
        SetBool(so, "autoDoor", true);
        SetBool(so, "autoClose", true);
        SetFloat(so, "autoCloseDelay", 1.5f);
        SetBool(so, "enemiesCanOpen", true);
        so.ApplyModifiedPropertiesWithoutUndo();
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

    // ---------------- 梁 ----------------

    private static void BuildBeams()
    {
        // 中央の通路の上に、横切る梁を4本(電灯の真上を避けて、x=3,9,15,21)
        // 梁は、基準点から ローカル+X方向に6m。90度回すと、手前(-Z)へ伸びる。
        // z=12の基準点から、z=6まで伸びる。
        float beamY = 8.9f;
        float[] positions = { 3f, 9f, 15f, 21f };
        foreach (float x in positions)
        {
            Spawn("Ceiling Support", structureRoot, new Vector3(x, beamY, Module * 2f), 90f);
        }
    }

    // ---------------- 棚の列 ----------------

    private static void BuildShelfRows()
    {
        // 列1(z=6): 西の壁から東へ。前(開いている面)は、北(中央の通路)を向く
        string[] row1 = { "Shelf Variation 02", "Shelf Variation 04", "Shelves Empty" };
        PlaceShelfRow(row1, Module, 180f, WallInner, true);

        // 列2(z=12): 東の壁から西へ。前は、南(中央の通路)を向く
        string[] row2 = { "Shelf Variation 05", "Shelf Variation 03", "Shelf Variation 06" };
        PlaceShelfRow(row2, Module * 2f, 0f, RoomX - WallInner, false);
    }

    /// <summary>
    /// 棚を、実際の大きさ(Bounds)をもとに、すきまをあけて1列に並べる。
    /// eastward が true なら startX から東へ、false なら startX から西へ。
    /// </summary>
    private static void PlaceShelfRow(string[] names, float z, float yaw, float startX, bool eastward)
    {
        float cursor = startX;

        foreach (string shelfName in names)
        {
            Anchor ax = eastward ? Anchor.Min : Anchor.Max;
            GameObject shelf = PlaceOnFloor(shelfName, propsRoot, cursor, z, yaw, ax, Anchor.Center, 0f);

            Bounds b;
            if (shelf == null || !TryGetBounds(shelf, out b)) continue;

            cursor = eastward ? b.max.x + ShelfGap : b.min.x - ShelfGap;
        }
    }

    // ---------------- 小物 ----------------

    private static void BuildProps()
    {
        // ---- 壁につける物(基準点が壁の中心線に合うように作られている。壁と同じ向きにする) ----

        // 入口のまわり
        Spawn("Exit Sign", propsRoot, new Vector3(9f, 3f, 0f), 270f);
        Spawn("Wall Light", propsRoot, new Vector3(3f, 5.6f, 0f), 270f);
        Spawn("Wall Light", propsRoot, new Vector3(15f, 5.6f, 0f), 270f);
        Spawn("Wall Light", propsRoot, new Vector3(21f, 5.6f, 0f), 270f);

        // 西の壁(中央の通路): ヒューズボックスを2つ
        Spawn("Fusebox 01", propsRoot, new Vector3(0f, 2f, 8.2f), 0f);
        Spawn("Fusebox 02", propsRoot, new Vector3(0f, 3.2f, 9.8f), 0f);
        Spawn("Wall Light", propsRoot, new Vector3(0f, 5.6f, 9f), 0f);

        // 東の壁
        Spawn("Wall Light", propsRoot, new Vector3(RoomX, 5.6f, 3f), 180f);
        Spawn("Wall Light", propsRoot, new Vector3(RoomX, 5.6f, 15f), 180f);

        // 北の壁(奥の通路)
        Spawn("Wall Light", propsRoot, new Vector3(4f, 5.6f, RoomZ), 90f);
        Spawn("Wall Light", propsRoot, new Vector3(21f, 5.6f, RoomZ), 90f);

        // ---- 南の通路(入口の通路) ----

        // 入口の左(南西の角): 木箱を積む
        GameObject crateLong = PlaceOnFloor("Crate Long", propsRoot, 0.3f, 0.3f, 0f, Anchor.Min, Anchor.Min, 0f);
        PlaceOnFloor("Crate Short", propsRoot, 0.3f, 1.8f, 0f, Anchor.Min, Anchor.Min, 0f);
        StackOn(crateLong, "Crate Short", 12f);

        PlaceOnFloor("Garbage Bin", propsRoot, 4.5f, 0.4f, 0f, Anchor.Center, Anchor.Min, 0f);

        // 南の壁ぞいの棚(入口の東がわ)。前は、北(部屋の中)を向く
        PlaceOnFloor("Shelf Variation 02", propsRoot, 17.5f, WallInner, 180f, Anchor.Center, Anchor.Min, 0f);

        // 南東の角: ドラム缶
        PlaceOnFloor("Barrel", propsRoot, 23.0f, 1.0f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 22.1f, 0.9f, 40f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 23.0f, 2.0f, 80f, Anchor.Center, Anchor.Center, 0f);

        // ---- 東の端(南の通路と中央の通路をつなぐ、隙間のあたり) ----
        // 通り道は x=19.5から22くらいの幅をあける。隠れる場所として、パレットを壁ぞいに。
        PlaceOnFloor("Pallet Variation 01", propsRoot, 22.2f, 8.2f, 80f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Fire Extinguisher", propsRoot, 13.2f, WallInner, 0f, Anchor.Center, Anchor.Min, 1.0f);

        // ---- 中央の通路 ----

        PlaceOnFloor("Cart", propsRoot, 11f, 7.9f, 15f, Anchor.Center, Anchor.Center, 0f);

        // 西のつきあたり(ヒューズボックスの手前は、あけておく)
        GameObject pallet = PlaceOnFloor("Pallet", propsRoot, 1.6f, 7.5f, 90f, Anchor.Center, Anchor.Center, 0f);
        GameObject palletCrate = StackOn(pallet, "Crate Long", 20f);
        StackOn(palletCrate, "Crate Short", 70f);

        // ---- 奥の通路 ----

        // 北西の角: ドラム缶
        PlaceOnFloor("Barrel", propsRoot, 1.0f, 16.9f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 1.9f, 16.8f, 55f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 1.0f, 15.9f, 20f, Anchor.Center, Anchor.Center, 0f);

        // 北の壁ぞいに、荷物を積んだパレット(隠れる場所)
        PlaceOnFloor("Pallet Variation 07", propsRoot, 8.0f, 16.5f, 190f, Anchor.Center, Anchor.Center, 0f);

        // 北の壁のまんなか(前は扉があった場所)
        PlaceOnFloor("Fire Extinguisher", propsRoot, 15f, RoomZ - WallInner, 180f, Anchor.Center, Anchor.Max, 1.0f);

        // 北東の奥: アイテムを乗せる、低い空のパレット。アイテムは、この上に置く
        GameObject itemPallet = PlaceOnFloor("Pallet", propsRoot, itemSpot.x, itemSpot.z, 300f, Anchor.Center, Anchor.Center, 0f);
        Bounds itemPalletBounds;
        if (itemPallet != null && TryGetBounds(itemPallet, out itemPalletBounds))
        {
            itemSpot = new Vector3(itemPalletBounds.center.x, itemPalletBounds.max.y, itemPalletBounds.center.z);
        }

        // 北東の角: ドラム缶
        PlaceOnFloor("Barrel", propsRoot, 23.0f, 17.0f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 23.0f, 16.0f, 35f, Anchor.Center, Anchor.Center, 0f);

        // 奥の通路のカート
        PlaceOnFloor("Cart", propsRoot, 15f, 14.2f, 350f, Anchor.Center, Anchor.Center, 0f);
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
    // ホラーっぽく、電灯の状態をばらばらにする。
    //   Steady  : 暗めで、ほぼ普通に光る
    //   Flicker : ちらちらして、よく消える
    //   Broken  : 壊れかけ(BrokenLight.prefab。火花と音つき)。プレハブがなければ、強い Flicker
    //   Dead    : 光っていない。少し傾いてぶら下がっている
    // さらに、天井から落ちた電灯を1つ、床に転がしておく。

    private enum LightState
    {
        Steady,
        Flicker,
        Broken,
        Dead
    }

    private static void BuildLights()
    {
        // 入口すぐの電灯が壊れかけで、奥に行くほど暗くなる並び
        AddCeilingLight(6f, 2.7f, LightState.Broken, 0f);     // 入口の左: いきなり火花
        AddCeilingLight(18f, 2.7f, LightState.Steady, 0f);    // 手前の通路で、唯一まともな明かり
        AddCeilingLight(6f, 9f, LightState.Dead, 8f);         // 中央の通路の西: 消えている
        AddCeilingLight(12f, 9f, LightState.Broken, 0f);      // 真ん中: 壊れかけ
        AddCeilingLight(18f, 9f, LightState.Flicker, 0f);     // 中央の通路の東: ちらつく
        AddCeilingLight(6f, 15.3f, LightState.Dead, -11f);    // 奥の通路の西: 消えている(真っ暗な角)
        AddCeilingLight(18f, 15.3f, LightState.Flicker, 0f);  // アイテムのそば: ちらつく

        // 落ちた電灯(中央の通路の、棚の足もと)
        AddFallenLight(8.5f, 10.6f, 35f);
    }

    private static void AddCeilingLight(float x, float z, LightState state, float tilt)
    {
        if (state == LightState.Broken)
        {
            GameObject brokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenLightPrefabPath);
            if (brokenPrefab != null)
            {
                SpawnPrefab(brokenPrefab, lightsRoot, new Vector3(x, CeilingUnderside, z), Random.Range(0f, 360f));
                return;
            }

            if (!missing.Contains(BrokenLightPrefabPath)) missing.Add(BrokenLightPrefabPath);
        }

        GameObject fixture = Spawn("Hanging Light", propsRoot, new Vector3(x, CeilingUnderside, z), 0f);

        if (state == LightState.Dead)
        {
            // 光らない。少し傾けて、壊れている感じを出す(基準点が上なので、上を軸に傾く)
            if (fixture != null)
            {
                fixture.name = "Hanging Light (Dead)";
                fixture.transform.localRotation = Quaternion.Euler(tilt, 20f, tilt * 0.5f);
            }
            return;
        }

        // 電球のあたりの高さに、Lightを置く
        float lightY = 7.8f;
        Bounds fixtureBounds;
        if (fixture != null && TryGetBounds(fixture, out fixtureBounds))
        {
            lightY = fixtureBounds.min.y + 0.3f;
        }

        GameObject lightObject = new GameObject("RoomLight_" + state);
        lightObject.transform.SetParent(lightsRoot, false);
        lightObject.transform.localPosition = new Vector3(x, lightY, z);

        Light roomLight = lightObject.AddComponent<Light>();
        roomLight.type = LightType.Point;
        roomLight.shadows = LightShadows.Soft;

        System.Type flickerType = System.Type.GetType("RoomLightFlicker, Assembly-CSharp");

        if (state == LightState.Steady)
        {
            roomLight.color = new Color(0.85f, 0.92f, 1f);   // 少し青白い蛍光灯
            roomLight.intensity = 2.2f;
            roomLight.range = 12f;

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
            // Flicker(と、BrokenLight.prefab がないときの Broken)
            bool strong = state == LightState.Broken;

            roomLight.color = new Color(0.8f, 0.88f, 0.95f);
            roomLight.intensity = strong ? 2.6f : 2.0f;
            roomLight.range = 11f;

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

        Vector3 world = fixture.transform.parent.TransformPoint(new Vector3(x, 0f, z));
        Vector3 delta = new Vector3(world.x - b.center.x, world.y - b.min.y, world.z - b.center.z);
        fixture.transform.position += delta;
    }

    private static void SetVector2(SerializedObject so, string field, Vector2 value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop != null) prop.vector2Value = value;
    }

    // ---------------- アイテム ----------------

    /// <summary>
    /// 選んでいたアイテムを、パレットの上に置く。部屋のプレハブの外(シーンの直下)に置く。
    /// 何も選んでいなければ、目印の ItemSpot を置く。
    /// </summary>
    private static string PlaceItem(GameObject room, GameObject source)
    {
        Vector3 worldSpot = room.transform.TransformPoint(itemSpot);

        bool usable = source != null
            && source != room
            && !source.transform.IsChildOf(room.transform)
            && HasItemPickup(source);

        if (!usable)
        {
            GameObject marker = new GameObject("Room_4x3_ItemSpot");
            Undo.RegisterCreatedObjectUndo(marker, "4x3の倉庫の部屋を作る");
            MoveToRoomScene(marker, room);
            marker.transform.position = worldSpot;
            return "選ばれていなかったので、目印の Room_4x3_ItemSpot を置きました。アイテムをそこへ動かしてください。";
        }

        GameObject item;
        if (EditorUtility.IsPersistent(source))
        {
            // Project のプレハブ
            item = (GameObject)PrefabUtility.InstantiatePrefab(source);
        }
        else
        {
            // Hierarchy の物をコピーする(プレハブのつながりも残す)
            GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(source);
            if (sourcePrefab != null && PrefabUtility.IsOutermostPrefabInstanceRoot(source))
            {
                item = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
                PrefabUtility.SetPropertyModifications(item, PrefabUtility.GetPropertyModifications(source));
            }
            else
            {
                item = Object.Instantiate(source);
            }
            item.name = source.name;
            item.tag = source.tag;
        }

        Undo.RegisterCreatedObjectUndo(item, "4x3の倉庫の部屋を作る");
        MoveToRoomScene(item, room);

        // 見た目の底を、パレットの上にそろえる(向きは、元のまま)
        item.transform.position = worldSpot;
        Bounds b;
        if (TryGetBounds(item, out b))
        {
            Vector3 delta = new Vector3(worldSpot.x - b.center.x, worldSpot.y - b.min.y, worldSpot.z - b.center.z);
            item.transform.position += delta;
        }

        return "「" + item.name + "」をパレットの上に置きました。";
    }

    private static bool HasItemPickup(GameObject target)
    {
        System.Type pickupType = System.Type.GetType("ItemPickup, Assembly-CSharp");
        if (pickupType == null) return false;
        return target.GetComponentInChildren(pickupType, true) != null;
    }

    private static void MoveToRoomScene(GameObject target, GameObject room)
    {
        if (target.scene != room.scene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target, room.scene);
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