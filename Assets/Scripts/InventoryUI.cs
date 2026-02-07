using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform InventoryPanel;
    public RectTransform ContentRoot;
    public GameObject ItemSlotPrefab;
    public GameObject StoreItemSlotPrefab;

    [Header("Inventory Data")]
    public List<InventoryItem> Items = new List<InventoryItem>();
    public List<InventoryItem> StoreItems = new List<InventoryItem>();

    [Header("Currency")]
    public int StartingCoins = 500;
    public Text CoinsText;

    [Header("Input")]
    public KeyCode ToggleKey = KeyCode.F;
    public bool KeepCursorUnlockedWhileVisible = true;

    [Header("Tabs")]
    public Button InventoryTabButton;
    public Button StoreTabButton;

    [Header("Slide Settings")]
    public float SlideDuration = 0.25f;
    public Vector2 VisibleAnchoredPosition = Vector2.zero;
    public Vector2 HiddenAnchoredPosition = new Vector2(-800f, 0f);

    [Header("Grid Layout")]
    public Vector2 Spacing = new Vector2(8f, 8f);
    public Vector4 PaddingLeftTopRightBottom = new Vector4(12f, 12f, 12f, 12f);
    public int FixedColumns = 2;
    public Vector2 ItemStep = new Vector2(10f, 20f);


    [Header("Placement Integration")]
    public GridSystem GridSystem;

    private bool isVisible;
    private Coroutine slideRoutine;
    private bool showingStore;
    private InventoryItemSlot selectedSlot;
    private int coins;

    private void Awake()
    {
        EnsureNoGridLayout();
        HookTabButtons();
        coins = StartingCoins;
        UpdateCoinsText();
        Populate();
        SetVisible(false, instant: true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            Toggle();
        }
    }

    private void LateUpdate()
    {
        if (isVisible && KeepCursorUnlockedWhileVisible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Toggle()
    {
        SetVisible(!isVisible, instant: false);
    }

    public void SetVisible(bool visible, bool instant)
    {
        isVisible = visible;

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        var target = visible ? VisibleAnchoredPosition : HiddenAnchoredPosition;
        if (instant)
        {
            InventoryPanel.anchoredPosition = target;
        }
        else
        {
            slideRoutine = StartCoroutine(SlideTo(target));
        }

        if (GridSystem != null)
        {
            GridSystem.PlacementModeActive = visible;
        }

        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }

    public void Populate()
    {
        if (ContentRoot == null || ItemSlotPrefab == null)
        {
            return;
        }

        var toDestroy = new List<GameObject>();
        for (int i = ContentRoot.childCount - 1; i >= 0; i--)
        {
            var child = ContentRoot.GetChild(i);
            child.SetParent(null, false);
            toDestroy.Add(child.gameObject);
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            Destroy(toDestroy[i]);
        }

        var source = showingStore ? StoreItems : Items;
        var prefabToUse = showingStore && StoreItemSlotPrefab != null
            ? StoreItemSlotPrefab
            : ItemSlotPrefab;
        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            var slot = Instantiate(prefabToUse, ContentRoot, false);
            slot.transform.localScale = Vector3.one;

            var image = slot.GetComponentInChildren<Image>();
            if (image != null)
            {
                image.sprite = item.Icon;
                image.enabled = item.Icon != null;
            }

            var text = slot.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = item.Name;
            }

            if (showingStore)
            {
                var priceText = FindTextByName(slot.transform, "Price");
                if (priceText != null)
                {
                    priceText.text = item.Cost.ToString();
                }
            }

            var index = i;
            var slotClick = slot.GetComponent<InventoryItemSlot>();
            if (slotClick == null)
            {
                slotClick = slot.AddComponent<InventoryItemSlot>();
            }
            slotClick.Inventory = this;
            slotClick.ItemIndex = index;
            slotClick.Prefab = item.Prefab;
            slotClick.Footprint = item.Footprint;
            slotClick.ItemName = item.Name;
            slotClick.Icon = item.Icon;
            slotClick.Cost = item.Cost;
            slotClick.ConfigureStore(showingStore);
            slotClick.SetSelected(slotClick == selectedSlot);
        }

        LayoutItems();
    }

    private System.Collections.IEnumerator SlideTo(Vector2 target)
    {
        var start = InventoryPanel.anchoredPosition;
        var time = 0f;

        while (time < SlideDuration)
        {
            time += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(time / SlideDuration);
            InventoryPanel.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        InventoryPanel.anchoredPosition = target;
        slideRoutine = null;
    }

    private void EnsureNoGridLayout()
    {
        if (ContentRoot == null)
        {
            return;
        }

        var grid = ContentRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            Destroy(grid);
        }
    }

    private void HookTabButtons()
    {
        if (InventoryTabButton != null)
        {
            InventoryTabButton.onClick.AddListener(ShowInventoryTab);
        }

        if (StoreTabButton != null)
        {
            StoreTabButton.onClick.AddListener(ShowStoreTab);
        }
    }

    public void ShowInventoryTab()
    {
        showingStore = false;
        selectedSlot = null;
        Populate();
    }

    public void ShowStoreTab()
    {
        showingStore = true;
        selectedSlot = null;
        Populate();
    }

    public bool TryBuyItem(string name, Sprite icon, GameObject prefab, Vector2Int footprint, int cost)
    {
        if (cost < 0)
        {
            cost = 0;
        }

        if (coins < cost)
        {
            return false;
        }

        coins -= cost;
        UpdateCoinsText();

        var newItem = new InventoryItem
        {
            Name = name,
            Icon = icon,
            Prefab = prefab,
            Footprint = footprint,
            Cost = cost
        };
        Items.Add(newItem);
        return true;
    }

    private void UpdateCoinsText()
    {
        if (CoinsText != null)
        {
            CoinsText.text = coins.ToString();
        }
    }

    public void SetSelectedSlot(InventoryItemSlot slot)
    {
        if (selectedSlot != null && selectedSlot != slot)
        {
            selectedSlot.SetSelected(false);
        }

        selectedSlot = slot;
        if (selectedSlot != null)
        {
            selectedSlot.SetSelected(true);
        }
    }

    public void SelectItem(int index)
    {
        if (GridSystem != null)
        {
            GridSystem.SelectItem(index);
        }
    }

    public void SelectPrefab(GameObject prefab)
    {
        if (GridSystem != null)
        {
            GridSystem.SelectPrefab(prefab);
        }
    }

    public void SelectItem(int index, Vector2Int footprint)
    {
        if (GridSystem != null)
        {
            GridSystem.SelectItem(index, footprint);
        }
    }

    public void SelectPrefab(GameObject prefab, Vector2Int footprint)
    {
        if (GridSystem != null)
        {
            GridSystem.SelectPrefab(prefab, footprint);
        }
    }

    private void LayoutItems()
    {
        if (ContentRoot == null || ContentRoot.childCount == 0)
        {
            return;
        }

        var paddingLeft = PaddingLeftTopRightBottom.x;
        var paddingTop = PaddingLeftTopRightBottom.y;
        var paddingRight = PaddingLeftTopRightBottom.z;
        var paddingBottom = PaddingLeftTopRightBottom.w;

        var firstRect = ContentRoot.GetChild(0) as RectTransform;
        if (firstRect == null)
        {
            return;
        }

        var columns = Mathf.Max(1, FixedColumns);
        var itemHeight = firstRect.rect.height;

        var verticalSign = ContentRoot.lossyScale.y < 0 ? 1f : -1f;

        for (int i = 0; i < ContentRoot.childCount; i++)
        {
            var child = ContentRoot.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 1f);

            var col = i % columns;
            var row = i / columns;
            var x = paddingLeft + col * ItemStep.x;
            var y = paddingTop + row * ItemStep.y;
            child.anchoredPosition = new Vector2(x, verticalSign * -y);
        }

        var rows = Mathf.CeilToInt(ContentRoot.childCount / (float)columns);
        var totalHeight = paddingTop + paddingBottom + itemHeight + Mathf.Max(0, rows - 1) * ItemStep.y;
        if (totalHeight > ContentRoot.rect.height)
        {
            ContentRoot.sizeDelta = new Vector2(ContentRoot.sizeDelta.x, totalHeight);
        }
    }

    private Text FindTextByName(Transform root, string objectName)
    {
        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
            {
                return texts[i];
            }
        }
        return null;
    }

    [System.Serializable]
    public class InventoryItem
    {
        public string Name;
        public Sprite Icon;
        public GameObject Prefab;
        public Vector2Int Footprint = Vector2Int.one;
        public int Cost;
    }
}
