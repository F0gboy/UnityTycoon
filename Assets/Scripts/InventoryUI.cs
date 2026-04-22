using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

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
    public TMP_Text CoinsTextTMP;

    [Header("Input")]
    public KeyCode ToggleKey = KeyCode.F;
    public bool KeepCursorUnlockedWhileVisible = true;

    [Header("Tabs")]
    public Button InventoryButton;
    public Button MinersStoreButton;
    public Button TransportationStoreButton;
    public Button UpgradersStoreButton;
    public Button SellersStoreButton;

    [Header("Slide Settings")]
    public float SlideDuration = 0.25f;
    public Vector2 VisibleAnchoredPosition = Vector2.zero;
    public Vector2 HiddenAnchoredPosition = new Vector2(-800f, 0f);
    public bool StartHiddenOnAwake = false;

    [Header("Grid Layout")]
    public Vector2 Spacing = new Vector2(8f, 8f);
    public Vector4 PaddingLeftTopRightBottom = new Vector4(12f, 12f, 12f, 12f);
    public int FixedColumns = 2;
    public float HorizontalStepRight = 0f;
    public Vector2 ItemStep = new Vector2(10f, 20f);
    public int MaxVisibleSlots = 9;
    public bool ForceTopLeftRuntimeAnchors = false;
    public bool AutoResizeContentRoot = false;


    [Header("Placement Integration")]
    public GridSystem GridSystem;

    [Header("Audio")]
    public AudioSource AudioSource;
    public AudioClip CoinGainSound;

    private bool isVisible;
