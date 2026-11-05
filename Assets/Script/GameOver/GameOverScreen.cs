using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ゲームオーバー画面（C案：電池切れの闇）
/// GameOverPanel にこのスクリプトを付ける。
/// パネルが SetActive(true) された瞬間（OnEnable）に、次の順で演出が始まる。
///   暗転 → 光がちらつきながら点く → 「GAME OVER」→ メッセージ → 電池アイコン → ボタン
/// Time.timeScale が 0 でも動くよう、すべて unscaled time で動かす。
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("演出する部品（CanvasGroup）")]
    [Tooltip("全面を覆う黒（Image。Raycast Targetは任意）")]
    [SerializeField] private CanvasGroup blackout;
    [Tooltip("円形の光（GameOver_Spot）")]
    [SerializeField] private CanvasGroup spot;
    [Tooltip("「GAME OVER」の見出し")]
    [SerializeField] private CanvasGroup titleGroup;
    [Tooltip("「光が消えた。〜」のメッセージ")]
    [SerializeField] private CanvasGroup messageGroup;
    [Tooltip("電池アイコンと 0%")]
    [SerializeField] private CanvasGroup batteryGroup;
    [Tooltip("TIP（ヒント）の文")]
    [SerializeField] private CanvasGroup tipGroup;
    [Tooltip("ボタンをまとめた親")]
    [SerializeField] private CanvasGroup buttonsGroup;

    [Header("ボタン")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;
    [Tooltip("タイトルシーン名。空ならタイトルボタンを隠す")]
    [SerializeField] private string titleSceneName = "";

    [Header("ゲームオーバー中に隠すUI（スタミナ、電池ゲージ、インベントリなど）")]
    [SerializeField] private GameObject[] hideWhileGameOver;

    [Header("タイミング（秒）")]
    [SerializeField] private float blackoutTime = 0.7f;
    [SerializeField] private float spotFlickerTime = 1.0f;
    [SerializeField] private float titleFadeTime = 0.9f;
    [SerializeField] private float messageFadeTime = 0.7f;
    [SerializeField] private float batteryFadeTime = 0.6f;
    [SerializeField] private float buttonsFadeTime = 0.5f;
    [SerializeField] private float pauseBetween = 0.25f;

    [Header("光")]
    [Tooltip("光のゆらぎ（点灯後も、弱くちらつき続ける）")]
    [SerializeField, Range(0f, 0.5f)] private float spotIdleFlicker = 0.12f;

    private bool spotOn;
    private float seed;

    void Awake()
    {
        seed = Random.Range(0f, 100f);

        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        if (titleButton != null) titleButton.onClick.AddListener(GoTitle);
    }

    void OnEnable()
    {
        // 他のUIを隠す
        if (hideWhileGameOver != null)
        {
            foreach (var go in hideWhileGameOver)
            {
                if (go != null) go.SetActive(false);
            }
        }

        // カーソルを出す（捕獲演出側でも出しているが、念のため）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 初期状態：全部透明
        SetAlpha(blackout, 0f);
        SetAlpha(spot, 0f);
        SetAlpha(titleGroup, 0f);
        SetAlpha(messageGroup, 0f);
        SetAlpha(batteryGroup, 0f);
        SetAlpha(tipGroup, 0f);
        SetAlpha(buttonsGroup, 0f);
        SetInteractable(buttonsGroup, false);

        if (titleButton != null)
        {
            titleButton.gameObject.SetActive(!string.IsNullOrEmpty(titleSceneName));
        }

        spotOn = false;
        StartCoroutine(Sequence());
    }

    void Update()
    {
        // 光が点いた後は、ゆるくちらつかせる
        if (spotOn && spot != null)
        {
            float n = Mathf.PerlinNoise(seed, Time.unscaledTime * 6f);
            spot.alpha = 1f - spotIdleFlicker * n;
        }
    }

    private IEnumerator Sequence()
    {
        // 1. 暗転
        yield return Fade(blackout, 1f, blackoutTime);
        yield return Wait(pauseBetween);

        // 2. 光が、ちらつきながら点く
        yield return SpotFlickerOn();
        yield return Wait(pauseBetween);

        // 3. 見出し
        yield return Fade(titleGroup, 1f, titleFadeTime);
        yield return Wait(pauseBetween);

        // 4. メッセージ、電池、ヒント
        yield return Fade(messageGroup, 1f, messageFadeTime);
        yield return Fade(batteryGroup, 1f, batteryFadeTime);
        yield return Fade(tipGroup, 1f, batteryFadeTime);

        // 5. ボタン
        yield return Fade(buttonsGroup, 1f, buttonsFadeTime);
        SetInteractable(buttonsGroup, true);
    }

    private IEnumerator SpotFlickerOn()
    {
        if (spot == null) yield break;

        float t = 0f;
        while (t < spotFlickerTime)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / spotFlickerTime);

            // 序盤はほとんど消えていて、だんだん安定して点く
            float noise = Mathf.PerlinNoise(seed, t * 30f);
            float flicker = Mathf.Lerp(noise > 0.55f ? 1f : 0.05f, 1f, progress * progress);
            spot.alpha = flicker * Mathf.Lerp(0.4f, 1f, progress);
            yield return null;
        }

        spot.alpha = 1f;
        spotOn = true;
    }

    private IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        if (group == null) yield break;

        float start = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = target;
    }

    private IEnumerator Wait(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
    }

    private static void SetAlpha(CanvasGroup g, float a)
    {
        if (g != null) g.alpha = a;
    }

    private static void SetInteractable(CanvasGroup g, bool value)
    {
        if (g == null) return;
        g.interactable = value;
        g.blocksRaycasts = value;
    }

    // ---------------- ボタン ----------------

    public void Retry()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoTitle()
    {
        if (string.IsNullOrEmpty(titleSceneName)) return;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(titleSceneName);
    }
}
