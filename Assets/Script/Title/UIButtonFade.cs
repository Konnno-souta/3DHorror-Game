using UnityEngine;
using UnityEngine.UI;

public class UIButtonFade : MonoBehaviour
{
    private Graphic graphic;

    void Start()
    {
        graphic = GetComponent<Graphic>();
    }

    void Update()
    {
        Color c = graphic.color;
        c.a = Mathf.PingPong(Time.time, 1f);
        graphic.color = c;
    }
}