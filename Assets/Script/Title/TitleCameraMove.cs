using UnityEngine;

public class TitleCameraMove : MonoBehaviour
{
    [Header("¶‰EˆÚ“®—Ê")]
    [SerializeField] private float moveX = 0.2f;

    [Header("ã‰ºˆÚ“®—Ê")]
    [SerializeField] private float moveY = 0.1f;

    [Header("ˆÚ“®‘¬“x")]
    [SerializeField] private float speed = 0.25f;

    private Vector3 startPos;

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        float x = Mathf.Sin(Time.time * speed) * moveX;
        float y = Mathf.Cos(Time.time * speed * 0.8f) * moveY;

        transform.position = startPos + new Vector3(x, y, 0f);
    }
}