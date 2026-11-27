using UnityEngine;

/// <summary>
/// シーン全体に霧を出す。シーンに1つ置くだけでよい(Assets/Prefab/HorrorFog.prefab)。
///
///   1. 距離の霧 : Unity の Fog(Lighting の Environment にある霧)をオンにする。
///                 遠くの物ほど、霧の色に溶けて見えなくなる。部屋も廊下も、シーンのどこでも効く。
///   2. 地面の霧 : 子の ParticleSystem(GroundMist)を、プレイヤーの足もとについてこさせる。
///                 床の近くを、うすい霧がゆっくり流れる。電灯の近くだけ、白く浮かんで見える。
///
/// エディタでも(再生していなくても)霧が見える。
/// このオブジェクトを消すか、オフにすると、霧は元の設定にもどる。
/// </summary>
[ExecuteAlways]
public class HorrorFog : MonoBehaviour
{
    [Header("距離の霧")]
    [Tooltip("霧の色。暗い場所では、暗い色のほうが自然に見える")]
    [SerializeField] private Color fogColor = new Color(0.11f, 0.12f, 0.12f);

    [Tooltip("Exponential は、近くはうすく、遠くほど急に濃くなる")]
    [SerializeField] private FogMode fogMode = FogMode.Exponential;

    [Tooltip("霧の濃さ(Exponential のとき)。0.03 でうっすら、0.08 でかなり濃い")]
    [SerializeField, Range(0f, 0.3f)] private float density = 0.055f;

    [Tooltip("Linear のとき、霧が始まる距離")]
    [SerializeField] private float linearStart = 2f;

    [Tooltip("Linear のとき、完全に見えなくなる距離")]
    [SerializeField] private float linearEnd = 25f;

    [Header("まわりの明るさ(暗くするとホラーらしくなる)")]
    [Tooltip("電灯が当たっていない所の明るさを、下げる(Lighting の Environment Lighting)")]
    [SerializeField] private bool darkenAmbient = true;

    [Tooltip("電灯が当たっていない所の色。黒に近いほど暗い")]
    [SerializeField] private Color ambientColor = new Color(0.035f, 0.04f, 0.045f);

    [Tooltip("金属などに映りこむ、まわりの景色の強さ。下げると、物が白っぽく光らなくなる")]
    [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 0.25f;

    [Header("霧のゆらぎ(再生中だけ)")]
    [Tooltip("濃さを、ゆっくり上げ下げする")]
    [SerializeField] private bool breathe = true;

    [Tooltip("ゆらぐ大きさ(0.2 なら、濃さが 20% 上下する)")]
    [SerializeField, Range(0f, 0.6f)] private float breatheAmount = 0.2f;

    [Tooltip("ゆらぐ速さ")]
    [SerializeField] private float breatheSpeed = 0.12f;

    [Header("地面の霧")]
    [Tooltip("子の ParticleSystem。空なら、子から探す")]
    [SerializeField] private ParticleSystem groundMist;

    [Tooltip("地面の霧を、プレイヤー(Tag が Player)の足もとについてこさせる。いなければカメラ")]
    [SerializeField] private bool mistFollowsCamera = true;

    [Tooltip("霧を出す高さ(床からの高さ)")]
    [SerializeField] private float mistHeight = 0.15f;

    [Tooltip("足場の上にいる時も、霧を一番下の床にためる")]
    [SerializeField] private bool mistStaysOnGround = true;

    [Tooltip("下の床を探す深さ(m)")]
    [SerializeField] private float mistSearchDepth = 20f;

    [Tooltip("カメラが、この距離より動いたら、霧の場所を動かす")]
    [SerializeField] private float followStep = 0.5f;

    // 元の設定(消したときにもどす)
    private bool savedFog;
    private Color savedColor;
    private FogMode savedMode;
    private float savedDensity;
    private float savedStart;
    private float savedEnd;
    private bool saved;
    private UnityEngine.Rendering.AmbientMode savedAmbientMode;
    private Color savedAmbientLight;
    private float savedReflection;

    private float noiseSeed;
    private Transform followTarget;

    private void OnEnable()
    {
        if (!saved)
        {
            savedFog = RenderSettings.fog;
            savedColor = RenderSettings.fogColor;
            savedMode = RenderSettings.fogMode;
            savedDensity = RenderSettings.fogDensity;
            savedStart = RenderSettings.fogStartDistance;
            savedEnd = RenderSettings.fogEndDistance;
            savedAmbientMode = RenderSettings.ambientMode;
            savedAmbientLight = RenderSettings.ambientLight;
            savedReflection = RenderSettings.reflectionIntensity;
            saved = true;
        }

        noiseSeed = Random.Range(0f, 100f);

        if (groundMist == null)
        {
            groundMist = GetComponentInChildren<ParticleSystem>(true);
        }

        ApplyFog(density);
    }

    private void OnDisable()
    {
        if (!saved) return;

        RenderSettings.fog = savedFog;
        RenderSettings.fogColor = savedColor;
        RenderSettings.fogMode = savedMode;
        RenderSettings.fogDensity = savedDensity;
        RenderSettings.fogStartDistance = savedStart;
        RenderSettings.fogEndDistance = savedEnd;
        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbientLight;
        RenderSettings.reflectionIntensity = savedReflection;
        saved = false;
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            ApplyFog(density);
        }
    }

    private void Update()
    {
        float current = density;

        if (Application.isPlaying && breathe)
        {
            float n = Mathf.PerlinNoise(noiseSeed, Time.time * breatheSpeed);
            current = density * (1f + (n * 2f - 1f) * breatheAmount);
        }

        ApplyFog(current);

        if (Application.isPlaying && mistFollowsCamera)
        {
            FollowCamera();
        }
    }

    private void ApplyFog(float currentDensity)
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = currentDensity;
        RenderSettings.fogStartDistance = linearStart;
        RenderSettings.fogEndDistance = linearEnd;

        if (darkenAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.reflectionIntensity = reflectionIntensity;
        }
    }

    private void FollowCamera()
    {
        if (groundMist == null) return;

        // プレイヤー(Tag が Player)の足もとについていく。いなければ、カメラ
        if (followTarget == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                followTarget = player.transform;
            }
            else if (Camera.main != null)
            {
                followTarget = Camera.main.transform;
            }
        }

        if (followTarget == null) return;

        // 床の高さは、真下へレイを飛ばして調べる。プレイヤー自身の当たり判定は、とばす
        Vector3 origin = followTarget.position + Vector3.up * 0.5f;
        float floorY = followTarget.position.y;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, mistSearchDepth, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        float lowest = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(followTarget.root)) continue;
            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                floorY = hit.point.y;
            }
            if (hit.point.y < lowest) lowest = hit.point.y;
        }

        // 足場(キャットウォークなど)の上にいる時も、霧は一番下の床にためる。
        // (足場の高さに霧を出すと、下の階の上に、霧の板が浮いて見えるため)
        if (mistStaysOnGround && lowest < float.MaxValue)
        {
            floorY = lowest;
        }

        Vector3 target = new Vector3(followTarget.position.x, floorY + mistHeight, followTarget.position.z);
        Transform mistTransform = groundMist.transform;

        if ((mistTransform.position - target).sqrMagnitude > followStep * followStep)
        {
            mistTransform.position = target;
        }
    }

    /// <summary>
    /// ほかのスクリプトから、霧の濃さを変える(例: 敵が近いと濃くする)。
    /// </summary>
    public void SetDensity(float newDensity)
    {
        density = Mathf.Max(0f, newDensity);
    }

    public float Density
    {
        get { return density; }
    }
}