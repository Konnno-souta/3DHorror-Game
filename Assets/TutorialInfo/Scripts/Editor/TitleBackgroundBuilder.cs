using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// タイトル画面の背景を、3Dの廊下で作る。
/// メニュー: Tools / Title / タイトルの背景を作る (タイトルのシーンを開いてから実行する)
///
///   ・SciFi Warehouse Kit の廊下を、奥へつなげる(十字路まで30m、その先も続く)。奥は十字路で、その先も暗い廊下が続く
///   ・天井の電灯は、ちらつく、消えている、壊れかけ(火花)を、まぜる
///   ・化け物(Shadowlop)が、十字路を、左の廊下から右の廊下へ、歩きのアニメーションで横切る。
///     ときどき真ん中で立ち止まり、こちらを見る。消えたら、しばらくして、また左から出てくる(TitleMonsterWalk)。
///     歩きのアニメーションは、Shadowlop_WALK.fbx から、くり返し再生する Animator Controller を作って使う
///   ・床と壁に、立体の血。全体を暗くして、霧を出す
///   ・Main Camera を廊下の手前に置き、奥を見るようにする。今の背景の画像(BackgroundImage)はオフにする
///
/// もう一度実行すると、前の背景を消して、作り直す。Ctrl+Z で、全部もとにもどせる。
/// </summary>
public static class TitleBackgroundBuilder
{
    private const string RootName = "TitleBackground3D";
    private const string KitFolderKeyword = "SciFi Warehouse Kit/Prefabs";
    private const string MonsterPrefabPath = "Assets/Aseets_iroiro/Shadowlop/_Prefabs/Shadowlop_Mesh.prefab";
    private const string MonsterControllerPath = "Assets/Scenes/Shadowlop_AC.controller";
    private const string BrokenLightPrefabPath = "Assets/Prefab/BrokenLight.prefab";
    private const string ShadowMaterialPath = "Assets/Material/Horror/Title_MonsterShadow.mat";
    private const string WalkFbxPath = "Assets/Aseets_iroiro/Shadowlop/_Animations/Shadowlop/Shadowlop_WALK.fbx";
    private const string WalkControllerPath = "Assets/Material/Title/Title_MonsterWalk.controller";

    private const float Module = 6f;
    private const int StraightCount = 5;          // 十字路までの、まっすぐな廊下の数(30m)
    private const float CrossZ = Module * StraightCount;   // 十字路の手前の端(30m)
    private const float MonsterHeight = 3.0f;     // 化け物の背の高さ(m)

    private static readonly Dictionary<string, GameObject> kitCache = new Dictionary<string, GameObject>();
    private static readonly List<string> missing = new List<string>();

    private static Transform root;
    private static Transform corridorRoot;
    private static Transform lightsRoot;
    private static Transform bloodRoot;
    private static float rightWallX = 5f;   // 廊下の右の壁の内側(ローカルのx)。実際に測る
    private static float leftWallX = 1f;

    private enum LightState { Steady, Flicker, Broken, Dead }

    // ---------------- メニュー ----------------

    [MenuItem("Tools/Title/タイトルの背景を作る")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("タイトルの背景", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        string sceneName = scene.name.ToLower();
        if (!sceneName.Contains("titl"))
        {
            if (!EditorUtility.DisplayDialog("タイトルの背景",
                "今開いているシーンは「" + scene.name + "」です。タイトルのシーンではないようです。\n"
                + "このシーンに、タイトルの背景を作りますか?", "作る", "やめる"))
            {
                return;
            }
        }

        Undo.SetCurrentGroupName("タイトルの背景を作る");
        int undoGroup = Undo.GetCurrentGroup();

        kitCache.Clear();
        missing.Clear();

        GameObject old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        GameObject rootObject = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "タイトルの背景を作る");
        root = rootObject.transform;
        root.position = new Vector3(0f, -500f, 0f);   // UIや、ほかの物とぶつからない、遠い場所

        corridorRoot = CreateGroup(root, "Corridor");
        lightsRoot = CreateGroup(root, "Lights");
        bloodRoot = CreateGroup(root, "Blood");

        float lightHeight = BuildCorridor();
        MeasureCorridorWidth();
        BuildLights(lightHeight);
        BuildMonster();
        BuildBlood();
        SetLayerDefault(rootObject);

        // ピンクになるマテリアル(化け物のシェーダーなど)を、URP の Lit に直す
        foreach (Renderer r in rootObject.GetComponentsInChildren<Renderer>(true))
        {
            PinkMaterialFixer.FixRenderer(r, null);
        }

        SetupCamera();
        SetupDarkness();
        HideOldBackground();

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = rootObject;
        EditorSceneManager.MarkSceneDirty(scene);

        string message = "タイトルの背景を作りました(" + RootName + ")。\n\n"
            + "・Main Camera を、廊下の手前に動かしました\n"
            + "・前の背景の画像(BackgroundImage)は、オフにしました\n"
            + "・Directional Light は、オフにしました(暗くするため)\n\n"
            + "再生すると、電灯がちらつき、奥の十字路を、化け物が左から右へ歩いて横切ります。";

        if (missing.Count > 0)
        {
            message += "\n\n見つからなかった物(飛ばしました):\n - " + string.Join("\n - ", missing);
        }

        EditorUtility.DisplayDialog("タイトルの背景", message, "OK");
    }

