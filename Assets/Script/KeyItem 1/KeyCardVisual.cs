using UnityEngine;

/// <summary>
/// 床に落ちているカードキーの見た目。
/// ゆっくり回って、上下にふわふわ動き、カードの色の光が、ゆっくり明るくなったり暗くなったりする。
/// 暗い部屋でも、見つけやすくするため。
/// カードキーのプレハブの、子の「Visual」に付いている。
/// </summary>
public class KeyCardVisual : MonoBehaviour
{
    [Header("回る")]
    [SerializeField] private float spinSpeed = 45f;

    [Header("ふわふわ")]
    [SerializeField] private float bobHeight = 0.05f;
    [SerializeField] private float bobSpeed = 1.6f;

    [Header("光")]
    [Tooltip("カードの色の光(子の Glow)。空なら、子から探す")]
    [SerializeField] private Light glow;
    [SerializeField] private float glowMin = 0.5f;
    [SerializeField] private float glowMax = 1.4f;
    [SerializeField] private float glowSpeed = 2.2f;

    private Vector3 startLocalPosition;
    private float phase;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        phase = Random.Range(0f, 10f);

        if (glow == null)
        {
            glow = GetComponentInChildren<Light>();
        }
    }

    private void Update()
    {
        float t = Time.time + phase;

        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.localPosition = startLocalPosition + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);

        if (glow != null)
        {
            float k = (Mathf.Sin(t * glowSpeed) + 1f) * 0.5f;
            glow.intensity = Mathf.Lerp(glowMin, glowMax, k);
        }
    }
}
