using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 鍵を、3色のカードキー(赤、青、黄)に変える道具。見た目と、役目(どの扉を開けるか)の両方を変える。
///
/// メニュー:
///   Tools / Key / 1. カードキーを作る(3色)
///       マテリアルとプレハブを作り、ItemDatabase に3枚を登録する。Tag(RedCard など)も作る。
///   Tools / Key / 2. 選んだ物をカードキーにする / 赤 青 黄
///       Hierarchy で選んだ鍵などを、その場所でカードキーに置きかえる。
///       何も選んでいなければ、Sceneビューの真ん中の床に置く。
///   Tools / Key / 3. 選んだ扉をカードキーの扉にする / 赤 青 黄
///       Hierarchy で選んだ扉(KeyDoor)を、その色のカードキーで開く扉にする。
///       扉の両側に、カードリーダー(ロック中は赤く点滅、開くと緑のランプ)を付ける。
///
/// モデルの置き場所(zip の中身):
///   Assets/Model/KeyCard/Red/KeyCard_Red.fbx と KeyCard_Red.png
///   Assets/Model/KeyCard/Blue/KeyCard_Blue.fbx と KeyCard_Blue.png
///   Assets/Model/KeyCard/Yellow/KeyCard_Yellow.fbx と KeyCard_Yellow.png
///   Assets/Model/KeyCard/Icon/KeyCard_Red_Icon.png など(持ち物の画面のアイコン)
/// </summary>
public static class KeyCardSetup
{
    private const string ModelFolder = "Assets/Model/KeyCard";
    private const string MaterialFolder = "Assets/Material/KeyCard";
    private const string PrefabFolder = "Assets/Prefab/KeyCard";

    private const float CardLength = 0.2f;        // カードの長い辺(m)。見つけやすいよう、本物より少し大きい
    private const float HoverHeight = 0.45f;      // 床から浮かせる高さ
    private const float PickupRange = 1.6f;       // 拾える範囲(四角の1辺)
    private const float ReaderHeight = 1.5f;      // カードリーダーの真ん中の、床からの高さ(m)

    private class CardColor
    {
        public string Key;          // Red など
        public string Tag;          // RedCard など
        public string JapaneseName; // 赤
        public Color Color;

        public CardColor(string key, string tag, string japaneseName, Color color)
        {
            Key = key;
            Tag = tag;
            JapaneseName = japaneseName;
            Color = color;
        }

        public string ItemName { get { return JapaneseName + "のカードキー"; } }
    }

    private static readonly CardColor Red = new CardColor("Red", "RedCard", "赤", new Color(1f, 0.12f, 0.1f));
    private static readonly CardColor Blue = new CardColor("Blue", "BlueCard", "青", new Color(0.15f, 0.4f, 1f));
    private static readonly CardColor Yellow = new CardColor("Yellow", "YellowCard", "黄", new Color(1f, 0.8f, 0.1f));
    private static readonly CardColor[] All = { Red, Blue, Yellow };

    // カードがいらない扉(リーダーの枠は緑、ランプも緑)
    private static readonly CardColor Free = new CardColor("Free", "", "緑", new Color(0.15f, 0.9f, 0.3f));

    private enum DoorMode
    {
        NeedCard,       // カードキーで開ける
        NeedCardAuto,   // カードキーで開けたあとは、自動ドア
        Unlocked,       // ロックなし。E で開け閉めできる
        AutoUnlocked,   // ロックなしの自動ドア(近づくと開いて、離れると閉まる)
        StartOpen       // 最初から開いている
    }

    // =====================================================================
    // 1. カードキーを作る
    // =====================================================================

