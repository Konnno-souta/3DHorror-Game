using UnityEngine;
using UnityEngine.UI;

public class ItemButton : MonoBehaviour
{
    public ItemData itemData;
    public ItemViewer viewer;

    public GameObject highlight; // Å© í«â¡

    public void OnClick()
    {
        viewer.ShowItem(itemData);
        viewer.SetSelected(this);
    }

    public void SetHighlight(bool active)
    {
        highlight.SetActive(active);
    }
}