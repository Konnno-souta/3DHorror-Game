using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// 鍵で開く扉。扉の板(doorLeaf)を動かして開閉する。
/// 近づいてEキーを押すと、鍵(InventoryUIの所持品)を確認して開く。
/// 鍵が不要な扉にする場合は、Start Locked をオフにする。
/// Wall BayDoor(上にスライド)と、蝶番で開く扉(回転)の両方に使える。
/// </summary>
public class KeyDoor : MonoBehaviour
{
    public enum OpenMode
    {
        Slide,  // 板を平行移動で動かす(Wall BayDoorは上へ)
        Rotate  // 板を回転させる(蝶番式の扉)
    }

    // ---------------- 扉本体 ----------------

    [Header("扉本体")]
    [Tooltip("動かす扉の板(Wall BayDoorでは LP_Bay_Door_snaps)")]
    [SerializeField] private Transform doorLeaf;

    [SerializeField] private OpenMode openMode = OpenMode.Slide;

    [Tooltip("Slide: 閉じた位置からの移動量(親のローカル座標)。Wall BayDoorは上へ約4.7")]
    [SerializeField] private Vector3 slideOffset = new Vector3(0f, 4.7f, 0f);

    [Tooltip("Rotate: 回転軸(ローカル座標)")]
    [SerializeField] private Vector3 rotateAxis = Vector3.up;

    [Tooltip("Rotate: 開いたときの角度")]
    [SerializeField] private float rotateAngle = 90f;

