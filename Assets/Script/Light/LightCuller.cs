using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カメラから遠い電灯を、自動で消して、近づいたらつける(重さを減らす)。
/// 霧で見えない所の電灯まで計算していると、とても重くなるため。
/// シーンに1つ置くだけでよい(Tools / Optimize / ゲームを軽くする が自動で置く)。
///
///   ・太陽(Directional Light)と、カメラやプレイヤーに付いている電灯(懐中電灯など)は、さわらない
///   ・ほかのスクリプトが自分で消した電灯は、勝手につけない(このスクリプトが消した物だけ、つけ直す)
/// </summary>
public class LightCuller : MonoBehaviour
{
    [Tooltip("電灯の光が届く距離に、この距離を足した所より遠ければ、消す(m)")]
    [SerializeField] private float extraDistance = 12f;

    [Tooltip("これより遠い電灯は、光の届く距離に関係なく、消す(m)")]
    [SerializeField] private float maxDistance = 45f;

    [Tooltip("調べる間隔(秒)")]
    [SerializeField] private float checkInterval = 0.25f;

    [Tooltip("電灯の一覧を作り直す間隔(秒)。あとから出てきた電灯も、見つける")]
    [SerializeField] private float refreshInterval = 5f;

    private readonly List<Light> lights = new List<Light>();
    private readonly HashSet<Light> culledByMe = new HashSet<Light>();
    private float checkTimer;
    private float refreshTimer;
    private Transform viewer;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) Refresh();

        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f) return;
        checkTimer = checkInterval;

        if (viewer == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            viewer = cam.transform;
        }

        Vector3 eye = viewer.position;

        for (int i = lights.Count - 1; i >= 0; i--)
        {
            Light l = lights[i];
            if (l == null)
            {
                lights.RemoveAt(i);
                continue;
            }

            float limit = Mathf.Min(l.range + extraDistance, maxDistance);
            bool far = (l.transform.position - eye).sqrMagnitude > limit * limit;

            if (far)
            {
                if (l.enabled)
                {
                    l.enabled = false;
                    culledByMe.Add(l);
                }
            }
            else if (culledByMe.Contains(l))
            {
                l.enabled = true;
                culledByMe.Remove(l);
            }
        }
    }

    private void Refresh()
    {
        refreshTimer = refreshInterval;

        Camera cam = Camera.main;
        viewer = cam != null ? cam.transform : null;

        GameObject player = GameObject.FindWithTag("Player");

        lights.Clear();
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;
            if (viewer != null && l.transform.IsChildOf(viewer)) continue;
            if (player != null && l.transform.IsChildOf(player.transform)) continue;
            // インベントリの3D表示など、別のカメラ用の電灯はさわらない
            if (l.GetComponentInParent<Camera>() != null) continue;
            // ふつうの物(Default レイヤー)を照らさない、特別な電灯もさわらない
            if ((l.cullingMask & 1) == 0) continue;
            lights.Add(l);
        }

        // 一覧から外れた電灯で、自分が消していた物は元にもどす
        culledByMe.RemoveWhere(l =>
        {
            if (l == null) return true;
            if (lights.Contains(l)) return false;
            l.enabled = true;
            return true;
        });
    }

    private void OnDisable()
    {
        // このスクリプトを止めたら、消した電灯を全部もどす
        foreach (Light l in culledByMe)
        {
            if (l != null) l.enabled = true;
        }
        culledByMe.Clear();
    }
}
