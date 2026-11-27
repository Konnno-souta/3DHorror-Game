using UnityEngine;

/// <summary>
/// ゲームオーバー画面が出た時に、曲を止めてゲームオーバーの音を鳴らす。
/// GameOverPanel(GameOverScreen が付いている物)に付ける。
/// </summary>
public class GameOverSE : MonoBehaviour
{
    [Tooltip("画面が出てから音を鳴らすまでの秒数")]
    [SerializeField] private float delay = 0.6f;

    private void OnEnable()
    {
        if (BGMPlayer.Instance != null) BGMPlayer.Instance.FadeOutAll(0.8f);
        SEManager.SilenceWorld = true;
        StartCoroutine(PlayLater());
    }

    private System.Collections.IEnumerator PlayLater()
    {
        yield return new WaitForSecondsRealtime(delay);
        SEManager.Play(SEManager.Bank.gameOver);
    }
}
