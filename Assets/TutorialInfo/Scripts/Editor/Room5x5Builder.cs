#if UNITY_EDITOR
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
///   電灯           : 8個。壊れかけ(火花)2、ちらつく2、暗めの普通2、消えている2。床に落ちた電灯が1つ。
///                    ヒューズボックスの上に、赤い非常灯
///
/// ホラーの飾り:
///   散らかった物 : 倒れた棚、横倒しのドラム缶とカート、転がった木箱、ゴミ箱、消火器
///   血しぶき     : 床に4か所、壁に5か所。中央から鍵付きの扉まで、引きずった血の跡
///                  (画像ではなく、立体のメッシュ。Assets/Model/Horror/Blood に保存する)
///   霧           : 部屋ではなく、シーン全体に出す。Tools / Horror / 霧をシーンに入れる
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
    private static Transform bloodRoot;

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
        bloodRoot = CreateGroup(root.transform, "Blood");

        BuildFloorAndCeiling();
        BuildWalls();
        BuildColumns();
        BuildProps();
        BuildLights();
        bool bloodPlaced = BuildBlood();

        SetStatic(structureRoot);
        SetStatic(propsRoot);
        SetStatic(bloodRoot);
        SetLayerDefault(root);

        string savedPath = SavePrefab(root);
        MoveToSceneView(root);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        string message = "5x5の部屋を作りました。\n\n"
            + "シーン : Room_5x5 (Sceneビューの中心あたりに置きました)\n"
            + "Prefab : " + savedPath;

        if (!bloodPlaced)
        {
            message += "\n\n血しぶきを置けませんでした。";
        }

        if (HorrorDecor.FindFogInScene() == null)
        {
            message += "\n\n霧は、Tools / Horror / 霧をシーンに入れる で出せます。";
        }

        if (missing.Count > 0)
        {
            message += "\n\n見つからなかったPrefab(置かずに飛ばしました):\n - " + string.Join("\n - ", missing);
        }

        EditorUtility.DisplayDialog("Room 5x5", message, "OK");
    }

    /// <summary>
    /// すでにシーンにある Room_5x5 に、血しぶきだけを付け直す(部屋は作り直さない)。
    /// 前の画像の血を、立体の血に置きかえるときにも使う。
    /// </summary>
    [MenuItem("Tools/Room/5x5の部屋に血しぶきを付け直す")]
    public static void RebuildBloodOnly()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Room 5x5", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        GameObject room = Selection.activeGameObject;
        if (room != null && room.name != "Room_5x5")
        {
            Transform up = room.transform;
            while (up != null && up.name != "Room_5x5") up = up.parent;
            room = (up != null) ? up.gameObject : null;
        }
        if (room == null) room = GameObject.Find("Room_5x5");

        if (room == null)
        {
            EditorUtility.DisplayDialog("Room 5x5", "シーンに Room_5x5 が見つかりません。", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(room, "5x5の部屋に血しぶきを付け直す");

        Transform blood = room.transform.Find("Blood");
        if (blood == null)
        {
            GameObject group = new GameObject("Blood");
            Undo.RegisterCreatedObjectUndo(group, "5x5の部屋に血しぶきを付け直す");
            group.transform.SetParent(room.transform, false);
            blood = group.transform;
        }

        // 前の血を消す
        for (int i = blood.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(blood.GetChild(i).gameObject);
        }

        bloodRoot = blood;
        BuildBlood();

        foreach (Transform t in blood.GetComponentsInChildren<Transform>(true))
        {
            if (t != blood) Undo.RegisterCreatedObjectUndo(t.gameObject, "5x5の部屋に血しぶきを付け直す");
        }

        SetStatic(blood);
        SetLayerDefault(blood.gameObject);

        // 部屋のプレハブにも、反映する
        if (PrefabUtility.IsOutermostPrefabInstanceRoot(room))
        {
            PrefabUtility.ApplyPrefabInstance(room, InteractionMode.UserAction);
        }

        Selection.activeGameObject = blood.gameObject;
        EditorSceneManager.MarkSceneDirty(room.scene);

        EditorUtility.DisplayDialog("Room 5x5",
            "血しぶきを " + blood.childCount + " 個、付けました(床と壁)。", "OK");
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

    // ---------------- 小物(散らかっている) ----------------

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

        // ---- 棚 ----
        // 東の壁ぞい(真ん中のFanは、見えるようにあける)
        PlaceOnFloor("Shelf Variation 02", propsRoot, RoomSize - WallInner, 4.0f, 90f, Anchor.Max, Anchor.Center, 0f);
        PlaceOnFloor("Shelf Variation 04", propsRoot, RoomSize - WallInner, 24.0f, 90f, Anchor.Max, Anchor.Center, 0f);

        // 西の壁ぞい。南は立っていて、北は部屋の内側へ倒れている
        PlaceOnFloor("Shelf Variation 03", propsRoot, WallInner, 4.5f, 270f, Anchor.Min, Anchor.Center, 0f);
        Quaternion fallen = Quaternion.AngleAxis(-84f, Vector3.forward) * Quaternion.Euler(0f, 270f, 0f);
        GameObject fallenShelf = PlaceRotated("Shelf Variation 06", 0.3f, 25.0f, fallen, Anchor.Min, Anchor.Center);
        if (fallenShelf != null) fallenShelf.name = "Shelf Variation 06 (Fallen)";

        // 北の壁ぞい(ヒューズボックスの両わき)
        PlaceOnFloor("Shelf Variation 05", propsRoot, 7.5f, RoomSize - WallInner, 0f, Anchor.Center, Anchor.Max, 0f);
        PlaceOnFloor("Shelves Empty", propsRoot, 23.5f, RoomSize - WallInner, 0f, Anchor.Center, Anchor.Max, 0f);

        // ---- 南西の角: 木箱。上の1つが落ちて転がっている ----
        GameObject crateLong = PlaceOnFloor("Crate Long", propsRoot, 0.3f, 0.3f, 0f, Anchor.Min, Anchor.Min, 0f);
        PlaceOnFloor("Crate Short", propsRoot, 0.3f, 1.8f, 0f, Anchor.Min, Anchor.Min, 0f);
        StackOn(crateLong, "Crate Short", 12f);
        PlaceRotated("Crate Short", 3.8f, 3.2f, Tipped(25f, 90f), Anchor.Center, Anchor.Center);

        // 横倒しのカート
        PlaceRotated("Cart", 5.5f, 6.8f, Tipped(330f, 90f), Anchor.Center, Anchor.Center);

        // ---- 入口のそば ----
        PlaceRotated("Garbage Bin", 10.4f, 1.9f, Tipped(65f, 90f), Anchor.Center, Anchor.Center);
        PlaceRotated("Fire Extinguisher", 19.8f, 2.4f, Tipped(70f, 90f), Anchor.Center, Anchor.Center);

        // ---- 北東の角: ドラム缶。2つは倒れて、転がっている ----
        PlaceOnFloor("Barrel", propsRoot, 28.9f, 28.9f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 27.7f, 29.0f, 35f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 25.8f, 26.6f, Tipped(30f, 90f), Anchor.Center, Anchor.Center);
        PlaceRotated("Barrel", 24.2f, 24.6f, Tipped(115f, 90f), Anchor.Center, Anchor.Center);

        // ---- 北西の角: ドラム缶(倒れた棚のむこう) ----
        PlaceOnFloor("Barrel", propsRoot, 1.0f, 29.2f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 6.6f, 27.4f, Tipped(80f, 90f), Anchor.Center, Anchor.Center);

        // ---- 荷物を積んだパレット(隠れる場所)。通り道は、あけておく ----
        PlaceOnFloor("Pallet Variation 01", propsRoot, 7f, 9f, 10f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 05", propsRoot, 23f, 9f, 80f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 03", propsRoot, 8.5f, 21f, 190f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 07", propsRoot, 22f, 21f, 300f, Anchor.Center, Anchor.Center, 0f);

        // パレットと木箱
        GameObject pallet = PlaceOnFloor("Pallet", propsRoot, 4.5f, 19.0f, 20f, Anchor.Center, Anchor.Center, 0f);
        StackOn(pallet, "Crate Long", 110f);

        // ずれたカートと、ばらばらの物
        PlaceOnFloor("Cart", propsRoot, 26.5f, 19.5f, 140f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Crate Short", 9.6f, 11.2f, Tipped(40f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Crate Short", propsRoot, 20.5f, 25.5f, 15f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet", propsRoot, 10.5f, 26.0f, 37f, Anchor.Center, Anchor.Center, 0f);
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
        AddCeilingLight(7.5f, 7.5f, LightState.Flicker, 0f);     // 入口の左: ちらつく
        AddCeilingLight(15f, 7.5f, LightState.Steady, 0f);       // 入口の先: 暗めの普通
        AddCeilingLight(22.5f, 7.5f, LightState.Dead, 9f);       // 入口の右: 消えている
        AddCeilingLight(7.5f, 15f, LightState.Broken, 0f);       // 鍵付きの扉の前: 壊れかけ(血の跡を照らす)
        AddCeilingLight(22.5f, 15f, LightState.Flicker, 0f);     // Fanの前: ちらつく
        AddCeilingLight(7.5f, 22.5f, LightState.Dead, -12f);     // 倒れた棚の上: 消えている(真っ暗)
        AddCeilingLight(15f, 22.5f, LightState.Broken, 0f);      // ヒューズボックスの前: 壊れかけ
        AddCeilingLight(22.5f, 22.5f, LightState.Steady, 0f);    // 北東: 暗めの普通

        // 落ちた電灯
        AddFallenLight(20.5f, 11.8f, 60f);

        // ヒューズボックスの上の、赤い非常灯(ゆらぎなし)
        GameObject warning = new GameObject("EmergencyLight_Red");
        warning.transform.SetParent(lightsRoot, false);
        warning.transform.localPosition = new Vector3(15f, 6.2f, RoomSize - 1.0f);

        Light warningLight = warning.AddComponent<Light>();
        warningLight.type = LightType.Point;
        warningLight.color = new Color(1f, 0.15f, 0.1f);
        warningLight.intensity = 1.6f;
        warningLight.range = 8f;
        warningLight.shadows = LightShadows.None;
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
            roomLight.color = new Color(1f, 0.9f, 0.72f);    // 古い電球の、にごった黄色
            roomLight.intensity = 2.2f;
            roomLight.range = 14f;

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

            roomLight.color = new Color(0.95f, 0.88f, 0.75f);
            roomLight.intensity = strong ? 2.6f : 2.0f;
            roomLight.range = 13f;

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

    // ---------------- 血しぶき ----------------

    /// <summary>
    /// 床と壁に、立体の血しぶきを置く。
    /// </summary>
    private static bool BuildBlood()
    {
        // ---- 床 ----
        // 中央の柱のそばの血だまりと、そこから鍵付きの扉(西)まで、引きずった跡
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_01", 13.0f, 16.0f, 0f, 20f, 3.2f, 3.2f);
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Smear_01", 7.2f, 15.2f, 0.006f, 180f, 10.5f, 2.4f);

        // 倒れた棚の下の、大きな血だまり
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_03", 5.0f, 24.2f, 0f, 75f, 3.6f, 3.6f);

        // 入口の先
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_02", 17.2f, 4.6f, 0f, 40f, 1.8f, 1.8f);

        // 倒れたドラム缶のそば
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_02", 25.2f, 22.8f, 0f, 200f, 2.3f, 2.3f);

        // ---- 壁 ----
        float north = RoomSize - WallInner;
        float east = RoomSize - WallInner;

        // ヒューズボックスの左の、したたる血
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Drip_01", new Vector3(11.7f, 3.0f, north), Vector3.back, 2.2f, 2.2f, 0f);

        // 鍵付きの扉のわきの、血の手形と、したたる血
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Hand_01", new Vector3(WallInner, 1.6f, 11.2f), Vector3.right, 0.9f, 0.9f, 8f);
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Drip_02", new Vector3(WallInner, 2.6f, 19.3f), Vector3.right, 2.0f, 2.0f, 0f);

        // 東の壁の、飛び散った血
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Splat_01", new Vector3(east, 2.8f, 9.5f), Vector3.left, 2.4f, 2.4f, 30f);

        // 入口の右の、血の手形
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Hand_01", new Vector3(19.4f, 1.5f, WallInner), Vector3.forward, 0.9f, 0.9f, -12f);

        return bloodRoot.childCount > 0;
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
#endif