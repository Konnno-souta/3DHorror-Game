using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemViewer : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public Image imageView;
    public Transform modelParent;

    private GameObject currentModel;
    private ItemButton currentButton;

    void Update()
    {
        if (currentModel != null)
        {
            currentModel.transform.Rotate(0f, 30f * Time.deltaTime, 0f);
        }
    }

    public void ShowItem(ItemData item)
    {
        nameText.text = item.itemName;
        descriptionText.text = item.description;

        if (item.displayPrefab != null)
        {
            ShowModel(item.displayPrefab);
        }
    }

    void ShowModel(GameObject prefab)
    {
        ClearModel();
        currentModel = Instantiate(prefab, modelParent);
    }

    void ClearModel()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
        }
    }

    public void SetSelected(ItemButton button)
    {
        if (currentButton != null)
        {
            currentButton.SetHighlight(false);
        }

        currentButton = button;
        currentButton.SetHighlight(true);
    }
}