using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlacedObjectInteractionController : MonoBehaviour
{
    [Header("References")]
    public GridSystem GridSystem;
    public InventoryUI Inventory;
    public Camera InteractionCamera;

    [Header("Selection")]
    public LayerMask SelectionMask = ~0;
    public bool BlockSelectionWhenPointerOverUI;

    [Header("World UI")]
    public GameObject WorldActionPanelPrefab;
    public Vector3 PanelOffset = new Vector3(0f, 1.5f, 0f);
    public float DistanceTowardsPlayer = 0.85f;
    public bool FaceCamera;
    public Vector3 WorldPanelScale = new Vector3(0.01f, 0.01f, 0.01f);
    [Range(0f, 1f)] public float SellRefundPercent = 0.2f;
    public bool EnableManualButtonFallback = true;
    public Color HoverOutlineColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Vector2 HoverOutlineDistance = new Vector2(3f, 3f);

    private PlaceableObjectData selectedData;
    private GameObject activePanel;
    private Button moveButton;
    private Button pickUpButton;
    private Button sellButton;
    private TMP_Text titleText;
    private readonly Dictionary<Button, Outline> hoverOutlines = new Dictionary<Button, Outline>();

    private void Awake()
    {
        if (GridSystem == null)
        {
            GridSystem = FindObjectOfType<GridSystem>();
        }

        if (Inventory == null)
        {
            Inventory = InventoryUI.Instance;
        }

        if (InteractionCamera == null)
        {
            InteractionCamera = Camera.main;
        }

        EnsureUIEventPipeline();
    }

    private void Update()
    {
        if (GridSystem == null)
        {
            return;
        }

        if (!GridSystem.PlacementModeActive || GridSystem.IsMovingPlacedObject)
        {
            ClearSelection();
            return;
        }

        if (GridSystem.HasPlacementSelection)
        {
            ClearSelection();
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (EnableManualButtonFallback && TryHandleManualButtonClick())
        {
            return;
        }

        if (EnableManualButtonFallback && IsMouseOverPanelArea())
        {
            return;
        }

        if (IsPointerOverActionPanelUI())
        {
            return;
        }

        if (BlockSelectionWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        TrySelectPlacedObject();
    }

    private void LateUpdate()
    {
        if (activePanel == null || !activePanel.activeSelf || selectedData == null)
        {
            return;
        }

        activePanel.transform.position = GetPanelSpawnPosition();
        UpdateButtonHoverOutlines();

        if (!FaceCamera)
        {
            return;
        }

        var cam = InteractionCamera != null ? InteractionCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        var forward = activePanel.transform.position - cam.transform.position;
        if (forward.sqrMagnitude > 0.001f)
        {
            activePanel.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }

    private void TrySelectPlacedObject()
    {
        var cam = InteractionCamera != null ? InteractionCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 600f, SelectionMask);
        if (hits == null || hits.Length == 0)
        {
            ClearSelection();
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var root = GridSystem.GetPlacedObjectRootFromHit(hits[i].transform);
            if (root == null)
            {
                continue;
            }

            if (!GridSystem.TryGetPlacedObjectData(root, out var data))
            {
                continue;
            }

            selectedData = data;
            ShowPanel();
            return;
        }

        ClearSelection();
    }

    private void ShowPanel()
    {
        if (WorldActionPanelPrefab == null || selectedData == null)
        {
            return;
        }

        if (activePanel == null)
        {
            activePanel = Instantiate(WorldActionPanelPrefab);
            ConfigureWorldPanel(activePanel);
            CachePanelReferences();
            HookButtons();
            EnsureButtonOutlines();
        }

        activePanel.SetActive(true);
        activePanel.transform.position = GetPanelSpawnPosition();

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(selectedData.DisplayName)
                ? selectedData.gameObject.name
                : selectedData.DisplayName;
        }
    }

    private void ConfigureWorldPanel(GameObject panelRoot)
    {
        panelRoot.transform.localScale = WorldPanelScale;

        var canvases = panelRoot.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].renderMode = RenderMode.WorldSpace;
            canvases[i].worldCamera = InteractionCamera != null ? InteractionCamera : Camera.main;

            if (canvases[i].GetComponent<GraphicRaycaster>() == null)
            {
                canvases[i].gameObject.AddComponent<GraphicRaycaster>();
            }

            var buttons = canvases[i].GetComponentsInChildren<Button>(true);
            for (int b = 0; b < buttons.Length; b++)
            {
                if (buttons[b].targetGraphic == null)
                {
                    var image = buttons[b].GetComponent<Image>();
                    if (image != null)
                    {
                        buttons[b].targetGraphic = image;
                    }
                }

                if (buttons[b].targetGraphic != null)
                {
                    buttons[b].targetGraphic.raycastTarget = true;
                }
            }

            var graphics = canvases[i].GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
            {
                graphics[g].raycastTarget = true;
            }
        }

        EnsureUIEventPipeline();
    }

    private void EnsureUIEventPipeline()
    {
        EnsureEventSystemExists();
        EnsureCameraHasPhysicsRaycaster();
    }

    private void EnsureEventSystemExists()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
            EnsureInputModuleExists(go);
            return;
        }

        EnsureInputModuleExists(eventSystem.gameObject);
    }

    private void EnsureInputModuleExists(GameObject eventSystemObject)
    {
        if (eventSystemObject == null)
        {
            return;
        }

        var modules = eventSystemObject.GetComponents<BaseInputModule>();
        if (modules != null && modules.Length > 0)
        {
            return;
        }

        var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
        {
            eventSystemObject.AddComponent(inputSystemModuleType);
            return;
        }

        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void EnsureCameraHasPhysicsRaycaster()
    {
        var cam = InteractionCamera != null ? InteractionCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        if (cam.GetComponent<PhysicsRaycaster>() == null)
        {
            cam.gameObject.AddComponent<PhysicsRaycaster>();
        }
    }

    private void CachePanelReferences()
    {
        moveButton = FindButton("MoveButton", "Move");
        pickUpButton = FindButton("PickUpButton", "PickUp");
        sellButton = FindButton("SellButton", "Sell");
        titleText = FindTMP("Title");
    }

    private Button FindButton(string primaryName, string fallbackName)
    {
        if (activePanel == null)
        {
            return null;
        }

        var buttons = activePanel.GetComponentsInChildren<Button>(true);
        var primaryNormalized = NormalizeButtonName(primaryName);
        var fallbackNormalized = NormalizeButtonName(fallbackName);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == primaryName || buttons[i].name == fallbackName)
            {
                return buttons[i];
            }
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            var current = NormalizeButtonName(buttons[i].name);
            if (current == primaryNormalized || current == fallbackNormalized)
            {
                return buttons[i];
            }

            if (current.Contains(primaryNormalized) || current.Contains(fallbackNormalized))
            {
                return buttons[i];
            }
        }

        return null;
    }

    private string NormalizeButtonName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    private TMP_Text FindTMP(string name)
    {
        if (activePanel == null)
        {
            return null;
        }

        var texts = activePanel.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == name)
            {
                return texts[i];
            }
        }

        return null;
    }

    private void HookButtons()
    {
        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(OnMoveClicked);
            moveButton.onClick.AddListener(OnMoveClicked);
        }

        if (pickUpButton != null)
        {
            pickUpButton.onClick.RemoveListener(OnPickUpClicked);
            pickUpButton.onClick.AddListener(OnPickUpClicked);
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveListener(OnSellClicked);
            sellButton.onClick.AddListener(OnSellClicked);
        }
    }

    private void OnMoveClicked()
    {
        if (selectedData == null || GridSystem == null)
        {
            return;
        }

        if (GridSystem.BeginMovePlacedObject(selectedData.gameObject))
        {
            ClearSelection();
        }
    }

    private void OnPickUpClicked()
    {
        if (selectedData == null || GridSystem == null)
        {
            return;
        }

        var inventory = Inventory != null ? Inventory : InventoryUI.Instance;
        var hasLinkedTeleporter = TryGetLinkedTeleporterData(selectedData, out var linkedData);

        if (inventory != null)
        {
            if (hasLinkedTeleporter && linkedData != null)
            {
                var pickupData = ResolveTeleporterPickupData(selectedData, linkedData);
                if (pickupData != null && inventory.TryResolveStorePrefabForPlaced(pickupData.SourcePrefab, out var storeTeleporterPrefab))
                {
                    inventory.TryAddStoreItemToInventory(storeTeleporterPrefab, 2);
                }
                else
                {
                    inventory.AddItemFromWorld(
                        pickupData.SourcePrefab,
                        pickupData.BaseFootprint,
                        pickupData.PlacementOffset,
                        pickupData.DisplayName);

                    inventory.AddItemFromWorld(
                        pickupData.SourcePrefab,
                        pickupData.BaseFootprint,
                        pickupData.PlacementOffset,
                        pickupData.DisplayName);
                }
            }
            else
            {
                inventory.AddItemFromWorld(
                    selectedData.SourcePrefab,
                    selectedData.BaseFootprint,
                    selectedData.PlacementOffset,
                    selectedData.DisplayName);
            }
        }

        if (hasLinkedTeleporter && linkedData != null)
        {
            GridSystem.RemovePlacedObject(linkedData.gameObject);
        }

        if (GridSystem.RemovePlacedObject(selectedData.gameObject))
        {
            ClearSelection();
        }
    }

    private bool TryGetLinkedTeleporterData(PlaceableObjectData source, out PlaceableObjectData linked)
    {
        linked = null;
        if (source == null || source.gameObject == null)
        {
            return false;
        }

        var teleporterItem = source.GetComponentInChildren<TeleporterItem>(true);
        if (teleporterItem == null)
        {
            return false;
        }

        var endpoint = teleporterItem.GetEndpoint();
        if (endpoint == null || endpoint.LinkedEndpoint == null)
        {
            return false;
        }

        var linkedRoot = endpoint.LinkedEndpoint.GetComponentInParent<PlaceableObjectData>();
        if (linkedRoot == null || linkedRoot == source)
        {
            return false;
        }

        if (!GridSystem.TryGetPlacedObjectData(linkedRoot.gameObject, out linked))
        {
            return false;
        }

        return linked != null;
    }

    private PlaceableObjectData ResolveTeleporterPickupData(PlaceableObjectData first, PlaceableObjectData second)
    {
        if (first == null)
        {
            return second;
        }

        if (second == null)
        {
            return first;
        }

        var firstTeleporter = first.GetComponentInChildren<TeleporterItem>(true);
        if (firstTeleporter != null && firstTeleporter.Variant == TeleporterItem.TeleporterVariant.A)
        {
            return first;
        }

        var secondTeleporter = second.GetComponentInChildren<TeleporterItem>(true);
        if (secondTeleporter != null && secondTeleporter.Variant == TeleporterItem.TeleporterVariant.A)
        {
            return second;
        }

        return first;
    }

    private void OnSellClicked()
    {
        if (selectedData == null || GridSystem == null)
        {
            return;
        }

        var inventory = Inventory != null ? Inventory : InventoryUI.Instance;
        if (inventory != null)
        {
            var value = ResolveSellValue(selectedData.gameObject, inventory);
            if (value > 0)
            {
                inventory.AddCoins(value);
            }
        }

        if (GridSystem.RemovePlacedObject(selectedData.gameObject))
        {
            ClearSelection();
        }
    }

    private int ResolveSellValue(GameObject target, InventoryUI inventory)
    {
        if (inventory.TryGetCostForPrefab(selectedData.SourcePrefab, out var cost))
        {
            return Mathf.RoundToInt(Mathf.Max(0f, SellRefundPercent) * Mathf.Max(0, cost));
        }

        var sellValue = target != null ? target.GetComponentInChildren<SellValue>(true) : null;
        return sellValue != null ? Mathf.Max(0, sellValue.Coins) : 0;
    }

    private Vector3 GetPanelSpawnPosition()
    {
        var basePosition = selectedData.transform.position + PanelOffset;
        var cam = InteractionCamera != null ? InteractionCamera : Camera.main;
        if (cam == null || DistanceTowardsPlayer <= 0f)
        {
            return basePosition;
        }

        var toPlayer = cam.transform.position - selectedData.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return basePosition;
        }

        return basePosition + toPlayer.normalized * DistanceTowardsPlayer;
    }

    private bool IsPointerOverActionPanelUI()
    {
        if (activePanel == null || !activePanel.activeInHierarchy || EventSystem.current == null)
        {
            return false;
        }

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject != null && results[i].gameObject.transform.IsChildOf(activePanel.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearSelection()
    {
        selectedData = null;
        SetButtonOutline(moveButton, false);
        SetButtonOutline(pickUpButton, false);
        SetButtonOutline(sellButton, false);
        if (activePanel != null)
        {
            activePanel.SetActive(false);
        }
    }

    private void EnsureButtonOutlines()
    {
        EnsureOutlineForButton(moveButton);
        EnsureOutlineForButton(pickUpButton);
        EnsureOutlineForButton(sellButton);
    }

    private void EnsureOutlineForButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (hoverOutlines.TryGetValue(button, out var existing) && existing != null)
        {
            existing.effectColor = HoverOutlineColor;
            existing.effectDistance = HoverOutlineDistance;
            return;
        }

        var targetGraphic = button.targetGraphic;
        if (targetGraphic == null)
        {
            targetGraphic = button.GetComponent<Graphic>();
            if (targetGraphic == null)
            {
                targetGraphic = button.GetComponentInChildren<Graphic>();
            }
        }

        if (targetGraphic == null)
        {
            return;
        }

        var outline = targetGraphic.GetComponent<Outline>();
        if (outline == null)
        {
            outline = targetGraphic.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = HoverOutlineColor;
        outline.effectDistance = HoverOutlineDistance;
        outline.enabled = false;
        hoverOutlines[button] = outline;
    }

    private void UpdateButtonHoverOutlines()
    {
        EnsureButtonOutlines();
        SetButtonOutline(moveButton, IsMouseOverButton(moveButton));
        SetButtonOutline(pickUpButton, IsMouseOverButton(pickUpButton));
        SetButtonOutline(sellButton, IsMouseOverButton(sellButton));
    }

    private void SetButtonOutline(Button button, bool enabled)
    {
        if (button == null)
        {
            return;
        }

        if (hoverOutlines.TryGetValue(button, out var outline) && outline != null)
        {
            outline.enabled = enabled;
            outline.effectColor = HoverOutlineColor;
            outline.effectDistance = HoverOutlineDistance;
        }
    }

    private bool TryHandleManualButtonClick()
    {
        if (activePanel == null || !activePanel.activeInHierarchy || !Input.GetMouseButtonDown(0))
        {
            return false;
        }

        if (IsMouseOverButton(moveButton))
        {
            OnMoveClicked();
            return true;
        }

        if (IsMouseOverButton(pickUpButton))
        {
            OnPickUpClicked();
            return true;
        }

        if (IsMouseOverButton(sellButton))
        {
            OnSellClicked();
            return true;
        }

        return TryHandlePanelAreaClickFallback();
    }

    private bool TryHandlePanelAreaClickFallback()
    {
        var panelRect = GetPrimaryPanelRect();
        if (panelRect == null)
        {
            return false;
        }

        var panelCanvas = panelRect.GetComponentInParent<Canvas>();
        var uiCamera = panelCanvas != null ? panelCanvas.worldCamera : (InteractionCamera != null ? InteractionCamera : Camera.main);
        if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, uiCamera))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, Input.mousePosition, uiCamera, out var local))
        {
            return false;
        }

        var rect = panelRect.rect;
        var normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);

        if (normalizedY >= 0.66f)
        {
            OnMoveClicked();
            return true;
        }

        if (normalizedY >= 0.33f)
        {
            OnPickUpClicked();
            return true;
        }

        OnSellClicked();
        return true;
    }

    private bool IsMouseOverPanelArea()
    {
        var panelRect = GetPrimaryPanelRect();
        if (panelRect == null)
        {
            return false;
        }

        var panelCanvas = panelRect.GetComponentInParent<Canvas>();
        var uiCamera = panelCanvas != null ? panelCanvas.worldCamera : (InteractionCamera != null ? InteractionCamera : Camera.main);
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, uiCamera);
    }

    private RectTransform GetPrimaryPanelRect()
    {
        if (activePanel == null)
        {
            return null;
        }

        var canvas = activePanel.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            return canvas.transform as RectTransform;
        }

        return activePanel.transform as RectTransform;
    }

    private bool IsMouseOverButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        var rect = button.transform as RectTransform;
        if (rect == null)
        {
            return false;
        }

        var panelCanvas = button.GetComponentInParent<Canvas>();
        var uiCamera = panelCanvas != null ? panelCanvas.worldCamera : (InteractionCamera != null ? InteractionCamera : Camera.main);
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, uiCamera);
    }
}
