using System.Collections;
using UnityEngine;

/// <summary>
/// 部屋の電灯を、ゆらゆら揺らして、ときどき消す。
/// Light と同じオブジェクトに付ける。
/// </summary>
[RequireComponent(typeof(Light))]
public class RoomLightFlicker : MonoBehaviour
{
    [Header("ゆらぎ")]
    [Tooltip("明るさの揺れ幅(元の明るさに対する割合)")]
    [SerializeField, Range(0f, 1f)] private float noiseAmount = 0.15f;

    [Tooltip("揺れの速さ")]
    [SerializeField] private float noiseSpeed = 8f;

    [Header("ときどき消える")]
    [SerializeField] private bool blackouts = true;

    [Tooltip("次に消えるかどうかを決めるまでの秒数(最小、最大)")]
    [SerializeField] private Vector2 blackoutInterval = new Vector2(6f, 20f);

    [Tooltip("消えている秒数(最小、最大)")]
    [SerializeField] private Vector2 blackoutDuration = new Vector2(0.15f, 0.6f);

    [Tooltip("決めたとき、実際に消える確率")]
    [SerializeField, Range(0f, 1f)] private float blackoutChance = 0.5f;

    private Light lightSource;
    private float baseIntensity;
    private float seed;
    private bool isOff;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.value * 100f;
    }

    private void OnEnable()
    {
        isOff = false;
        if (blackouts) StartCoroutine(BlackoutRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isOff = false;
        if (lightSource != null) lightSource.intensity = baseIntensity;
    }

    private void Update()
    {
        if (isOff)
        {
            lightSource.intensity = 0f;
            return;
        }

        float n = Mathf.PerlinNoise(seed, Time.time * noiseSpeed) - 0.5f;
        lightSource.intensity = baseIntensity * (1f + n * 2f * noiseAmount);
    }

    private IEnumerator BlackoutRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blackoutInterval.x, blackoutInterval.y));

            if (Random.value > blackoutChance) continue;

            int blinks = Random.Range(1, 4);
            for (int i = 0; i < blinks; i++)
            {
                isOff = true;
                yield return new WaitForSeconds(Random.Range(blackoutDuration.x, blackoutDuration.y));
                isOff = false;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
        }
    }
}