    // ---------------- 廊下 ----------------
    // 廊下の1枚は 6m x 6m。基準点は角で、ローカル+X と +Z へ広がる。
    // まっすぐな廊下は x=0から6、z=0から30。十字路は z=30から36。

    private static float BuildCorridor()
    {
        float ceilingY = 4f;

        for (int i = 0; i < StraightCount; i++)
        {
            string piece = (i % 2 == 0) ? "Corridor Passthrough Lights" : "Corridor Passthrough";
            GameObject p = Spawn(piece, corridorRoot, new Vector3(0f, 0f, i * Module), 0f);

            Bounds b;
            if (i == 0 && p != null && TryGetBounds(p, out b))
            {
                ceilingY = b.max.y - root.position.y;
            }
        }

        // 十字路と、その先(奥は真っ暗)
        Spawn("Corridor 4-Way", corridorRoot, new Vector3(0f, 0f, CrossZ), 0f);
        Spawn("Corridor Passthrough", corridorRoot, new Vector3(0f, 0f, CrossZ + Module), 0f);
        Spawn("Corridor Passthrough", corridorRoot, new Vector3(0f, 0f, CrossZ + Module * 2f), 0f);
        Spawn("Corridor Deadend", corridorRoot, new Vector3(0f, 0f, CrossZ + Module * 3f), 0f);

        // 右の曲がり角(化け物がいる)と、左の曲がり角
        Spawn("Corridor Passthrough", corridorRoot, new Vector3(Module, 0f, CrossZ + Module), 90f);
        Spawn("Corridor Passthrough", corridorRoot, new Vector3(Module * 2f, 0f, CrossZ + Module), 90f);
        Spawn("Corridor Passthrough", corridorRoot, new Vector3(0f, 0f, CrossZ), 270f);

        return Mathf.Clamp(ceilingY - 0.5f, 2.5f, 8f);
    }

