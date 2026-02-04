using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform InventoryPanel;
    public RectTransform ContentRoot;
    public GameObject ItemSlotPrefab;

    [Header("Inventory Data")]
    public List<InventoryItem> Items = new List<InventoryItem>();

    [Header("Input")]
    public KeyCode ToggleKey = KeyCode.F;

    [Header("Slide Settings")]
    public float SlideDuration = 0.25f;
    public Vector2 VisibleAnchoredPosition = Vector2.zero;
    public Vector2 HiddenAnchoredPosition = new Vector2(-800f, 0f);

    [Header("Grid Layout")]
    public Vector2 CellSize = new Vector2(80f, 80f);
    public Vector2 Spacing = new Vector2(8f, 8f);
    public Vector4 PaddingLeftTopRightBottom = new Vector4(12f, 12f, 12f, 12f);
    public int FixedColumns = 5;

    [Header("Placement Integration")]
    public GridSystem GridSystem;

    private bool isVisible;
    private Coroutine slideRoutine;

    private void Awake()
    {
        EnsureGridLayout();
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
    }

    public void Populate()
    {
        if (ContentRoot == null || ItemSlotPrefab == null)
        {
            return;
        }

        for (int i = ContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(ContentRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var slot = Instantiate(ItemSlotPrefab, ContentRoot);

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

            var index = i;
            var button = slot.GetComponent<Button>();
            if (button != null && GridSystem != null)
            {
                button.onClick.AddListener(() => GridSystem.SelectItem(index));
            }
        }
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

    private void EnsureGridLayout()
    {
        if (ContentRoot == null)
        {
            return;
        }

        var grid = ContentRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = ContentRoot.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.cellSize = CellSize;
        grid.spacing = Spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, FixedColumns);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.padding = new RectOffset(
            (int)PaddingLeftTopRightBottom.x,
            (int)PaddingLeftTopRightBottom.z,
            (int)PaddingLeftTopRightBottom.y,
            (int)PaddingLeftTopRightBottom.w);
    }

    [System.Serializable]
    public class InventoryItem
    {
        public string Name;
        public Sprite Icon;
    }
}