    [Tooltip("開く、閉じるのにかかる秒数")]
    [SerializeField] private float openDuration = 2.5f;

    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("開いたあと、扉の板を非表示にする(壁の中に残って見えるのを防ぐ)")]
    [SerializeField] private bool hideLeafWhenOpen = false;

    [Tooltip("開いている間は無効にする当たり判定。空なら、扉の板の中のColliderを自動で使う")]
    [SerializeField] private Collider[] leafColliders;

    // ---------------- 敵 ----------------

    [Header("敵(NavMeshAgent を持つキャラ)")]
    [Tooltip("オンなら、敵が近づくと、扉を自動で開ける。通ったあとは、自動で閉まる")]
    [SerializeField] private bool enemiesCanOpen = true;

    [Tooltip("オンなら、鍵がかかっている扉も、敵は開けられる")]
    [SerializeField] private bool enemiesOpenLockedDoors = false;

    [Tooltip("閉まっている間、敵の経路をふさぐ(扉が開ききるまで、敵は手前で待つ)")]
    [SerializeField] private bool blockEnemyPathWhenClosed = true;

    [Tooltip("敵の位置を調べる間隔(秒)")]
    [SerializeField] private float enemyCheckInterval = 0.15f;

    // ---------------- 自動ドア(鍵なし) ----------------

    [Header("自動ドア(鍵なし)")]
    [Tooltip("オンなら、鍵なしで、近づくと自動で開き、離れると自動で閉まる。Eキーはいらない")]
    [SerializeField] private bool autoDoor = false;

    [Tooltip("近づいてから、開き始めるまでの秒数")]
    [SerializeField] private float autoOpenDelay = 0f;

    // ---------------- 閉じる動き ----------------

    [Header("閉じる動き")]
    [Tooltip("オンなら、プレイヤーが扉から離れたあと、自動で閉まる")]
    [SerializeField] private bool autoClose = false;

    [Tooltip("プレイヤーが範囲から出て、自動で閉まるまでの秒数(自動ドアでも使う)")]
    [SerializeField] private float autoCloseDelay = 2f;

    [Tooltip("扉の通り道にプレイヤーがいる間は、閉めない(挟まれ防止)")]
    [SerializeField] private bool blockCloseWhilePlayerInDoorway = true;

    [Tooltip("通り道の判定を、扉の大きさから広げる量(メートル)")]
    [SerializeField] private float doorwayPadding = 0.4f;

    [SerializeField] private string blockedCloseText = "通り抜けてから閉めよう";

    // ---------------- 鍵 ----------------

    [Header("鍵")]
    [Tooltip("オンなら、最初は鍵がかかっている")]
    [SerializeField] private bool startLocked = true;

    [Tooltip("必要な鍵のTag(ItemDataの Item Tag と同じにする)")]
    [SerializeField] private string requiredKeyTag = "Key";

    [Tooltip("鍵を使ったら、所持品から消す")]
    [SerializeField] private bool consumeKey = true;

    [Tooltip("開けたあと、もう一度Eキーで閉められるようにする")]
    [SerializeField] private bool closeable = true;

    // ---------------- 操作 ----------------

    [Header("操作")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    // ---------------- メッセージ ----------------

    [Header("メッセージ")]
    [SerializeField] private string promptOpenText = "Eキーで開ける";
    [SerializeField] private string promptCloseText = "Eキーで閉める";
    [SerializeField] private string lockedText = "鍵がかかっている…";
    [SerializeField] private string unlockedText = "鍵を使って扉を開けた";
    [SerializeField] private float messageSeconds = 2f;

    [Tooltip("メッセージを出すパネル。空なら、PickupText を探して、その親のパネルを使う")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TMP_Text promptText;

    // ---------------- 音(任意) ----------------

    [Header("音(任意)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip lockedClip;
    [SerializeField] private AudioClip unlockClip;

    // ---------------- イベント(任意) ----------------

    [Header("イベント(任意)")]
    public UnityEvent onUnlocked;
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    // ---------------- デバッグ ----------------

    [Header("デバッグ")]
    [Tooltip("状態の変化を、Consoleに出す")]
    [SerializeField] private bool debugLog = false;

    [Tooltip("Sceneビューに、トリガーの範囲と、開いたときの扉の位置を描く")]
    [SerializeField] private bool debugGizmos = true;

    [Tooltip("Gameビューに、扉の状態を表示する(エディタと開発ビルドのみ)")]
    [SerializeField] private bool debugOverlay = false;

    [Tooltip("デバッグ用のキー操作を有効にする(エディタと開発ビルドのみ)")]
    [SerializeField] private bool debugKeys = false;

    [Tooltip("F1: 近くの扉を、鍵を無視して開閉  F2: 鍵を外す  F3: 鍵をかける  F4: 鍵を所持品に追加")]
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F1;
    [SerializeField] private KeyCode debugUnlockKey = KeyCode.F2;
    [SerializeField] private KeyCode debugLockKey = KeyCode.F3;
    [SerializeField] private KeyCode debugGiveKeyKey = KeyCode.F4;

    [Tooltip("F4で鍵を追加するときに使う、アイテム図鑑(ItemDatabase)")]
    [SerializeField] private ItemDatabase debugItemDatabase;

    // ---------------- 状態 ----------------

    private static readonly List<KeyDoor> doorsInRange = new List<KeyDoor>();

    private bool isLocked;
    private bool isOpen;
    private bool isMoving;
    private bool playerInRange;
    private float messageTimer;

    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private Renderer[] leafRenderers;
    private Bounds closedBounds;
    private bool hasClosedBounds;
    private float autoCloseTimer = -1f;
    private float autoOpenTimer;

    private BoxCollider triggerBox;
    private NavMeshAgent[] agents = new NavMeshAgent[0];
    private float agentRefreshTimer;
    private float enemyTimer;
    private bool enemyInRange;
    private bool closeAfterEnemy;
    private GameObject navObstacleObject;
    private NavMeshObstacle navObstacle;

    public bool IsLocked => isLocked;
    public bool IsOpen => isOpen;
    public string RequiredKeyTag => requiredKeyTag;

    // ---------------- 初期化 ----------------

    private void Awake()
    {
        isLocked = startLocked;
        if (autoDoor) isLocked = false;

        foreach (BoxCollider box in GetComponents<BoxCollider>())
        {
            if (box.isTrigger)
            {
                triggerBox = box;
                break;
            }
        }

        if (doorLeaf != null)
        {
            closedLocalPosition = doorLeaf.localPosition;
            closedLocalRotation = doorLeaf.localRotation;
            leafRenderers = doorLeaf.GetComponentsInChildren<Renderer>(true);

            // 閉じているときの扉の範囲を覚えておく(通り道の判定に使う)
            foreach (Renderer r in leafRenderers)
            {
                if (r == null) continue;
                if (!hasClosedBounds)
                {
                    closedBounds = r.bounds;
                    hasClosedBounds = true;
                }
                else
                {
                    closedBounds.Encapsulate(r.bounds);
                }
            }

            if (leafColliders == null || leafColliders.Length == 0)
            {
                leafColliders = doorLeaf.GetComponentsInChildren<Collider>(true);
            }

            CreateNavObstacle();
        }
        else
        {
            Debug.LogWarning($"[KeyDoor] '{name}' に Door Leaf が設定されていません。");
        }
    }

    private void OnDestroy()
    {
        if (navObstacleObject != null) Destroy(navObstacleObject);
    }

    /// <summary>
    /// 閉まっている間だけ、敵の経路をふさぐ(NavMeshObstacle)。
    /// 扉の板は動くので、板とは別のオブジェクトに付けて、閉じた位置に固定する。
    /// 板をNavMeshの焼き込みから外しておくこと(Tools / Door / ドアをNavMeshの焼き込みから外す)。
    /// </summary>
    private void CreateNavObstacle()
    {
        if (!blockEnemyPathWhenClosed || !hasClosedBounds) return;

        navObstacleObject = new GameObject("NavObstacle_" + name);
        navObstacleObject.transform.position = closedBounds.center;
        navObstacleObject.transform.rotation = Quaternion.identity;

        navObstacle = navObstacleObject.AddComponent<NavMeshObstacle>();
        navObstacle.shape = NavMeshObstacleShape.Box;
        navObstacle.center = Vector3.zero;
        navObstacle.size = closedBounds.size + new Vector3(0.2f, 0f, 0.2f);
        navObstacle.carving = true;
        navObstacle.carveOnlyStationary = true;
        navObstacle.enabled = !isOpen;
    }

    private void SetNavObstacle(bool blocking)
    {
        if (navObstacle != null) navObstacle.enabled = blocking;
    }

    private void Start()
    {
        FindPromptIfNeeded();
        HidePrompt();
    }

    private void OnDisable()
    {
        doorsInRange.Remove(this);
        playerInRange = false;
    }

    private void FindPromptIfNeeded()
    {
        if (promptText != null && promptUI != null) return;

        if (promptText == null)
        {
            TMP_Text[] all = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text t in all)
            {
                if (t.name == "PickupText")
                {
                    promptText = t;
                    break;
                }
            }
        }

        if (promptUI == null && promptText != null && promptText.transform.parent != null)
        {
            promptUI = promptText.transform.parent.gameObject;
        }
    }

    // ---------------- 毎フレーム ----------------

    private void Update()
    {
        UpdateDebugKeys();
        UpdateEnemy();
        UpdateAutoOpen();
        UpdateAutoClose();

        if (!playerInRange) return;

        if (messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;
            if (messageTimer <= 0f) RefreshPrompt();
        }

        if (isMoving) return;

        // 自動ドアは、Eキーを使わない
        if (autoDoor) return;

        if (Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    /// <summary>
    /// 自動ドア: プレイヤーが範囲にいて、扉が閉じていれば、開ける。
    /// </summary>
    private void UpdateAutoOpen()
    {
        if (!autoDoor || !playerInRange || isOpen || isMoving || isLocked)
        {
            autoOpenTimer = Mathf.Max(0f, autoOpenDelay);
            return;
        }

        autoOpenTimer -= Time.deltaTime;
        if (autoOpenTimer > 0f) return;

        autoOpenTimer = Mathf.Max(0f, autoOpenDelay);
        DebugLog("自動ドア: 近づいたので開ける");
        Open();
    }

    private void UpdateAutoClose()
    {
        if (!(autoClose || autoDoor || closeAfterEnemy) || autoCloseTimer < 0f) return;

        if (!isOpen || isMoving || playerInRange || enemyInRange)
        {
            autoCloseTimer = -1f;
            return;
        }

        autoCloseTimer -= Time.deltaTime;
        if (autoCloseTimer > 0f) return;

        if (IsPlayerInDoorway())
        {
            // まだ通り道にいる。少し待ってからやり直す
            autoCloseTimer = 0.5f;
            return;
        }

        autoCloseTimer = -1f;
        Close();
    }

    // ---------------- 敵 ----------------

    private void UpdateEnemy()
    {
        if (!enemiesCanOpen || triggerBox == null)
        {
            enemyInRange = false;
            return;
        }

        enemyTimer -= Time.deltaTime;
        if (enemyTimer > 0f) return;
        enemyTimer = Mathf.Max(0.05f, enemyCheckInterval);

        agentRefreshTimer -= enemyCheckInterval;
        if (agentRefreshTimer <= 0f)
        {
            agentRefreshTimer = 1f;
            agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        }

        bool found = IsAnyEnemyInZone();

        // 敵が離れた: 閉める準備をする
        if (enemyInRange && !found && isOpen && (autoClose || autoDoor || closeAfterEnemy))
        {
            autoCloseTimer = Mathf.Max(0f, autoCloseDelay);
        }
        enemyInRange = found;

        if (!found || isOpen || isMoving) return;
        if (isLocked && !enemiesOpenLockedDoors) return;
        if (doorLeaf == null) return;

        DebugLog("敵が近づいたので開ける");
        closeAfterEnemy = true;
        StartCoroutine(MoveRoutine(true));
    }

    /// <summary>
    /// 近づく判定の箱(トリガー)の中に、NavMeshAgent を持つキャラがいるか。
    /// (敵に Rigidbody や Collider がなくても、位置で調べられる)
    /// </summary>
    private bool IsAnyEnemyInZone()
    {
        Transform boxTransform = triggerBox.transform;
        Vector3 half = triggerBox.size * 0.5f + new Vector3(0f, 0.5f, 0f);

        foreach (NavMeshAgent agent in agents)
        {
            if (agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy) continue;

            Vector3 local = boxTransform.InverseTransformPoint(agent.transform.position) - triggerBox.center;
            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsAnyEnemyInDoorway()
    {
        if (!hasClosedBounds || !enemiesCanOpen) return false;

        Bounds area = closedBounds;
        area.Expand(Mathf.Max(0f, doorwayPadding) * 2f);

        foreach (NavMeshAgent agent in agents)
        {
            if (agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy) continue;
            if (area.Contains(agent.transform.position + Vector3.up * 0.5f)) return true;
        }
        return false;
    }

    /// <summary>
    /// 閉じた扉がある場所(通り道)に、プレイヤーがいるか。
    /// </summary>
    private bool IsPlayerInDoorway()
    {
        if (!hasClosedBounds) return false;

        Vector3 half = closedBounds.extents + Vector3.one * Mathf.Max(0f, doorwayPadding);
        Collider[] hits = Physics.OverlapBox(
            closedBounds.center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider c in hits)
        {
            if (c != null && IsPlayer(c)) return true;
        }
        return IsAnyEnemyInDoorway();
    }

    private void Interact()
    {
        if (isLocked)
        {
            TryUnlockWithInventory();
            return;
        }

        if (!isOpen)
        {
            Open();
        }
        else if (closeable)
        {
            if (blockCloseWhilePlayerInDoorway && IsPlayerInDoorway())
            {
                DebugLog("通り道にプレイヤーがいるため、閉められない");
                ShowMessage(blockedCloseText);
                return;
            }
            Close();
        }
    }

    private void TryUnlockWithInventory()
    {
        if (InventoryUI.Instance == null)
        {
            Debug.LogWarning("[KeyDoor] InventoryUI が見つかりません。");
            return;
        }

        bool hasKey = consumeKey
            ? InventoryUI.Instance.ConsumeItemByTag(requiredKeyTag)
            : InventoryUI.Instance.HasItemByTag(requiredKeyTag);

        if (hasKey)
        {
            Unlock(true);
        }
        else
        {
            DebugLog("鍵がないため開けられない。必要なTag: " + requiredKeyTag);
            PlayClip(lockedClip);
            ShowMessage(lockedText);
        }
    }

    // ---------------- 公開メソッド ----------------

    /// <summary>
    /// 鍵を外す。openAfterなら、そのまま開ける。
    /// 謎解きなど、ほかのスクリプトから呼べる。
    /// </summary>
    public void Unlock(bool openAfter)
    {
        if (isLocked)
        {
            isLocked = false;
            DebugLog("鍵を外した");
            PlayClip(unlockClip);
            onUnlocked?.Invoke();
        }

        if (openAfter && !isOpen && !isMoving)
        {
            ShowMessage(unlockedText);
            Open();
        }
    }

    public void Open()
    {
        if (isLocked || isOpen || isMoving || doorLeaf == null) return;
        StartCoroutine(MoveRoutine(true));
    }

    public void Close()
    {
        if (!isOpen || isMoving || doorLeaf == null) return;
        StartCoroutine(MoveRoutine(false));
    }

    /// <summary>
    /// インベントリで鍵を使ったとき用。近くに、そのTagの鍵がかかった扉があれば開ける。
    /// 開けたら true を返す(その場合、鍵の消費は呼び出し側で行う)。
    /// </summary>
    public static bool TryUseKeyNearby(string keyTag)
    {
        foreach (KeyDoor door in doorsInRange)
        {
            if (door == null) continue;
            if (!door.isLocked || door.isMoving) continue;
            if (door.requiredKeyTag != keyTag) continue;

            door.Unlock(true);
            return true;
        }
        return false;
    }

    // ---------------- 開閉の動き ----------------

    private IEnumerator MoveRoutine(bool opening)
    {
        isMoving = true;
        DebugLog(opening ? "開き始めた" : "閉じ始めた");
        HidePromptWhileMoving();

        if (opening)
        {
            SetLeafColliders(false);
            SetLeafVisible(true);
            PlayClip(openClip);
        }
        else
        {
            // 閉じるときは、板を先に表示して、当たり判定も戻す。
            // (開いたあとに板を非表示にしていると、見えない板が下りてくるだけになる)
            SetLeafVisible(true);
            SetLeafColliders(true);
            SetNavObstacle(true);
            PlayClip(closeClip);
        }

        float startAmount = opening ? 0f : 1f;
        float endAmount = opening ? 1f : 0f;
        float duration = Mathf.Max(0.01f, openDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Apply(Mathf.Lerp(startAmount, endAmount, openCurve.Evaluate(t)));
            yield return null;
        }

        Apply(endAmount);

        isOpen = opening;
        isMoving = false;
        DebugLog(opening ? "開ききった" : "閉まりきった");

        if (opening)
        {
            // 開ききってから、敵の経路を開ける(板が下りたまま、すり抜けるのを防ぐ)
            SetNavObstacle(false);
            if (hideLeafWhenOpen) SetLeafVisible(false);
            onOpened?.Invoke();
        }
        else
        {
            SetLeafColliders(true);
            closeAfterEnemy = false;
            onClosed?.Invoke();
        }

        // 自動ドア: 開ききったとき、もうプレイヤーが離れていたら、閉める準備をする
        if (autoDoor && opening && !playerInRange)
        {
            autoCloseTimer = Mathf.Max(0f, autoCloseDelay);
        }

        if (playerInRange) RefreshPrompt();
    }

    /// <summary>
    /// 0 = 閉じている、1 = 開いている。
    /// </summary>
    private void Apply(float amount)
    {
        if (openMode == OpenMode.Slide)
        {
            doorLeaf.localPosition = closedLocalPosition + slideOffset * amount;
        }
        else
        {
            Quaternion open = Quaternion.AngleAxis(rotateAngle * amount, rotateAxis.normalized);
            doorLeaf.localRotation = closedLocalRotation * open;
        }
    }

    private void SetLeafColliders(bool enabled)
    {
        if (leafColliders == null) return;
        foreach (Collider c in leafColliders)
        {
            if (c != null) c.enabled = enabled;
        }
    }

    private void SetLeafVisible(bool visible)
    {
        if (leafRenderers == null) return;
        foreach (Renderer r in leafRenderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

    // ---------------- 近づいた判定 ----------------

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInRange = true;
        if (!doorsInRange.Contains(this)) doorsInRange.Add(this);
        DebugLog("プレイヤーが範囲に入った");
        RefreshPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInRange = false;
        doorsInRange.Remove(this);
        DebugLog("プレイヤーが範囲から出た");
        HidePrompt();

        if ((autoClose || autoDoor || closeAfterEnemy) && isOpen) autoCloseTimer = Mathf.Max(0f, autoCloseDelay);
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<HorrorPlayerController>() != null;
    }

    // ---------------- メッセージ表示 ----------------

    private void RefreshPrompt()
    {
        // 自動ドアは、「Eキーで開ける」を出さない
        if (autoDoor)
        {
            HidePrompt();
            return;
        }

        if (!playerInRange || isMoving)
        {
            HidePrompt();
            return;
        }

        if (isOpen && !closeable)
        {
            HidePrompt();
            return;
        }

        ShowText(isOpen ? promptCloseText : promptOpenText);
    }

    private void ShowMessage(string message)
    {
        messageTimer = messageSeconds;
        ShowText(message);
    }

    private void ShowText(string message)
    {
        if (promptText != null) promptText.text = message;
        if (promptUI != null) promptUI.SetActive(true);
    }

    private void HidePromptWhileMoving()
    {
        messageTimer = 0f;
        HidePrompt();
    }

    private void HidePrompt()
    {
        if (promptUI != null) promptUI.SetActive(false);
    }

    // ---------------- デバッグ機能 ----------------

    private void DebugLog(string message)
    {
        if (!debugLog) return;
        Debug.Log("[KeyDoor:" + name + "] " + message, this);
    }

    private static bool DebugAllowed()
    {
        return Application.isEditor || Debug.isDebugBuild;
    }

    /// <summary>
    /// F1からF4のデバッグキー。範囲内の扉だけに効く(範囲内にない場合は、この扉自身には効かない)。
    /// 複数の扉が範囲内にあるときは、最初の1つだけが反応する。
    /// </summary>
    private void UpdateDebugKeys()
    {
        if (!debugKeys || !DebugAllowed()) return;
        if (!playerInRange) return;
        if (doorsInRange.Count == 0 || doorsInRange[0] != this) return;

        if (Input.GetKeyDown(debugToggleKey)) DebugForceToggle();
        if (Input.GetKeyDown(debugUnlockKey)) DebugUnlock();
        if (Input.GetKeyDown(debugLockKey)) DebugLock();
        if (Input.GetKeyDown(debugGiveKeyKey)) DebugGiveKey();
    }

    [ContextMenu("デバッグ: 鍵を無視して開閉する")]
    public void DebugForceToggle()
    {
        if (!Application.isPlaying) return;
        if (isMoving) return;

        isLocked = false;
        DebugLog("デバッグ: 鍵を無視して開閉");

        if (isOpen) Close();
        else Open();
    }

    [ContextMenu("デバッグ: 鍵を外す")]
    public void DebugUnlock()
    {
        if (!Application.isPlaying) return;
        isLocked = false;
        DebugLog("デバッグ: 鍵を外した");
        if (playerInRange) RefreshPrompt();
    }

    [ContextMenu("デバッグ: 鍵をかける(閉じている時のみ)")]
    public void DebugLock()
    {
        if (!Application.isPlaying) return;
        if (isOpen || isMoving)
        {
            DebugLog("デバッグ: 開いている間は、鍵をかけられない");
            return;
        }

        isLocked = true;
        DebugLog("デバッグ: 鍵をかけた");
        if (playerInRange) RefreshPrompt();
    }

    [ContextMenu("デバッグ: 鍵を所持品に追加する")]
    public void DebugGiveKey()
    {
        if (!Application.isPlaying) return;

        if (InventoryUI.Instance == null)
        {
            Debug.LogWarning("[KeyDoor] InventoryUI が見つかりません。");
            return;
        }

        if (debugItemDatabase == null)
        {
            Debug.LogWarning("[KeyDoor] Debug Item Database が設定されていないため、鍵を追加できません。");
            return;
        }

        ItemData key = debugItemDatabase.GetItemByTag(requiredKeyTag);
        if (key == null)
        {
            Debug.LogWarning("[KeyDoor] Tag '" + requiredKeyTag + "' のアイテムが、図鑑に見つかりません。");
            return;
        }

        InventoryUI.Instance.AddItem(key);
        DebugLog("デバッグ: 鍵(" + key.itemName + ")を所持品に追加した");
    }

    private string DebugStateText()
    {
        string state = isMoving ? (isOpen ? "閉じている途中" : "開いている途中")
                                : (isOpen ? "開いている" : "閉じている");
        string lockText = isLocked ? "鍵: かかっている(必要: " + requiredKeyTag + ")" : "鍵: 開いている";
        string range = playerInRange ? "範囲内" : "範囲外";
        return name + "\n" + state + "\n" + lockText + "\n" + range;
    }

    private void OnGUI()
    {
        if (!debugOverlay || !DebugAllowed()) return;
        if (!Application.isPlaying) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 world = transform.TransformPoint(new Vector3(0f, 3f, 3f));
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 14;
        style.normal.textColor = isLocked ? new Color(1f, 0.6f, 0.4f) : new Color(0.6f, 1f, 0.6f);

        Rect rect = new Rect(screen.x - 90f, Screen.height - screen.y - 40f, 220f, 78f);
        GUI.Box(rect, DebugStateText(), style);
    }

    private void OnDrawGizmos()
    {
        if (!debugGizmos) return;

        // トリガー(近づいたことを調べる範囲)
        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger != null && trigger.isTrigger)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = playerInRange ? new Color(0.3f, 1f, 0.3f, 0.9f) : new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(trigger.center, trigger.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        // 開いたときの、扉の板の位置
        if (doorLeaf != null && openMode == OpenMode.Slide)
        {
            Renderer[] renderers = doorLeaf.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            Transform parent = doorLeaf.parent != null ? doorLeaf.parent : transform;
            Vector3 worldOffset = parent.TransformVector(slideOffset);

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(bounds.center + worldOffset, bounds.size);
            Gizmos.DrawLine(bounds.center, bounds.center + worldOffset);
        }
    }
}