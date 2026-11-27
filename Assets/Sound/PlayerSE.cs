using UnityEngine;

/// <summary>
/// プレイヤーの音: 足音(歩き/走り)、着地、息切れ、ダメージ、死亡、回復。
/// HorrorPlayerController と同じオブジェクトに付ける。元のスクリプトは変えなくてよい。
/// </summary>
public class PlayerSE : MonoBehaviour
{
    [Header("足音")]
    [Tooltip("歩きの足音の間隔(秒)")]
    [SerializeField] private float walkInterval = 0.55f;
    [Tooltip("走りの足音の間隔(秒)")]
    [SerializeField] private float runInterval = 0.32f;
    [Tooltip("この速さより遅いと足音を鳴らさない")]
    [SerializeField] private float minMoveSpeed = 0.4f;
    [Tooltip("これより長く空中にいたら、着地音を鳴らす(秒)")]
    [SerializeField] private float landAirTime = 0.35f;

    [Header("息切れ")]
    [Tooltip("スタミナがこの割合より少ないと、息が聞こえ始める")]
    [SerializeField, Range(0f, 1f)] private float breathStartRatio = 0.35f;
    [SerializeField] private float breathFadeSpeed = 0.8f;

    private CharacterController cc;
    private HorrorPlayerController pc;
    private PlayerHealth health;
    private AudioSource breathSource;

    private Vector3 lastPos;
    private float stepTimer;
    private float airTimer;
    private int lastHp = -1;
    private bool deadPlayed;

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        pc = GetComponent<HorrorPlayerController>();
        health = GetComponent<PlayerHealth>();
        if (health == null) health = GetComponentInParent<PlayerHealth>();
        if (health == null) health = FindFirstObjectByType<PlayerHealth>();

        breathSource = SEManager.CreateLoopSource(gameObject, SEManager.Bank.breathTired, false);
        lastPos = transform.position;
        if (health != null) lastHp = health.CurrentHealth;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateFootsteps(dt);
        UpdateBreath();
        UpdateHealth();
    }

    private void UpdateFootsteps(float dt)
    {
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        delta.y = 0f;
        float speed = delta.magnitude / dt;

        bool grounded = cc == null || cc.isGrounded;
        if (!grounded)
        {
            airTimer += dt;
            return;
        }

        if (airTimer > landAirTime)
        {
            SEManager.Play(SEManager.Bank.land);
            stepTimer = 0f;
        }
        airTimer = 0f;

        if (speed < minMoveSpeed)
        {
            stepTimer = Mathf.Min(stepTimer, 0.15f);
            return;
        }

        bool running = pc != null && pc.IsRunning;
        stepTimer -= dt;
        if (stepTimer <= 0f)
        {
            SoundGroup g = running ? SEManager.Bank.footstepRun : SEManager.Bank.footstepWalk;
            if (!g.HasClip) g = SEManager.Bank.footstepWalk;
            float pitchScale = running && g == SEManager.Bank.footstepWalk ? 1.12f : 1f;
            SEManager.Play(g, running ? 1.2f : 1f, pitchScale);
            stepTimer = running ? runInterval : walkInterval;
        }
    }

    private void UpdateBreath()
    {
        if (breathSource == null || pc == null) return;
        float target = 0f;
        if (pc.MaxStamina > 0f)
        {
            float ratio = pc.CurrentStamina / pc.MaxStamina;
            if (pc.IsExhausted) target = 1f;
            else if (ratio < breathStartRatio) target = 1f - ratio / breathStartRatio;
        }
        if (deadPlayed) target = 0f;
        float vol = target * SEManager.Bank.breathTired.volume * SEManager.SEVolume;
        SEManager.FadeLoop(breathSource, vol, breathFadeSpeed);
    }

    private void UpdateHealth()
    {
        if (health == null) return;
        int hp = health.CurrentHealth;
        if (lastHp < 0) lastHp = hp;

        if (hp < lastHp)
        {
            if (hp <= 0 && !deadPlayed)
            {
                deadPlayed = true;
                SEManager.Play(SEManager.Bank.damage);
                SEManager.Play(SEManager.Bank.death);
            }
            else if (hp > 0)
            {
                SEManager.Play(SEManager.Bank.damage);
            }
        }
        else if (hp > lastHp)
        {
            SEManager.Play(SEManager.Bank.heal);
            if (hp > 0) deadPlayed = false;
        }
        lastHp = hp;
    }
}
