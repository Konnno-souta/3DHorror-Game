using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全アイテムのItemDataをまとめて管理し、Unityの Tag 名から検索できるようにする図鑑。
/// Project ウィンドウで右クリック → Create > Inventory > Item Database で作成し、
/// シーンに置く InventoryUI や ItemPickup から参照します。
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Tooltip("ゲーム内に存在する全アイテムのItemDataをここに登録する")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    private Dictionary<string, ItemData> lookup;

    /// <summary>
    /// 指定したTag名に対応するItemDataを取得する。見つからなければnull。
    /// </summary>
    public ItemData GetItemByTag(string tag)
    {
        if (lookup == null) BuildLookup();

        if (lookup.TryGetValue(tag, out var item))
        {
            return item;
        }

        Debug.LogWarning($"[ItemDatabase] Tag '{tag}' に対応する ItemData が見つかりません。" +
                          "ItemData.itemTag の設定を確認してください。");
        return null;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<string, ItemData>();

        foreach (var item in allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.itemTag))
            {
                continue;
            }

            if (lookup.ContainsKey(item.itemTag))
            {
                Debug.LogWarning($"[ItemDatabase] Tag '{item.itemTag}' が重複しています: {item.itemName}");
                continue;
            }

            lookup.Add(item.itemTag, item);
        }
    }

    private void OnEnable()
    {
        lookup = null;
    }
}