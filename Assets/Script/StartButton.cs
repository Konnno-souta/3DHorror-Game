using UnityEngine;

public class StartButton : MonoBehaviour
{
    [Tooltip("行き先のシーンの名前")]
    [SerializeField] private string sceneName = "MainScene";

    // ボタンが押されたときに呼ばれる
    public void OnClickStart()
    {
        // ロード画面をはさんでから、ゲームのシーンへ行く
        LoadingScreen.Load(sceneName);
    }
}