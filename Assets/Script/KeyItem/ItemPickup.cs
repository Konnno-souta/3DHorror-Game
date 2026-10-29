using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("アイテム図鑑への参照")]
    [SerializeField] private ItemDatabase itemDatabase;

    private ItemData itemData;
    private bool isNear;
    private InventoryUI inventoryUI;
    private HorrorCameraFollow cameraFollow;
    private bool isShowing = false;

    void Awake()
    {
        if (itemDatabase == null)
        {
            Debug.LogError($"[ItemPickup] '{gameObject.name}' に ItemDatabase が割り当てられていません。");
            return;
        }

        itemData = itemDatabase.GetItemByTag(gameObject.tag);

        if (itemData == null)
        {
            Debug.LogError($"[ItemPickup] '{gameObject.name}' のTag '{gameObject.tag}' に対応するアイテムが見つかりません。");
        }
    }

    void Start()
    {
        inventoryUI = FindObjectOfType<InventoryUI>();
        cameraFollow = FindObjectOfType<HorrorCameraFollow>();
    }

    void Update()
    {
        if (isNear && !isShowing && Input.GetKeyDown(KeyCode.E))
        {
            if (itemData == null)
            {
                Debug.LogWarning($"'{gameObject.name}' の itemData が null のため拾えません。");
                return;
            }

            isShowing = true;
            inventoryUI.OpenPickupUI(this, itemData);
        }
    }

    public void Pickup()
    {
        inventoryUI.AddItem(itemData);

        // 懐中電灯アイテムなら、拾った瞬間に使用可能にする
        if (itemData.unlocksFlashlight && cameraFollow != null)
        {
            cameraFollow.UnlockFlashlight();
        }

        Destroy(gameObject);
    }

    public void CancelPickup()
    {
        isShowing = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            isNear = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            isNear = false;
    }
}