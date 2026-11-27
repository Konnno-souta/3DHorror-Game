using UnityEngine;

/// <summary>
/// 敵が近いほど、心臓の音が大きく・速くなる。追われている時は最大に近くなる。
/// プレイヤーに付ける。
/// </summary>
public class HeartbeatSE : MonoBehaviour
{
    [Tooltip("この距離より近いと最大")]
    [SerializeField] private float nearDistance = 4f;
    [Tooltip("この距離より遠いと聞こえない")]
    [SerializeField] private float farDistance = 18f;
    [Tooltip("追われている時の最低の強さ")]
    [SerializeField, Range(0f, 1f)] private float chaseMinStrength = 0.7f;
    [SerializeField] private float pitchSlow = 0.9f;
    [SerializeField] private float pitchFast = 1.35f;
    [SerializeField] private float fadeSpeed = 0.6f;

    private AudioSource source;
    private ShadowlopEnemyAI[] enemies = new ShadowlopEnemyAI[0];
    private float refreshTimer;
    private float strength;

    private void Start()
    {
        source = SEManager.CreateLoopSource(gameObject, SEManager.Bank.heartbeat, false);
    }

    private void Update()
    {
        if (source == null) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 1f;
            enemies = FindObjectsByType<ShadowlopEnemyAI>(FindObjectsSortMode.None);
        }

        float nearest = float.MaxValue;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || !enemies[i].isActiveAndEnabled) continue;
            float d = Vector3.Distance(transform.position, enemies[i].transform.position);
            if (d < nearest) nearest = d;
        }

        float target = 0f;
        if (nearest < float.MaxValue)
        {
            target = 1f - Mathf.InverseLerp(nearDistance, farDistance, nearest);
        }
        if (EnemySE.AnyChasing) target = Mathf.Max(target, chaseMinStrength);

        strength = Mathf.MoveTowards(strength, target, fadeSpeed * Time.unscaledDeltaTime);
        source.pitch = Mathf.Lerp(pitchSlow, pitchFast, strength);
        SEManager.FadeLoop(source, strength * SEManager.Bank.heartbeat.volume * SEManager.SEVolume, 2f);
    }
}
