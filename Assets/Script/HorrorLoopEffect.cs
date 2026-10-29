using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HorrorLoopEffect : MonoBehaviour
{
    [Header("UI")]
    public Image noiseImage;     // ノイズ画像
    public Image secondImage;    // 2枚目の画像

    [Header("時間設定")]
    public float interval = 5f;      // 通常状態の時間
    public float noiseDuration = 1f; // ノイズ表示時間
    public float imageDuration = 1f; // 2枚目表示時間

    void Start()
    {
        // 最初は非表示
        noiseImage.gameObject.SetActive(false);
        secondImage.gameObject.SetActive(false);

        StartCoroutine(LoopEffect());
    }

    IEnumerator LoopEffect()
    {
        while (true)
        {
            // 通常状態
            yield return new WaitForSeconds(interval);

            // ノイズ表示
            noiseImage.gameObject.SetActive(true);
            yield return new WaitForSeconds(noiseDuration);
            noiseImage.gameObject.SetActive(false);

            // 2枚目の画像表示
            secondImage.gameObject.SetActive(true);
            yield return new WaitForSeconds(imageDuration);
            secondImage.gameObject.SetActive(false);
        }
    }
}