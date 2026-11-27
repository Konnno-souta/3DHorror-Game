using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敵(Shadowlop)の音: 足音、うろうろ中のうなり声、見つけた時の叫び、攻撃、ダメージ、死亡。
/// 見つかった瞬間は、ドキッとする音(stinger)も鳴らす。
/// ShadowlopEnemyAI と同じオブジェクトに付ける。
/// </summary>
public class EnemySE : MonoBehaviour
{
    private static readonly HashSet<EnemySE> chasing = new HashSet<EnemySE>();
    private static float lastStingerTime = -999f;

    /// <summary>どれか1体でも追いかけ中か</summary>
    public static bool AnyChasing => chasing.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        chasing.Clear();
        lastStingerTime = -999f;
    }

    [Header("足音")]
    [Tooltip("1歩の長さ(m)。速く動くほど足音の間隔が短くなる")]
    [SerializeField] private float stepLength = 1.6f;
    [SerializeField] private float minMoveSpeed = 0.3f;

    [Header("うなり声")]
    [Tooltip("うろうろ中に、うなる間隔(秒、最小と最大)")]
    [SerializeField] private Vector2 idleInterval = new Vector2(6f, 14f);
    [Tooltip("追いかけ中に、うなる間隔(秒、最小と最大)")]
    [SerializeField] private Vector2 chaseInterval = new Vector2(3f, 6f);

    [Header("ドキッとする音")]
    [SerializeField] private bool playStinger = true;
    [Tooltip("連続で鳴らさない時間(秒)")]
    [SerializeField] private float stingerCooldown = 15f;

    [Header("聞こえる距離")]
    [SerializeField] private float hearDistance = 30f;

    private NavMeshAgent agent;
    private AudioSource voice;
    private SEField stateField;
    private string lastState = "";
    private Vector3 lastPos;
    private float stepTimer;
    private float growlTimer;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        Component ai = GetComponent<ShadowlopEnemyAI>();
        stateField = new SEField(ai, "currentState");

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 2f;
        voice.maxDistance = hearDistance;
        voice.dopplerLevel = 0f;

        lastPos = transform.position;
        growlTimer = Random.Range(idleInterval.x, idleInterval.y);
    }

    private void OnDisable()
    {
        chasing.Remove(this);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        string state = stateField.GetName();
        if (state != lastState)
        {
            OnStateChanged(state);
            lastState = state;
        }
        if (state == "Dead") return;

        UpdateSteps(dt);
        UpdateGrowl(dt, state);
    }

    private void OnStateChanged(string state)
    {
        SEBank b = SEManager.Bank;
        if (state == "Chase")
        {
            if (lastState != "Attack" && lastState != "GetDamage")
            {
                SEManager.PlayOn(voice, b.enemyChase);
                if (playStinger && Time.unscaledTime - lastStingerTime > stingerCooldown)
                {
                    lastStingerTime = Time.unscaledTime;
                    SEManager.Play(b.stinger);
                }
            }
            chasing.Add(this);
            growlTimer = Random.Range(chaseInterval.x, chaseInterval.y);
        }
        else if (state == "Attack")
        {
            SEManager.PlayOn(voice, b.enemyAttack);
            chasing.Add(this);
        }
        else if (state == "GetDamage")
        {
            SEManager.PlayOn(voice, b.enemyHurt);
        }
        else if (state == "Dead")
        {
            SEManager.PlayOn(voice, b.enemyDeath);
            chasing.Remove(this);
        }
        else
        {
            chasing.Remove(this);
        }
    }

    private void UpdateSteps(float dt)
    {
        float speed;
        if (agent != null && agent.enabled) speed = agent.velocity.magnitude;
        else
        {
            Vector3 d = transform.position - lastPos;
            d.y = 0f;
            speed = d.magnitude / dt;
        }
        lastPos = transform.position;

        if (speed < minMoveSpeed) return;
        stepTimer -= dt;
        if (stepTimer <= 0f)
        {
            SEManager.PlayAt(SEManager.Bank.enemyStep, transform.position, hearDistance);
            stepTimer = Mathf.Clamp(stepLength / speed, 0.2f, 1.2f);
        }
    }

    private void UpdateGrowl(float dt, string state)
    {
        if (state == "Attack" || state == "GetDamage") return;
        growlTimer -= dt;
        if (growlTimer > 0f) return;

        bool isChasing = state == "Chase";
        if (!voice.isPlaying)
        {
            SEManager.PlayOn(voice, isChasing ? SEManager.Bank.enemyChase : SEManager.Bank.enemyIdle, isChasing ? 0.7f : 1f);
        }
        Vector2 r = isChasing ? chaseInterval : idleInterval;
        growlTimer = Random.Range(r.x, r.y);
    }
}
