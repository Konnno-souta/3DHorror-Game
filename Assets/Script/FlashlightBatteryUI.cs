using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 懐中電灯の電池残量ゲージUI。
/// 画面右下、懐中電灯モデルの近くに置くことを想定。
/// 懐中電灯を未入手の間は非表示にする。
/// </summary>
public class FlashlightBatteryUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private HorrorCameraFollow flashlightController;

    [Header("ゲージ本体（表示/非表示を切り替えるルート）")]
    [SerializeField] private GameObject gaugeRoot;

    [Header("塗り（Image Type: Filled / Fill Method: Vertical を想定）")]
    [SerializeField] private Image fillImage;

    [Header("残量ごとの色")]
    [SerializeField] private Color fullColor = new Color(0.55f, 0.85f, 0.35f);   // 緑
    [SerializeField] private Color midColor = new Color(0.95f, 0.75f, 0.2f);    // 黄
    [SerializeField] private Color lowColor = new Color(0.9f, 0.25f, 0.2f);     // 赤

    [Header("しきい値（％）")]
    [SerializeField] private float midThreshold = 50f;
    [SerializeField] private float lowThreshold = 20f;

    [Header("残量が少ない時の点滅")]
    [SerializeField] private float lowBlinkSpeed = 6f;

    void Update()
    {
        if (flashlightController == null) return;

        bool unlocked = flashlightController.IsFlashlightUnlocked;

        if (gaugeRoot != null)
        {
            gaugeRoot.SetActive(unlocked);
        }

        if (!unlocked || fillImage == null) return;

        float percent01 = flashlightController.BatteryPercent01;
        float percent = percent01 * 100f;

        fillImage.fillAmount = percent01;

        Color targetColor;
        if (percent <= lowThreshold)
        {
            targetColor = lowColor;
        }
        else if (percent <= midThreshold)
        {
            targetColor = midColor;
        }
        else
        {
            targetColor = fullColor;
        }

        float alpha = 1f;
        if (percent <= lowThreshold)
        {
            // 残量が少ない間は、警告として点滅させる
            alpha = Mathf.Lerp(0.4f, 1f, (Mathf.Sin(Time.unscaledTime * lowBlinkSpeed) + 1f) * 0.5f);
        }

        targetColor.a = alpha;
        fillImage.color = targetColor;
    }
}
