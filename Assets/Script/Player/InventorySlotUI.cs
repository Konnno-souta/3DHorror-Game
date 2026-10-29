using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// スロットUI
/// ・背景は「空」「アイテムあり」で自動的にスプライトを切り替える
/// ・ホバー：ブラケット枠が表示され、アイコンが少し拡大する
/// ・クリック：選択状態になり、アンバーの枠がハイライトされ続ける（他のスロットの選択は自動解除）
/// </summary>
public class InventorySlotUI :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("アイコン")]
    [SerializeField] private Image iconImage;

    [Header("背景（空/アイテムありで自動切替・任意）")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite filledBackgroundSprite;   // slot_frame_normal
    [SerializeField] private Sprite emptyBackgroundSprite;    // slot_frame_empty

    [Header("選択ハイライト（任意）")]
    [Tooltip("選択中だけ表示する枠。子にImageを作り、slot_frame_selectedを割り当てて普段は非アクティブにしておく")]
    [SerializeField] private GameObject selectedFrame;

    [Header("ホバー時の枠（任意）")]
    [Tooltip("ホバー中だけ表示する枠。子にImageを作り、slot_frame_hoverを割り当てて普段は非アクティブにしておく")]
    [SerializeField] private GameObject hoverFrame;

    [Header("ホバー演出")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float scaleLerpSpeed = 12f;

    private ItemData currentItem;
    private InventoryUI inventoryUI;
    private bool isHovering = false;
    private Vector3 baseScale = Vector3.one;
    private RectTransform rectTransform;

    public ItemData CurrentItem => currentItem;

    void Awake()
    {
        rectTransform = (RectTransform)transform;
        baseScale = rectTransform.localScale;

        if (selectedFrame != null) selectedFrame.SetActive(false);
        if (hoverFrame != null) hoverFrame.SetActive(false);

        ApplyEmptyBackground();
    }

    void Update()
    {
        // ホバー中は少し拡大、それ以外は元のサイズへ滑らかに戻す
        // unscaledDeltaTime を使うので、インベントリを開いて Time.timeScale = 0 でも動く
        Vector3 targetScale = isHovering ? baseScale * hoverScale : baseScale;
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale, targetScale, scaleLerpSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// アイテム設定
    /// </summary>
    public void SetItem(ItemData item, InventoryUI ui)
    {
        if (item == null || ui == null || iconImage == null)
        {
            Debug.LogError($"SetItem：必要な参照が不足しています。({gameObject.name})");
            return;
        }

        currentItem = item;
        inventoryUI = ui;
        iconImage.sprite = item.icon;
        iconImage.enabled = true;

        ApplyFilledBackground();
    }

    /// <summary>
    /// 空にする
    /// </summary>
    public void ClearSlot()
    {
        currentItem = null;
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        SetSelected(false);
        ApplyEmptyBackground();
    }

    private void ApplyFilledBackground()
    {
        if (backgroundImage != null && filledBackgroundSprite != null)
        {
            backgroundImage.sprite = filledBackgroundSprite;
        }
    }

    private void ApplyEmptyBackground()
    {
        if (backgroundImage != null && emptyBackgroundSprite != null)
        {
            backgroundImage.sprite = emptyBackgroundSprite;
        }
    }

    /// <summary>
    /// 選択状態の切り替え。InventoryUI側から呼ばれる。
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
        {
            selectedFrame.SetActive(selected);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (hoverFrame != null)
        {
            hoverFrame.SetActive(true);
        }

        if (currentItem != null && inventoryUI != null)
        {
            inventoryUI.ShowItemDetail(currentItem);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (hoverFrame != null)
        {
            hoverFrame.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentItem == null || inventoryUI == null) return;

        inventoryUI.SelectSlot(this, currentItem);
    }
}