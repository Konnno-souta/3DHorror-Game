using UnityEngine;
using TMPro;

/// <summary>
/// 鍵が必要な扉。プレイヤーが近づいてEキーを押すと、
/// InventoryUIに対応するTagのアイテムがあるか確認し、あれば開く。
/// Colliderを IsTrigger = true にしてアタッチしてください。
/// </summary>
[RequireComponent(typeof(Collider))]
public class LockedDoor : MonoBehaviour
{
    [Header("必要な鍵のTag（ItemDataのItem Tagと一致させる）")]
    [SerializeField] private string requiredKeyTag = "Key";

    [Header("鍵を使ったら消費するか")]
    [SerializeField] private bool consumeKeyOnUse = true;

    [Header("開錠演出（任意）")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string openTriggerName = "Open";

    [Header("操作キー")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("メッセージUI（任意）")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TMP_Text promptText;

    private bool playerInRange = false;
    private bool isOpen = false;

    void Update()
    {
        if (!playerInRange || isOpen) return;

        if (Input.GetKeyDown(interactKey))
        {
            TryOpen();
        }
    }

    private void TryOpen()
    {
        if (InventoryUI.Instance == null)
        {
            Debug.LogWarning("InventoryUI が見つかりません。");
            return;
        }

        bool hasKey = consumeKeyOnUse
            ? InventoryUI.Instance.ConsumeItemByTag(requiredKeyTag)
            : InventoryUI.Instance.HasItemByTag(requiredKeyTag);

        if (hasKey)
        {
            Open();
        }
        else if (promptText != null)
        {
            promptText.text = "鍵が必要だ…";
        }
        else
        {
            Debug.Log($"'{requiredKeyTag}' が必要です。");
        }
    }

    private void Open()
    {
        isOpen = true;

        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger(openTriggerName);
        }

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;

        if (!isOpen && promptUI != null)
        {
            if (promptText != null)
            {
                promptText.text = "Eキーで開ける";
            }
            promptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }
}
