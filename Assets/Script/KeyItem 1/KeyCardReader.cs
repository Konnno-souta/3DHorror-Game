using UnityEngine;

/// <summary>
/// 扉のわきの、カードリーダー。
/// 扉(KeyDoor)にロックがかかっている間は、ランプが赤く光る。ロックが外れると、緑になる。
/// 枠の色(赤、青、黄)で、どの色のカードキーが必要か分かる。
/// Tools / Key / 選んだ扉をカードキーの扉にする で、自動で付く。
/// </summary>
public class KeyCardReader : MonoBehaviour
{
    [Tooltip("見張る扉")]
    [SerializeField] private KeyDoor door;

    [Tooltip("光らせるランプ")]
    [SerializeField] private Renderer lamp;

    [Tooltip("ランプの近くを照らす光(なくてもよい)")]
    [SerializeField] private Light lampLight;

    [SerializeField] private Color lockedColor = new Color(1f, 0.08f, 0.05f);
    [SerializeField] private Color unlockedColor = new Color(0.1f, 1f, 0.25f);

    [Tooltip("ロック中に、ランプを点滅させる")]
    [SerializeField] private bool blinkWhileLocked = true;
    [SerializeField] private float blinkSpeed = 1.5f;

    private MaterialPropertyBlock block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        block = new MaterialPropertyBlock();

        if (door == null)
        {
            door = GetComponentInParent<KeyDoor>();
        }
    }

    private void Update()
    {
        bool locked = door != null && door.IsLocked;
        Color color = locked ? lockedColor : unlockedColor;

        float strength = 1f;
        if (locked && blinkWhileLocked)
        {
            strength = Mathf.Lerp(0.25f, 1f, (Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
        }

        if (lamp != null)
        {
            lamp.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color * 0.6f);
            block.SetColor(EmissionColorId, color * (3f * strength));
            lamp.SetPropertyBlock(block);
        }

        if (lampLight != null)
        {
            lampLight.color = color;
            lampLight.intensity = 0.6f * strength;
        }
    }

    public void Setup(KeyDoor targetDoor, Renderer targetLamp, Light targetLight)
    {
        door = targetDoor;
        lamp = targetLamp;
        lampLight = targetLight;
    }
}
