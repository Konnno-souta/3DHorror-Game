using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SciFi Warehouse Kit で、6x3マス(36m x 18m、高さ9m)の「柱の並ぶ、細長い搬入ホール」を組み立てる。
/// メニュー: Tools / Room / 6x3のホールの部屋を作る
///
/// ほかの部屋との違い:
///   東西に長い部屋。柱が2列(z=6 と z=12)に5本ずつ並び、真ん中に、入口から出口までの広い通路がある。
///   通路の南と北は、棚や荷物が置かれた物置き(柱のかげに、隠れる場所が多い)。
///
/// 部屋の向き(部屋の左下が原点。X が右(36m)、Z が奥(18m)):
///   西(X=0)   : 扉なし。窓(Wall Window)
///   東(X=36)  : 扉なし。ふさがれた扉(Wall DoorClosed。開かない飾り)
///   南(Z=0)   : 右から2マス目(x=24から30)に、動く扉(KeyDoor_BayDoor.prefab)。鍵がかかっている
///               (Tools / Key / 3 で、カードキーの色や、自動ドアにするかを決められる)
///               x=12から18 に Wall Fan
///   北(Z=18)  : ヒューズボックス
///   通り道    : 南の扉から北へ入り、真ん中の通路へ。通路には荷物が散らばり、ジグザグに進む
///   壁と天井の飾り: 壁の継ぎ目の柱、床ぎわと天井ぎわの配管、天井のダクト、スプリンクラー、
///               監視カメラ、柱の上で格子になった梁
///   電灯      : 12個。壊れかけ(火花)2、ちらつく3、暗めの普通3、消えている4。床に落ちた電灯が1つ。
///               扉の上に、赤い非常灯
///   ホラーの飾り: 倒れた棚、横倒しのドラム缶とカート、転がった木箱。
///               立体の血しぶき(通路の血だまりから、扉へ引きずった跡など)
/// </summary>
public static class Room6x3Builder
{
    // ---------------- 寸法(キットの規格) ----------------

    private const float Module = 6f;
    private const int CountX = 6;
    private const int CountZ = 3;
    private const float CeilingPivotY = 9f;
    private const float CeilingUnderside = 8.8f;
    private const float WallInner = 0.17f;
    private const float RoomX = Module * CountX; // 36m
    private const float RoomZ = Module * CountZ; // 18m
    private const int DoorModule = 4;            // 南の壁の、扉を置くマス(x=24から30。右から2マス目)
    private const float DoorCenterX = DoorModule * Module + Module * 0.5f; // 27m

    private const string KitFolderKeyword = "SciFi Warehouse Kit/Prefabs";
    private const string MovingDoorPrefabPath = "Assets/Prefab/KeyDoor_BayDoor.prefab";
    private const string BrokenLightPrefabPath = "Assets/Prefab/BrokenLight.prefab";
    private const string OutputFolder = "Assets/Prefab";
    private const string OutputPath = "Assets/Prefab/Room_6x3.prefab";

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

    [MenuItem("Tools/Room/6x3のホールの部屋を作る")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Room 6x3", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        kitCache.Clear();
        missing.Clear();

        GameObject root = new GameObject("Room_6x3");
        Undo.RegisterCreatedObjectUndo(root, "6x3のホールの部屋を作る");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        structureRoot = CreateGroup(root.transform, "Structure");
        doorsRoot = CreateGroup(root.transform, "Doors");
        propsRoot = CreateGroup(root.transform, "Props");
        lightsRoot = CreateGroup(root.transform, "Lights");
        bloodRoot = CreateGroup(root.transform, "Blood");

        BuildFloorAndCeiling();
        BuildWalls();
        BuildPillarsAndBeams();
        BuildWallAndCeilingDecor();
        BuildProps();
        BuildLights();
        BuildBlood();

        SetStatic(structureRoot);
        SetStatic(propsRoot);
        SetStatic(bloodRoot);
        SetLayerDefault(root);

        string savedPath = SavePrefab(root);
        MoveToSceneView(root);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        string message = "6x3のホールの部屋を作りました。\n\n"
            + "シーン : Room_6x3 (Sceneビューの中心あたりに置きました)\n"
            + "Prefab : " + savedPath + "\n\n"
            + "南の扉(HallDoor)は、鍵がかかっています。扉を選んで、\n"
            + "Tools / Key / 3. 選んだ扉をカードキーの扉にする で、色を決めてください。\n"
            + "物を置いたので、NavMesh を焼き直してください。";

        if (missing.Count > 0)
        {
            message += "\n\n見つからなかったPrefab(置かずに飛ばしました):\n - " + string.Join("\n - ", missing);
        }

        EditorUtility.DisplayDialog("Room 6x3", message, "OK");
    }

