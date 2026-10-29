using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class HorrorVideoLoop : MonoBehaviour
{
    public VideoPlayer noiseVideo;
    public RawImage noiseImage;   // ← ここ重要（Imageじゃない）

    public Image secondImage;

    public float interval = 5f;
    public float noiseDuration = 1f;
    public float imageDuration = 1f;

    void Start()
    {
        noiseImage.gameObject.SetActive(false);
        secondImage.gameObject.SetActive(false);

        StartCoroutine(LoopEffect());
    }

    IEnumerator LoopEffect()
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);

            // ノイズ表示
            noiseImage.gameObject.SetActive(true);
            noiseVideo.Play();

            yield return new WaitForSeconds(noiseDuration);

            noiseVideo.Stop();
            noiseImage.gameObject.SetActive(false);

            // 画像表示
            secondImage.gameObject.SetActive(true);
            yield return new WaitForSeconds(imageDuration);
            secondImage.gameObject.SetActive(false);
        }
    }
}