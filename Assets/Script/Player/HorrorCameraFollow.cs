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

    [Header("敵に捕まった時のカメラ演出")]
    [SerializeField] private float focusTurnSpeed = 5f;
    [SerializeField] private float focusFOV = 42f;
    [Tooltip("ONなら、敵の方へ「横方向だけ」向く。上下の角度（目線の高さ）は捕まる直前のまま変えない")]
    [SerializeField] private bool focusYawOnly = true;
    [Tooltip("Focus Yaw Only がONの時、捕まっている間に上下の角度を足す（度）。マイナスで上向き、プラスで下向き。0なら捕まる直前のまま")]
    [SerializeField] private float focusPitchOffset = 0f;

    private Transform focusTarget;
    private float focusStartPitch;
    private Vector3 focusOffset;

    /// <summary>
    /// 敵に捕まった時など、カメラを指定の対象へ向けて操作を奪う。
    /// </summary>
    public void BeginFocus(Transform targetTransform, Vector3 offset)
    {
        focusTarget = targetTransform;
        focusOffset = offset;

        // 捕まる直前の上下の角度を覚えておく
        focusStartPitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
    }

    /// <summary>
    /// 演出を終わらせて、通常の視点操作に戻す。
    /// </summary>
    public void EndFocus()
    {
        focusTarget = null;

        // 現在の向きから、通常の視点操作を引き継ぐ
        yRotation = transform.eulerAngles.y;
        xRotation = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x), minXRotation, maxXRotation);
    }

    private void FocusLook()
    {
        Vector3 dir = (focusTarget.position + focusOffset) - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        // タイムスケールが0やスローでも、同じ速さで向く
        Quaternion want;
        if (focusYawOnly)
        {
            // 横方向だけ敵へ向け、上下の角度は固定する（目線の高さが変わらない）
            Vector3 flat = dir;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            float yaw = Quaternion.LookRotation(flat).eulerAngles.y;
            want = Quaternion.Euler(focusStartPitch + focusPitchOffset, yaw, 0f);
        }
        else
        {
            want = Quaternion.LookRotation(dir);
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            want,
            1f - Mathf.Exp(-focusTurnSpeed * Time.unscaledDeltaTime));
    }

    [Header("懐中電灯 電池（3マス式）")]
    [Tooltip("電池のマス数。ゲージの画像は3マス用")]
    [SerializeField] private int maxCells = 3;
    [Tooltip("点灯中、1マスが減るまでの時間（秒）。消灯中は減らない")]
    [SerializeField] private float secondsPerCell = 40f;

    [Header("マスが減る時の点滅")]
    [SerializeField] private int flickerCountMin = 2;
    [SerializeField] private int flickerCountMax = 3;
    [Tooltip("1回の点滅で、消えている時間（秒）")]
    [SerializeField] private float flickerOffTime = 0.07f;
    [Tooltip("1回の点滅で、点いている時間（秒）")]
    [SerializeField] private float flickerOnTime = 0.11f;
    [Tooltip("消えている間の明るさ（0で完全に消える）")]
    [SerializeField, Range(0f, 0.3f)] private float flickerOffLevel = 0.03f;

    [Header("マスが減るごとの明るさ")]
    [Tooltip("1マス減るごとに暗くなる割合（0.2なら 100% → 80% → 60%）")]
    [SerializeField, Range(0f, 0.5f)] private float dimPerLostCell = 0.2f;
    [Tooltip("どれだけ減っても、これ以下にはならない明るさ")]
    [SerializeField, Range(0f, 1f)] private float minBrightness = 0.25f;
    [Tooltip("明るさが切り替わる速さ")]
    [SerializeField] private float brightnessSmooth = 3f;

    private int currentCells;
    private float cellTimer;
    private float baseLightIntensity = 1f;
    private float currentBrightness = 1f;

    // マスが減る直前の点滅の状態
    private bool isFlickering;
    private float flickerClock;
    private int flickerCount;

    /// <summary>現在の電池のマス数</summary>
    public int CurrentCells => currentCells;
    public int MaxCells => maxCells;

    /// <summary>現在の電池残量（0から1）。マス数 ÷ 最大マス数</summary>
    public float BatteryPercent01 => maxCells > 0 ? Mathf.Clamp01((float)currentCells / maxCells) : 0f;
    public bool IsBatteryEmpty => currentCells <= 0;
    public bool IsBatteryFull => currentCells >= maxCells;

    /// <summary>マスが減る直前の点滅中かどうか（UI用）</summary>
    public bool IsLosingCell => isFlickering;


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
        currentCells = maxCells;
        cellTimer = secondsPerCell;
        currentBrightness = BrightnessForCells(currentCells);

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

        // 視点回転（敵に捕まっている間は、敵の方を向く）
        if (focusTarget != null)
        {
            FocusLook();
        }
        else
        {
            MouseLook();
        }

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

            // 消灯したら、点滅の途中状態は取り消す（ゲージが点滅し続けないように）
            if (!isFlashlightOn)
            {
                isFlickering = false;
            }

            // Light有効切替
            if (spotLight != null)
            {
                spotLight.enabled = isFlashlightOn;
            }
        }
    }

    /// <summary>
    /// 電池の消費。1マスの時間が尽きると、ライトが2回か3回点滅して、1マス減る。
    /// マスが減るたびに、ライトは少しずつ暗くなる。
    /// </summary>
    private void UpdateBattery()
    {
        if (!isFlashlightUnlocked) return;

        // 点灯中だけ電池を消費する
        if (isFlashlightOn && currentCells > 0)
        {
            if (isFlickering)
            {
                UpdateCellLossFlicker();
            }
            else
            {
                cellTimer -= Time.deltaTime;

                if (cellTimer <= 0f)
                {
                    StartCellLossFlicker();
                }
            }
        }

        ApplyLightBrightness();
    }

    /// <summary>マス数に応じた、基準の明るさ（1 → 暗くなっていく）</summary>
    private float BrightnessForCells(int cells)
    {
        int lost = Mathf.Max(0, maxCells - cells);
        return Mathf.Max(minBrightness, 1f - lost * dimPerLostCell);
    }

    private void StartCellLossFlicker()
    {
        isFlickering = true;
        flickerClock = 0f;
        flickerCount = Random.Range(flickerCountMin, flickerCountMax + 1);
    }

    private void UpdateCellLossFlicker()
    {
        flickerClock += Time.deltaTime;

        float cycle = flickerOffTime + flickerOnTime;
        int index = Mathf.FloorToInt(flickerClock / cycle);

        // 指定回数の点滅が終わったら、1マス減らす
        if (index >= flickerCount)
        {
            FinishCellLoss();
        }
    }

    /// <summary>今が点滅中の「消えている瞬間」かどうか</summary>
    private bool IsFlickerOffMoment()
    {
        if (!isFlickering) return false;

        float cycle = flickerOffTime + flickerOnTime;
        float inCycle = flickerClock % cycle;
        return inCycle < flickerOffTime;
    }

    private void FinishCellLoss()
    {
        isFlickering = false;
        currentCells = Mathf.Max(0, currentCells - 1);
        cellTimer = secondsPerCell;

        Debug.Log($"懐中電灯の電池が1マス減った。残り {currentCells}/{maxCells}");

        // 電池切れ：強制消灯
        if (currentCells <= 0)
        {
            isFlashlightOn = false;

            if (spotLight != null)
            {
                spotLight.enabled = false;
            }

            Debug.Log("懐中電灯の電池が切れた。");
        }
    }

    /// <summary>ライトの強さを、残りマス数と点滅の状態に合わせて反映する</summary>
    private void ApplyLightBrightness()
    {
        if (spotLight == null || !spotLight.enabled) return;

        // 基準の明るさへ、なめらかに近づける（マスが減った時、少しずつ暗くなる）
        float target = BrightnessForCells(currentCells);
        currentBrightness = Mathf.Lerp(
            currentBrightness,
            target,
            1f - Mathf.Exp(-brightnessSmooth * Time.unscaledDeltaTime));

        float level = IsFlickerOffMoment() ? flickerOffLevel : 1f;
        spotLight.intensity = baseLightIntensity * currentBrightness * level;
    }

    /// <summary>
    /// アイテム（電池など）で、電池のマスを回復する。
    /// すでに満タンなら何もせず false を返す（アイテムを消費しないために使う）。
    /// </summary>
    public bool RechargeCells(int cells)
    {
        if (cells <= 0) return false;
        if (IsBatteryFull) return false;

        bool wasEmpty = IsBatteryEmpty;
        currentCells = Mathf.Min(maxCells, currentCells + cells);

        // 点滅中だった場合は取り消し、今のマスの時間を満タンに戻す
        if (wasEmpty || isFlickering)
        {
            isFlickering = false;
            cellTimer = secondsPerCell;
        }

        Debug.Log($"懐中電灯の電池が回復した。残り {currentCells}/{maxCells}");
        return true;
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

        // 敵に捕まっている間は少し寄る
        if (focusTarget != null)
        {
            targetFOV = focusFOV;
        }

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovSpeed * Time.unscaledDeltaTime);
    }
}