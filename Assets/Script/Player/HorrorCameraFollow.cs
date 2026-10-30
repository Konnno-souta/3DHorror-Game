using UnityEngine;

public class HorrorCameraFollow : MonoBehaviour
{
    [Header("プレイヤー")]
    [SerializeField] private Transform target;

    [Header("ライト")]
    [SerializeField] private Light spotLight;

    [Header("懐中電灯の見た目（カメラ右下に表示するモデル、任意）")]
    [SerializeField] private GameObject flashlightModel;

    [Header("頭の高さ")]
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 1.6f, 0f);

    [Header("マウス感度")]
    [SerializeField] private float mouseSensitivity = 100f;

    [Header("上下制限")]
    [SerializeField] private float minXRotation = -80f;
    [SerializeField] private float maxXRotation = 80f;

    [Header("懐中電灯キー")]
    [SerializeField] private KeyCode flashlightKey = KeyCode.F;

    [Header("FOV設定")]
    [SerializeField] private Camera playerCamera;

    [SerializeField] private HorrorPlayerController player;

    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float yellowFOV = 58f;
    [SerializeField] private float orangeFOV = 54f;
    [SerializeField] private float redFOV = 48f;

    [SerializeField] private float fovSpeed = 5f;

    // 縦回転
    private float xRotation = 0f;

    // 横回転
    private float yRotation = 0f;

    // 懐中電灯ON/OFF状態
    private bool isFlashlightOn = true;

    // 懐中電灯を入手済みかどうか（未入手なら使用不可）
    [Header("懐中電灯 入手状態")]
    [SerializeField] private bool isFlashlightUnlocked = false;

    public bool CanLook = true;
    public bool IsFlashlightUnlocked => isFlashlightUnlocked;

    [Header("懐中電灯 電池")]
    [SerializeField] private float maxBattery = 100f;
    [Tooltip("点灯中、1秒あたりに減る電池量")]
    [SerializeField] private float batteryDrainPerSecond = 2f;
    [Tooltip("この％を下回ると、緩やかに明滅し始める")]
    [SerializeField] private float lowBatteryPercent = 25f;
    [Tooltip("この％を下回ると、激しく明滅する")]
    [SerializeField] private float criticalBatteryPercent = 10f;

    private float currentBattery;
    private float baseLightIntensity = 1f;
    private float flickerSeed;

    /// <summary>現在の電池残量（0から1）</summary>
    public float BatteryPercent01 => maxBattery > 0f ? Mathf.Clamp01(currentBattery / maxBattery) : 0f;
    public bool IsBatteryEmpty => currentBattery <= 0f;


    void Start()
    {
        // マウスカーソルを非表示＆中央固定
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        playerCamera.fieldOfView = normalFOV;

        // 初期状態：未入手なら強制的にOFF・非表示
        if (!isFlashlightUnlocked)
        {
            isFlashlightOn = false;
        }

        if (spotLight != null)
        {
            spotLight.enabled = isFlashlightUnlocked && isFlashlightOn;
        }

        if (flashlightModel != null)
        {
            flashlightModel.SetActive(isFlashlightUnlocked);
        }

        // 電池初期化
        currentBattery = maxBattery;
        flickerSeed = Random.Range(0f, 100f);

        if (spotLight != null)
        {
            baseLightIntensity = spotLight.intensity;
        }
    }

    void LateUpdate()
    {
        // target未設定なら処理しない
        if (target == null) return;

        if (target == null)
            return;

        // 視点回転
        MouseLook();

        // プレイヤー追従
        Follow();

        // ライト位置更新
        UpdateLight();

        // 懐中電灯ON/OFF
        FlashlightToggle();

        // 電池消費・明滅
        UpdateBattery();

        // FOV更新
        UpdateFOV();
    }

    /// <summary>
    /// マウス視点操作
    /// </summary>
    private void MouseLook()
    {
        // マウス入力取得
        float mouseX =
            Input.GetAxis("Mouse X") *
            mouseSensitivity *
            Time.deltaTime;

        float mouseY =
            Input.GetAxis("Mouse Y") *
            mouseSensitivity *
            Time.deltaTime;

        //==============================
        // 横回転
        //==============================

        yRotation += mouseX;

        //==============================
        // 縦回転
        //==============================

        xRotation -= mouseY;

        // 上下制限
        xRotation = Mathf.Clamp(
            xRotation,
            minXRotation,
            maxXRotation
        );

        //==============================
        // プレイヤー本体回転
        //==============================

        target.rotation =
            Quaternion.Euler(0f, yRotation, 0f);

        //==============================
        // カメラ回転
        //==============================

        transform.rotation =
            Quaternion.Euler(xRotation, yRotation, 0f);
    }

    /// <summary>
    /// プレイヤー追従
    /// </summary>
    private void Follow()
    {
        // 頭位置へ移動
        transform.position =
            target.position + headOffset;
    }

    /// <summary>
    /// ライト位置更新
    /// </summary>
    private void UpdateLight()
    {
        // ライト未設定なら終了
        if (spotLight == null) return;

        // カメラ位置
        spotLight.transform.position =
            transform.position;

        // カメラ向き
        spotLight.transform.rotation =
            transform.rotation;
    }

    /// <summary>
    /// 懐中電灯ON/OFF
    /// </summary>
    private void FlashlightToggle()
    {
        // 未入手なら操作不可
        if (!isFlashlightUnlocked) return;

        // Fキー押した瞬間
        if (Input.GetKeyDown(flashlightKey))
        {
            // 電池切れの間はONにできない
            if (IsBatteryEmpty && !isFlashlightOn)
            {
                Debug.Log("電池切れで懐中電灯がつけられない。");
                return;
            }

            // ON/OFF切り替え
            isFlashlightOn = !isFlashlightOn;

            // Light有効切替
            if (spotLight != null)
            {
                spotLight.enabled = isFlashlightOn;
            }
        }
    }

    /// <summary>
    /// 電池の消費と、残量に応じたライトの明滅
    /// </summary>
    private void UpdateBattery()
    {
        if (!isFlashlightUnlocked) return;

        // 点灯中だけ電池を消費する
        if (isFlashlightOn && currentBattery > 0f)
        {
            currentBattery = Mathf.Max(0f, currentBattery - batteryDrainPerSecond * Time.deltaTime);
        }

        // 電池切れになった瞬間、強制消灯
        if (isFlashlightOn && currentBattery <= 0f)
        {
            isFlashlightOn = false;

            if (spotLight != null)
            {
                spotLight.enabled = false;
            }

            Debug.Log("懐中電灯の電池が切れた。");
            return;
        }

        if (!isFlashlightOn || spotLight == null || !spotLight.enabled)
        {
            return;
        }

        float percent = BatteryPercent01 * 100f;

        if (percent > lowBatteryPercent)
        {
            // 残量十分：通常の明るさに戻す
            spotLight.intensity = baseLightIntensity;
            return;
        }

        // 残量が少ないほど、明滅が速く・激しくなる
        float lowness = Mathf.InverseLerp(lowBatteryPercent, 0f, percent); // 0から1
        bool isCritical = percent <= criticalBatteryPercent;

        float flickerSpeed = Mathf.Lerp(6f, 22f, lowness);
        float flickerAmount = Mathf.Lerp(0.15f, 0.8f, lowness);

        float noise = Mathf.PerlinNoise(flickerSeed, Time.unscaledTime * flickerSpeed);
        float flicker = 1f - flickerAmount * noise;

        // 電池が尽きる直前は、たまに完全に落ちる瞬間を混ぜる
        if (isCritical && Random.value < 0.02f)
        {
            flicker = 0f;
        }

        spotLight.intensity = baseLightIntensity * Mathf.Clamp01(flicker);
    }

    /// <summary>
    /// アイテム（電池など）で懐中電灯の電池を回復する。
    /// </summary>
    public void RechargeBattery(float amount)
    {
        currentBattery = Mathf.Clamp(currentBattery + amount, 0f, maxBattery);

        if (spotLight != null && isFlashlightOn)
        {
            spotLight.intensity = baseLightIntensity;
        }
    }

    /// <summary>
    /// 最大電池量に対する割合（0から100）で回復する。
    /// インベントリの電池アイテム（バッテリー(大)/(小)など）用。
    /// </summary>
    public void RechargeBatteryByPercent(float percent)
    {
        RechargeBattery(maxBattery * (percent / 100f));
    }

    /// <summary>
    /// アイテム取得時に呼ぶ。懐中電灯を使用可能にする。
    /// </summary>
    public void UnlockFlashlight()
    {
        if (isFlashlightUnlocked)
        {
            Debug.Log("懐中電灯は既に入手済みです。");
            return;
        }

        isFlashlightUnlocked = true;
        isFlashlightOn = true;

        if (spotLight != null)
        {
            spotLight.enabled = true;
        }

        if (flashlightModel != null)
        {
            flashlightModel.SetActive(true);
        }

        Debug.Log($"懐中電灯を入手した。{flashlightKey}キーでオン/オフを切り替えられます。");
    }

    /// <summary>
    /// スタミナに応じて視野角変更
    /// </summary>
    private void UpdateFOV()
    {
        if (player == null || playerCamera == null)
            return;

        float targetFOV = normalFOV;

        if (player.CurrentStamina <= 40)
        {
            targetFOV = yellowFOV;
        }

        if (player.CurrentStamina <= 30)
        {
            targetFOV = orangeFOV;
        }

        if (player.CurrentStamina <= 10)
        {
            targetFOV = redFOV;
        }

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovSpeed * Time.deltaTime);
    }
}