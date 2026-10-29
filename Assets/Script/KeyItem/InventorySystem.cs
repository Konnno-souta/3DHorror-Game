using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// インベントリ管理
/// </summary>
public class InventorySystem : MonoBehaviour
{
    // アイテム一覧
    private List<string> items =
        new List<string>();

    /// <summary>
    /// アイテム追加
    /// </summary>
    public void AddItem(string itemName)
    {
        // リストへ追加
        items.Add(itemName);

        Debug.Log(itemName + " を取得しました");
    }

    /// <summary>
    /// アイテム確認用
    /// </summary>
    public List<string> GetItems()
    {
        return items;
    }
}