    // ---------------- 床と天井 ----------------

    private static void BuildFloorAndCeiling()
    {
        string[] pattern = { "Floor Tile 01", "Floor Tile 01", "Floor Tile 03", "Floor Tile 01",
                             "Floor Tile 02", "Floor Tile 01", "Floor Tile 01", "Floor Tile 04" };
        int index = 0;

        for (int ix = 0; ix < CountX; ix++)
        {
            for (int iz = 0; iz < CountZ; iz++)
            {
                Spawn(pattern[index % pattern.Length], structureRoot, new Vector3(ix * Module, 0f, iz * Module), 0f);
                Spawn("Ceiling Closed", structureRoot, new Vector3(ix * Module, CeilingPivotY, iz * Module), 0f);
                index++;
            }
        }
    }

    // ---------------- 壁 ----------------
    // 壁は、基準点から ローカル+Z方向 に6m伸びる。ローカル+X側が部屋の内側。

    private static void BuildWalls()
    {
        Spawn("Wall Corner", structureRoot, new Vector3(0f, 0f, 0f), 0f);
        Spawn("Wall Corner", structureRoot, new Vector3(0f, 0f, RoomZ), 90f);
        Spawn("Wall Corner", structureRoot, new Vector3(RoomX, 0f, 0f), 270f);
        Spawn("Wall Corner", structureRoot, new Vector3(RoomX, 0f, RoomZ), 180f);

        GameObject movingDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MovingDoorPrefabPath);
        if (movingDoorPrefab == null && !missing.Contains(MovingDoorPrefabPath)) missing.Add(MovingDoorPrefabPath);

        // 南(Z=0): -X方向へ伸びる。i=2(x=12から18)が Wall Fan、i=4(x=24から30)が扉
        for (int i = 1; i <= CountX - 2; i++)
        {
            float offset = i * Module;
            Vector3 pos = new Vector3(offset + Module, 0f, 0f);

            if (i == DoorModule)
            {
                if (movingDoorPrefab != null)
                {
                    GameObject door = SpawnPrefab(movingDoorPrefab, doorsRoot, pos, 270f);
                    door.name = "HallDoor";
                    SetupLockedDoor(door);
                }
                else
                {
                    Spawn("Wall Door", structureRoot, pos, 270f);
                }
                continue;
            }

            Spawn(i == 2 ? "Wall Fan" : "Wall Plain", structureRoot, pos, 270f);
        }

        // 北(Z=18): +X方向へ伸びる
        for (int i = 1; i <= CountX - 2; i++)
        {
            Spawn("Wall Plain", structureRoot, new Vector3(i * Module, 0f, RoomZ), 90f);
        }