private Coroutine slideRoutine;
    private bool showingStore;
    private StoreCategory activeStoreCategory = StoreCategory.Miners;
    private InventoryItemSlot selectedSlot;
    private int coins;
    private readonly List<GameObject> spawnedSlots = new List<GameObject>();
    private GameObject pendingTeleporterPrefab;
    private GameObject pendingTeleporterFirstInstance;

    private void Awake()
    {
        Instance = this;
        EnsureNoGridLayout();
        HookTabButtons();
        coins = StartingCoins;
        UpdateCoinsText();
        HookGridEvents();
        Populate();
        SetVisible(!StartHiddenOnAwake, instant: true);
    }

    private void OnDestroy()
    {
        UnhookTabButtons();

        if (GridSystem != null)
        {
            GridSystem.ItemPlacedWithInstance -= HandleItemPlacedWithInstance;
        }

        if (Instance == this)
        {
            Instance = null;
        }
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

        if (!visible)
        {
            HandleIncompleteTeleporterPlacementOnInventoryClose();
            SetSelectedSlot(null);
            if (GridSystem != null)
            {
                GridSystem.ClearSelection();
            }
        }
    }

    public void Populate()
    {
        if (ContentRoot == null || ItemSlotPrefab == null)
        {
            return;
        }

        for (int i = spawnedSlots.Count - 1; i >= 0; i--)
        {
            var existingSlot = spawnedSlots[i];
            if (existingSlot == null)
            {
                spawnedSlots.RemoveAt(i);
                continue;
            }

            existingSlot.transform.SetParent(null, false);
            Destroy(existingSlot);
            spawnedSlots.RemoveAt(i);
        }

        var source = showingStore
            ? GetStoreItemsForActiveCategory()
            : Items;
        var prefabToUse = showingStore && StoreItemSlotPrefab != null
            ? StoreItemSlotPrefab
            : ItemSlotPrefab;
        var maxSlots = Mathf.Max(0, MaxVisibleSlots);
        var visibleCount = Mathf.Min(source.Count, maxSlots);

        for (int i = 0; i < visibleCount; i++)
        {
            var item = source[i];
            var slot = Instantiate(prefabToUse, ContentRoot, false);
            slot.transform.localScale = Vector3.one;
            spawnedSlots.Add(slot);

            ApplySlotIcon(slot, item.Icon);

            var nameText = slot.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                nameText.text = showingStore
                    ? item.Name
                    : item.Quantity + "x " + item.Name;
            }

            if (showingStore)
            {
                var priceTmp = FindTMPByName(slot.transform, "Price");
                if (priceTmp != null)
                {
                    priceTmp.text = item.Cost.ToString();
                }
            }
            else
            {
                var countTmp = FindTMPByName(slot.transform, "Count");
                if (countTmp != null)
                {
                    countTmp.text = item.Quantity.ToString();
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
            slotClick.PlacementOffset = item.PlacementOffset;
            slotClick.ItemName = item.Name;
            slotClick.Icon = item.Icon;
            slotClick.Cost = item.Cost;
            slotClick.Category = item.Category;
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
        if (InventoryButton != null)
        {
            InventoryButton.onClick.RemoveListener(ShowInventoryTab);
            InventoryButton.onClick.AddListener(ShowInventoryTab);
        }

        if (MinersStoreButton != null)
        {
            MinersStoreButton.onClick.RemoveListener(ShowMinersStoreTab);
            MinersStoreButton.onClick.AddListener(ShowMinersStoreTab);
        }

        if (TransportationStoreButton != null)
        {
            TransportationStoreButton.onClick.RemoveListener(ShowTransportationStoreTab);
            TransportationStoreButton.onClick.AddListener(ShowTransportationStoreTab);
        }

        if (UpgradersStoreButton != null)
        {
            UpgradersStoreButton.onClick.RemoveListener(ShowUpgradersStoreTab);
            UpgradersStoreButton.onClick.AddListener(ShowUpgradersStoreTab);
        }

        if (SellersStoreButton != null)
        {
            SellersStoreButton.onClick.RemoveListener(ShowSellersStoreTab);
            SellersStoreButton.onClick.AddListener(ShowSellersStoreTab);
        }
    }

    private void UnhookTabButtons()
    {
        if (InventoryButton != null)
        {
            InventoryButton.onClick.RemoveListener(ShowInventoryTab);
        }

        if (MinersStoreButton != null)
        {
            MinersStoreButton.onClick.RemoveListener(ShowMinersStoreTab);
        }

        if (TransportationStoreButton != null)
        {
            TransportationStoreButton.onClick.RemoveListener(ShowTransportationStoreTab);
        }

        if (UpgradersStoreButton != null)
        {
            UpgradersStoreButton.onClick.RemoveListener(ShowUpgradersStoreTab);
        }

        if (SellersStoreButton != null)
        {
            SellersStoreButton.onClick.RemoveListener(ShowSellersStoreTab);
        }
    }

    public void ShowInventoryTab()
    {
        showingStore = false;
        selectedSlot = null;
        Populate();
    }

    public void ShowMinersStoreTab()
    {
        ShowStoreTab(StoreCategory.Miners);
    }

    public void ShowTransportationStoreTab()
    {
        ShowStoreTab(StoreCategory.Transportation);
    }

    public void ShowUpgradersStoreTab()
    {
        ShowStoreTab(StoreCategory.Upgraders);
    }

    public void ShowSellersStoreTab()
    {
        ShowStoreTab(StoreCategory.Sellers);
    }

    private void ShowStoreTab(StoreCategory category)
    {
        showingStore = true;
        activeStoreCategory = category;
        SetSelectedSlot(null);
        if (GridSystem != null)
        {
            GridSystem.ClearSelection();
        }
        Populate();
    }

    public void ShowStoreTab()
    {
        ShowMinersStoreTab();
    }

    public bool TryBuyItem(string name, Sprite icon, GameObject prefab, Vector2Int footprint, Vector3 placementOffset, int cost, StoreCategory category)
    {
        cost = Mathf.Max(0, cost);

        if (coins < cost)
        {
            return false;
        }

        coins -= cost;
        UpdateCoinsText();

        var quantityToAdd = IsTeleporterPrefab(prefab) ? 2 : 1;
        AddOrIncrementItem(prefab, quantityToAdd, name, icon, footprint, placementOffset, cost, category);

        SetSelectedSlot(null);
        if (GridSystem != null)
        {
            GridSystem.ClearSelection();
        }
        return true;
    }


    private void UpdateCoinsText()
    {
        if (CoinsTextTMP != null)
        {
            CoinsTextTMP.text = coins.ToString();
        }
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins += amount;
        UpdateCoinsText();

        if (AudioSource != null && CoinGainSound != null)
        {
            AudioSource.PlayOneShot(CoinGainSound);
        }
    }

    public bool TryGetCostForPrefab(GameObject prefab, out int cost)
    {
        cost = 0;
        if (prefab == null)
        {
            return false;
        }

        prefab = ResolveInventoryPrefabForPlaced(prefab);

        var storeItem = FindStoreItemByPrefab(prefab);
        if (storeItem != null)
        {
            cost = Mathf.Max(0, storeItem.Cost);
            return true;
        }

        var inventoryItem = FindItemByPrefab(prefab);
        if (inventoryItem != null)
        {
            cost = Mathf.Max(0, inventoryItem.Cost);
            return true;
        }

        return false;
    }

    public void AddItemFromWorld(GameObject prefab, Vector2Int footprint, Vector3 placementOffset, string displayName = null)
    {
        if (prefab == null)
        {
            return;
        }

        prefab = ResolveInventoryPrefabForPlaced(prefab);

        var storeItem = FindStoreItemByPrefab(prefab);
        AddOrIncrementItem(
            prefab,
            1,
            !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : (storeItem != null ? storeItem.Name : prefab.name),
            storeItem != null ? storeItem.Icon : null,
            footprint,
            placementOffset,
            storeItem != null ? storeItem.Cost : 0,
            storeItem != null ? storeItem.Category : StoreCategory.Miners);
        Populate();
    }

    public bool TryAddStoreItemToInventory(GameObject storePrefab, int quantity = 1)
    {
        if (storePrefab == null || quantity <= 0)
        {
            return false;
        }

        var storeItem = FindStoreItemByPrefab(storePrefab);
        if (storeItem == null)
        {
            return false;
        }

        AddOrIncrementItem(
            storeItem.Prefab,
            quantity,
            storeItem.Name,
            storeItem.Icon,
            storeItem.Footprint,
            storeItem.PlacementOffset,
            storeItem.Cost,
            storeItem.Category);
        Populate();
        return true;
    }

    private List<InventoryItem> GetStoreItemsForActiveCategory()
    {
        var filtered = new List<InventoryItem>();
        for (int i = 0; i < StoreItems.Count; i++)
        {
            var item = StoreItems[i];
            if (item == null)
            {
                continue;
            }

            if (item.Category != activeStoreCategory)
            {
                continue;
            }

            filtered.Add(item);
        }

        return filtered;
    }

    public bool TryResolveStorePrefabForPlaced(GameObject placedPrefab, out GameObject storePrefab)
    {
        storePrefab = null;
        if (placedPrefab == null)
        {
            return false;
        }

        var resolved = ResolveInventoryPrefabForPlaced(placedPrefab);
        if (resolved != null && FindStoreItemByPrefab(resolved) != null)
        {
            storePrefab = resolved;
            return true;
        }

        var teleporter = GetTeleporterItemFromPrefab(placedPrefab);
        if (teleporter != null)
        {
            var canonicalA = FindTeleporterPrefabByPair(
                teleporter.PairId,
                TeleporterItem.TeleporterVariant.A,
                null);
            if (canonicalA != null && FindStoreItemByPrefab(canonicalA) != null)
            {
                storePrefab = canonicalA;
                return true;
            }
        }

        return false;
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

    public void SelectItem(int index, Vector2Int footprint, Vector3 placementOffset)
    {
        if (GridSystem != null)
        {
            GridSystem.SelectItem(index, footprint, placementOffset);
        }
    }

    public void SelectPrefab(GameObject prefab, Vector2Int footprint)
    {
        if (GridSystem != null)
        {
            GridSystem.SelectPrefab(prefab, footprint);
        }
    }

    public void SelectPrefab(GameObject prefab, Vector2Int footprint, Vector3 placementOffset)
    {
        if (GridSystem != null)
        {
            GridSystem.SelectPrefab(prefab, footprint, placementOffset);
        }
    }

    private void LayoutItems()
    {
        if (ContentRoot == null || ContentRoot.childCount == 0)
        {
            return;
        }

        if (ForceTopLeftRuntimeAnchors)
        {
            ContentRoot.anchorMin = new Vector2(0f, 1f);
            ContentRoot.anchorMax = new Vector2(0f, 1f);
            ContentRoot.pivot = new Vector2(0f, 1f);
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

        var itemWidth = firstRect.rect.width;
        var itemHeight = firstRect.rect.height;
        var stepX = HorizontalStepRight > 0f
            ? HorizontalStepRight
            : (itemWidth + Spacing.x);

        for (int i = 0; i < ContentRoot.childCount; i++)
        {
            var child = ContentRoot.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            if (ForceTopLeftRuntimeAnchors)
            {
                child.anchorMin = new Vector2(0f, 1f);
                child.anchorMax = new Vector2(0f, 1f);
                child.pivot = new Vector2(0f, 1f);
            }

            var x = paddingLeft + i * stepX;
            child.anchoredPosition = new Vector2(x, -paddingTop);
        }

        var slotCountForWidth = Mathf.Max(ContentRoot.childCount, Mathf.Max(0, MaxVisibleSlots));
        var totalWidth = paddingLeft + paddingRight + itemWidth + Mathf.Max(0, slotCountForWidth - 1) * stepX;
        var totalHeight = paddingTop + paddingBottom + itemHeight;

        if (AutoResizeContentRoot)
        {
            ContentRoot.sizeDelta = new Vector2(totalWidth, totalHeight);
        }
    }

    private void HookGridEvents()
    {
        if (GridSystem != null)
        {
            GridSystem.ItemPlacedWithInstance -= HandleItemPlacedWithInstance;
            GridSystem.ItemPlacedWithInstance += HandleItemPlacedWithInstance;
        }
    }

    private void HandleItemPlacedWithInstance(GameObject prefab, GameObject placedInstance)
    {
        if (prefab == null)
        {
            return;
        }

        var inventoryPrefab = ResolveInventoryPrefabForPlaced(prefab);
        if (IsTeleporterPrefab(prefab)
            && pendingTeleporterPrefab != null
            && pendingTeleporterFirstInstance != null
            && placedInstance != pendingTeleporterFirstInstance)
        {
            inventoryPrefab = pendingTeleporterPrefab;
        }

        var item = FindItemByPrefab(inventoryPrefab);
        if (item == null)
        {
            return;
        }

        item.Quantity = Mathf.Max(0, item.Quantity - 1);
        if (item.Quantity == 0)
        {
            Items.Remove(item);
            SetSelectedSlot(null);
            if (GridSystem != null)
            {
                GridSystem.ClearSelection();
            }
        }

        if (IsTeleporterPrefab(prefab))
        {
            if (pendingTeleporterPrefab == inventoryPrefab && pendingTeleporterFirstInstance != null)
            {
                TryLinkTeleporterPair(pendingTeleporterFirstInstance, placedInstance);
                ClearPendingTeleporterPlacement();

                SetSelectedSlot(null);
                if (GridSystem != null)
                {
                    GridSystem.ClearSelection();
                }
            }
            else
            {
                pendingTeleporterPrefab = inventoryPrefab;
                pendingTeleporterFirstInstance = placedInstance;
                var secondPrefab = ResolveTeleporterSecondPrefab(prefab, inventoryPrefab);
                SelectTeleporterSecondPlacementPrefab(prefab, secondPrefab);
            }
        }

        Populate();
    }

    private void HandleIncompleteTeleporterPlacementOnInventoryClose()
    {
        if (pendingTeleporterPrefab == null)
        {
            return;
        }

        var wasRemoved = false;
        if (pendingTeleporterFirstInstance != null)
        {
            if (GridSystem != null)
            {
                wasRemoved = GridSystem.RemovePlacedObject(pendingTeleporterFirstInstance);
            }
            else
            {
                Destroy(pendingTeleporterFirstInstance);
                wasRemoved = true;
            }
        }

        if (wasRemoved)
        {
            AddItemQuantity(pendingTeleporterPrefab, 1);
            Populate();
        }

        ClearPendingTeleporterPlacement();
    }

    private void ClearPendingTeleporterPlacement()
    {
        pendingTeleporterPrefab = null;
        pendingTeleporterFirstInstance = null;
    }

    private void TryLinkTeleporterPair(GameObject firstInstance, GameObject secondInstance)
    {
        if (firstInstance == null || secondInstance == null)
        {
            return;
        }

        var firstItem = firstInstance.GetComponentInChildren<TeleporterItem>(true);
        var secondItem = secondInstance.GetComponentInChildren<TeleporterItem>(true);
        if (firstItem == null || secondItem == null)
        {
            return;
        }

        var firstEndpoint = firstItem.GetEndpoint();
        var secondEndpoint = secondItem.GetEndpoint();
        if (firstEndpoint == null || secondEndpoint == null)
        {
            return;
        }

        firstEndpoint.LinkBidirectional(secondEndpoint);
    }

    private bool IsTeleporterPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        return prefab.GetComponent<TeleporterItem>() != null
            || prefab.GetComponentInChildren<TeleporterItem>(true) != null;
    }

    private GameObject ResolveInventoryPrefabForPlaced(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        var teleporter = GetTeleporterItemFromPrefab(prefab);
        if (teleporter == null)
        {
            return prefab;
        }

        var owner = teleporter.GetInventoryOwnerPrefab(prefab);
        if (owner == prefab && teleporter.Variant == TeleporterItem.TeleporterVariant.B)
        {
            owner = FindTeleporterOwnerBySecondPrefab(prefab)
                ?? FindTeleporterPrefabByPair(teleporter.PairId, TeleporterItem.TeleporterVariant.A, prefab);
        }

        return owner ?? prefab;
    }

    private GameObject ResolveTeleporterSecondPrefab(GameObject placedPrefab, GameObject inventoryPrefab)
    {
        var teleporter = placedPrefab != null
            ? placedPrefab.GetComponent<TeleporterItem>()
            : null;
        if (teleporter == null && placedPrefab != null)
        {
            teleporter = placedPrefab.GetComponentInChildren<TeleporterItem>(true);
        }

        if (teleporter != null)
        {
            var explicitB = teleporter.GetTeleporterBPrefab();
            if (explicitB != null && explicitB != placedPrefab)
            {
                return explicitB;
            }

            var targetVariant = teleporter.Variant == TeleporterItem.TeleporterVariant.A
                ? TeleporterItem.TeleporterVariant.B
                : TeleporterItem.TeleporterVariant.A;

            var byPair = FindTeleporterPrefabByPair(
                teleporter.PairId,
                targetVariant,
                placedPrefab);
            if (byPair != null)
            {
                return byPair;
            }
        }

        return inventoryPrefab;
    }

    private GameObject FindTeleporterOwnerBySecondPrefab(GameObject secondPrefab)
    {
        if (secondPrefab == null)
        {
            return null;
        }

        var fromStore = FindTeleporterPrefabInItems(
            StoreItems,
            TeleporterItem.TeleporterVariant.A,
            pairId: null,
            teleporterBPrefab: secondPrefab,
            exclude: null);
        if (fromStore != null)
        {
            return fromStore;
        }

        var fromInventory = FindTeleporterPrefabInItems(
            Items,
            TeleporterItem.TeleporterVariant.A,
            pairId: null,
            teleporterBPrefab: secondPrefab,
            exclude: null);
        if (fromInventory != null)
        {
            return fromInventory;
        }

        if (GridSystem != null && GridSystem.PlaceablePrefabs != null)
        {
            for (int i = 0; i < GridSystem.PlaceablePrefabs.Count; i++)
            {
                var candidate = GridSystem.PlaceablePrefabs[i];
                var teleporter = GetTeleporterItemFromPrefab(candidate);
                if (teleporter == null)
                {
                    continue;
                }

                if (teleporter.Variant != TeleporterItem.TeleporterVariant.A)
                {
                    continue;
                }

                if (teleporter.GetTeleporterBPrefab() == secondPrefab)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private GameObject FindTeleporterPrefabByPair(string pairId, TeleporterItem.TeleporterVariant variant, GameObject exclude)
    {
        if (string.IsNullOrWhiteSpace(pairId))
        {
            return null;
        }

        var fromStore = FindTeleporterPrefabInItems(
            StoreItems,
            variant,
            pairId,
            teleporterBPrefab: null,
            exclude);
        if (fromStore != null)
        {
            return fromStore;
        }

        var fromInventory = FindTeleporterPrefabInItems(
            Items,
            variant,
            pairId,
            teleporterBPrefab: null,
            exclude);
        if (fromInventory != null)
        {
            return fromInventory;
        }

        if (GridSystem != null && GridSystem.PlaceablePrefabs != null)
        {
            for (int i = 0; i < GridSystem.PlaceablePrefabs.Count; i++)
            {
                var prefab = GridSystem.PlaceablePrefabs[i];
                var teleporter = GetTeleporterItemFromPrefab(prefab);
                if (teleporter == null)
                {
                    continue;
                }

                if (prefab == exclude)
                {
                    continue;
                }

                if (teleporter.Variant != variant)
                {
                    continue;
                }

                if (!string.Equals(teleporter.PairId, pairId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return prefab;
            }
        }

        return null;
    }

    private GameObject FindTeleporterPrefabInItems(
        List<InventoryItem> source,
        TeleporterItem.TeleporterVariant variant,
        string pairId,
        GameObject teleporterBPrefab,
        GameObject exclude)
    {
        if (source == null)
        {
            return null;
        }

        for (int i = 0; i < source.Count; i++)
        {
            var prefab = source[i] != null ? source[i].Prefab : null;
            if (prefab == null || prefab == exclude)
            {
                continue;
            }

            var teleporter = GetTeleporterItemFromPrefab(prefab);
            if (teleporter == null)
            {
                continue;
            }

            if (teleporter.Variant != variant)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(pairId)
                && !string.Equals(teleporter.PairId, pairId, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (teleporterBPrefab != null && teleporter.GetTeleporterBPrefab() != teleporterBPrefab)
            {
                continue;
            }

            return prefab;
        }

        return null;
    }

    private TeleporterItem GetTeleporterItemFromPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        var teleporter = prefab.GetComponent<TeleporterItem>();
        if (teleporter == null)
        {
            teleporter = prefab.GetComponentInChildren<TeleporterItem>(true);
        }

        return teleporter;
    }

    private void SelectTeleporterSecondPlacementPrefab(GameObject sourcePrefab, GameObject secondPrefab)
    {
        if (GridSystem == null || secondPrefab == null)
        {
            return;
        }

        var storeItem = FindStoreItemByPrefab(secondPrefab);
        var inventoryItem = FindItemByPrefab(secondPrefab);

        var footprint = Vector2Int.one;
        var placementOffset = Vector3.zero;

        if (storeItem != null)
        {
            footprint = storeItem.Footprint;
            placementOffset = storeItem.PlacementOffset;
        }
        else if (inventoryItem != null)
        {
            footprint = inventoryItem.Footprint;
            placementOffset = inventoryItem.PlacementOffset;
        }
        else
        {
            var sourceTeleporter = GetTeleporterItemFromPrefab(sourcePrefab);
            if (sourceTeleporter != null && sourceTeleporter.GetTeleporterBPrefab() == secondPrefab)
            {
                footprint = sourceTeleporter.GetTeleporterBFootprint();
            }
        }

        GridSystem.SelectPrefab(secondPrefab, footprint, placementOffset);
    }

    private void AddItemQuantity(GameObject prefab, int amount)
    {
        if (prefab == null || amount <= 0)
        {
            return;
        }

        var storeItem = FindStoreItemByPrefab(prefab);
        AddOrIncrementItem(
            prefab,
            amount,
            storeItem != null ? storeItem.Name : prefab.name,
            storeItem != null ? storeItem.Icon : null,
            storeItem != null ? storeItem.Footprint : Vector2Int.one,
            storeItem != null ? storeItem.PlacementOffset : Vector3.zero,
            storeItem != null ? storeItem.Cost : 0,
            storeItem != null ? storeItem.Category : StoreCategory.Miners);
    }

    private void AddOrIncrementItem(
        GameObject prefab,
        int amount,
        string name,
        Sprite icon,
        Vector2Int footprint,
        Vector3 placementOffset,
        int cost,
        StoreCategory category)
    {
        if (prefab == null || amount <= 0)
        {
            return;
        }

        var existing = FindItemByPrefab(prefab);
        if (existing != null)
        {
            existing.Quantity += amount;
            return;
        }

        Items.Add(new InventoryItem
        {
            Name = name,
            Icon = icon,
            Prefab = prefab,
            Footprint = footprint,
            PlacementOffset = placementOffset,
            Cost = cost,
            Category = category,
            Quantity = amount
        });
    }

    private InventoryItem FindItemByPrefab(GameObject prefab)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Prefab == prefab)
            {
                return Items[i];
            }
        }
        return null;
    }

    private InventoryItem FindStoreItemByPrefab(GameObject prefab)
    {
        for (int i = 0; i < StoreItems.Count; i++)
        {
            if (StoreItems[i].Prefab == prefab)
            {
                return StoreItems[i];
            }
        }
        return null;
    }

    private TMP_Text FindTMPByName(Transform root, string objectName)
    {
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
            {
                return texts[i];
            }
        }

        var objectNameLower = objectName.ToLowerInvariant();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name.ToLowerInvariant().Contains(objectNameLower))
            {
                return texts[i];
            }
        }
        return null;
    }

    private void ApplySlotIcon(GameObject slot, Sprite icon)
    {
        var rootImage = slot.GetComponent<Image>();
        if (rootImage != null)
        {
            if (icon == null)
            {
                rootImage.enabled = true;
                return;
            }

            EnsureImageBackgroundOverlay(slot.transform, rootImage);
            rootImage.sprite = icon;
            rootImage.enabled = true;
            return;
        }

        var rootRawImage = slot.GetComponent<RawImage>();
        if (rootRawImage != null)
        {
            if (icon == null)
            {
                rootRawImage.enabled = true;
                return;
            }

            EnsureRawImageBackgroundOverlay(slot.transform, rootRawImage);
            rootRawImage.texture = icon.texture;
            rootRawImage.enabled = true;
            return;
        }
    }

    private void EnsureRawImageBackgroundOverlay(Transform root, RawImage rootRawImage)
    {
        var existingOverlay = root.Find("BackgroundOverlay")?.GetComponent<RawImage>();
        if (existingOverlay != null)
        {
            return;
        }

        var overlayObject = new GameObject("BackgroundOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var overlayTransform = overlayObject.GetComponent<RectTransform>();
        overlayTransform.SetParent(root, false);
        overlayTransform.anchorMin = Vector2.zero;
        overlayTransform.anchorMax = Vector2.one;
        overlayTransform.offsetMin = Vector2.zero;
        overlayTransform.offsetMax = Vector2.zero;
        overlayTransform.SetSiblingIndex(0);

        var overlayRawImage = overlayObject.GetComponent<RawImage>();
        overlayRawImage.texture = rootRawImage.texture;
        overlayRawImage.material = rootRawImage.material;
        overlayRawImage.color = rootRawImage.color;
        overlayRawImage.raycastTarget = false;
        overlayRawImage.maskable = rootRawImage.maskable;
        overlayRawImage.uvRect = rootRawImage.uvRect;
    }

    private void EnsureImageBackgroundOverlay(Transform root, Image rootImage)
    {
        var existingOverlay = root.Find("BackgroundOverlay")?.GetComponent<Image>();
        if (existingOverlay != null)
        {
            return;
        }

        var overlayObject = new GameObject("BackgroundOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var overlayTransform = overlayObject.GetComponent<RectTransform>();
        overlayTransform.SetParent(root, false);
        overlayTransform.anchorMin = Vector2.zero;
        overlayTransform.anchorMax = Vector2.one;
        overlayTransform.offsetMin = Vector2.zero;
        overlayTransform.offsetMax = Vector2.zero;
        overlayTransform.SetSiblingIndex(0);

        var overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.sprite = rootImage.sprite;
        overlayImage.material = rootImage.material;
        overlayImage.color = rootImage.color;
        overlayImage.raycastTarget = false;
        overlayImage.maskable = rootImage.maskable;
        overlayImage.type = rootImage.type;
        overlayImage.preserveAspect = rootImage.preserveAspect;
        overlayImage.fillCenter = rootImage.fillCenter;
        overlayImage.fillMethod = rootImage.fillMethod;
        overlayImage.fillAmount = rootImage.fillAmount;
        overlayImage.fillClockwise = rootImage.fillClockwise;
        overlayImage.fillOrigin = rootImage.fillOrigin;
    }

    [System.Serializable]
    public enum StoreCategory
    {
        Miners,
        Transportation,
        Upgraders,
        Sellers
    }

    [System.Serializable]
    public class InventoryItem
    {
        public string Name;
        public Sprite Icon;
        public GameObject Prefab;
        public Vector2Int Footprint = Vector2Int.one;
        public Vector3 PlacementOffset = Vector3.zero;
        public int Cost;
        public StoreCategory Category = StoreCategory.Miners;
        public int Quantity = 1;
    }
}
