using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// インベントリの「3D PREVIEW」専用のライト。
/// ItemDisplayCamera に付ける。ItemDisplay レイヤーだけを照らすので、ゲームの画面は明るくならない。
/// 真っ黒になる原因は2つ:
///  1. 霧(HorrorFog)が、このカメラにもかかっていた(アイテムが遠くにあるので、霧で真っ黒)
///  2. 霧の設定で周りの明るさを暗くしたので、ライトが無いと暗い
/// このスクリプトは、このカメラで描く時だけ霧を切り、専用のライトで照らす。
/// </summary>
[RequireComponent(typeof(Camera))]
public class ItemDisplayLighting : MonoBehaviour
{
    [Tooltip("アイテムが置かれる場所。空なら、子の DisplayPoint を探す")]
    [SerializeField] private Transform displayPoint;
    [SerializeField] private string displayLayerName = "ItemDisplay";

    [Tooltip("このカメラで描く時だけ、霧を切る")]
    [SerializeField] private bool disableFog = true;

    [Header("明るさ")]
    [SerializeField] private float keyIntensity = 2.2f;
    [SerializeField] private Color keyColor = new Color(1f, 0.93f, 0.82f);
    [SerializeField] private float fillIntensity = 0.9f;
    [SerializeField] private Color fillColor = new Color(0.6f, 0.72f, 1f);
    [SerializeField] private float rimIntensity = 2.5f;
    [SerializeField] private Color rimColor = new Color(1f, 0.55f, 0.35f);

    [Tooltip("ライトとアイテムの距離(アイテムの大きさの何倍か)")]
    [SerializeField] private float distance = 1.8f;
    [Tooltip("アイテムの大きさの目安(InventoryUI の Target Display Size と同じくらい)")]
    [SerializeField] private float itemSize = 1.5f;

    private Camera cam;
    private Light keyLight, fillLight, rimLight;
    private bool savedFog;
    private bool fogChanged;

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        RestoreFog();
    }

    private void OnBeginCamera(ScriptableRenderContext context, Camera c)
    {
        if (!disableFog || c != cam) return;
        savedFog = RenderSettings.fog;
        fogChanged = true;
        RenderSettings.fog = false;
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera c)
    {
        if (c != cam) return;
        RestoreFog();
    }

    private void RestoreFog()
    {
        if (!fogChanged) return;
        RenderSettings.fog = savedFog;
        fogChanged = false;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (displayPoint == null)
        {
            Transform t = transform.Find("DisplayPoint");
            if (t == null)
            {
                foreach (Transform c in GetComponentsInChildren<Transform>(true))
                {
                    if (c.name == "DisplayPoint") { t = c; break; }
                }
            }
            displayPoint = t;
        }

        int layer = LayerMask.NameToLayer(displayLayerName);
        int mask = layer >= 0 ? (1 << layer) : cam.cullingMask;

        keyLight = MakeLight("ItemLight_Key", keyColor, keyIntensity, mask);
        fillLight = MakeLight("ItemLight_Fill", fillColor, fillIntensity, mask);
        rimLight = MakeLight("ItemLight_Rim", rimColor, rimIntensity, mask);
    }

    private Light MakeLight(string objName, Color color, float intensity, int mask)
    {
        Transform old = transform.Find(objName);
        GameObject go = old != null ? old.gameObject : new GameObject(objName);
        go.transform.SetParent(transform, false);
        Light l = go.GetComponent<Light>();
        if (l == null) l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = itemSize * distance * 4f;
        l.shadows = LightShadows.None;
        l.cullingMask = mask;
        l.renderMode = LightRenderMode.ForcePixel;
        return l;
    }

    private void LateUpdate()
    {
        bool on = cam.enabled && cam.gameObject.activeInHierarchy;
        SetOn(keyLight, on);
        SetOn(fillLight, on);
        SetOn(rimLight, on);
        if (!on) return;

        Vector3 center = displayPoint != null ? displayPoint.position : transform.position + transform.forward * 3f;
        Vector3 toCam = (transform.position - center).normalized;
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        float d = itemSize * distance;

        // 左上の手前から(メイン)、右下の手前から(弱い補助)、後ろの上から(ふちを光らせる)
        Place(keyLight, center + (toCam * 1.0f - right * 0.8f + up * 0.9f).normalized * d);
        Place(fillLight, center + (toCam * 1.0f + right * 1.0f - up * 0.3f).normalized * d);
        Place(rimLight, center + (-toCam * 1.0f + right * 0.4f + up * 0.8f).normalized * d);

        float range = d * 3f;
        keyLight.range = range;
        fillLight.range = range;
        rimLight.range = range;
    }

    private static void Place(Light l, Vector3 pos)
    {
        if (l != null) l.transform.position = pos;
    }

    private static void SetOn(Light l, bool on)
    {
        if (l != null && l.enabled != on) l.enabled = on;
    }
}
