using UnityEngine;

/// <summary>
/// 部屋のライトの音: ジーという音(ループ)と、消えた時/点いた時のバチッという音。
/// Light のあるオブジェクト(RoomLightFlicker など)に付ける。
/// 明るさを見て音を出すので、ライトのスクリプトは変えなくてよい。
/// ※ BrokenLight は自分で音を鳴らすので、これは付けなくてよい(Tools/SE/2 が音を入れる)。
/// </summary>
[RequireComponent(typeof(Light))]
public class LightSE : MonoBehaviour
{
    [SerializeField] private bool hum = true;
    [Tooltip("ジー音が聞こえる距離")]
    [SerializeField] private float humDistance = 9f;
    [Tooltip("消えた時にバチッと鳴る確率")]
    [SerializeField, Range(0f, 1f)] private float offSoundChance = 0.8f;
    [Tooltip("点き直した時に鳴る確率")]
    [SerializeField, Range(0f, 1f)] private float onSoundChance = 0.5f;
    [Tooltip("音が鳴る距離")]
    [SerializeField] private float popDistance = 18f;

    private Light lt;
    private AudioSource humSource;
    private float maxIntensity;
    private bool wasOff;
    private float popCooldown;

    private void Start()
    {
        lt = GetComponent<Light>();
        maxIntensity = Mathf.Max(0.01f, lt.intensity);
        if (hum && SEManager.Bank.lightHum.HasClip)
        {
            humSource = SEManager.CreateLoopSource(gameObject, SEManager.Bank.lightHum, true, humDistance);
        }
    }

    private void Update()
    {
        if (lt.intensity > maxIntensity) maxIntensity = lt.intensity;
        float ratio = lt.enabled ? Mathf.Clamp01(lt.intensity / maxIntensity) : 0f;
        bool off = ratio < 0.08f;

        popCooldown -= Time.deltaTime;
        if (off != wasOff && popCooldown <= 0f)
        {
            float chance = off ? offSoundChance : onSoundChance;
            if (Random.value < chance)
            {
                SoundGroup g = off ? SEManager.Bank.lightSpark : SEManager.Bank.lightFlicker;
                if (!g.HasClip) g = SEManager.Bank.lightSpark;
                SEManager.PlayAt(g, transform.position, popDistance, 0.8f);
                popCooldown = 0.08f;
            }
        }
        wasOff = off;

        if (humSource != null)
        {
            float vol = SEManager.SilenceWorld ? 0f : SEManager.Bank.lightHum.volume * SEManager.SEVolume * ratio;
            humSource.volume = vol;
            if (vol > 0.001f && !humSource.isPlaying) { humSource.time = Random.Range(0f, humSource.clip.length * 0.9f); humSource.Play(); }
            else if (vol <= 0.001f && humSource.isPlaying) humSource.Stop();
        }
    }
}
