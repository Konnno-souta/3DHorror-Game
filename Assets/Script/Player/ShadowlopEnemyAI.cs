using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

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

    [Header("Catch Sequence（捕まった時の演出）")]
    [Tooltip("ONにすると、攻撃範囲に入った時に「一時停止→スローモーションで攻撃アニメ」を流す")]
    [SerializeField] private bool useCatchSequence = true;
    [Tooltip("最初の完全停止の長さ（秒・実時間）")]
    [SerializeField] private float freezeDuration = 0.4f;
    [Tooltip("スローモーションの速さ（0.1で10分の1）")]
    [SerializeField, Range(0.02f, 1f)] private float slowMotionScale = 0.15f;
    [Tooltip("スローモーションの最大の長さ（秒・実時間）。アニメが進まない場合の保険。ATTACKアニメの長さより少し長めに")]
    [SerializeField] private float slowMotionDuration = 4.0f;
    [Tooltip("ONなら、攻撃アニメの進み具合で「ちょうどいいタイミング」を決める。OFFなら Slow Motion Duration の時間ぴったりで終わる")]
    [SerializeField] private bool syncToAttackAnimation = true;
    [Tooltip("攻撃アニメがどこまで進んだらゲームオーバーにするか（0から1）。攻撃が当たる瞬間あたりに合わせる。0.6から0.8が目安")]
    [SerializeField, Range(0.1f, 1f)] private float gameOverAtNormalizedTime = 0.7f;
    [Tooltip("攻撃が当たった瞬間から、ゲームオーバー画面を出すまでの間（秒・実時間）。余韻用")]
    [SerializeField] private float gameOverExtraDelay = 0.35f;
    [Tooltip("カメラが向く敵の高さ（敵の足元からのm）")]
    [SerializeField] private float focusHeight = 1.4f;
    [Tooltip("ONなら捕まった＝即ゲームオーバー。OFFなら attackDamage を受けて操作が戻る")]
    [SerializeField] private bool killPlayerOnCatch = true;
    [Tooltip("演出が終わった時に呼ぶ（ゲームオーバー画面を出す等。GameOverPanel.SetActive(true) を登録）")]
    [SerializeField] private UnityEvent onCatchFinished;

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

    private bool isCatching = false;
    private float defaultFixedDeltaTime;
    private float defaultStoppingDistance;

    private static readonly int HashIsWalking = Animator.StringToHash("isWalking");
    private static readonly int HashIsChasing = Animator.StringToHash("isChasing");
    private static readonly int HashIsAttacking = Animator.StringToHash("isAttacking");
    private static readonly int HashTakeDamage = Animator.StringToHash("takeDamage");
    private static readonly int HashDie = Animator.StringToHash("die");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        defaultStoppingDistance = agent.stoppingDistance;
        currentHealth = maxHealth;
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        Debug.Log("[Shadowlop] Player found: " + (player != null) + (player != null ? $" ({player.name})" : ""));
    }

    void Start()
    {
        agent.speed = patrolSpeed;

        // 旋回が遅いとプレイヤーの周りをぐるぐる回って攻撃範囲に入れないため、最低限の値を保証する
        agent.angularSpeed = Mathf.Max(agent.angularSpeed, 480f);
        agent.acceleration = Mathf.Max(agent.acceleration, 20f);

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

        float dist = FlatDistanceToPlayer();

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

    /// <summary>
    /// 高さ（Y）を無視した、プレイヤーまでの水平距離。
    /// プレイヤーの原点が体の中心にある場合などで、高さの差のぶん距離が伸びるのを防ぐ。
    /// </summary>
    float FlatDistanceToPlayer()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // ---------------- Attack ----------------
    void TickAttack()
    {
        if (player == null) { ChangeState(State.Patrol); return; }

        // 捕獲演出中は、プレイヤーの方を向くだけ（再追跡・再攻撃はしない）
        if (isCatching)
        {
            Vector3 d = player.position - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.unscaledDeltaTime * 8f);
            return;
        }

        float dist = FlatDistanceToPlayer();

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
        // 捕まった瞬間：一時停止 → スローモーションで攻撃アニメ
        Debug.Log($"[Shadowlop] PerformAttack 呼び出し（useCatchSequence={useCatchSequence}, isCatching={isCatching}）");

        if (useCatchSequence)
        {
            if (!isCatching) StartCoroutine(CatchSequence());
            return;
        }

        animator.SetTrigger(HashIsAttacking);
        // ダメージ処理はAnimation Event経由で呼ぶのが望ましい（下記 DealDamageEvent 参照）
    }

    // ---------------- 捕獲演出 ----------------
    IEnumerator CatchSequence()
    {
        isCatching = true;
        Debug.Log("[Shadowlop] 捕獲演出 開始");

        // プレイヤーの操作を止める
        var playerMove = player.GetComponent<HorrorPlayerController>();
        if (playerMove != null) playerMove.enabled = false;
        else Debug.LogWarning("[Shadowlop] Player に HorrorPlayerController が見つかりません（操作は止まりません）");

        // カメラを敵の方へ向ける
        var cam = FindObjectOfType<HorrorCameraFollow>();
        if (cam != null) cam.BeginFocus(transform, Vector3.up * focusHeight);
        else Debug.LogWarning("[Shadowlop] HorrorCameraFollow が見つかりません（カメラは敵を向きません）");

        // 1. 一時停止
        SetTimeScale(0f);
        yield return new WaitForSecondsRealtime(freezeDuration);

        // 2. スローモーションで攻撃アニメーション
        SetTimeScale(slowMotionScale);

        int stateBefore = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        animator.SetTrigger(HashIsAttacking);

        if (syncToAttackAnimation)
        {
            float elapsed = 0f;

            // (a) 攻撃アニメの状態に切り替わるのを待つ
            while (elapsed < slowMotionDuration)
            {
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                if (!animator.IsInTransition(0) && st.fullPathHash != stateBefore) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // (b) 攻撃アニメが指定の進み具合に達するまで、スローのまま流す
            while (elapsed < slowMotionDuration)
            {
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.normalizedTime >= gameOverAtNormalizedTime) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // (c) 当たった瞬間の余韻（スローのまま）
            yield return new WaitForSecondsRealtime(gameOverExtraDelay);
        }
        else
        {
            yield return new WaitForSecondsRealtime(slowMotionDuration);
        }

        // 3. 通常速度へ戻す
        Debug.Log("[Shadowlop] スロー終了 → ゲームオーバー処理へ（onCatchFinished の登録数: " + onCatchFinished.GetPersistentEventCount() + "）");
        SetTimeScale(1f);

        // 4. ダメージ処理
        var health = player.GetComponent<IDamageable>();
        if (killPlayerOnCatch)
        {
            health?.TakeDamage(99999);

            // ゲームオーバー画面を操作できるようにカーソルを出す
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            onCatchFinished?.Invoke();
            // isCatching は true のまま（二度と攻撃・再追跡はしない）
        }
        else
        {
            health?.TakeDamage(attackDamage);

            if (playerMove != null) playerMove.enabled = true;
            if (cam != null) cam.EndFocus();

            lastAttackTime = Time.time;
            isCatching = false;
            onCatchFinished?.Invoke();
        }
    }

    void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        // 物理の更新間隔もスケールに合わせる（0のときは更新されないので何でもよい）
        Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(scale, 0.02f);
    }

    void OnDestroy()
    {
        // 演出中に敵が消えても、時間が止まったままにならないようにする
        if (isCatching && Time.timeScale != 1f)
        {
            SetTimeScale(1f);
        }
    }

    /// <summary>
    /// ATTACK アニメーションのAnimation Eventから呼び出す（攻撃が実際にヒットする瞬間）
    /// </summary>
    public void DealDamageEvent()
    {
        if (player == null) return;
        if (isCatching) return; // 捕獲演出中のダメージは演出側でまとめて与える
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
                agent.stoppingDistance = defaultStoppingDistance;
                agent.isStopped = true;
                animator.SetBool(HashIsChasing, false);
                animator.SetBool(HashIsWalking, false);
                break;

            case State.Patrol:
                agent.stoppingDistance = defaultStoppingDistance;
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                animator.SetBool(HashIsChasing, false);
                animator.SetBool(HashIsWalking, true);
                if (patrolPoints != null && patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                break;

            case State.Chase:
                // 攻撃範囲の内側で止まるようにして、プレイヤーの周りを回り続けないようにする
                agent.stoppingDistance = attackRange * 0.7f;
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
            if (debugTick) Debug.Log($"[Shadowlop] 検知失敗: 距離オーバー dist={dist:F1} > sightRange={sightRange}");
            return false;
        }

        float angle = Vector3.Angle(transform.forward, toPlayer);
        if (angle > sightAngle * 0.5f)
        {
            if (debugTick) Debug.Log($"[Shadowlop] 検知失敗: 視野角外 angle={angle:F1} > {sightAngle * 0.5f}");
            return false;
        }

        // 障害物チェック（壁の裏にいたら見えない）
        // プレイヤー自身のコライダーに当たって誤検知しないよう、判定距離を少し短くする
        float raycastDist = Mathf.Max(0.1f, dist - 0.5f);
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, toPlayer.normalized, out RaycastHit hit, raycastDist, obstacleMask))
        {
            if (debugTick) Debug.Log($"[Shadowlop] 検知失敗: 障害物に遮られている hit={hit.collider.name}");
            return false; // 何かに遮られている
        }

        if (debugTick) Debug.Log($"[Shadowlop] 検知成功: dist={dist:F1}, angle={angle:F1}");
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