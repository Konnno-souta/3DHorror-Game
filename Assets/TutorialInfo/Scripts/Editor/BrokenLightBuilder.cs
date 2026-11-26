using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 壊れかけて点滅する電灯のPrefab(Assets/Prefab/BrokenLight.prefab)を作って、
/// Sceneビューの中心の真上の天井に、1つ置く。
///
/// 使い方:
///   Sceneビューで、電灯を付けたい廊下の床を、画面の中心に映す(Fキーで、選んだ物に寄せられる)。
///   メニュー Tools / Light / 壊れかけの点滅ライトを作る を実行する。
///   2つ目からは、Prefabを Hierarchy にドラッグして、Y の高さを、天井に合わせる。
/// </summary>
public static class BrokenLightBuilder
{
    private const string OutputFolder = "Assets/Prefab";
    private const string OutputPath = "Assets/Prefab/BrokenLight.prefab";
    private const string SparkMaterialPath = "Assets/Material/BrokenLight_Spark.mat";

    [MenuItem("Tools/Light/壊れかけの点滅ライトを作る")]
    public static void Build()
    {
        GameObject root = new GameObject("BrokenLight");

        // 見た目: キットの Hanging Light(基準点が、天井にくっつく側)
        GameObject fixture = null;
        GameObject fixturePrefab = FindKitPrefab("Hanging Light");
        if (fixturePrefab != null)
        {
            fixture = (GameObject)PrefabUtility.InstantiatePrefab(fixturePrefab, root.transform);
            fixture.name = "Fixture";
            fixture.transform.localPosition = Vector3.zero;
            fixture.transform.localRotation = Quaternion.identity;
        }

        // 電球と、光る場所
        Renderer[] bulbRenderers = FindBulbRenderers(fixture);
        Vector3 lightLocalPos = new Vector3(0f, -0.6f, 0f);
        if (bulbRenderers.Length > 0)
        {
            Bounds b = bulbRenderers[0].bounds;
            for (int i = 1; i < bulbRenderers.Length; i++) b.Encapsulate(bulbRenderers[i].bounds);
            lightLocalPos = root.transform.InverseTransformPoint(b.center);
        }

        // Light
        GameObject lightObject = new GameObject("Light");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = lightLocalPos;

        Light pointLight = lightObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = new Color(1f, 0.92f, 0.75f);
        pointLight.intensity = 4f;
        pointLight.range = 12f;
        pointLight.shadows = LightShadows.Soft;

        // 火花
        ParticleSystem sparks = CreateSparks(root.transform, lightLocalPos);

        // ジーという音(Clip は、あとで自分で入れる)
        GameObject audioObject = new GameObject("Buzz");
        audioObject.transform.SetParent(root.transform, false);
        audioObject.transform.localPosition = lightLocalPos;
        AudioSource buzz = audioObject.AddComponent<AudioSource>();
        buzz.playOnAwake = false;
        buzz.loop = true;
        buzz.spatialBlend = 1f;
        buzz.minDistance = 2f;
        buzz.maxDistance = 14f;

        // スクリプト(あれば)
        System.Type type = System.Type.GetType("BrokenLight, Assembly-CSharp");
        if (type != null)
        {
            Component comp = root.AddComponent(type);
            SerializedObject so = new SerializedObject(comp);
            so.FindProperty("targetLight").objectReferenceValue = pointLight;
            so.FindProperty("sparks").objectReferenceValue = sparks;
            so.FindProperty("buzzSource").objectReferenceValue = buzz;

            SerializedProperty bulbs = so.FindProperty("bulbRenderers");
            bulbs.arraySize = bulbRenderers.Length;
            for (int i = 0; i < bulbRenderers.Length; i++)
            {
                bulbs.GetArrayElementAtIndex(i).objectReferenceValue = bulbRenderers[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[BrokenLightBuilder] BrokenLight.cs が見つかりません。Assets/Script に入れて、もう一度実行してください。");
        }

        SetLayerDefault(root);

        // Prefabに保存
        if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets", "Prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, OutputPath, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();

        // 天井に置く
        string placeMessage = PlaceOnCeiling(root);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.RegisterCreatedObjectUndo(root, "壊れかけの点滅ライトを作る");

        string message = "壊れかけの点滅ライトを作りました。\n\nPrefab: " + OutputPath + "\n" + placeMessage;
        if (type == null) message += "\n\n(BrokenLight.cs がないので、点滅はしません)";
        if (fixture == null) message += "\n\n(キットの Hanging Light が見つからなかったので、Light だけです)";
        EditorUtility.DisplayDialog("Broken Light", message, "OK");
    }

    // ---------------- 部品づくり ----------------

    private static Renderer[] FindBulbRenderers(GameObject fixture)
    {
        if (fixture == null) return new Renderer[0];

        Renderer[] all = fixture.GetComponentsInChildren<Renderer>(true);
        System.Collections.Generic.List<Renderer> bulbs = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in all)
        {
            if (r.gameObject.name.ToLower().Contains("bulb")) bulbs.Add(r);
        }

        // 名前で見つからなければ、全部
        return bulbs.Count > 0 ? bulbs.ToArray() : all;
    }

    private static ParticleSystem CreateSparks(Transform parent, Vector3 localPos)
    {
        GameObject go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(1f, 0.75f, 0.3f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 10, 18) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 50f;
        shape.radius = 0.05f;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        Material sparkMat = GetSparkMaterial();
        if (sparkMat != null) psr.sharedMaterial = sparkMat;

        return ps;
    }

    private static Material GetSparkMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;

        if (!AssetDatabase.IsValidFolder("Assets/Material")) AssetDatabase.CreateFolder("Assets", "Material");

        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.4f));
        AssetDatabase.CreateAsset(mat, SparkMaterialPath);
        return mat;
    }

    private static void SetLayerDefault(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 0;
        }
    }

    private static GameObject FindKitPrefab(string prefabName)
    {
        string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("SciFi Warehouse Kit")) continue;
            if (Path.GetFileNameWithoutExtension(path) != prefabName) continue;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    // ---------------- 天井に置く ----------------

    private static string PlaceOnCeiling(GameObject root)
    {
        Vector3 pivot = Vector3.zero;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) pivot = view.pivot;

        Physics.SyncTransforms();

        // 1. 真上に光線を飛ばして、天井のColliderを探す
        RaycastHit hit;
        if (Physics.Raycast(pivot + Vector3.up * 0.2f, Vector3.up, out hit, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            root.transform.position = hit.point;
            return "天井(" + hit.collider.name + ")の下に置きました。";
        }

        // 2. Colliderがなければ、名前に Ceiling が付いた見た目を探す
        float bestY = float.MaxValue;
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer r in all)
        {
            if (r == null || r.transform.IsChildOf(root.transform)) continue;
            if (!r.gameObject.name.ToLower().Contains("ceiling")) continue;

            Bounds b = r.bounds;
            if (pivot.x < b.min.x || pivot.x > b.max.x) continue;
            if (pivot.z < b.min.z || pivot.z > b.max.z) continue;
            if (b.min.y < pivot.y + 0.5f) continue;

            if (b.min.y < bestY) bestY = b.min.y;
        }

        if (bestY < float.MaxValue)
        {
            root.transform.position = new Vector3(pivot.x, bestY, pivot.z);
            return "天井の見た目の下に置きました。";
        }

        // 3. 見つからなければ、とりあえず少し上
        root.transform.position = pivot + Vector3.up * 3f;
        return "天井が見つからなかったので、Sceneビューの中心の3m上に置きました。\nPosition の Y を、天井の高さに合わせてください。";
    }
}
