using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HorrorFlicker : MonoBehaviour
{
    [Header("対象画像")]
    public Image targetImage; // 点滅させるUI画像

    [Header("フェード速度")]
    public float fadeSpeed = 5f; // フェードの速さ

    [Header("表示時間（ランダム）")]
    public float minOnTime = 0.05f;
    public float maxOnTime = 0.3f;

    [Header("非表示時間（ランダム）")]
    public float minOffTime = 0.05f;
    public float maxOffTime = 0.5f;

    [Header("たまに長く消える確率")]
    [Range(0f, 1f)]
    public float longOffChance = 0.2f;

    public float longOffTime = 1.5f; // 長く消える時間

    private void Start()
    {
        // コルーチン開始
        StartCoroutine(FlickerRoutine());
    }

    IEnumerator FlickerRoutine()
    {
        while (true)
        {
            // ===== 表示 =====
            yield return StartCoroutine(Fade(1f));

            // 表示時間（ランダム）
            yield return new WaitForSeconds(Random.Range(minOnTime, maxOnTime));

            // ===== 非表示 =====
            yield return StartCoroutine(Fade(0f));

            // 長く消えるか判定
            if (Random.value < longOffChance)
            {
                // 長めに消える（ホラー感アップ）
                yield return new WaitForSeconds(longOffTime);
            }
            else
            {
                // 通常の非表示時間
                yield return new WaitForSeconds(Random.Range(minOffTime, maxOffTime));
            }
        }
    }

    // フェード処理
    IEnumerator Fade(float targetAlpha)
    {
        Color color = targetImage.color;

        // 現在の透明度から目標値まで徐々に変化
        while (!Mathf.Approximately(color.a, targetAlpha))
        {
            color.a = Mathf.MoveTowards(color.a, targetAlpha, fadeSpeed * Time.deltaTime);
            targetImage.color = color;
            yield return null;
        }
    }
}