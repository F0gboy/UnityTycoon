using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public InventoryUI Inventory;
    public int ItemIndex;
    public GameObject Prefab;
    public Color HoverOutlineColor = new Color(1f, 1f, 1f, 0.8f);
    public Color SelectedOutlineColor = new Color(1f, 0.8f, 0.2f, 1f);
    public Vector2 OutlineDistance = new Vector2(2f, 2f);

    private bool isHovered;
    private bool isSelected;
    private Outline outline;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Inventory == null)
        {
            return;
        }

        Inventory.SetSelectedSlot(this);
        if (Prefab != null)
        {
            Inventory.SelectPrefab(Prefab);
        }
        else
        {
            Inventory.SelectItem(ItemIndex);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        UpdateOutline();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        UpdateOutline();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateOutline();
    }

    private void UpdateOutline()
    {
        EnsureOutline();
        if (outline == null)
        {
            return;
        }

        if (isSelected)
        {
            outline.effectColor = SelectedOutlineColor;
            outline.enabled = true;
        }
        else if (isHovered)
        {
            outline.effectColor = HoverOutlineColor;
            outline.enabled = true;
        }
        else
        {
            outline.enabled = false;
        }
    }

    private void EnsureOutline()
    {
        if (outline != null)
        {
            return;
        }

        var graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            graphic = GetComponentInChildren<Graphic>();
        }

        if (graphic == null)
        {
            return;
        }

        outline = graphic.GetComponent<Outline>();
        if (outline == null)
        {
            outline = graphic.gameObject.AddComponent<Outline>();
        }

        outline.effectDistance = OutlineDistance;
        outline.enabled = false;
    }
}
