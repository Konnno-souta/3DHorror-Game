#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// シーンに霧を入れる。
/// メニュー: Tools / Horror / 霧をシーンに入れる
///
///   1. Assets/Prefab/HorrorFog.prefab を作る(なければ)。
///      HorrorFog(距離の霧) と、子の GroundMist(地面をはう霧のパーティクル) が入っている。
///   2. 今のシーンに、1つ置く。すでにあれば、それを選ぶだけ。
///   3. Main Camera の背景を、霧と同じ暗い色にする(空が明るく見えないように)。
///
/// ほかのシーンにも霧を出したいときは、そのシーンを開いて、もう一度このメニューを実行する。
/// (または HorrorFog.prefab を、シーンにドラッグする)
/// </summary>
public static class HorrorFogSetup
{
    private const string PrefabFolder = "Assets/Prefab";
    private const string PrefabPath = "Assets/Prefab/HorrorFog.prefab";

    [MenuItem("Tools/Horror/霧をシーンに入れる")]
    public static void AddFogToScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("霧", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        System.Type fogType = HorrorDecor.FindType("HorrorFog");
        if (fogType == null)
        {
            EditorUtility.DisplayDialog("霧",
                "HorrorFog.cs が見つかりません。\n"
                + "zip の Assets/Script/Horror/HorrorFog.cs を、プロジェクトに入れてください。", "OK");
            return;
        }

        Component existing = HorrorDecor.FindFogInScene();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            EditorUtility.DisplayDialog("霧",
                "このシーンには、もう霧(" + existing.name + ")があります。\n"
                + "濃さは、Inspector の Density で変えられます。", "OK");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            prefab = CreatePrefab(fogType);
            if (prefab == null) return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "霧をシーンに入れる");
        instance.transform.position = Vector3.zero;

        SetCameraBackground();

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);

        string message = "霧を入れました。\n\n"
            + "シーン : HorrorFog\n"
            + "Prefab : " + PrefabPath + "\n\n"
            + "濃さ : HorrorFog の Density(0.03 でうっすら、0.08 でかなり濃い)\n"
            + "地面の霧 : 再生中は、プレイヤーの足もとについていきます。";


        EditorUtility.DisplayDialog("霧", message, "OK");
    }

    /// <summary>
    /// 霧を作り直す。シーンの霧、HorrorFog.prefab、霧のマテリアルを消して、新しい設定で入れ直す。
    /// 地面の霧が四角い板に見えるときに使う。
    /// </summary>
    [MenuItem("Tools/Horror/霧を作り直す")]
    public static void RebuildFog()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("霧", "再生を止めてから、もう一度実行してください。", "OK");
            return;
        }

        System.Type fogType = HorrorDecor.FindType("HorrorFog");
        if (fogType != null)
        {
            Object[] found = Object.FindObjectsByType(fogType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Object o in found)
            {
                Component c = o as Component;
                if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
            }
        }

        AssetDatabase.DeleteAsset(PrefabPath);
        AssetDatabase.DeleteAsset(HorrorDecor.MaterialFolder + "/Fog_Mist.mat");
        AssetDatabase.Refresh();

        AddFogToScene();
    }

    // ---------------- プレハブを作る ----------------

    private static GameObject CreatePrefab(System.Type fogType)
    {
        GameObject root = new GameObject("HorrorFog");
        root.AddComponent(fogType);

        GameObject mistObject = new GameObject("GroundMist");
        mistObject.transform.SetParent(root.transform, false);
        mistObject.transform.localPosition = new Vector3(0f, 0.15f, 0f);

        ParticleSystem mist = mistObject.AddComponent<ParticleSystem>();
        SetupGroundMist(mist);

        HorrorDecor.EnsureFolder(PrefabFolder);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        if (saved == null)
        {
            EditorUtility.DisplayDialog("霧", "HorrorFog.prefab を保存できませんでした。", "OK");
        }

        return saved;
    }

    /// <summary>
    /// 地面をはう霧。床と平行な、大きくうすい板を、ゆっくり流す。
    /// </summary>
    private static void SetupGroundMist(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.72f, 0.75f, 0.05f),
            new Color(0.78f, 0.8f, 0.84f, 0.12f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 220;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 0.15f, 30f);   // 床のすぐ上だけ(物を横切る線が見えにくい)

        // 出てくるときと、消えるときに、ふわっと
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.3f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        // ゆっくり横に流れる
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.1f);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sharedMaterial = HorrorDecor.GetFogMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 5f;
    }

    // ---------------- カメラ ----------------

    private static void SetCameraBackground()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Undo.RecordObject(cam, "霧をシーンに入れる");
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.11f, 0.12f, 0.12f);
        EditorUtility.SetDirty(cam);
    }
}
#endif