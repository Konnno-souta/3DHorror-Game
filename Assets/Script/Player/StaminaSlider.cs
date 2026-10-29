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

    private float hideTimer = 0f;

    void Start()
    {
        staminaSlider.maxValue = player.MaxStamina; 
        displayStamina = player.CurrentStamina;
        staminaSlider.value = player.CurrentStamina;

        // FillのImage取得
        fillImage = staminaSlider.fillRect.GetComponent<Image>();

        staminaUI.alpha = 0f;
        staminaUI.gameObject.SetActive(true);
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
            fillImage.color = new Color(1f, 0f, 0f, blink);
        }
        else if (currentStamina <= 30)
        {
            fillImage.color = new Color(1f, 0.5f, 0f); // オレンジ
        }
        else if (currentStamina <= 40)
        {
            fillImage.color = Color.yellow;
        }
        else
        {
            fillImage.color = Color.green;
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