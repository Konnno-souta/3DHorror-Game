using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// インベントリUI管理
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Header("インベントリ画面")]
    [SerializeField] private GameObject inventoryPanel;

    [SerializeField] private HorrorCameraFollow cameraController;

    [Header("スロット親")]
    [SerializeField] private Transform slotParent;

    [Header("詳細UI")]
    [SerializeField] private TMP_Text itemNameText;

    [Header("説明")]
    [SerializeField] private TMP_Text descriptionText;

    [Header("3Dモデル表示位置")]
    [SerializeField] private Transform displayPoint;

    [Header("3D表示専用カメラ（任意：開いている間だけON）")]
    [SerializeField] private Camera itemDisplayCamera;

    [Header("拾う確認UI")]
    [SerializeField] private GameObject pickupPanel;
    [SerializeField] private TMP_Text pickupText;
    [SerializeField] private Image pickupIconImage;

    [Header("満杯通知UI（任意）")]
    [SerializeField] private GameObject fullNoticePanel;
    [SerializeField] private TMP_Text fullNoticeText;
    [SerializeField] private float fullNoticeDuration = 2f;

    [Header("使用キー")]
    [SerializeField] private KeyCode useKey = KeyCode.Return;

    [Header("プレイヤー体力（回復アイテム用、任意・未設定なら自動検索）")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("懐中電灯（電池アイテム用、任意・未設定なら自動検索）")]
    [SerializeField] private HorrorCameraFollow flashlightController;

    [Header("3D表示 回転設定")]
    [SerializeField] private float displayRotateSpeed = 30f;
    [SerializeField] private string displayLayerName = "ItemDisplay";
    [Tooltip("表示時に自動調整する、モデルの最大辺の目安サイズ（ワールド単位）")]
    [SerializeField] private float targetDisplaySize = 1.5f;

    private bool isOpen = false;
    private ItemPickup currentPickup;
    private readonly List<ItemData> items = new List<ItemData>();
    private GameObject currentDisplayObject;
    private bool isPickupOpen = false;
    private readonly int maxSlot = 9;

    private ItemData selectedItem;
    private InventorySlotUI selectedSlotUI;
    private Coroutine fullNoticeCoroutine;

    public ItemData SelectedItem => selectedItem;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        inventoryPanel.SetActive(false);

        if (fullNoticePanel != null)
        {
            fullNoticePanel.SetActive(false);
        }

        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
        }

        if (flashlightController == null)
        {
            flashlightController = FindObjectOfType<HorrorCameraFollow>();
        }

        if (itemDisplayCamera != null)
        {
            itemDisplayCamera.enabled = false;
        }
    }

    private void Update()
    {
        // インベントリが開いている間、表示中の3Dモデルを回転させる
        if (currentDisplayObject != null)
        {
            currentDisplayObject.transform.Rotate(0f, displayRotateSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }

        // 確認画面表示中のみ受付
        if (isPickupOpen)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                PickupYes();
            }
            if (Input.GetKeyDown(KeyCode.T))
            {
                PickupCancel();
            }
            return;
        }

        // TABでインベントリ開閉
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }

        // インベントリを開いている間、選択中アイテムを使用
        if (isOpen && Input.GetKeyDown(useKey))
        {
            UseSelectedItem();
        }
    }

    // ---------------- 拾う確認 ----------------

    /// <summary>
    /// アイテム取得確認画面
    /// </summary>
    public void OpenPickupUI(ItemPickup pickup, ItemData item)
    {
        currentPickup = pickup;

        pickupPanel.SetActive(true);
        pickupText.text = item.itemName + "\n\nE：拾う\nT：キャンセル";

        pickupIconImage.sprite = item.icon;
        pickupIconImage.enabled = item.icon != null;

        isPickupOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void PickupYes()
    {
        currentPickup?.Pickup();
        ClosePickupUI();
    }

    private void PickupCancel()
    {
        currentPickup?.CancelPickup();
        ClosePickupUI();
    }

    private void ClosePickupUI()
    {
        pickupPanel.SetActive(false);
        isPickupOpen = false;
        currentPickup = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ---------------- 開閉 ----------------

    /// <summary>
    /// インベントリ開閉
    /// </summary>
    private void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel.SetActive(isOpen);

        if (itemDisplayCamera != null)
        {
            itemDisplayCamera.enabled = isOpen;
        }

        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;

            if (items.Count > 0)
            {
                ShowItemDetail(items[0]);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;

            // 閉じたら選択状態・3Dプレビューを後片付けする
            ClearSelection();

            if (currentDisplayObject != null)
            {
                Destroy(currentDisplayObject);
                currentDisplayObject = null;
            }
        }
    }

    // ---------------- アイテム追加・スロット更新 ----------------

    /// <summary>
    /// アイテム追加
    /// </summary>
    public void AddItem(ItemData item)
    {
        if (items.Count >= maxSlot)
        {
            ShowFullNotice();
            return;
        }

        items.Add(item); // 常に末尾に追加＝拾った順
        RefreshSlots();
    }

    /// <summary>
    /// アイテムをインベントリから1つ取り除く（使用・消費時に呼ぶ）。
    /// </summary>
    public void RemoveItem(ItemData item)
    {
        items.Remove(item);
        RefreshSlots();

        if (selectedItem == item)
        {
            ClearSelection();

            if (currentDisplayObject != null)
            {
                Destroy(currentDisplayObject);
                currentDisplayObject = null;
            }
        }
    }

    /// <summary>
    /// スロット更新
    /// </summary>
    private void RefreshSlots()
    {
        InventorySlotUI[] slots = slotParent.GetComponentsInChildren<InventorySlotUI>(true);

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < items.Count)
                slots[i].SetItem(items[i], this);
            else
                slots[i].ClearSlot();
        }
    }

    private void ShowFullNotice()
    {
        Debug.Log("インベントリ満杯");

        if (fullNoticePanel == null) return;

        if (fullNoticeText != null)
        {
            fullNoticeText.text = "インベントリがいっぱいです";
        }

        if (fullNoticeCoroutine != null)
        {
            StopCoroutine(fullNoticeCoroutine);
        }
        fullNoticeCoroutine = StartCoroutine(FullNoticeRoutine());
    }

    private IEnumerator FullNoticeRoutine()
    {
        fullNoticePanel.SetActive(true);
        yield return new WaitForSecondsRealtime(fullNoticeDuration);
        fullNoticePanel.SetActive(false);
        fullNoticeCoroutine = null;
    }

    // ---------------- 選択 ----------------

    /// <summary>
    /// スロットがクリックされた時、InventorySlotUI側から呼ばれる。
    /// 前の選択を解除し、新しいスロットを選択状態にする。
    /// </summary>
    public void SelectSlot(InventorySlotUI slot, ItemData item)
    {
        if (selectedSlotUI != null && selectedSlotUI != slot)
        {
            selectedSlotUI.SetSelected(false);
        }

        selectedSlotUI = slot;
        selectedSlotUI.SetSelected(true);
        selectedItem = item;

        ShowItemDetail(item);
    }

    private void ClearSelection()
    {
        if (selectedSlotUI != null)
        {
            selectedSlotUI.SetSelected(false);
        }
        selectedSlotUI = null;
        selectedItem = null;
    }

    // ---------------- 使用 ----------------

    /// <summary>
    /// 選択中のアイテムを使用する（useKey、デフォルトEnter）。
    /// </summary>
    public void UseSelectedItem()
    {
        if (selectedItem == null)
        {
            Debug.Log("使用するアイテムが選択されていません。");
            return;
        }

        switch (selectedItem.itemType)
        {
            case ItemType.Consumable:
                UseConsumable(selectedItem);
                break;

            case ItemType.Battery:
                UseBattery(selectedItem);
                break;

            case ItemType.Key:
                Debug.Log($"'{selectedItem.itemName}' は鍵のかかった扉の前で使ってください。");
                break;

            case ItemType.Document:
                Debug.Log($"'{selectedItem.itemName}' を読んだ。");
                break;

            case ItemType.Tool:
                Debug.Log($"'{selectedItem.itemName}' は装備品です。インベントリからの使用はできません。");
                break;

            default:
                Debug.Log($"'{selectedItem.itemName}' は使用できません。");
                break;
        }
    }

    private void UseConsumable(ItemData item)
    {
        if (playerHealth != null)
        {
            playerHealth.Heal(item.healAmount);
            Debug.Log($"'{item.itemName}' を使用した。HPを{item.healAmount}回復。");
        }
        else
        {
            Debug.LogWarning("PlayerHealth が見つからないため、回復処理を行えません。");
        }

        RemoveItem(item);
    }

    private void UseBattery(ItemData item)
    {
        if (flashlightController != null)
        {
            flashlightController.RechargeBatteryByPercent(item.batteryRechargePercent);
            Debug.Log($"'{item.itemName}' を使用した。懐中電灯の電池を{item.batteryRechargePercent}%回復。");
        }
        else
        {
            Debug.LogWarning("HorrorCameraFollow が見つからないため、電池の回復処理を行えません。");
        }

        RemoveItem(item);
    }

    // ---------------- 鍵・扉との連携用 ----------------

    /// <summary>
    /// 指定Tagのアイテムを所持しているか（扉の鍵判定などに使用）。
    /// </summary>
    public bool HasItemByTag(string tag)
    {
        return items.Exists(i => i.itemTag == tag);
    }

    /// <summary>
    /// 指定Tagのアイテムを1つ消費する（扉を開けたら鍵を消費する、等）。
    /// </summary>
    public bool ConsumeItemByTag(string tag)
    {
        ItemData found = items.Find(i => i.itemTag == tag);
        if (found == null) return false;

        RemoveItem(found);
        return true;
    }

    // ---------------- 詳細表示 ----------------

    /// <summary>
    /// 詳細表示
    /// </summary>
    public void ShowItemDetail(ItemData item)
    {
        if (item == null) return;

        itemNameText.text = item.itemName;
        descriptionText.text = item.description;

        if (currentDisplayObject != null)
        {
            Destroy(currentDisplayObject);
        }

        if (item.displayPrefab != null)
        {
            currentDisplayObject = Instantiate(
                item.displayPrefab,
                displayPoint.position,
                Quaternion.identity,
                displayPoint
            );

            SetLayerRecursively(currentDisplayObject, displayLayerName);
            AutoFitAndCenter(currentDisplayObject);
        }
        else
        {
            currentDisplayObject = null;
        }
    }

    private void SetLayerRecursively(GameObject obj, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogWarning($"Layer '{layerName}' が存在しません。Project Settings > Tags and Layers で作成してください。");
            return;
        }

        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layerName);
        }
    }

    private void AutoFitAndCenter(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDimension <= 0.0001f) return;

        float scaleFactor = targetDisplaySize / maxDimension;
        obj.transform.localScale *= scaleFactor;

        renderers = obj.GetComponentsInChildren<Renderer>();
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 offset = obj.transform.position - bounds.center;
        obj.transform.position += offset;
    }
}