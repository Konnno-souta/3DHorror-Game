using UnityEngine;

/// <summary>
/// 懐中電灯の音: ON/OFF、電池切れでカチカチ、電池が減る時のジジッ、電池の回復。
/// HorrorCameraFollow と同じオブジェクトに付ける。
/// </summary>
public class FlashlightSE : MonoBehaviour
{
    [SerializeField] private HorrorCameraFollow flashlight;
    [SerializeField] private KeyCode flashlightKey = KeyCode.F;

    private SEField onField;
    private bool lastOn;
    private int lastCells;
    private bool lastLosing;
    private bool lastUnlocked;

    private void Start()
    {
        if (flashlight == null) flashlight = GetComponent<HorrorCameraFollow>();
        if (flashlight == null) flashlight = FindFirstObjectByType<HorrorCameraFollow>();
        if (flashlight == null) { enabled = false; return; }

        onField = new SEField(flashlight, "isFlashlightOn");
        lastOn = onField.GetBool();
        lastCells = flashlight.CurrentCells;
        lastUnlocked = flashlight.IsFlashlightUnlocked;
    }

    private void Update()
    {
        bool unlocked = flashlight.IsFlashlightUnlocked;
        bool on = onField.GetBool() && unlocked;
        int cells = flashlight.CurrentCells;
        bool losing = flashlight.IsLosingCell;
        SEBank b = SEManager.Bank;

        if (unlocked && !lastUnlocked)
        {
            // 拾った瞬間は ON の音だけ
            SEManager.Play(b.flashlightOn);
        }
        else if (on != lastOn)
        {
            if (on) SEManager.Play(b.flashlightOn);
            else if (cells > 0) SEManager.Play(b.flashlightOff);
            else SEManager.Play(b.batteryLosing, 1.2f, 0.8f);
        }
        else if (unlocked && !on && cells <= 0 && Input.GetKeyDown(flashlightKey))
        {
            // 電池が無いのにFを押した
            SEManager.Play(b.flashlightEmpty);
        }

        if (losing && !lastLosing) SEManager.Play(b.batteryLosing);
        if (cells > lastCells && lastUnlocked) SEManager.Play(b.batteryRecharge);

        lastOn = on;
        lastCells = cells;
        lastLosing = losing;
        lastUnlocked = unlocked;
    }
}
