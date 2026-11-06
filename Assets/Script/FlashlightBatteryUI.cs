using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 懐中電灯の電池ゲージUI（3マス式）。
/// 画面右下、懐中電灯モデルの近くに置くことを想定。
/// ・マスは上から1つずつ消える（3 → 2 → 1 → 0）
/// ・マスが減る直前のライトの点滅に合わせて、ゲージも点滅する
/// ・懐中電灯を未入手の間は非表示にする
/// </summary>
public class FlashlightBatteryUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private HorrorCameraFollow flashlightController;

    [Header("ゲージ本体（表示/非表示を切り替えるルート）")]
    [SerializeField] private GameObject gaugeRoot;

    [Header("塗り（Image Type: Filled / Fill Method: Vertical を想定）")]
    [SerializeField] private Image fillImage;

    [Header("マス数ごとの色")]
    [SerializeField] private Color fullColor = new Color(0.55f, 0.85f, 0.35f);   // 満タン（緑）
    [SerializeField] private Color midColor = new Color(0.95f, 0.75f, 0.2f);    // 残り2マス以下（黄）
    [SerializeField] private Color lowColor = new Color(0.9f, 0.25f, 0.2f);     // 残り1マス（赤）

    [Header("塗りの高さをマスの区切り線に合わせる（Battery_Gauge_Frame用）")]
    [Tooltip("画像の下端から、一番下のマスが始まるまでの割合")]
    [SerializeField] private float fillBottomPad = 0.05f;
    [Tooltip("1マスあたりの高さの割合（枠の3分割線に合わせた値）")]
    [SerializeField] private float fillPerCell = 0.27f;
    [Tooltip("マスが減る・増える時の、塗りの動く速さ")]
    [SerializeField] private float fillSmoothSpeed = 8f;

    [Header("点滅")]
    [Tooltip("残り1マスの間のゆっくりした点滅")]
    [SerializeField] private float lowBlinkSpeed = 6f;
    [Tooltip("マスが減る直前の、素早い点滅")]
    [SerializeField] private float losingBlinkSpeed = 40f;

    private float displayedFill;

    void Update()
    {
        if (flashlightController == null) return;

        bool unlocked = flashlightController.IsFlashlightUnlocked;

        if (gaugeRoot != null)
        {
            gaugeRoot.SetActive(unlocked);
        }

        if (!unlocked || fillImage == null) return;

        int cells = flashlightController.CurrentCells;
        int maxCells = flashlightController.MaxCells;

        // マス数 → 塗りの高さ（区切り線にぴったり合わせる）
        float targetFill = cells <= 0 ? 0f : Mathf.Clamp01(fillBottomPad + fillPerCell * cells);
        displayedFill = Mathf.Lerp(displayedFill, targetFill, 1f - Mathf.Exp(-fillSmoothSpeed * Time.unscaledDeltaTime));
        fillImage.fillAmount = displayedFill;

        // 色
        Color targetColor;
        if (cells <= 1)
        {
            targetColor = lowColor;
        }
        else if (cells < maxCells)
        {
            targetColor = midColor;
        }
        else
        {
            targetColor = fullColor;
        }

        // 点滅
        float alpha = 1f;
        if (flashlightController.IsLosingCell)
        {
            // 1マス減る直前：素早く点滅
            alpha = Mathf.Lerp(0.2f, 1f, (Mathf.Sin(Time.unscaledTime * losingBlinkSpeed) + 1f) * 0.5f);
        }
        else if (cells == 1)
        {
            // 残り1マス：ゆっくり点滅して警告
            alpha = Mathf.Lerp(0.5f, 1f, (Mathf.Sin(Time.unscaledTime * lowBlinkSpeed) + 1f) * 0.5f);
        }

        targetColor.a = alpha;
        fillImage.color = targetColor;
    }
}