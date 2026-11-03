using UnityEngine;
using UnityEngine.UI;

public class StaminaSlider : MonoBehaviour
{
    [Header("Slider")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private HorrorPlayerController player;

    [Header("設定")]
    public float maxStamina = 100f;

    [Header("なめらか速度")]
    [SerializeField] private float smoothSpeed = 5f;

    private float displayStamina;
    private Image fillImage;

    [Header("スタミナUI")]
    [SerializeField] private CanvasGroup staminaUI;

    [SerializeField] private float hideDelay = 2f;

    [Header("フェード速度")]
    [SerializeField] private float fadeSpeed = 3f;

    [Header("色")]
    [SerializeField] private Color greenColor = new Color(0.55f, 0.85f, 0.35f);
    [SerializeField] private Color yellowColor = new Color(0.95f, 0.78f, 0.2f);
    [SerializeField] private Color orangeColor = new Color(0.95f, 0.5f, 0.15f);
    [SerializeField] private Color redColor = new Color(0.9f, 0.2f, 0.18f);

    [Header("新しい枠（Stamina_Bar_Frame）に合わせる")]
    [Tooltip("ONにすると、Fill Area を Frame 画像(1024x128)の内側ぴったりに自動配置する")]
    [SerializeField] private bool autoFitToFrame = true;
    [SerializeField] private float innerLeft = 36f;
    [SerializeField] private float innerRight = 988f;
    [SerializeField] private float innerBottom = 44f;
    [SerializeField] private float innerTop = 84f;

    private const float FrameWidth = 1024f;
    private const float FrameHeight = 128f;

    private float hideTimer = 0f;

    void Start()
    {
        staminaSlider.maxValue = player.MaxStamina;
        displayStamina = player.CurrentStamina;
        staminaSlider.value = player.CurrentStamina;

        // FillのImage取得
        fillImage = staminaSlider.fillRect.GetComponent<Image>();

        if (autoFitToFrame)
        {
            FitFillToFrame();
        }

        staminaUI.alpha = 0f;
        staminaUI.gameObject.SetActive(true);
    }

    /// <summary>
    /// Fill Area を枠の内側に合わせ、Fill自体の余白（Sliderの初期値は幅10の余白がある）をゼロにする。
    /// </summary>
    private void FitFillToFrame()
    {
        RectTransform fillRect = staminaSlider.fillRect;
        RectTransform fillArea = fillRect.parent as RectTransform;

        if (fillArea != null && fillArea != staminaSlider.transform)
        {
            fillArea.anchorMin = new Vector2(innerLeft / FrameWidth, innerBottom / FrameHeight);
            fillArea.anchorMax = new Vector2(innerRight / FrameWidth, innerTop / FrameHeight);
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
        }

        // Sliderは anchor だけを動かすので、offset をゼロにして幅が正確に比例するようにする
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    void Update()
    {
        float currentStamina = player.CurrentStamina;
        // =========================
        // なめらか処理
        // =========================
        displayStamina = Mathf.Lerp(
            displayStamina,
            currentStamina,
            smoothSpeed * Time.deltaTime
        );

        staminaSlider.value = displayStamina;

        // =========================
        // 色変化 + 点滅
        // =========================
        if (currentStamina <= 10)
        {
            // 赤 + 点滅
            float blink = Mathf.PingPong(Time.time * 5f, 1f);
            Color c = redColor;
            c.a = blink;
            fillImage.color = c;
        }
        else if (currentStamina <= 30)
        {
            fillImage.color = orangeColor;
        }
        else if (currentStamina <= 40)
        {
            fillImage.color = yellowColor;
        }
        else
        {
            fillImage.color = greenColor;
        }


        //=========================
        // UI表示制御
        //=========================


        // スタミナがほぼ満タンなら
        if (player.CurrentStamina >= player.MaxStamina - 0.01f)
        {
            hideTimer += Time.deltaTime;

            if (hideTimer >= hideDelay)
            {
                staminaUI.alpha = Mathf.MoveTowards(
                    staminaUI.alpha,
                    0f,
                    fadeSpeed * Time.deltaTime
                );
            }
        }
        else
        {
            staminaUI.alpha = Mathf.MoveTowards(
                staminaUI.alpha,
                1f,
                fadeSpeed * Time.deltaTime
            );

            hideTimer = 0f;
        }
    }
}