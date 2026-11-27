using TMPro;
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

    [Header("ホバー演出（リスト式にする場合は Hover Scale を 1 にする）")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float scaleLerpSpeed = 12f;

    [Header("リスト式の行表示（任意。使わない項目は空のままでよい）")]
    [Tooltip("行の番号（01、02...）。並び順から自動で入る")]
    [SerializeField] private TMP_Text numberText;
    [Tooltip("アイテム名")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("種類の表示（KEY、BATTERY、TOOL...）")]
    [SerializeField] private TMP_Text typeText;
    [Tooltip("空の行に出す文字（- EMPTY -）。空でない時は自動で隠れる")]
    [SerializeField] private TMP_Text emptyText;
    [Tooltip("選択中の行の、種類表示の後ろに付ける記号")]
    [SerializeField] private string selectedSuffix = " <";

    [Header("リスト式の文字色")]
    [SerializeField] private Color normalTextColor = new Color(0.82f, 0.85f, 0.88f);
    [SerializeField] private Color selectedTextColor = new Color(1f, 0.89f, 0.70f);
    [SerializeField] private Color typeTextColor = new Color(0.58f, 0.63f, 0.67f);
    [SerializeField] private Color selectedTypeColor = new Color(0.88f, 0.57f, 0.18f);
    [SerializeField] private Color emptyTextColor = new Color(0.44f, 0.48f, 0.52f);

    private bool isSelected = false;
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

        if (numberText != null)
        {
            numberText.text = (transform.GetSiblingIndex() + 1).ToString("00");
        }

        RefreshRowTexts();
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
        RefreshRowTexts();
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
        RefreshRowTexts();
    }

    /// <summary>
    /// リスト式の行の文字（名前、種類、EMPTY）と色を、今の状態に合わせて更新する。
    /// 対応するTMP_Textが未設定の項目は、何もしない。
    /// </summary>
    private void RefreshRowTexts()
    {
        bool hasItem = currentItem != null;

        if (nameText != null)
        {
            nameText.gameObject.SetActive(hasItem);
            if (hasItem)
            {
                nameText.text = currentItem.itemName;
                nameText.color = isSelected ? selectedTextColor : normalTextColor;
            }
        }

        if (typeText != null)
        {
            typeText.gameObject.SetActive(hasItem);
            if (hasItem)
            {
                typeText.text = TypeLabel(currentItem.itemType) + (isSelected ? selectedSuffix : "");
                typeText.color = isSelected ? selectedTypeColor : typeTextColor;
            }
        }

        if (numberText != null)
        {
            numberText.color = hasItem
                ? (isSelected ? selectedTypeColor : typeTextColor)
                : emptyTextColor;
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(!hasItem);
            emptyText.color = emptyTextColor;
        }
    }

    private static string TypeLabel(ItemType type)
    {
        switch (type)
        {
            case ItemType.Key: return "KEY";
            case ItemType.Battery: return "BATTERY";
            case ItemType.Tool: return "TOOL";
            case ItemType.Consumable: return "SUPPLY";
            case ItemType.Document: return "MEMO";
            default: return "ITEM";
        }
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
        isSelected = selected;

        if (selectedFrame != null)
        {
            selectedFrame.SetActive(selected);
        }

        RefreshRowTexts();
    }

    /// <summary>
    /// 枠(ホバー枠)を付けたり消したりする。マウスでも、上下キーでも、ここを通る。
    /// </summary>
    public void SetHover(bool on)
    {
        isHovering = on;

        if (hoverFrame != null)
        {
            hoverFrame.SetActive(on);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 空の行は inventoryUI が入っていないので、Instance も見る
        InventoryUI ui = inventoryUI != null ? inventoryUI : InventoryUI.Instance;

        if (ui != null)
        {
            // カーソルをこの行に合わせる(枠と、詳しい表示は InventoryUI がそろえる)
            ui.OnSlotPointerEnter(this);
        }
        else
        {
            SetHover(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 上下キーでも選べるので、マウスが外れても、枠は残す(カーソルの位置を見失わないため)
        if (inventoryUI == null && InventoryUI.Instance == null)
        {
            SetHover(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (currentItem == null || inventoryUI == null) return;

        inventoryUI.SelectSlot(this, currentItem);

        // 左クリックでそのまま使用する（回復薬・電池などはここで消費される）
        inventoryUI.UseSelectedItem();
    }
}