    [MenuItem("Tools/Key/1. カードキーを作る(3色)", false, 1)]
    public static void CreateAllCards()
    {
        if (!NotPlaying()) return;

        List<string> report = new List<string>();
        List<string> problems = new List<string>();

        foreach (CardColor card in All)
        {
            EnsureTag(card.Tag);
        }

        ScriptableObject database = FindItemDatabase();
        if (database == null)
        {
            problems.Add("ItemDatabase が見つからないので、図鑑への登録は飛ばしました。");
        }

        foreach (CardColor card in All)
        {
            GameObject model = FindModel(card);
            Texture2D texture = FindTexture(card);

            if (model == null)
            {
                problems.Add(card.JapaneseName + ": モデル(" + ModelFolder + "/" + card.Key + " の .fbx)が見つかりません。");
                continue;
            }

            Material mat = GetCardMaterial(card, texture);
            GameObject display = CreateDisplayPrefab(card, model, mat);
            CreateWorldPrefab(card, model, mat, database);

            Sprite icon = GetIcon(card);

            if (database != null)
            {
                RegisterItem(database, card, display, icon);
            }

            report.Add(card.ItemName + "  (Tag: " + card.Tag + ")");
        }

        AssetDatabase.SaveAssets();

        string message = "作ったカードキー:\n - " + string.Join("\n - ", report)
            + "\n\nプレハブ: " + PrefabFolder
            + "\n\n次に、Tools / Key / 2 で、鍵をカードキーに置きかえて、"
            + "Tools / Key / 3 で、扉の色を決めてください。";

        if (problems.Count > 0)
        {
            message += "\n\n注意:\n - " + string.Join("\n - ", problems);
        }

        EditorUtility.DisplayDialog("カードキー", message, "OK");
    }

    // ---------------- モデルとマテリアル ----------------

