using UnityEngine;

/// <summary>
/// タイトルの背景の、廊下の奥の化け物。
/// 十字路を、左の廊下から右の廊下へ、ゆっくり歩いて横切る。
/// 右の廊下へ消えたら、すぐに、また左から出てくる(ずっとくり返す)。
/// Ping Pong をオンにすると、右まで行ったら、今度は右から左へ歩いてもどる(行ったり来たり)。
/// 歩きのアニメーションも、止まらないように、くり返し再生する。
/// ときどき、十字路の真ん中で立ち止まり、こちらを見る。
/// Tools / Title / タイトルの背景を作る で、自動で付く。
/// </summary>
public class TitleMonsterWalk : MonoBehaviour
{
    [Header("道のり(親から見た位置)")]
    [Tooltip("出てくる所(左の廊下の中)")]
    [SerializeField] private Vector3 startPosition;

    [Tooltip("消える所(右の廊下の中)")]
    [SerializeField] private Vector3 endPosition;

    [Header("速さと時間")]
    [Tooltip("歩く速さ(m/秒)")]
    [SerializeField] private float walkSpeed = 1.1f;

    [Tooltip("消えてから、また出てくるまでの秒数(0 なら、すぐ出てくる)")]
    [SerializeField] private Vector2 waitTime = new Vector2(0f, 0f);

    [Tooltip("オンなら、行ったり来たり。オフなら、いつも左から右へ")]
    [SerializeField] private bool pingPong = false;

    [Header("立ち止まって、こちらを見る")]
    [Tooltip("横切るたびに、真ん中で立ち止まる確率")]
    [SerializeField, Range(0f, 1f)] private float stareChance = 0.25f;

    [Tooltip("立ち止まっている秒数")]
    [SerializeField] private Vector2 stareTime = new Vector2(1.5f, 3f);

    [Tooltip("ふり向く速さ(度/秒)")]
    [SerializeField] private float turnSpeed = 120f;

    [Tooltip("見る向き(親から見た向き。手前のカメラの方)")]
    [SerializeField] private Vector3 lookDirection = new Vector3(0f, 0f, -1f);

    [Header("アニメーション(任意)")]
    [SerializeField] private Animator animator;

    private enum State { Waiting, Walking, Staring }

    private State state = State.Waiting;
    private float timer;
    private bool willStare;
    private bool stared;
    private Renderer[] renderers;

    private void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>(true);

        transform.localPosition = startPosition;
        timer = Random.Range(1f, 3f);
        SetVisible(false);
    }

    private void Update()
    {
        KeepAnimationLooping();

        Vector3 walkDir = endPosition - startPosition;
        walkDir.y = 0f;
        if (walkDir.sqrMagnitude < 0.0001f) return;
        Vector3 dir = walkDir.normalized;

        switch (state)
        {
            case State.Waiting:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    transform.localPosition = startPosition;
                    transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                    willStare = Random.value < stareChance;
                    stared = false;
                    SetVisible(true);
                    SetAnimSpeed(1f);
                    state = State.Walking;
                }
                break;

            case State.Walking:
                Turn(dir);
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, endPosition, walkSpeed * Time.deltaTime);

                // 真ん中まで来たら、立ち止まる
                float done = Vector3.Dot(transform.localPosition - startPosition, dir) / walkDir.magnitude;
                if (willStare && !stared && done >= 0.5f)
                {
                    stared = true;
                    timer = Random.Range(stareTime.x, stareTime.y);
                    SetAnimSpeed(0f);
                    state = State.Staring;
                }

                if ((transform.localPosition - endPosition).sqrMagnitude < 0.0004f)
                {
                    if (pingPong)
                    {
                        // 向きを変えて、反対の端へ
                        Vector3 tmp = startPosition;
                        startPosition = endPosition;
                        endPosition = tmp;
                    }

                    timer = Random.Range(Mathf.Max(0f, waitTime.x), Mathf.Max(waitTime.x, waitTime.y));
                    if (timer > 0f) SetVisible(false);
                    state = State.Waiting;
                }
                break;

            case State.Staring:
                Turn(lookDirection);
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    SetAnimSpeed(1f);
                    state = State.Walking;
                }
                break;
        }
    }

    /// <summary>
    /// 歩きのアニメーションが、くり返しの設定になっていなくても、最後まで行ったら最初から再生しなおす。
    /// </summary>
    private void KeepAnimationLooping()
    {
        if (animator == null || animator.runtimeAnimatorController == null || animator.speed <= 0f) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.loop && info.normalizedTime >= 1f)
        {
            animator.Play(info.fullPathHash, 0, 0f);
        }
    }

    private void Turn(Vector3 localDirection)
    {
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(localDirection.normalized, Vector3.up);
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, turnSpeed * Time.deltaTime);
    }

    private void SetAnimSpeed(float speed)
    {
        if (animator != null) animator.speed = speed;
    }

    private void SetVisible(bool visible)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers)
        {
            if (r != null) r.enabled = visible;
        }
        foreach (Light l in GetComponentsInChildren<Light>(true))
        {
            l.enabled = visible;
        }
    }
}