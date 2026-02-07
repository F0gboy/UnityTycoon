using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public InventoryUI Inventory;
    public int ItemIndex;
    public GameObject Prefab;
    public Vector2Int Footprint = Vector2Int.one;
    public string ItemName;
    public Sprite Icon;
    public int Cost;
    public bool IsStoreItem;
    public Button BuyButton;
    public Button SelectButton;
    public Color HoverOutlineColor = new Color(1f, 1f, 1f, 0.8f);
    public Color SelectedOutlineColor = new Color(1f, 0.8f, 0.2f, 1f);
    public Vector2 OutlineDistance = new Vector2(2f, 2f);

    private bool isHovered;
    private bool isSelected;
    private Outline outline;

    private void Awake()
    {
        HookButtons();
    }

    private void OnEnable()
    {
        HookButtons();
    }

    public void ConfigureStore(bool isStoreItem)
    {
        IsStoreItem = isStoreItem;
        HookButtons();
    }

    private void HookButtons()
    {
        if (IsStoreItem)
        {
            if (BuyButton == null)
            {
                BuyButton = FindButtonByName("BuyButton");
            }

            if (BuyButton != null)
            {
                BuyButton.onClick.RemoveListener(OnBuyClicked);
                BuyButton.onClick.AddListener(OnBuyClicked);
                var image = BuyButton.GetComponent<Image>();
                if (image != null)
                {
                    image.enabled = true;
                }
            }
            return;
        }

        if (SelectButton == null)
        {
            SelectButton = FindButtonByName("SelectButton");
        }

        if (SelectButton != null)
        {
            SelectButton.onClick.RemoveListener(OnSelectClicked);
            SelectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    private Button FindButtonByName(string buttonName)
    {
        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == buttonName)
            {
                return buttons[i];
            }
        }
        return buttons.Length > 0 ? buttons[0] : null;
    }

    private void OnSelectClicked()
    {
        if (Inventory == null)
        {
            return;
        }

        Inventory.SetSelectedSlot(this);
        if (Prefab != null)
        {
            Inventory.SelectPrefab(Prefab, Footprint);
        }
        else
        {
            Inventory.SelectItem(ItemIndex, Footprint);
        }
    }

    private void OnBuyClicked()
    {
        if (Inventory == null)
        {
            return;
        }

        Inventory.TryBuyItem(ItemName, Icon, Prefab, Footprint, Cost);
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