        // 西(X=0)と東(X=36)の真ん中: 扉なし。西は窓、東は、ふさがれた扉(開かない飾り)
        Spawn("Wall Window", structureRoot, new Vector3(0f, 0f, Module), 0f);
        Spawn("Wall DoorClosed", structureRoot, new Vector3(RoomX, 0f, Module * 2f), 180f);
    }

    /// <summary>
    /// 出口の扉を、鍵のかかった扉にする(自動ドアにはしない)。
    /// </summary>
    private static void SetupLockedDoor(GameObject door)
    {
        System.Type keyDoorType = System.Type.GetType("KeyDoor, Assembly-CSharp");
        if (keyDoorType == null) return;

        Component keyDoor = door.GetComponentInChildren(keyDoorType, true);
        if (keyDoor == null) return;

        SerializedObject so = new SerializedObject(keyDoor);
        SetBool(so, "startLocked", true);
        SetBool(so, "startOpen", false);
        SetBool(so, "autoDoor", false);
        SetBool(so, "enemiesOpenLockedDoors", false);
        so.ApplyModifiedPropertiesWithoutUndo();
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

    // ---------------- 柱と梁 ----------------

    private static void BuildPillarsAndBeams()
    {
        float[] rows = { Module, Module * 2f };          // z=6 と z=12
        float beamY = 8.9f;

        foreach (float z in rows)
        {
            for (int i = 1; i <= CountX - 1; i++)
            {
                Spawn("Pillar", structureRoot, new Vector3(i * Module, 0f, z), 0f);
            }

            // 柱と柱のあいだの梁(+X方向に6m)
            for (int i = 1; i <= CountX - 2; i++)
            {
                Spawn("Ceiling Support", structureRoot, new Vector3(i * Module, beamY, z), 0f);
            }
        }

        // 横切る梁(90度回すと -Z 方向へ6m)。柱の上で、格子になる
        for (int i = 1; i <= CountX - 1; i++)
        {
            float x = i * Module;
            Spawn("Ceiling Support", structureRoot, new Vector3(x, beamY, Module), 90f);        // 南の壁から柱まで
            Spawn("Ceiling Support", structureRoot, new Vector3(x, beamY, Module * 2f), 90f);   // 柱から柱まで
            Spawn("Ceiling Support", structureRoot, new Vector3(x, beamY, RoomZ), 90f);         // 柱から北の壁まで
        }
    }

    // ---------------- 壁と天井の飾り ----------------

    private enum Wall
    {
        South,
        North,
        West,
        East
    }

    private static void BuildWallAndCeilingDecor()
    {
        // ---- 壁の継ぎ目の柱(Wall Support) ----
        for (int i = 1; i <= CountX - 1; i++)
        {
            float x = i * Module;
            Spawn("Wall Support", structureRoot, new Vector3(x, 0f, 0f), 270f);
            Spawn("Wall Support", structureRoot, new Vector3(x, 0f, RoomZ), 90f);
        }
        for (int i = 1; i <= CountZ - 1; i++)
        {
            float z = i * Module;
            Spawn("Wall Support", structureRoot, new Vector3(0f, 0f, z), 0f);
            Spawn("Wall Support", structureRoot, new Vector3(RoomX, 0f, z), 180f);
        }

        // ---- 床ぎわの配管 ----
        PlaceWallDecor("Pipes 02", Wall.North, 17.0f, 0f, structureRoot);
        PlaceWallDecor("Pipes 05", Wall.South, 14.5f, 0f, structureRoot);
        PlaceWallDecor("Pipes 02", Wall.West, 9.0f, 0f, structureRoot);
        PlaceWallDecor("Pipes 05", Wall.East, 14.8f, 0f, structureRoot);

        // ---- 天井ぎわの配管(上の端を、天井の下にそろえる) ----
        PlaceWallDecor("Pipes 03", Wall.South, 9.0f, CeilingPivotY, structureRoot);
        PlaceWallDecor("Pipes 06", Wall.South, 33.0f, CeilingPivotY, structureRoot);
        PlaceWallDecor("Pipes 04", Wall.North, 16.5f, CeilingPivotY, structureRoot);
        PlaceWallDecor("Pipes 03", Wall.North, 27.0f, CeilingPivotY, structureRoot);
        PlaceWallDecor("Pipes 06", Wall.West, 15.0f, CeilingPivotY, structureRoot);
        PlaceWallDecor("Pipes 03", Wall.East, 3.0f, CeilingPivotY, structureRoot);

        // ---- 天井のダクト(物置きの上を、東西に走る) ----
        ChainDuctAlongX(1.5f, 22.5f, 16.3f, 7.9f);
        ChainDuctAlongX(4.0f, 22.0f, 1.7f, 7.9f);

        // ---- スプリンクラー(天井に付ける) ----
        float[] aisleX = { 6f, 12f, 18f, 24f, 30f };
        foreach (float x in aisleX)
        {
            PlaceOnCeiling("Sprinkler 01", x - 1.5f, 9f);
        }
        PlaceOnCeiling("Sprinkler 03", 4.5f, 3f);
        PlaceOnCeiling("Sprinkler 01", 16.5f, 3f);
        PlaceOnCeiling("Sprinkler 03", 28.5f, 3f);
        PlaceOnCeiling("Sprinkler 01", 10.5f, 15f);
        PlaceOnCeiling("Sprinkler 03", 22.5f, 15f);
        PlaceOnCeiling("Sprinkler 01", 34.5f, 15f);

        // ---- 監視カメラ(高い所から、扉と通路を見下ろす) ----
        PlaceWallDecor("Security Cam", Wall.South, DoorModule * Module - 1.6f, 5.8f, propsRoot);
        PlaceWallDecor("Security Cam", Wall.North, 2.0f, 6.0f, propsRoot);
        PlaceWallDecor("Security Cam", Wall.East, 16.6f, 6.0f, propsRoot);
    }

    /// <summary>
    /// 壁につける飾りを置く。基準点は壁の線の上で、向きは壁と同じ(キットのデモと同じ置き方)。
    /// 見た目の真ん中を along の位置にそろえ、天井より上や床より下に出たら、中へ動かす。
    /// 部屋の外側に出てしまったら、裏返す。
    /// </summary>
    private static GameObject PlaceWallDecor(string kitName, Wall wall, float along, float y, Transform parent)
    {
        Vector3 pos;
        float yaw;
        switch (wall)
        {
            case Wall.South: pos = new Vector3(along, y, 0f); yaw = 270f; break;
            case Wall.North: pos = new Vector3(along, y, RoomZ); yaw = 90f; break;
            case Wall.West: pos = new Vector3(0f, y, along); yaw = 0f; break;
            default: pos = new Vector3(RoomX, y, along); yaw = 180f; break;
        }

        GameObject instance = Spawn(kitName, parent, pos, yaw);
        if (instance == null) return null;

        Bounds b;
        if (!TryGetBounds(instance, out b)) return instance;

        bool outside = (wall == Wall.South && b.center.z < 0f) || (wall == Wall.North && b.center.z > RoomZ)
            || (wall == Wall.West && b.center.x < 0f) || (wall == Wall.East && b.center.x > RoomX);
        if (outside)
        {
            instance.transform.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            TryGetBounds(instance, out b);
        }

        Vector3 delta = Vector3.zero;
        if (wall == Wall.South || wall == Wall.North) delta.x = along - b.center.x;
        else delta.z = along - b.center.z;

        if (b.max.y > CeilingUnderside - 0.05f) delta.y = (CeilingUnderside - 0.05f) - b.max.y;
        if (b.min.y + delta.y < 0f) delta.y = -b.min.y;

        instance.transform.position += delta;
        return instance;
    }

    /// <summary>
    /// 天井の下に付ける。上の端を、天井の下面にそろえる。
    /// </summary>
    private static GameObject PlaceOnCeiling(string kitName, float x, float z)
    {
        GameObject instance = Spawn(kitName, structureRoot, new Vector3(x, CeilingUnderside, z), 0f);
        if (instance == null) return null;

        Bounds b;
        if (!TryGetBounds(instance, out b)) return instance;

        instance.transform.position += new Vector3(x - b.center.x, CeilingUnderside - b.max.y, z - b.center.z);
        return instance;
    }

    /// <summary>
    /// ダクトを、東西(X方向)に、つなげて並べる。1本の長さは、実際の大きさを測って決める。
    /// 最後に、吹き出し口(Duct Vent)を付ける。topY は、ダクトの上の端の高さ。
    /// </summary>
    private static void ChainDuctAlongX(float startX, float endX, float z, float topY)
    {
        float cursor = startX;
        int guard = 0;

        while (cursor < endX && guard < 20)
        {
            guard++;
            GameObject duct = PlaceAlongX("Duct Straight", cursor, z, topY);
            if (duct == null) return;

            Bounds b;
            if (!TryGetBounds(duct, out b) || b.size.x < 0.2f) return;
            cursor = b.max.x - 0.01f;
        }

        PlaceAlongX("Duct Vent", cursor, z, topY);
    }

    /// <summary>
    /// 長い向きが X になるように回して、西の端を x に、上の端を topY にそろえて置く。
    /// </summary>
    private static GameObject PlaceAlongX(string kitName, float x, float z, float topY)
    {
        GameObject instance = Spawn(kitName, structureRoot, Vector3.zero, 90f);
        if (instance == null) return null;

        Bounds b;
        if (!TryGetBounds(instance, out b)) return instance;

        if (b.size.x < b.size.z)
        {
            instance.transform.localRotation = Quaternion.identity;
            TryGetBounds(instance, out b);
        }

        instance.transform.position += new Vector3(x - b.min.x, topY - b.max.y, z - b.center.z);
        return instance;
    }

    // ---------------- 小物(散らかっている) ----------------

    private static void BuildProps()
    {
        // ---- 壁につける物 ----
        for (int i = 0; i < CountX; i++)
        {
            float x = i * Module + 3f;
            if (i != DoorModule)   // 扉の上には付けない
            {
                Spawn("Wall Light", propsRoot, new Vector3(x, 5.6f, 0f), 270f);
            }
            Spawn("Wall Light", propsRoot, new Vector3(x, 5.6f, RoomZ), 90f);
        }

        Spawn("Exit Sign", propsRoot, new Vector3(DoorModule * Module - 1.2f, 3f, 0f), 270f);
        Spawn("Fusebox 01", propsRoot, new Vector3(19.5f, 2f, RoomZ), 90f);
        Spawn("Fusebox 02", propsRoot, new Vector3(22.5f, 3.2f, RoomZ), 90f);

        // ---- 南の物置き(z=0から6) ----
        PlaceOnFloor("Shelf Variation 02", propsRoot, 8.6f, WallInner, 180f, Anchor.Center, Anchor.Min, 0f);
        PlaceOnFloor("Shelf Variation 03", propsRoot, 19.0f, WallInner, 180f, Anchor.Center, Anchor.Min, 0f);

        // 南東: 東の壁から、部屋の内側(西)へ倒れた棚。南の扉の前は、あけておく
        Quaternion fallen = Quaternion.AngleAxis(84f, Vector3.forward) * Quaternion.Euler(0f, 90f, 0f);
        GameObject fallenShelf = PlaceRotated("Shelf Variation 04", RoomX - 0.4f, 3.6f, fallen, Anchor.Max, Anchor.Center);
        if (fallenShelf != null) fallenShelf.name = "Shelf Variation 04 (Fallen)";

        PlaceOnFloor("Pallet Variation 01", propsRoot, 9.5f, 3.0f, 15f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 05", propsRoot, 20.0f, 3.4f, 80f, Anchor.Center, Anchor.Center, 0f);

        // 南西の角: ドラム缶
        PlaceOnFloor("Barrel", propsRoot, 1.0f, 1.0f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 1.9f, 0.9f, 40f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 1.0f, 2.0f, 80f, Anchor.Center, Anchor.Center, 0f);

        PlaceRotated("Barrel", 14.2f, 4.2f, Tipped(25f, 90f), Anchor.Center, Anchor.Center);
        PlaceRotated("Cart", 17.2f, 4.6f, Tipped(300f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Fire Extinguisher", propsRoot, 1.2f, 4.2f, 0f, Anchor.Center, Anchor.Center, 0f);

        // ---- 北の物置き(z=12から18) ----
        PlaceOnFloor("Shelf Variation 05", propsRoot, 4.0f, RoomZ - WallInner, 0f, Anchor.Center, Anchor.Max, 0f);
        PlaceOnFloor("Shelf Variation 06", propsRoot, 10.6f, RoomZ - WallInner, 0f, Anchor.Center, Anchor.Max, 0f);
        PlaceOnFloor("Shelves Empty", propsRoot, 27.2f, RoomZ - WallInner, 0f, Anchor.Center, Anchor.Max, 0f);

        GameObject crate = PlaceOnFloor("Crate Long", propsRoot, 16.0f, 15.0f, 10f, Anchor.Center, Anchor.Center, 0f);
        StackOn(crate, "Crate Short", 30f);
        PlaceRotated("Crate Short", 14.0f, 13.6f, Tipped(60f, 90f), Anchor.Center, Anchor.Center);

        PlaceOnFloor("Pallet Variation 07", propsRoot, 24.0f, 14.2f, 200f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Garbage Bin", 14.6f, 16.9f, Tipped(100f, 90f), Anchor.Center, Anchor.Center);

        // 北東の角: ドラム缶(1つは倒れている)
        PlaceOnFloor("Barrel", propsRoot, 35.0f, 17.0f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 33.9f, 17.1f, 50f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 33.0f, 14.6f, Tipped(140f, 90f), Anchor.Center, Anchor.Center);

        // ---- 真ん中の通路(z=6.5から11.5) ----
        // 荷物を、南側と北側に、互いちがいに置く。通り道は、ジグザグに、少なくとも約1.5m の幅を残す。
        // 真ん中の血だまり(x=17)のまわりは、あけておく。
        BuildAisleClutter();
    }

    private static void BuildAisleClutter()
    {
        // 西の端(窓のそば): パレットを積んだ山と、ドラム缶
        GameObject westPallet = PlaceOnFloor("Pallet", propsRoot, 2.0f, 10.6f, 10f, Anchor.Center, Anchor.Center, 0f);
        GameObject westPallet2 = StackOn(westPallet, "Pallet", 35f);
        StackOn(westPallet2, "Crate Short", 80f);
        PlaceOnFloor("Barrel", propsRoot, 2.2f, 7.3f, 0f, Anchor.Center, Anchor.Center, 0f);

        // x=4から9
        PlaceOnFloor("Pallet Variation 02", propsRoot, 5.0f, 7.4f, 10f, Anchor.Center, Anchor.Center, 0f);
        GameObject crateA = PlaceOnFloor("Crate Long", propsRoot, 7.2f, 10.6f, 80f, Anchor.Center, Anchor.Center, 0f);
        StackOn(crateA, "Crate Short", 20f);
        PlaceRotated("Crate Short", 8.0f, 6.9f, Tipped(35f, 90f), Anchor.Center, Anchor.Center);

        // x=10から14
        PlaceOnFloor("Barrel", propsRoot, 10.3f, 7.2f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 11.2f, 7.4f, 30f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 10.9f, 10.6f, Tipped(70f, 90f), Anchor.Center, Anchor.Center);
        PlaceOnFloor("Cart", propsRoot, 13.0f, 10.4f, 200f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 06", propsRoot, 14.0f, 7.4f, 95f, Anchor.Center, Anchor.Center, 0f);

        // x=19から23(血だまりの東)
        PlaceOnFloor("Pallet Variation 08", propsRoot, 20.0f, 10.6f, 20f, Anchor.Center, Anchor.Center, 0f);
        GameObject crateB = PlaceOnFloor("Crate Long", propsRoot, 22.5f, 7.4f, 5f, Anchor.Center, Anchor.Center, 0f);
        StackOn(crateB, "Crate Short", 50f);
        PlaceRotated("Crate Short", 21.0f, 8.4f, Tipped(140f, 90f), Anchor.Center, Anchor.Center);

        // x=24から29
        PlaceOnFloor("Barrel", propsRoot, 24.2f, 10.7f, 0f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 25.1f, 10.9f, 45f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Barrel", propsRoot, 24.6f, 9.9f, 90f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Pallet Variation 04", propsRoot, 27.5f, 7.5f, 60f, Anchor.Center, Anchor.Center, 0f);
        PlaceOnFloor("Garbage Bin", propsRoot, 29.0f, 10.8f, 15f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Barrel", 28.2f, 10.0f, Tipped(200f, 90f), Anchor.Center, Anchor.Center);

        // 東の端(ふさがれた扉の前)
        PlaceOnFloor("Pallet Variation 09", propsRoot, 33.5f, 10.4f, 15f, Anchor.Center, Anchor.Center, 0f);
        PlaceRotated("Crate Short", 32.4f, 8.2f, Tipped(10f, 90f), Anchor.Center, Anchor.Center);
        PlaceRotated("Cart", 34.6f, 7.6f, Tipped(80f, 90f), Anchor.Center, Anchor.Center);
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
        // 真ん中の通路(入口から出口へ、だんだん暗く、こわれていく)
        // (横切る梁の真下をさけて、柱と柱のあいだに付ける)
        AddCeilingLight(3f, 9f, LightState.Steady, 0f);
        AddCeilingLight(9f, 9f, LightState.Flicker, 0f);
        AddCeilingLight(15f, 9f, LightState.Broken, 0f);
        AddCeilingLight(21f, 9f, LightState.Dead, 10f);
        AddCeilingLight(27f, 9f, LightState.Flicker, 0f);
        AddCeilingLight(33f, 9f, LightState.Dead, -9f);

        // 南の物置き
        AddCeilingLight(9f, 3f, LightState.Dead, -8f);
        AddCeilingLight(21f, 3f, LightState.Steady, 0f);
        AddCeilingLight(33f, 3f, LightState.Broken, 0f);

        // 北の物置き
        AddCeilingLight(3f, 15f, LightState.Flicker, 0f);
        AddCeilingLight(15f, 15f, LightState.Dead, 12f);
        AddCeilingLight(27f, 15f, LightState.Steady, 0f);

        // 落ちた電灯(壊れかけの電灯の下)
        AddFallenLight(16.4f, 10.8f, 50f);

        // 南の扉の上の、赤い非常灯
        GameObject warning = new GameObject("EmergencyLight_Red");
        warning.transform.SetParent(lightsRoot, false);
        warning.transform.localPosition = new Vector3(DoorCenterX, 6.2f, 1.2f);

        Light warningLight = warning.AddComponent<Light>();
        warningLight.type = LightType.Point;
        warningLight.color = new Color(1f, 0.15f, 0.1f);
        warningLight.intensity = 1.8f;
        warningLight.range = 9f;
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

    // ---------------- 血しぶき(立体) ----------------

    private static void BuildBlood()
    {
        // ---- 床 ----
        // 通路の血だまりと、そこから南の扉へ引きずった跡(柱のあいだを、ななめに通る)
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_03", 17.0f, 8.4f, 0f, 30f, 3.4f, 3.4f);
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Smear_01", 22.0f, 4.7f, 0.006f, 36.5f, 10f, 2.4f);

        // 倒れた棚のそば
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_01", 31.0f, 7.4f, 0f, 120f, 2.6f, 2.6f);

        // 入口のすぐ先
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_02", 4.0f, 10.2f, 0f, 70f, 1.6f, 1.6f);

        // 北の物置き
        HorrorDecor.SpawnBloodFloor(bloodRoot, "Blood_Splat_02", 21.5f, 15.6f, 0f, 220f, 2.0f, 2.0f);

        // ---- 壁 ----
        float east = RoomX - WallInner;
        float north = RoomZ - WallInner;

        // 南の扉の両わきの、血の手形
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Hand_01", new Vector3(DoorModule * Module - 0.6f, 1.5f, WallInner), Vector3.forward, 0.9f, 0.9f, 10f);
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Hand_01", new Vector3((DoorModule + 1) * Module + 0.6f, 1.8f, WallInner), Vector3.forward, 0.9f, 0.9f, -15f);

        // 東の壁の、飛び散った血(倒れた棚の上)
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Splat_02", new Vector3(east, 4.2f, 9.5f), Vector3.left, 2.0f, 2.0f, 40f);

        // ヒューズボックスの左の、したたる血
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Drip_01", new Vector3(17.6f, 3.0f, north), Vector3.back, 2.2f, 2.2f, 0f);

        // 南の壁の、飛び散った血
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Splat_01", new Vector3(4.0f, 2.6f, WallInner), Vector3.forward, 2.4f, 2.4f, 20f);

        // 西の壁(入口の北)の、したたる血
        HorrorDecor.SpawnBloodWall(bloodRoot, "Blood_Drip_02", new Vector3(WallInner, 2.8f, 14.5f), Vector3.right, 2.0f, 2.0f, 0f);
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