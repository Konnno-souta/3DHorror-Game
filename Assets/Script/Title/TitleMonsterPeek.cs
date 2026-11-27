using UnityEngine;

/// <summary>
/// タイトルの背景の、廊下の奥の化け物。
/// 曲がり角のかげに隠れていて、ときどき、ゆっくり顔を出しては、また引っこむ。
/// 引っこむときは、少し速い。たまに、顔を出したまま、じっと止まる。
/// Tools / Title / タイトルの背景を作る で、自動で付く。
/// </summary>
public class TitleMonsterPeek : MonoBehaviour
{
    [Header("位置(親から見た位置)")]
    [Tooltip("かくれているときの位置")]
    [SerializeField] private Vector3 hiddenPosition;

    [Tooltip("顔を出したときの位置")]
    [SerializeField] private Vector3 peekPosition;

    [Header("時間(秒)")]
    [SerializeField] private Vector2 hiddenTime = new Vector2(3f, 7f);
    [SerializeField] private Vector2 peekTime = new Vector2(2f, 5f);
    [SerializeField] private float peekOutSeconds = 2.5f;   // 顔を出すのにかかる時間(ゆっくり)
    [SerializeField] private float hideSeconds = 0.6f;      // 引っこむのにかかる時間(速い)

    [Header("ゆれ")]
    [Tooltip("顔を出している間、少しだけ体をゆらす")]
    [SerializeField] private float swayAmount = 0.05f;
    [SerializeField] private float swaySpeed = 0.7f;

    private enum State { Hidden, PeekingOut, Peeking, Hiding }

    private State state = State.Hidden;
    private float timer;
    private float progress;     // 0 = かくれている、1 = 顔を出している

    private void Start()
    {
        transform.localPosition = hiddenPosition;
        timer = Random.Range(1f, 3f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Hidden:
                timer -= dt;
                if (timer <= 0f) state = State.PeekingOut;
                break;

            case State.PeekingOut:
                progress = Mathf.MoveTowards(progress, 1f, dt / Mathf.Max(0.01f, peekOutSeconds));
                if (progress >= 1f)
                {
                    state = State.Peeking;
                    timer = Random.Range(peekTime.x, peekTime.y);
                }
                break;

            case State.Peeking:
                timer -= dt;
                if (timer <= 0f) state = State.Hiding;
                break;

            case State.Hiding:
                progress = Mathf.MoveTowards(progress, 0f, dt / Mathf.Max(0.01f, hideSeconds));
                if (progress <= 0f)
                {
                    state = State.Hidden;
                    timer = Random.Range(hiddenTime.x, hiddenTime.y);
                }
                break;
        }

        float eased = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 pos = Vector3.Lerp(hiddenPosition, peekPosition, eased);

        if (progress > 0f)
        {
            pos.x += Mathf.Sin(Time.time * swaySpeed) * swayAmount * eased;
        }

        transform.localPosition = pos;
    }

    public void Setup(Vector3 hidden, Vector3 peek)
    {
        hiddenPosition = hidden;
        peekPosition = peek;
    }
}
