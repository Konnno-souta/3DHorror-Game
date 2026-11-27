using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ボタンの音: マウスを乗せた時/キーで選んだ時と、押した時。
/// Button に付ける。スタートボタンは Kind を Start にすると、重い音になる。
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIButtonSE : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public enum Kind { Normal, Start, Silent }

    [SerializeField] private Kind kind = Kind.Normal;
    [SerializeField] private bool hoverSound = true;

    private static float lastHoverTime = -1f;
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        Button button = selectable as Button;
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (kind == Kind.Silent) return;
        if (kind == Kind.Start && SEManager.Bank.gameStart.HasClip)
        {
            SEManager.Play(SEManager.Bank.gameStart);
            if (BGMPlayer.Instance != null) BGMPlayer.Instance.FadeOutAll(1.5f);
        }
        else
        {
            SEManager.Play(SEManager.Bank.uiClick);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { Hover(); }
    public void OnSelect(BaseEventData eventData) { Hover(); }

    private void Hover()
    {
        if (!hoverSound || kind == Kind.Silent) return;
        if (selectable != null && !selectable.IsInteractable()) return;
        if (Time.unscaledTime - lastHoverTime < 0.06f) return;
        lastHoverTime = Time.unscaledTime;
        SEManager.Play(SEManager.Bank.uiHover);
    }
}
