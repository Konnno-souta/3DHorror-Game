using System.Collections;
using UnityEngine;

/// <summary>
/// 壊れかけの電灯。
/// ふだんは弱くちらついて、ときどきバチバチと点滅して、たまに長く消える。
/// 点滅のたびに、火花や音を出せる(どちらも任意)。
/// Light と同じ階層に付ける(Light は Target Light に入れる)。
/// </summary>
public class BrokenLight : MonoBehaviour
{
    // ---------------- 部品 ----------------

    [Header("部品")]
    [SerializeField] private Light targetLight;

    [Tooltip("電球の見た目。点滅に合わせて、光り方(Emission)を変える")]
    [SerializeField] private Renderer[] bulbRenderers;

    [Tooltip("バチバチのときに出す火花(任意)")]
    [SerializeField] private ParticleSystem sparks;

    [Tooltip("ジーという音を出すAudioSource(任意)。Clip を入れて、Loop をオンにする")]
    [SerializeField] private AudioSource buzzSource;

    [Tooltip("バチッという音(任意)")]
    [SerializeField] private AudioClip popClip;

    // ---------------- 明るさ ----------------

    [Header("明るさ")]
    [Tooltip("ふだんのちらつきの強さ(0で、ちらつかない)")]
    [SerializeField, Range(0f, 1f)] private float idleFlutter = 0.25f;

    [Tooltip("ふだんの明るさ(1で、元の明るさ)")]
    [SerializeField, Range(0.1f, 1f)] private float idleLevel = 0.8f;

    [Tooltip("光り方(Emission)の強さ")]
    [SerializeField] private float emissionIntensity = 2f;

    [SerializeField] private string emissionProperty = "_EmissionColor";

    // ---------------- 点滅のしかた ----------------

    [Header("点滅のしかた")]
    [Tooltip("バチバチが始まるまでの秒数(最小、最大)")]
    [SerializeField] private Vector2 idleSeconds = new Vector2(1.5f, 6f);

    [Tooltip("バチバチの回数(最小、最大)")]
    [SerializeField] private Vector2Int burstCount = new Vector2Int(2, 7);

    [Tooltip("バチバチ中、消えている秒数(最小、最大)")]
    [SerializeField] private Vector2 burstOffSeconds = new Vector2(0.02f, 0.18f);

    [Tooltip("バチバチ中、点いている秒数(最小、最大)")]
    [SerializeField] private Vector2 burstOnSeconds = new Vector2(0.02f, 0.12f);

    [Tooltip("バチバチのあと、長く消える確率")]
    [SerializeField, Range(0f, 1f)] private float longOutChance = 0.3f;

    [Tooltip("長く消える秒数(最小、最大)")]
    [SerializeField] private Vector2 longOutSeconds = new Vector2(0.6f, 2.5f);

    [Tooltip("バチバチのとき、火花が出る確率")]
    [SerializeField, Range(0f, 1f)] private float sparkChance = 0.5f;

    [Header("音の大きさ")]
    [SerializeField, Range(0f, 1f)] private float buzzVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float popVolume = 0.6f;

    // ---------------- 状態 ----------------

    private float baseIntensity;
    private float level = 1f;
    private float seed;
    private bool inBurst;
    private float appliedLevel = -1f;

    private MaterialPropertyBlock block;
    private Color[] baseEmission;
    private int emissionId;

    // ---------------- 初期化 ----------------

    private void Awake()
    {
        if (targetLight == null) targetLight = GetComponentInChildren<Light>();
        if (targetLight != null) baseIntensity = targetLight.intensity;

        seed = Random.value * 100f;
        emissionId = Shader.PropertyToID(emissionProperty);
        block = new MaterialPropertyBlock();

        if (bulbRenderers == null) bulbRenderers = new Renderer[0];
        baseEmission = new Color[bulbRenderers.Length];
        for (int i = 0; i < bulbRenderers.Length; i++)
        {
            baseEmission[i] = Color.white;
            if (bulbRenderers[i] == null) continue;

            Material mat = bulbRenderers[i].sharedMaterial;
            if (mat != null && mat.HasProperty(emissionId))
            {
                Color c = mat.GetColor(emissionId);
                if (c.maxColorComponent > 0.01f) baseEmission[i] = c;
            }
        }
    }

    private void OnEnable()
    {
        level = idleLevel;
        inBurst = false;
        appliedLevel = -1f;

        if (buzzSource != null && buzzSource.clip != null && !buzzSource.isPlaying)
        {
            buzzSource.loop = true;
            buzzSource.Play();
        }

        StartCoroutine(Routine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (targetLight != null) targetLight.intensity = baseIntensity;
        ApplyEmission(1f);
        if (buzzSource != null) buzzSource.Stop();
    }

    // ---------------- 毎フレーム ----------------

    private void Update()
    {
        float shown = level;

        if (!inBurst && level > 0f)
        {
            // ふだんの、弱いちらつき
            float n = Mathf.PerlinNoise(seed, Time.time * 11f) - 0.5f;
            shown = Mathf.Clamp01(level * (1f + n * 2f * idleFlutter));
        }

        if (targetLight != null) targetLight.intensity = baseIntensity * shown;
        if (!Mathf.Approximately(shown, appliedLevel)) ApplyEmission(shown);
        appliedLevel = shown;

        if (buzzSource != null) buzzSource.volume = buzzVolume * shown;
    }

    private void ApplyEmission(float amount)
    {
        for (int i = 0; i < bulbRenderers.Length; i++)
        {
            Renderer r = bulbRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(block);
            block.SetColor(emissionId, baseEmission[i] * (emissionIntensity * amount));
            r.SetPropertyBlock(block);
        }
    }

    // ---------------- 点滅の流れ ----------------

    private IEnumerator Routine()
    {
        while (true)
        {
            // ふだん
            inBurst = false;
            level = idleLevel;
            yield return new WaitForSeconds(Random.Range(idleSeconds.x, idleSeconds.y));

            // バチバチ
            inBurst = true;
            int count = Random.Range(burstCount.x, burstCount.y + 1);
            bool sparked = false;

            for (int i = 0; i < count; i++)
            {
                level = 0f;
                yield return new WaitForSeconds(Random.Range(burstOffSeconds.x, burstOffSeconds.y));

                level = Random.Range(0.6f, 1f);
                PlayPop();
                if (!sparked && sparks != null && Random.value < sparkChance)
                {
                    sparks.Play();
                    sparked = true;
                }
                yield return new WaitForSeconds(Random.Range(burstOnSeconds.x, burstOnSeconds.y));
            }

            // 長く消える
            if (Random.value < longOutChance)
            {
                level = 0f;
                yield return new WaitForSeconds(Random.Range(longOutSeconds.x, longOutSeconds.y));

                // ぼうっと戻る
                level = idleLevel * 0.4f;
                yield return new WaitForSeconds(0.12f);
            }
        }
    }

    private void PlayPop()
    {
        if (popClip == null) return;
        AudioSource.PlayClipAtPoint(popClip, transform.position, popVolume);
    }
}