    /// <summary>
    /// 廊下の左右の壁の内側の位置を、横にレイを飛ばして測る(廊下の部品の、本当の幅に合わせるため)。
    /// </summary>
    private static void MeasureCorridorWidth()
    {
        Physics.SyncTransforms();
        Vector3 origin = root.TransformPoint(new Vector3(3f, 1.5f, 14f));

        RaycastHit hit;
        if (Physics.Raycast(origin, root.right, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            rightWallX = root.InverseTransformPoint(hit.point).x;
        }
        if (Physics.Raycast(origin, -root.right, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            leftWallX = root.InverseTransformPoint(hit.point).x;
        }
    }

    // ---------------- 電灯 ----------------

    private static void BuildLights(float y)
    {
        // 手前から奥へ: だんだん壊れていく
        AddCorridorLight(new Vector3(3f, y, 3f), LightState.Flicker, 0.7f);
        AddCorridorLight(new Vector3(3f, y, 9f), LightState.Dead, 1f);
        AddCorridorLight(new Vector3(3f, y, 15f), LightState.Broken, 1f);
        AddCorridorLight(new Vector3(3f, y, 21f), LightState.Steady, 0.7f);
        AddCorridorLight(new Vector3(3f, y, 27f), LightState.Flicker, 1.1f);

        // 十字路の、青白い弱い光
        AddPlainLight("CrossLight", new Vector3(3f, y, CrossZ + 3f), new Color(0.6f, 0.7f, 0.85f), 2.4f, 10f);

        // 左の廊下(化け物が出てくる所)の、弱い光
        AddPlainLight("LeftBranchLight", new Vector3(-3f, y, CrossZ + Module * 0.5f), new Color(0.7f, 0.78f, 0.9f), 1.0f, 6f);

        // 奥の赤い非常灯
        AddPlainLight("EmergencyRed", new Vector3(3f, y - 0.3f, CrossZ + Module * 2.5f), new Color(1f, 0.12f, 0.08f), 2.2f, 10f);
    }

    private static void AddCorridorLight(Vector3 localPos, LightState state, float strength)
    {
        if (state == LightState.Dead) return;

        if (state == LightState.Broken)
        {
            GameObject brokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenLightPrefabPath);
            if (brokenPrefab != null)
            {
                GameObject broken = (GameObject)PrefabUtility.InstantiatePrefab(brokenPrefab, lightsRoot);
                broken.transform.localPosition = localPos + Vector3.up * 0.4f;

                // 天井の電灯の器具は、廊下の部品にあるので、BrokenLight の器具は隠す
                Transform fixture = broken.transform.Find("Fixture");
                if (fixture != null) fixture.gameObject.SetActive(false);
                return;
            }
            if (!missing.Contains(BrokenLightPrefabPath)) missing.Add(BrokenLightPrefabPath);
        }

        GameObject lightObject = new GameObject("CorridorLight_" + state);
        lightObject.transform.SetParent(lightsRoot, false);
        lightObject.transform.localPosition = localPos;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.85f, 0.9f, 0.95f);
        light.intensity = (state == LightState.Steady ? 1.6f : 2.0f) * strength;
        light.range = 8f;
        light.shadows = LightShadows.Soft;

        System.Type flickerType = FindType("RoomLightFlicker");
        if (flickerType == null) return;

        SerializedObject so = new SerializedObject(lightObject.AddComponent(flickerType));
        bool strong = state == LightState.Broken;
        bool steady = state == LightState.Steady;
        SetFloat(so, "noiseAmount", steady ? 0.1f : (strong ? 0.7f : 0.45f));
        SetFloat(so, "noiseSpeed", steady ? 6f : (strong ? 22f : 14f));
        SetBool(so, "blackouts", true);
        SetVector2(so, "blackoutInterval", steady ? new Vector2(8f, 20f) : (strong ? new Vector2(0.5f, 2.5f) : new Vector2(1.5f, 5f)));
        SetVector2(so, "blackoutDuration", steady ? new Vector2(0.1f, 0.3f) : new Vector2(0.05f, 0.6f));
        SetFloat(so, "blackoutChance", steady ? 0.5f : 0.9f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddPlainLight(string lightName, Vector3 localPos, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(lightsRoot, false);
        lightObject.transform.localPosition = localPos;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    // ---------------- 化け物 ----------------

    private static void BuildMonster()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
        if (prefab == null)
        {
            missing.Add(MonsterPrefabPath);
            return;
        }

        // 動かす用の入れ物(歩くのも、向きを変えるのも、入れ物で行う。+Z が前)
        GameObject holder = new GameObject("Monster");
        holder.transform.SetParent(root, false);

        GameObject monster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
        monster.transform.localPosition = Vector3.zero;
        monster.transform.localRotation = Quaternion.identity;

        // 敵のAIなどは、タイトルではいらないので外す
        foreach (MonoBehaviour mb in monster.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.DestroyImmediate(mb);
        }
        foreach (UnityEngine.AI.NavMeshAgent agent in monster.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
        {
            Object.DestroyImmediate(agent);
        }
        foreach (Collider c in monster.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(c);
        }
        foreach (Rigidbody rb in monster.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.DestroyImmediate(rb);
        }

        // 歩きのアニメーション(くり返し)
        Animator animator = monster.GetComponentInChildren<Animator>();
        if (animator == null) animator = monster.AddComponent<Animator>();
        animator.applyRootMotion = false;   // 進むのはスクリプトで行う
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        RuntimeAnimatorController walk = GetWalkController();
        if (walk != null)
        {
            animator.runtimeAnimatorController = walk;
        }
        else
        {
            RuntimeAnimatorController fallback = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MonsterControllerPath);
            if (fallback != null) animator.runtimeAnimatorController = fallback;
            missing.Add(WalkFbxPath + "(歩きのアニメーションを作れませんでした)");
        }

        foreach (Renderer r in monster.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        // 大きさをそろえて、足を床につける
        Bounds b;
        if (TryGetBounds(monster, out b) && b.size.y > 0.01f)
        {
            monster.transform.localScale *= MonsterHeight / b.size.y;
            TryGetBounds(monster, out b);
            monster.transform.position += new Vector3(holder.transform.position.x - b.center.x,
                holder.transform.position.y - b.min.y, holder.transform.position.z - b.center.z);
        }

        // 顔のあたりの、赤い弱い光(目が光っているように見える)
        GameObject eyes = new GameObject("EyeGlow");
        eyes.transform.SetParent(holder.transform, false);
        eyes.transform.localPosition = new Vector3(0f, MonsterHeight * 0.88f, 0.35f);
        Light eyeLight = eyes.AddComponent<Light>();
        eyeLight.type = LightType.Point;
        eyeLight.color = new Color(1f, 0.05f, 0.02f);
        eyeLight.intensity = 0.8f;
        eyeLight.range = 1.4f;
        eyeLight.shadows = LightShadows.None;

        // 道のり: 十字路の真ん中を、左の廊下の中から、右の廊下の中まで
        float crossZ = CrossZ + Module * 0.5f;
        Vector3 start = new Vector3(leftWallX - 4.5f, 0f, crossZ);
        Vector3 end = new Vector3(rightWallX + 4.5f, 0f, crossZ);
        holder.transform.localPosition = start;
        holder.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        System.Type walkType = FindType("TitleMonsterWalk");
        if (walkType != null)
        {
            SerializedObject so = new SerializedObject(holder.AddComponent(walkType));
            SetVector3(so, "startPosition", start);
            SetVector3(so, "endPosition", end);
            SetVector3(so, "lookDirection", new Vector3(0f, 0f, -1f));
            SerializedProperty anim = so.FindProperty("animator");
            if (anim != null) anim.objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            missing.Add("TitleMonsterWalk.cs (化け物は、歩かずに止まっています)");
        }
    }

    /// <summary>
    /// Shadowlop_WALK.fbx の歩きのアニメーションを、くり返し再生する Animator Controller を作る。
    /// (fbx の設定の Loop Time をオンにする)
    /// </summary>
    private static RuntimeAnimatorController GetWalkController()
    {
        RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WalkControllerPath);
        if (existing != null) return existing;

        ModelImporter importer = AssetImporter.GetAtPath(WalkFbxPath) as ModelImporter;
        if (importer == null) return null;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation c in clips)
        {
            if (!c.loopTime)
            {
                c.loopTime = true;
                changed = true;
            }
        }
        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        AnimationClip clip = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(WalkFbxPath))
        {
            AnimationClip ac = o as AnimationClip;
            if (ac != null && !ac.name.StartsWith("__preview__"))
            {
                clip = ac;
                break;
            }
        }
        if (clip == null) return null;

        EnsureFolder(Path.GetDirectoryName(WalkControllerPath).Replace('\\', '/'));
        return UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(WalkControllerPath, clip);
    }

