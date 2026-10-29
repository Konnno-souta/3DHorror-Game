using UnityEngine;

/// <summary>
/// プレイヤーの体力管理
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(currentHealth - amount, 0);
        Debug.Log("プレイヤーがダメージを受けた: " + amount + " 残りHP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Debug.Log("プレイヤー死亡");
            // ここにゲームオーバー処理などを追加
        }
    }

    /// <summary>
    /// 回復アイテム使用時に呼ぶ。最大HPを超えて回復はしない。
    /// </summary>
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log("HPを回復した: +" + amount + " 現在HP: " + currentHealth);
    }
}