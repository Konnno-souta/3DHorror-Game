using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cursed Toy: Shadowlop 用の敵AIスクリプト
/// 状態: Idle -> Patrol -> Chase -> Attack / GetDamage -> Death
/// NavMeshAgent + Animator (ATTACK, CHASE, DEATH, GETDAMAGE, IDLE, WALK) を使用
///
/// 事前準備:
/// 1. シーンに NavMesh を Bake しておくこと (Window > AI > Navigation)
/// 2. キャラクターに NavMeshAgent, Animator, (任意)CapsuleCollider を追加
/// 3. Animator Controller に isWalking(bool), isChasing(bool), isAttacking(trigger),
///    takeDamage(trigger), die(trigger) を追加済みであること
/// 4. プレイヤーオブジェクトに Tag "Player" を設定
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ShadowlopEnemyAI : MonoBehaviour
{
    private enum State { Idle, Patrol, Chase, Attack, GetDamage, Dead }

    [Header("Patrol")]
    [Tooltip("巡回ポイント（未設定ならその場で待機のみ）")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitTime = 3f;
    [SerializeField] private float patrolSpeed = 1.5f;

    [Header("Detection")]
    [SerializeField] private float sightRange = 12f;
    [SerializeField] private float sightAngle = 90f; // 視野角（度）
    [SerializeField] private LayerMask obstacleMask; // 視線を遮る壁など
    [SerializeField] private float loseSightRange = 18f; // これ以上離れたら見失う

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int attackDamage = 10;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;
    private State currentState = State.Idle;

    private int currentHealth;
    private int currentPatrolIndex = 0;
    private float stateTimer = 0f;
    private float lastAttackTime = -999f;

    private static readonly int HashIsWalking = Animator.StringToHash("isWalking");
    private static readonly int HashIsChasing = Animator.StringToHash("isChasing");
    private static readonly int HashIsAttacking = Animator.StringToHash("isAttacking");
    private static readonly int HashTakeDamage = Animator.StringToHash("takeDamage");
    private static readonly int HashDie = Animator.StringToHash("die");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        Debug.Log("[Shadowlop] Player found: " + (player != null) + (player != null ? $" ({player.name})" : ""));
    }

    void Start()
    {
        agent.speed = patrolSpeed;

        // NavMesh上の最も近い点にワープさせて安全に配置
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            Debug.LogWarning("近くにNavMeshが見つかりません: " + transform.position);
        }

        ChangeState(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
    }

    void Update()
    {
        if (currentState == State.Dead) return;

        switch (currentState)
        {
            case State.Idle:
                TickIdle();
                break;
            case State.Patrol:
                TickPatrol();
                break;
            case State.Chase:
                TickChase();
                break;
            case State.Attack:
                TickAttack();
                break;
            case State.GetDamage:
                TickGetDamage();
                break;
        }

        // どの状態からでもプレイヤーを発見したら Chase に割り込む
        if (currentState != State.Chase && currentState != State.Attack &&
            currentState != State.GetDamage && CanSeePlayer())
        {
            ChangeState(State.Chase);
        }
    }

    // ---------------- Idle ----------------
    void TickIdle()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer >= patrolWaitTime && patrolPoints != null && patrolPoints.Length > 0)
        {
            ChangeState(State.Patrol);
        }
    }

    // ---------------- Patrol ----------------
    void TickPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            ChangeState(State.Idle);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            // 到着して待機中はIdleアニメーションに戻す
            animator.SetBool(HashIsWalking, false);

            stateTimer += Time.deltaTime;
            if (stateTimer >= patrolWaitTime)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                animator.SetBool(HashIsWalking, true); // 移動再開でWalkに戻す
                stateTimer = 0f;
            }
        }
    }

    // ---------------- Chase ----------------
    void TickChase()
    {
        if (player == null) { ChangeState(State.Patrol); return; }

        float dist = Vector3.Distance(transform.position, player.position);

        // 見失ったら巡回に戻る
        if (dist > loseSightRange)
        {
            ChangeState(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
            return;
        }

        if (dist <= attackRange)
        {
            ChangeState(State.Attack);
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    // ---------------- Attack ----------------
    void TickAttack()
    {
        if (player == null) { ChangeState(State.Patrol); return; }

        float dist = Vector3.Distance(transform.position, player.position);

        // 範囲外に出たら再追跡
        if (dist > attackRange * 1.2f)
        {
            ChangeState(State.Chase);
            return;
        }

        // プレイヤーの方を向く
        Vector3 dir = (player.position - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);

        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            PerformAttack();
        }
    }

    void PerformAttack()
    {
        animator.SetTrigger(HashIsAttacking);
        // ダメージ処理はAnimation Event経由で呼ぶのが望ましい（下記 DealDamageEvent 参照）
    }

    /// <summary>
    /// ATTACK アニメーションのAnimation Eventから呼び出す（攻撃が実際にヒットする瞬間）
    /// </summary>
    public void DealDamageEvent()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange)
        {
            var playerHealth = player.GetComponent<IDamageable>();
            playerHealth?.TakeDamage(attackDamage);
        }
    }

    // ---------------- GetDamage ----------------
    void TickGetDamage()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer >= 0.6f) // GETDAMAGE クリップ長に合わせて調整
        {
            ChangeState(player != null && CanSeePlayer() ? State.Chase : State.Patrol);
        }
    }

    // ---------------- Damage / Death (外部から呼ぶ) ----------------
    public void TakeDamage(int amount)
    {
        if (currentState == State.Dead) return;

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            ChangeState(State.Dead);
            return;
        }

        ChangeState(State.GetDamage);
    }

    // ---------------- 状態遷移共通処理 ----------------
    void ChangeState(State newState)
    {
        currentState = newState;
        stateTimer = 0f;
        Debug.Log("[Shadowlop] State changed to: " + newState);

        switch (newState)
        {
            case State.Idle:
                agent.isStopped = true;
                animator.SetBool(HashIsChasing, false);
                animator.SetBool(HashIsWalking, false);
                break;

            case State.Patrol:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                animator.SetBool(HashIsChasing, false);
                animator.SetBool(HashIsWalking, true);
                if (patrolPoints != null && patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                break;

            case State.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                animator.SetBool(HashIsChasing, true);
                animator.SetBool(HashIsWalking, false);
                break;

            case State.Attack:
                agent.isStopped = true;
                animator.SetBool(HashIsChasing, false);
                animator.SetBool(HashIsWalking, false);
                break;

            case State.GetDamage:
                agent.isStopped = true;
                animator.SetBool(HashIsWalking, false);
                animator.SetTrigger(HashTakeDamage);
                break;

            case State.Dead:
                agent.isStopped = true;
                agent.enabled = false;
                animator.SetBool(HashIsWalking, false);
                animator.SetTrigger(HashDie);
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
                Destroy(gameObject, 5f); // アニメーション再生後に削除（任意）
                break;
        }
    }

    // ---------------- 視界判定 ----------------
    private float debugLogTimer = 0f;

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        bool debugTick = false;
        debugLogTimer += Time.deltaTime;
        if (debugLogTimer >= 1f) { debugLogTimer = 0f; debugTick = true; }

        if (dist > sightRange)
        {
            //if (debugTick) Debug.Log($"[Shadowlop] 検知失敗: 距離オーバー dist={dist:F1} > sightRange={sightRange}");
            return false;
        }

        float angle = Vector3.Angle(transform.forward, toPlayer);
        if (angle > sightAngle * 0.5f)
        {
            //if (debugTick) Debug.Log($"[Shadowlop] 検知失敗: 視野角外 angle={angle:F1} > {sightAngle * 0.5f}");
            return false;
        }

        // 障害物チェック（壁の裏にいたら見えない）
        // プレイヤー自身のコライダーに当たって誤検知しないよう、判定距離を少し短くする
        float raycastDist = Mathf.Max(0.1f, dist - 0.5f);
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, toPlayer.normalized, out RaycastHit hit, raycastDist, obstacleMask))
        {
            //if (debugTick) Debug.Log($"[Shadowlop] 検知失敗: 障害物に遮られている hit={hit.collider.name}");
            return false; // 何かに遮られている
        }

        //if (debugTick) Debug.Log($"[Shadowlop] 検知成功: dist={dist:F1}, angle={angle:F1}");
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

/// <summary>
/// プレイヤー側に実装するダメージ受け取り用インターフェース
/// </summary>
public interface IDamageable
{
    void TakeDamage(int amount);
}