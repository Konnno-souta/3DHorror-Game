using UnityEngine;

/// <summary>
/// アイテムの種類。使用時の挙動を分岐するために使う。
/// </summary>
public enum ItemType
{
    Consumable, // 使うと消費される（回復薬など）
    Key,        // 扉などの鍵として使う（itemTagが扉のRequired Key Tagと照合される）
    Document,   // メモ・手紙（読むだけ、消費されない）
    Tool,       // 懐中電灯などの装備系（インベントリからの「使用」はしない）
    Battery,    // 懐中電灯の電池を回復する（バッテリー(大)/(小)など）
    Misc        // その他
}

/// <summary>
/// アイテムデータ（普通のクラス）
/// </summary>
[System.Serializable]
public class ItemData
{
    [Header("名前")]
    public string itemName;

    [Header("説明")]
    [TextArea]
    public string description;

    [Header("アイコン")]
    public Sprite icon;

    [Header("3D表示Prefab")]
    public GameObject displayPrefab;

    [Header("Tag対応")]
    [Tooltip("このアイテムに対応するUnityのTag名（例: Key, Battery）。鍵の場合は扉のRequired Key Tagと一致させる")]
    public string itemTag;

    [Header("種類")]
    public ItemType itemType = ItemType.Misc;

    [Header("使用効果")]
    [Tooltip("ItemType = Consumable の時、使用すると回復するHP量")]
    public int healAmount = 20;

    [Tooltip("ItemType = Battery の時、使用すると回復する懐中電灯の電池量（％、最大電池量に対する割合）")]
    public float batteryRechargePercent = 50f;

    [Header("特殊効果")]
    [Tooltip("このアイテムを拾うと、懐中電灯が使用可能になる")]
    public bool unlocksFlashlight;
}