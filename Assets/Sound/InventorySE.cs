using UnityEngine;

/// <summary>
/// インベントリの音: 開く、閉じる、カーソル移動、アイテムを拾う、アイテムを使う。
/// InventoryUI と同じオブジェクトに付ける。
/// </summary>
public class InventorySE : MonoBehaviour
{
    [SerializeField] private InventoryUI inventory;

    private SEField openField;
    private SEField pickupField;
    private SEField cursorField;
    private SEField itemsField;

    private bool lastOpen;
    private bool lastPickup;
    private int lastCursor = -1;
    private int lastCount;

    private void Start()
    {
        if (inventory == null) inventory = GetComponent<InventoryUI>();
        if (inventory == null) inventory = FindFirstObjectByType<InventoryUI>();
        if (inventory == null) { enabled = false; return; }

        openField = new SEField(inventory, "isOpen");
        pickupField = new SEField(inventory, "isPickupOpen");
        cursorField = new SEField(inventory, "cursorIndex");
        itemsField = new SEField(inventory, "items");

        lastOpen = openField.GetBool();
        lastPickup = pickupField.GetBool();
        lastCursor = cursorField.GetInt(-1);
        lastCount = itemsField.GetCount();
    }

    private void Update()
    {
        SEBank b = SEManager.Bank;
        bool open = openField.GetBool();
        bool pickupOpen = pickupField.GetBool();
        int cursor = cursorField.GetInt(-1);
        int count = itemsField.GetCount();

        if (open != lastOpen)
        {
            SEManager.Play(open ? b.inventoryOpen : b.inventoryClose);
        }
        else if (open && cursor != lastCursor && cursor >= 0)
        {
            SEManager.Play(b.inventoryMove);
        }

        // 拾う画面が出た時(アイテムを調べた時)
        if (pickupOpen && !lastPickup) SEManager.Play(b.inventoryOpen, 0.6f, 1.15f);

        if (count > lastCount) SEManager.Play(b.pickup);
        else if (count < lastCount) SEManager.Play(b.inventoryUse);

        lastOpen = open;
        lastPickup = pickupOpen;
        lastCursor = cursor;
        lastCount = count;
    }
}