    private static GameObject FindModel(CardColor card)
    {
        string folder = ModelFolder + "/" + card.Key;
        if (!AssetDatabase.IsValidFolder(folder)) return null;

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.ToLower().EndsWith(".fbx"))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
        return null;
    }

    private static Texture2D FindTexture(CardColor card)
    {
        string folder = ModelFolder + "/" + card.Key;
        if (!AssetDatabase.IsValidFolder(folder)) return null;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }
        return null;
    }

    private static Material GetCardMaterial(CardColor card, Texture2D texture)
    {
        string path = MaterialFolder + "/KeyCard_" + card.Key + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (texture != null)
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", texture);
            mat.mainTexture = texture;

            // 暗い所でも、カードの模様が少しだけ見えるように、うすく光らせる
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", Color.white * 0.25f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.SetColor("_BaseColor", card.Color);
        }

        mat.SetFloat("_Metallic", 0.25f);
        mat.SetFloat("_Smoothness", 0.6f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// モデルを入れて、向きと大きさをそろえた物を作る。
    /// 一番うすい向きを Z、一番長い向きを X にして(カードを立てた形)、真ん中を原点にする。
    /// </summary>
    private static GameObject BuildModel(GameObject model, Material mat, Transform parent)
    {
        GameObject holder = new GameObject("Model");
        holder.transform.SetParent(parent, false);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.transform.SetParent(holder.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        // マテリアルを、全部カードのマテリアルにする
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 当たり判定は、いらない(拾う範囲は、外側の四角でとる)
        foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(c);
        }

        Bounds b;
        if (!TryGetLocalBounds(holder.transform, out b)) return holder;

        // 向きをそろえる
        Vector3 size = b.size;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        float[] lengths = { size.x, size.y, size.z };

        int thin = 0, longest = 0;
        for (int i = 1; i < 3; i++)
        {
            if (lengths[i] < lengths[thin]) thin = i;
            if (lengths[i] > lengths[longest]) longest = i;
        }
        if (thin == longest) longest = (thin + 1) % 3;
        int middle = 3 - thin - longest;

        Quaternion toCard = Quaternion.Inverse(Quaternion.LookRotation(axes[thin], axes[middle]));
        instance.transform.localRotation = toCard;

        // 大きさをそろえる
        TryGetLocalBounds(holder.transform, out b);
        float longSide = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longSide > 0.0001f)
        {
            instance.transform.localScale = Vector3.one * (CardLength / longSide);
        }

        // 真ん中を原点にする
        TryGetLocalBounds(holder.transform, out b);
        instance.transform.localPosition -= b.center;

        return holder;
    }

    private static bool TryGetLocalBounds(Transform root, out Bounds result)
    {
        result = new Bounds();
        bool has = false;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Bounds wb = r.bounds;
            Bounds lb = new Bounds(root.InverseTransformPoint(wb.center), wb.size);

            if (!has) { result = lb; has = true; }
            else result.Encapsulate(lb);
        }
        return has;
    }

    // ---------------- プレハブ ----------------

    /// <summary>
    /// 持ち物の画面で、くるくる回して見せる用(スクリプトも光もない、見た目だけ)。
    /// </summary>
    private static GameObject CreateDisplayPrefab(CardColor card, GameObject model, Material mat)
    {
        GameObject root = new GameObject("KeyCard_" + card.Key + "_Display");
        BuildModel(model, mat, root.transform);

        EnsureFolder(PrefabFolder);
        string path = PrefabFolder + "/KeyCard_" + card.Key + "_Display.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    /// <summary>
    /// ゲームの中に置く用。床から少し浮いて回り、カードの色に光る。近づいて E で拾える。
    /// </summary>
    private static GameObject CreateWorldPrefab(CardColor card, GameObject model, Material mat, ScriptableObject database)
    {
        GameObject root = new GameObject("KeyCard_" + card.Key);
        root.tag = card.Tag;

        BoxCollider range = root.AddComponent<BoxCollider>();
        range.isTrigger = true;
        range.size = Vector3.one * PickupRange;
        range.center = new Vector3(0f, PickupRange * 0.5f, 0f);

        System.Type pickupType = FindType("ItemPickup");
        if (pickupType != null)
        {
            Component pickup = root.AddComponent(pickupType);
            if (database != null)
            {
                SerializedObject so = new SerializedObject(pickup);
                SerializedProperty prop = so.FindProperty("itemDatabase");
                if (prop != null) prop.objectReferenceValue = database;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, HoverHeight, 0f);

        // 向きを調べるので、モデルを入れてから傾ける
        BuildModel(model, mat, visual.transform);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);

        GameObject glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(visual.transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        Light glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = card.Color;
        glow.range = 2.5f;
        glow.intensity = 1f;
        glow.shadows = LightShadows.None;

        System.Type visualType = FindType("KeyCardVisual");
        if (visualType != null)
        {
            visual.AddComponent(visualType);
        }

        EnsureFolder(PrefabFolder);
        string path = PrefabFolder + "/KeyCard_" + card.Key + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject LoadWorldPrefab(CardColor card)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/KeyCard_" + card.Key + ".prefab");
    }

    // ---------------- アイコン ----------------

    private static Sprite GetIcon(CardColor card)
    {
        string path = ModelFolder + "/Icon/KeyCard_" + card.Key + "_Icon.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------- 図鑑(ItemDatabase)に登録 ----------------

    private static ScriptableObject FindItemDatabase()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDatabase"))
        {
            ScriptableObject db = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (db != null) return db;
        }
        return null;
    }

    /// <summary>
    /// 同じ Tag のアイテムがあれば書きかえる。なければ、最後に足す。
    /// </summary>
    private static void RegisterItem(ScriptableObject database, CardColor card, GameObject display, Sprite icon)
    {
        SerializedObject so = new SerializedObject(database);
        SerializedProperty list = so.FindProperty("allItems");
        if (list == null || !list.isArray) return;

        SerializedProperty item = null;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            SerializedProperty tag = element.FindPropertyRelative("itemTag");
            if (tag != null && tag.stringValue == card.Tag)
            {
                item = element;
                break;
            }
        }

        if (item == null)
        {
            list.arraySize++;
            item = list.GetArrayElementAtIndex(list.arraySize - 1);
        }

        SetString(item, "itemName", card.ItemName);
        SetString(item, "description", Description(card));
        SetString(item, "itemTag", card.Tag);
        SetObject(item, "icon", icon);
        SetObject(item, "displayPrefab", display);

        SerializedProperty type = item.FindPropertyRelative("itemType");
        if (type != null)
        {
            int keyIndex = System.Array.IndexOf(type.enumNames, "Key");
            if (keyIndex >= 0) type.enumValueIndex = keyIndex;
        }

        SerializedProperty heal = item.FindPropertyRelative("healAmount");
        if (heal != null) heal.intValue = 0;

        SerializedProperty flashlight = item.FindPropertyRelative("unlocksFlashlight");
        if (flashlight != null) flashlight.boolValue = false;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
    }

    private static string Description(CardColor card)
    {
        if (card == Red)
        {
            return "赤いラインの入った、職員用のカードキー。\n赤い枠のカードリーダーの扉を開けられる。\n表面に、乾いた血がこびりついている。";
        }
        if (card == Blue)
        {
            return "青いラインの入った、管理区画のカードキー。\n青い枠のカードリーダーの扉を開けられる。\n裏に、誰かの名前が削られた跡がある。";
        }
        return "黄色いラインの入った、警備用のカードキー。\n黄色い枠のカードリーダーの扉を開けられる。\nひびが入っているが、まだ使えそうだ。";
    }

    private static void SetString(SerializedProperty parent, string field, string value)
    {
        SerializedProperty p = parent.FindPropertyRelative(field);
        if (p != null) p.stringValue = value;
    }

    private static void SetObject(SerializedProperty parent, string field, Object value)
    {
        SerializedProperty p = parent.FindPropertyRelative(field);
        if (p != null && value != null) p.objectReferenceValue = value;
    }

    // =====================================================================
    // 2. 選んだ物をカードキーにする
    // =====================================================================

    [MenuItem("Tools/Key/2. 選んだ物をカードキーにする/赤", false, 20)]
    private static void PlaceRed() { PlaceCards(Red); }

    [MenuItem("Tools/Key/2. 選んだ物をカードキーにする/青", false, 21)]
    private static void PlaceBlue() { PlaceCards(Blue); }

    [MenuItem("Tools/Key/2. 選んだ物をカードキーにする/黄", false, 22)]
    private static void PlaceYellow() { PlaceCards(Yellow); }

    private static void PlaceCards(CardColor card)
    {
        if (!NotPlaying()) return;

        GameObject prefab = LoadWorldPrefab(card);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("カードキー", "先に Tools / Key / 1. カードキーを作る(3色) を実行してください。", "OK");
            return;
        }

        Undo.SetCurrentGroupName(card.ItemName + "を置く");
        int group = Undo.GetCurrentGroup();

        List<GameObject> created = new List<GameObject>();
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            created.Add(SpawnCard(prefab, null, SceneViewFloorPoint(), null));
        }
        else
        {
            foreach (GameObject old in selected)
            {
                if (old == null || EditorUtility.IsPersistent(old)) continue;

                // 見た目の底を、床の位置として使う
                Vector3 spot = old.transform.position;
                Bounds b;
                if (TryGetWorldBounds(old, out b))
                {
                    spot = new Vector3(b.center.x, b.min.y, b.center.z);
                }

                int sibling = old.transform.GetSiblingIndex();
                GameObject made = SpawnCard(prefab, old.transform.parent, spot, old);
                made.transform.SetSiblingIndex(sibling);
                created.Add(made);

                Undo.DestroyObjectImmediate(old);
            }
        }

        Undo.CollapseUndoOperations(group);
        Selection.objects = created.ToArray();

        if (created.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(created[0].scene);
        }
    }

    private static GameObject SpawnCard(GameObject prefab, Transform parent, Vector3 position, GameObject replaced)
    {
        GameObject card = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(card, "カードキーを置く");

        if (parent != null)
        {
            Undo.SetTransformParent(card.transform, parent, "カードキーを置く");
        }
        card.transform.position = position;
        card.transform.rotation = Quaternion.identity;

        CopyPrompt(card, replaced);
        return card;
    }

    /// <summary>
    /// 「Eキーで拾う」の表示(シーンの中のUI)を、置きかえる前の物か、シーンの別の拾える物から、写す。
    /// </summary>
    private static void CopyPrompt(GameObject card, GameObject replaced)
    {
        System.Type pickupType = FindType("ItemPickup");
        if (pickupType == null) return;

        Component target = card.GetComponent(pickupType);
        if (target == null) return;

        Component source = null;
        if (replaced != null)
        {
            source = replaced.GetComponentInChildren(pickupType, true);
        }

        if (source == null || !HasPrompt(source))
        {
            source = null;
            foreach (Object o in Object.FindObjectsByType(pickupType, FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Component c = o as Component;
                if (c != null && c != target && HasPrompt(c))
                {
                    source = c;
                    break;
                }
            }
        }

        if (source == null) return;

        SerializedObject src = new SerializedObject(source);
        SerializedObject dst = new SerializedObject(target);

        string[] fields = { "promptUI", "promptText" };
        foreach (string f in fields)
        {
            SerializedProperty a = src.FindProperty(f);
            SerializedProperty b = dst.FindProperty(f);
            if (a != null && b != null) b.objectReferenceValue = a.objectReferenceValue;
        }

        dst.ApplyModifiedProperties();
    }

    private static bool HasPrompt(Component pickup)
    {
        SerializedObject so = new SerializedObject(pickup);
        SerializedProperty p = so.FindProperty("promptUI");
        return p != null && p.objectReferenceValue != null;
    }

    private static Vector3 SceneViewFloorPoint()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return Vector3.zero;

        Vector3 pivot = view.pivot;
        RaycastHit hit;
        if (Physics.Raycast(pivot + Vector3.up * 5f, Vector3.down, out hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return new Vector3(pivot.x, 0f, pivot.z);
    }

    // =====================================================================
    // 3. 選んだ扉をカードキーの扉にする
    // =====================================================================

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/赤", false, 40)]
    private static void DoorRed() { SetupDoors(Red, DoorMode.NeedCard); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/青", false, 41)]
    private static void DoorBlue() { SetupDoors(Blue, DoorMode.NeedCard); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/黄", false, 42)]
    private static void DoorYellow() { SetupDoors(Yellow, DoorMode.NeedCard); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/カードで開けたあと自動ドア/赤", false, 50)]
    private static void DoorRedAuto() { SetupDoors(Red, DoorMode.NeedCardAuto); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/カードで開けたあと自動ドア/青", false, 51)]
    private static void DoorBlueAuto() { SetupDoors(Blue, DoorMode.NeedCardAuto); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/カードで開けたあと自動ドア/黄", false, 52)]
    private static void DoorYellowAuto() { SetupDoors(Yellow, DoorMode.NeedCardAuto); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/自動ドア(緑のランプ。近づくと開く)", false, 59)]
    private static void DoorAutoUnlocked() { SetupDoors(Free, DoorMode.AutoUnlocked); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/ロックなし(緑のランプ。Eで開く)", false, 60)]
    private static void DoorUnlocked() { SetupDoors(Free, DoorMode.Unlocked); }

    [MenuItem("Tools/Key/3. 選んだ扉をカードキーの扉にする/最初から開いている(緑のランプ)", false, 61)]
    private static void DoorStartOpen() { SetupDoors(Free, DoorMode.StartOpen); }

    private static void SetupDoors(CardColor card, DoorMode mode)
    {
        if (!NotPlaying()) return;

        System.Type doorType = FindType("KeyDoor");
        if (doorType == null)
        {
            EditorUtility.DisplayDialog("カードキー", "KeyDoor のスクリプトが見つかりません。", "OK");
            return;
        }

        HashSet<Component> doors = new HashSet<Component>();
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null || EditorUtility.IsPersistent(go)) continue;

            Component d = go.GetComponentInChildren(doorType, true);
            if (d == null) d = go.GetComponentInParent(doorType, true);
            if (d != null) doors.Add(d);
        }

        if (doors.Count == 0)
        {
            EditorUtility.DisplayDialog("カードキー", "Hierarchy で、扉(KeyDoor が付いた物)を選んでから、実行してください。", "OK");
            return;
        }

        Undo.SetCurrentGroupName("扉の設定を変える");
        int group = Undo.GetCurrentGroup();

        foreach (Component door in doors)
        {
            SetDoorFields(door, card, mode);
            AddReaders(door, card);
            EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
        }

        Undo.CollapseUndoOperations(group);

        string what;
        if (mode == DoorMode.NeedCard)
        {
            what = card.ItemName + "で開く扉にしました。\n"
                + "扉の両側に、カードリーダーを付けました(ロック中は赤く点滅、開くと緑)。";
        }
        else if (mode == DoorMode.NeedCardAuto)
        {
            what = card.ItemName + "で開く扉にしました。一度開けたあとは、自動ドアになります。\n"
                + "(ロック中は赤く点滅、開けたあとは緑。近づくと開いて、離れると閉まります)";
        }
        else if (mode == DoorMode.AutoUnlocked)
        {
            what = "ロックのない自動ドアにしました。近づくと開いて、離れると閉まります。\n"
                + "カードリーダーのランプは、はじめから緑です。";
        }
        else if (mode == DoorMode.Unlocked)
        {
            what = "ロックのない扉にしました。カードなしで、E で開け閉めできます。\n"
                + "カードリーダーのランプは、はじめから緑です。";
        }
        else
        {
            what = "最初から開いている扉にしました(ゲームが始まると、開いた状態になります)。\n"
                + "カードリーダーのランプは、はじめから緑です。E で閉めることもできます。";
        }

        EditorUtility.DisplayDialog("カードキー", doors.Count + " 個の扉を、" + what, "OK");
    }

    private static void SetDoorFields(Component door, CardColor card, DoorMode mode)
    {
        Undo.RecordObject(door, "扉の設定を変える");

        SerializedObject so = new SerializedObject(door);
        SetProp(so, "consumeKey", false);        // カードは、何度でも使える
        SetProp(so, "enemiesOpenLockedDoors", false);

        bool auto = mode == DoorMode.NeedCardAuto || mode == DoorMode.AutoUnlocked;
        SetProp(so, "autoDoor", auto);
        SetProp(so, "autoDoorAfterUnlock", mode == DoorMode.NeedCardAuto);
        if (auto)
        {
            SetProp(so, "autoClose", true);
            SetFloat(so, "autoCloseDelay", 1.5f);
        }

        if (mode == DoorMode.NeedCard || mode == DoorMode.NeedCardAuto)
        {
            SetProp(so, "requiredKeyTag", card.Tag);
            SetProp(so, "startLocked", true);
            SetProp(so, "startOpen", false);
            SetProp(so, "lockedText", card.ItemName + "が必要だ…");
            SetProp(so, "unlockedText", card.ItemName + "をかざした。ロックが外れた");
        }
        else
        {
            SetProp(so, "startLocked", false);
            SetProp(so, "startOpen", mode == DoorMode.StartOpen);
            if (mode == DoorMode.StartOpen)
            {
                SetProp(so, "autoClose", false); // 開いたままにする
            }
        }

        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// 扉の両側に、カードリーダーを付ける。扉の板(Door Leaf)の横、高さ1.3mのあたり。
    /// </summary>
    private static void AddReaders(Component door, CardColor card)
    {
        Transform doorRoot = door.transform;

        // 前に付けたリーダーは消す
        for (int i = doorRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = doorRoot.GetChild(i);
            if (child.name.StartsWith("CardReader_"))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        // 扉の板の大きさから、横の位置を決める
        Vector3 along = doorRoot.forward;       // 壁にそった向き
        Vector3 normal = doorRoot.right;        // 壁から部屋の内側への向き
        Vector3 center = doorRoot.position + along * 3f;
        float halfWidth = 2f;
        float floorY = doorRoot.position.y;

        SerializedObject so = new SerializedObject(door);
        SerializedProperty leafProp = so.FindProperty("doorLeaf");
        Transform leaf = leafProp != null ? leafProp.objectReferenceValue as Transform : null;

        Bounds b;
        if (leaf != null && TryGetWorldBounds(leaf.gameObject, out b))
        {
            center = b.center;
            Vector3 e = b.extents;
            halfWidth = Mathf.Abs(along.x) * e.x + Mathf.Abs(along.y) * e.y + Mathf.Abs(along.z) * e.z;
            floorY = b.min.y;
        }

        float wallHalf = 0.2f;
        Vector3 basePoint = new Vector3(center.x, floorY, center.z) + along * (halfWidth + 0.45f);

        // 扉の板の下の端は、床と同じ高さとはかぎらない。実際の床を、下へレイを飛ばして調べる
        float probeTop = (leaf != null && TryGetWorldBounds(leaf.gameObject, out b)) ? b.center.y : floorY + 2f;
        float frontFloor = FindFloorY(basePoint + normal * 0.6f, probeTop, floorY, door.transform);
        float backFloor = FindFloorY(basePoint - normal * 0.6f, probeTop, floorY, door.transform);
        Vector3 front = new Vector3(basePoint.x, frontFloor + ReaderHeight, basePoint.z);
        Vector3 back = new Vector3(basePoint.x, backFloor + ReaderHeight, basePoint.z);

        CreateReader(door, card, "CardReader_Front", front + normal * wallHalf, normal);
        CreateReader(door, card, "CardReader_Back", back - normal * wallHalf, -normal);
    }

    /// <summary>
    /// point の真上(高さ top)から下へレイを飛ばして、床の高さを返す。扉そのものは、とばす。
    /// 見つからなければ fallback。
    /// </summary>
    private static float FindFloorY(Vector3 point, float top, float fallback, Transform ignore)
    {
        Vector3 origin = new Vector3(point.x, top, point.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);

        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (ignore != null && hit.collider.transform.IsChildOf(ignore)) continue;
            if (hit.point.y > best) best = hit.point.y;
        }

        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    // =====================================================================
    // 4. カードリーダーの高さをそろえる
    // =====================================================================

    /// <summary>
    /// すでに付けたカードリーダーを、床から ReaderHeight の高さに動かす。
    /// Hierarchy で選んだ扉(またはリーダー)だけ。何も選んでいなければ、シーンの全部。
    /// </summary>
    [MenuItem("Tools/Key/4. カードリーダーの高さをそろえる", false, 60)]
    private static void FixReaderHeights()
    {
        if (!NotPlaying()) return;

        List<Transform> readers = new List<Transform>();
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length > 0)
        {
            foreach (GameObject go in selected)
            {
                if (go == null || EditorUtility.IsPersistent(go)) continue;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("CardReader_") && !readers.Contains(t)) readers.Add(t);
                }
                if (go.name.StartsWith("CardReader_") && !readers.Contains(go.transform)) readers.Add(go.transform);
            }
        }
        else
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("CardReader_")) readers.Add(t);
            }
        }

        if (readers.Count == 0)
        {
            EditorUtility.DisplayDialog("カードキー", "カードリーダー(CardReader_ という名前の物)が見つかりません。", "OK");
            return;
        }

        Undo.SetCurrentGroupName("カードリーダーの高さをそろえる");
        int group = Undo.GetCurrentGroup();

        foreach (Transform reader in readers)
        {
            Transform door = reader.parent;
            Vector3 p = reader.position;

            // 壁から 0.6m 手前の床を調べる(リーダーは壁の外向きに付いている)
            Vector3 probe = p + reader.forward * 0.6f;
            float floor = FindFloorY(probe, p.y + 1.5f, p.y - ReaderHeight, door);

            Undo.RecordObject(reader, "カードリーダーの高さをそろえる");
            reader.position = new Vector3(p.x, floor + ReaderHeight, p.z);
            EditorSceneManager.MarkSceneDirty(reader.gameObject.scene);
        }

        Undo.CollapseUndoOperations(group);

        EditorUtility.DisplayDialog("カードキー",
            readers.Count + " 個のカードリーダーを、床から " + ReaderHeight + "m の高さにしました。\n"
            + "高さを変えたいときは、KeyCardSetup.cs の ReaderHeight を変えて、もう一度実行してください。", "OK");
    }

    private static void CreateReader(Component door, CardColor card, string readerName, Vector3 position, Vector3 facing)
    {
        GameObject reader = new GameObject(readerName);
        Undo.RegisterCreatedObjectUndo(reader, "カードリーダーを付ける");
        reader.transform.SetParent(door.transform, true);
        reader.transform.position = position;
        reader.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

        // 本体(黒い箱)、色の枠、カードを差しこむ溝、ランプ
        MakeBox(reader.transform, "Body", new Vector3(0f, 0f, 0.025f), new Vector3(0.16f, 0.26f, 0.05f),
            GetPlainMaterial("Reader_Body", new Color(0.06f, 0.06f, 0.07f), 0.5f, Color.black));
        MakeBox(reader.transform, "ColorFrame", new Vector3(0f, -0.02f, 0.052f), new Vector3(0.13f, 0.12f, 0.006f),
            GetPlainMaterial("Reader_" + card.Key, card.Color * 0.6f, 0.4f, card.Color * 0.8f));
        MakeBox(reader.transform, "Slot", new Vector3(0f, -0.02f, 0.056f), new Vector3(0.1f, 0.012f, 0.004f),
            GetPlainMaterial("Reader_Slot", Color.black, 0.2f, Color.black));

        GameObject lamp = MakeBox(reader.transform, "Lamp", new Vector3(0f, 0.085f, 0.055f), new Vector3(0.05f, 0.03f, 0.01f),
            GetPlainMaterial("Reader_Lamp", Color.red, 0.8f, Color.red * 2f));

        GameObject lightObject = new GameObject("LampLight");
        lightObject.transform.SetParent(reader.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.085f, 0.15f);
        Light lampLight = lightObject.AddComponent<Light>();
        lampLight.type = LightType.Point;
        lampLight.range = 1.5f;
        lampLight.intensity = 0.6f;
        lampLight.color = Color.red;
        lampLight.shadows = LightShadows.None;

        System.Type readerType = FindType("KeyCardReader");
        if (readerType != null)
        {
            Component comp = reader.AddComponent(readerType);
            SerializedObject so = new SerializedObject(comp);
            SetRef(so, "door", door);
            SetRef(so, "lamp", lamp.GetComponent<Renderer>());
            SetRef(so, "lampLight", lampLight);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameObject MakeBox(Transform parent, string boxName, Vector3 localPos, Vector3 size, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = boxName;
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = mat;
        return box;
    }

    private static Material GetPlainMaterial(string matName, Color color, float smoothness, Color emission)
    {
        string path = MaterialFolder + "/" + matName + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        mat.SetFloat("_Metallic", 0.3f);
        mat.SetFloat("_Smoothness", smoothness);

        if (emission.maxColorComponent > 0.001f)
        {
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EnsureFolder(MaterialFolder);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // =====================================================================
    // 共通
    // =====================================================================

    private static bool NotPlaying()
    {
        if (!EditorApplication.isPlaying) return true;
        EditorUtility.DisplayDialog("カードキー", "再生を止めてから、もう一度実行してください。", "OK");
        return false;
    }

    private static void SetProp(SerializedObject so, string field, string value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.stringValue = value;
    }

    private static void SetProp(SerializedObject so, string field, bool value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.boolValue = value;
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.floatValue = value;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    private static bool TryGetWorldBounds(GameObject target, out Bounds result)
    {
        result = new Bounds(target.transform.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (!has) { result = r.bounds; has = true; }
            else result.Encapsulate(r.bounds);
        }
        return has;
    }

    private static System.Type FindType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type found = assembly.GetType(typeName);
            if (found != null) return found;
        }
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>
    /// Tag がなければ作る(Project Settings の Tags and Layers)。
    /// </summary>
    private static void EnsureTag(string tag)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");

        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        }

        tags.arraySize++;
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }
}