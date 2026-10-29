using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホラーゲーム向けプレイヤー移動
/// ・WASD移動
/// ・Shiftでダッシュ
/// ・スタミナ消費
/// ・スタミナ回復
/// ・UIゲージ表示
/// ・ゆっくりしたホラー風移動
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class HorrorPlayerController : MonoBehaviour
{
    //==================================================
    // 移動設定
    //==================================================

    [Header("移動速度")]
    [SerializeField] private float walkSpeed = 2.0f;   // 通常移動
    [SerializeField] private float runSpeed = 4.0f;    // ダッシュ速度

    [Header("ダッシュ速度")]
    [SerializeField] private float normalRunSpeed = 6f;   // 緑
    [SerializeField] private float yellowRunSpeed = 5f;   // 黄色
    [SerializeField] private float orangeRunSpeed = 2f;   // オレンジ
    [SerializeField] private float redRunSpeed = 1f;      // 赤

    [Header("重力")]
    [SerializeField] private float gravity = -9.81f;

    //==================================================
    // スタミナ設定
    //==================================================

    [Header("スタミナ")]
    [SerializeField] private float maxStamina = 100f;          // 最大スタミナ
    [SerializeField] private float staminaConsume = 20f;       // 秒間消費量
    [SerializeField] private float staminaRecovery = 15f;      // 秒間回復量
    [SerializeField] private float recoveryDelay = 1.5f;       // 回復開始までの待機時間

    //==================================================
    // UI
    //==================================================

    //==================================================
    // 内部変数
    //==================================================

    private CharacterController controller;

    // 現在のスタミナ
    private float currentStamina;
    // スタミナ切れ状態
    private bool isExhausted = false;
    // 疲労状態になってからの時間
    private float exhaustedTimer = 0f;
    // 疲労後の待機時間
    [SerializeField]
    private float exhaustedRecoveryDelay = 3f;
    // この値まで回復したら再び走れる
    [SerializeField]
    private float runRecoveryThreshold = 20f;
    // 重力用
    private Vector3 velocity;

    // ダッシュ終了後の時間計測
    private float recoveryTimer;

    // ここに追加
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    // ダッシュ中かどうか
    public bool IsRunning { get; private set; }
    void Start()
    {
        // CharacterController取得
        controller = GetComponent<CharacterController>();

        // スタミナ初期化
        currentStamina = maxStamina;
    }

    void Update()
    {
        Move();
    }

    /// <summary>
    /// プレイヤー移動
    /// </summary>
    private void Move()
    {
        // キー入力取得
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // 移動方向
        Vector3 move = transform.right * x + transform.forward * z;

        //==================================================
        // ダッシュ判定
        //==================================================
        bool isRunning =
            Input.GetKey(KeyCode.LeftShift) &&
            move.magnitude > 0.1f &&
            !isExhausted;

        // 他のスクリプトから取得できるように保存
        IsRunning = isRunning;

        // 現在のダッシュ速度
        float currentRunSpeed = normalRunSpeed;

        // 黄色（40以下）
        if (currentStamina <= 40f)
        {
            currentRunSpeed = yellowRunSpeed;
        }

        // オレンジ（30以下）
        if (currentStamina <= 30f)
        {
            currentRunSpeed = orangeRunSpeed;
        }

        // 赤（10以下）
        if (currentStamina <= 10f)
        {
            currentRunSpeed = redRunSpeed;
        }

        // 実際の移動速度
        float currentSpeed = isRunning ? currentRunSpeed : walkSpeed;

        //==================================================
        // スタミナ処理
        //==================================================

        if (isRunning)
        {
            recoveryTimer = 0f;
            exhaustedTimer = 0f;

            // スタミナ消費
            currentStamina -= staminaConsume * Time.deltaTime;

            // 0～100に制限
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

            // スタミナ切れ
            if (currentStamina <= 0f)
            {
                isExhausted = true;
                exhaustedTimer = 0f;
                recoveryTimer = 0f;
            }
        }
        else
        {
            //========================
            // 疲労状態
            //========================
            if (isExhausted)
            {
                exhaustedTimer += Time.deltaTime;

                // 3秒後から回復
                if (exhaustedTimer >= exhaustedRecoveryDelay)
                {
                    currentStamina += staminaRecovery * Time.deltaTime;
                    currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

                    // 一定まで回復したら走れる
                    if (currentStamina >= runRecoveryThreshold)
                    {
                        isExhausted = false;
                        exhaustedTimer = 0f;
                        recoveryTimer = 0f;
                    }
                }
            }
            //========================
            // 通常回復
            //========================
            else
            {
                recoveryTimer += Time.deltaTime;

                if (recoveryTimer >= recoveryDelay)
                {
                    currentStamina += staminaRecovery * Time.deltaTime;
                    currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
                }
            }
        }

        //==================================================
        // 移動処理
        //==================================================

        controller.Move(move * currentSpeed * Time.deltaTime);

        //==================================================
        // 重力処理
        //==================================================

        // 地面にいる時
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 重力加算
        velocity.y += gravity * Time.deltaTime;

        // 重力移動
        controller.Move(velocity * Time.deltaTime);
    }
}