    private static void SetVector3(SerializedObject so, string field, Vector3 value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.vector3Value = value;
    }

    private static Material GetShadowMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.01f, 0.01f, 0.012f));
        mat.color = new Color(0.01f, 0.01f, 0.012f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.15f);

        EnsureFolder(Path.GetDirectoryName(ShadowMaterialPath).Replace('\\', '/'));
        AssetDatabase.CreateAsset(mat, ShadowMaterialPath);
        return mat;
    }

    // ---------------- 血 ----------------

    private static void BuildBlood()
    {
        System.Type decor = FindType("HorrorDecor");
        if (decor == null)
        {
            missing.Add("HorrorDecor.cs (血は置きませんでした)");
            return;
        }

        // 化け物のいる角へ、引きずった跡と、壁の手形
        CallFloor(decor, "Blood_Smear_01", 3.6f, 22f, -95f, 11f, 1.8f);
        CallFloor(decor, "Blood_Splat_02", 2.2f, 10f, 40f, 1.4f, 1.4f);
        CallFloor(decor, "Blood_Splat_03", 4.6f, CrossZ + 2.4f, 0f, 2.6f, 2.6f);
        CallWall(decor, "Blood_Hand_01", new Vector3(rightWallX, 1.4f, 18f), Vector3.left, 0.8f, 0.8f, 10f);
        CallWall(decor, "Blood_Drip_01", new Vector3(leftWallX, 2.0f, 12f), Vector3.right, 1.6f, 1.6f, 0f);
    }

    private static void CallFloor(System.Type decor, string kind, float x, float z, float yaw, float w, float l)
    {
        var m = decor.GetMethod("SpawnBloodFloor");
        if (m == null) return;
        m.Invoke(null, new object[] { bloodRoot, kind, x, z, 0f, yaw, w, l });
    }

    private static void CallWall(System.Type decor, string kind, Vector3 pos, Vector3 normal, float w, float h, float roll)
    {
        var m = decor.GetMethod("SpawnBloodWall");
        if (m == null) return;
        m.Invoke(null, new object[] { bloodRoot, kind, pos, normal, w, h, roll });
    }

    // ---------------- カメラと暗さ ----------------

    private static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            missing.Add("Main Camera");
            return;
        }

        Undo.RecordObject(cam.transform, "タイトルの背景を作る");
        Undo.RecordObject(cam, "タイトルの背景を作る");

        // 少し左に寄せて、右の曲がり角が見えるようにする(下書きの絵と同じ、左の壁が手前に大きく見える構図)
        float camX = Mathf.Lerp(leftWallX, rightWallX, 0.38f);
        cam.transform.position = root.TransformPoint(new Vector3(camX, 1.6f, 0.8f));
        cam.transform.rotation = Quaternion.Euler(2f, 3f, 0f);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 150f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        // 少しだけゆれる(TitleCameraMove があれば)
        System.Type moveType = FindType("TitleCameraMove");
        if (moveType != null && cam.GetComponent(moveType) == null)
        {
            Undo.AddComponent(cam.gameObject, moveType);
        }
    }

    private static void SetupDarkness()
    {
        // 霧(HorrorFog があれば使う。なければ、直接設定する)
        GameObject fogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/HorrorFog.prefab");
        System.Type fogType = FindType("HorrorFog");
        if (fogPrefab != null && fogType != null && Object.FindFirstObjectByType(fogType, FindObjectsInactive.Include) == null)
        {
            GameObject fog = (GameObject)PrefabUtility.InstantiatePrefab(fogPrefab);
            Undo.RegisterCreatedObjectUndo(fog, "タイトルの背景を作る");
            fog.transform.position = root.position;
            SerializedObject so = new SerializedObject(fog.GetComponent(fogType));
            SetFloat(so, "density", 0.045f);
            SetProp(so, "mistFollowsCamera", true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else if (fogType == null)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.025f);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.035f);
        }

        // 太陽の光は消す
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional)
            {
                Undo.RecordObject(l.gameObject, "タイトルの背景を作る");
                l.gameObject.SetActive(false);
            }
        }
    }

    private static void HideOldBackground()
    {
        GameObject bg = GameObject.Find("BackgroundImage");
        if (bg != null)
        {
            Undo.RecordObject(bg, "タイトルの背景を作る");
            bg.SetActive(false);
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
        foreach (string guid in AssetDatabase.FindAssets(kitName + " t:Prefab"))
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

    private static GameObject Spawn(string kitName, Transform parent, Vector3 localPos, float yaw)
    {
        GameObject prefab = LoadKit(kitName);
        if (prefab == null) return null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return instance;
    }

    private static bool TryGetBounds(GameObject target, out Bounds result)
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

    private static void SetLayerDefault(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 0;
        }
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
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.floatValue = value;
    }

    private static void SetBool(SerializedObject so, string field, bool value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.boolValue = value;
    }

    private static void SetProp(SerializedObject so, string field, bool value)
    {
        SetBool(so, field, value);
    }

    private static void SetVector2(SerializedObject so, string field, Vector2 value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.vector2Value = value;
    }
}