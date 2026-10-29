using UnityEngine;
using UnityEngine.SceneManagement;

public class Title : MonoBehaviour
{
    // ƒ{ƒ^ƒ“‚ª‰Ÿ‚³‚ê‚½‚Æ‚«‚ÉŒÄ‚Î‚ê‚é
    public void OnClickStart()
    {
        SceneManager.LoadScene("Main");
    }
}