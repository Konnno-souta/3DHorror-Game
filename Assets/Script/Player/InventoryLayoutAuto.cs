using UnityEngine;

/// <summary>
/// インベントリの左パネルと右パネルを、親パネルの大きさに合わせて自動で配置する。
/// 画面の縦横比が変わっても、はみ出さないように縮小して中央に寄せる。
/// 親パネル（InventoryPanel）に付けて、左右のパネルを割り当てる。
/// エディタでも動くので、Inspectorの数値を変えるとその場で位置が変わる。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class InventoryLayoutAuto : MonoBehaviour
{
    public enum HorizontalAlign { Left, Center }
    public enum VerticalAlign { Top, Middle }

    [Header("配置するパネル")]
    [Tooltip("左の一覧（LeftPanel_A）")]
    [SerializeField] private RectTransform leftPanel;
    [Tooltip("右の詳細（RightPanel_A）")]
    [SerializeField] private RectTransform rightPanel;

    [Header("余白（親パネルの端から）")]
    [Tooltip("左右の余白")]
    [SerializeField] private float sideMargin = 72f;
    [Tooltip("上の余白（見出しの下から始めたい場合は大きくする）")]
    [SerializeField] private float contentTop = 160f;
    [Tooltip("下の余白（操作ヒントの上で止めたい場合は大きくする）")]
    [SerializeField] private float contentBottom = 128f;

    [Header("左右パネルの間隔")]
    [SerializeField] private float gap = 56f;

    [Header("寄せ方")]
    [SerializeField] private HorizontalAlign horizontalAlign = HorizontalAlign.Left;
    [SerializeField] private VerticalAlign verticalAlign = VerticalAlign.Top;

    [Header("大きさの調整")]
    [Tooltip("入りきらないときに縮小する。オフにすると縮小しない")]
    [SerializeField] private bool shrinkToFit = true;
    [Tooltip("余裕があるときに拡大する。オフなら等倍が上限")]
    [SerializeField] private bool allowScaleUp = false;
    [Tooltip("縮小の下限。これより小さくはしない")]
    [SerializeField] private float minScale = 0.5f;

    [Header("エディタでの動作")]
    [Tooltip("エディタでも自動で配置する")]
    [SerializeField] private bool updateInEditor = true;

    private RectTransform selfRect;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void OnRectTransformDimensionsChange()
    {
        Apply();
    }

    private void LateUpdate()
    {
        // 画面サイズの変更に追従するため、毎フレーム確認する（値が同じなら何もしない）
        Apply();
    }

    [ContextMenu("今すぐ配置する")]
    public void Apply()
    {
        if (!Application.isPlaying && !updateInEditor) return;
        if (leftPanel == null || rightPanel == null) return;

        if (selfRect == null) selfRect = transform as RectTransform;
        if (selfRect == null) return;

        Rect area = selfRect.rect;
        if (area.width <= 1f || area.height <= 1f) return;

        Vector2 leftSize = leftPanel.rect.size;
        Vector2 rightSize = rightPanel.rect.size;

        float totalW = leftSize.x + gap + rightSize.x;
        float totalH = Mathf.Max(leftSize.y, rightSize.y);
        if (totalW <= 0f || totalH <= 0f) return;

        float availW = Mathf.Max(1f, area.width - sideMargin * 2f);
        float availH = Mathf.Max(1f, area.height - contentTop - contentBottom);

        float scale = 1f;
        float fit = Mathf.Min(availW / totalW, availH / totalH);
        if (fit < 1f)
        {
            // 入りきらない場合
            if (shrinkToFit) scale = Mathf.Max(minScale, fit);
        }
        else if (allowScaleUp)
        {
            // 余裕がある場合
            scale = fit;
        }

        float groupW = totalW * scale;
        float groupH = totalH * scale;

        float startX = sideMargin;
        if (horizontalAlign == HorizontalAlign.Center)
        {
            startX += (availW - groupW) * 0.5f;
        }

        float startY = contentTop;
        if (verticalAlign == VerticalAlign.Middle)
        {
            startY += (availH - groupH) * 0.5f;
        }

        PlaceTopLeft(leftPanel, startX, startY, scale);
        PlaceTopLeft(rightPanel, startX + (leftSize.x + gap) * scale, startY, scale);
    }

    /// <summary>
    /// 親の左上を基準に、パネルの左上を指定の位置へ置く。
    /// </summary>
    private void PlaceTopLeft(RectTransform target, float x, float y, float scale)
    {
        Vector2 anchor = new Vector2(0f, 1f);
        Vector2 pos = new Vector2(x, -y);
        Vector3 s = new Vector3(scale, scale, 1f);

        if (target.anchorMin != anchor) target.anchorMin = anchor;
        if (target.anchorMax != anchor) target.anchorMax = anchor;
        if (target.pivot != anchor) target.pivot = anchor;

        if ((target.anchoredPosition - pos).sqrMagnitude > 0.0001f)
        {
            target.anchoredPosition = pos;
        }
        if ((target.localScale - s).sqrMagnitude > 0.000001f)
        {
            target.localScale = s;
        }
    }
}
