using UnityEngine;

/// <summary>
/// BGMと環境音。シーンに1つ置く。
/// ・Title: タイトル曲
/// ・Game : ゲーム曲 + 部屋の環境音 + 追いかけ中の曲(あれば)
/// 追いかけられると、ゲーム曲が小さくなり、追いかけ曲が大きくなる。
/// </summary>
public class BGMPlayer : MonoBehaviour
{
    public enum Mode { Title, Game }

    public static BGMPlayer Instance { get; private set; }

    [SerializeField] private Mode mode = Mode.Game;
    [Tooltip("部屋の環境音(風のような音)を鳴らすか。いらないのでオフ")]
    [SerializeField] private bool playAmbient = false;
    [Tooltip("曲が切り替わる速さ(1秒あたりの音量)")]
    [SerializeField] private float fadeSpeed = 0.5f;
    [Tooltip("追いかけ中、ゲーム曲をどれくらい小さくするか")]
    [SerializeField, Range(0f, 1f)] private float duckWhileChase = 0.3f;

    private AudioSource main;
    private AudioSource ambient;
    private AudioSource chase;
    private bool fadingOut;
    private float fadeOutSpeed = 1f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        SEBank b = SEManager.Bank;
        main = SEManager.CreateLoopSource(gameObject, mode == Mode.Title ? b.bgmTitle : b.bgmGame, false);
        if (mode == Mode.Game)
        {
            if (playAmbient) ambient = SEManager.CreateLoopSource(gameObject, b.ambientRoom, false);
            chase = SEManager.CreateLoopSource(gameObject, b.bgmChase, false);
        }
        foreach (AudioSource s in new[] { main, ambient, chase })
        {
            if (s != null) s.ignoreListenerPause = true;
        }
    }

    /// <summary>全部の曲を、だんだん消す(ゲームオーバーやシーン移動の時)</summary>
    public void FadeOutAll(float seconds)
    {
        fadingOut = true;
        fadeOutSpeed = 1f / Mathf.Max(0.05f, seconds);
    }

    private void Update()
    {
        SEBank b = SEManager.Bank;
        float vol = SEManager.BGMVolume;

        if (fadingOut)
        {
            float sp = fadeOutSpeed * Mathf.Max(0.05f, vol);
            SEManager.FadeLoop(main, 0f, sp, false);
            SEManager.FadeLoop(ambient, 0f, sp, false);
            SEManager.FadeLoop(chase, 0f, sp, false);
            return;
        }

        bool isChase = mode == Mode.Game && EnemySE.AnyChasing;
        bool hasChase = chase != null && chase.clip != null;

        SoundGroup mainGroup = mode == Mode.Title ? b.bgmTitle : b.bgmGame;
        float mainTarget = mainGroup.volume * vol * (isChase && hasChase ? duckWhileChase : 1f);
        SEManager.FadeLoop(main, mainTarget, fadeSpeed, false);

        if (ambient != null) SEManager.FadeLoop(ambient, b.ambientRoom.volume * vol, fadeSpeed, false);
        if (hasChase) SEManager.FadeLoop(chase, isChase ? b.bgmChase.volume * vol : 0f, isChase ? fadeSpeed * 2f : fadeSpeed * 0.5f, false);
    }
}
