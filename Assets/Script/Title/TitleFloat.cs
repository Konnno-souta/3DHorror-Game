using UnityEngine;

public class TitleFloat : MonoBehaviour
{
    [SerializeField] private float height = 8f;
    [SerializeField] private float speed = 2f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        transform.localPosition =
            startPos +
            Vector3.up *
            Mathf.Sin(Time.time * speed) *
            height;
    }
}