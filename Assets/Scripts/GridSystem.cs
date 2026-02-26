using System.Collections.Generic;
using UnityEngine;

public class GridSystem : MonoBehaviour
{
    [Header("Grid Settings")]
    public Vector2Int GridSize = new Vector2Int(10, 10);
    public float CellSize = 1.0f;
    public Color GridColor = new Color(0f, 1f, 1f, 0.75f);
    public Material GridMaterial;
    public float GridLineWidth = 0.02f;
    public float GridHeightOffset = 0.02f;
    public bool AlignToSurfaceBounds = true;

    [Header("Placement")]
    public GameObject ObjectToPlace;
    public List<GameObject> PlaceablePrefabs = new List<GameObject>();
    public LayerMask PlacementSurfaceMask = ~0;
    public bool RotateWithQAndE = true;
    public bool RotateWithR = true;
    public float RRotationStep = 90f;
    public Transform PlacedParent;
    public bool ShowGhost = true;
    public Material GhostMaterial;
    public Color GhostValidColor = new Color(0f, 1f, 0f, 0.25f);
    public Color GhostBlockedColor = new Color(1f, 0f, 0f, 0.25f);
    public float GhostAlpha = 0.35f;

    [Header("Debug Cell Overlay")]
    public bool ShowDebugCellOverlay = true;
    public bool ShowOccupiedCellsInGameView = true;
    public Color OccupiedCellColor = new Color(1f, 0f, 0f, 0.35f);
    public Color GhostCellColor = new Color(1f, 0.75f, 0f, 0.42f);
    public Color GhostCellBlockedColor = new Color(1f, 0f, 0f, 0.6f);

    [Header("Build Hover Outline")]
    public bool ShowPlacedObjectOutline = true;
    public Color PlacedObjectOutlineColor = new Color(1f, 1f, 0f, 1f);
    public float PlacedObjectOutlineWidth = 0.03f;

    [Header("Inventory / Placement Mode")]
    public KeyCode ToggleInventoryKey = KeyCode.F;
    public bool PlacementModeActive = false;
    public int SelectedIndex = 0;
    public bool UseInputToggle = true;
    public System.Action<GameObject> ItemPlaced;
    public System.Action<GameObject, GameObject> ItemPlacedWithInstance;
    public bool IsMovingPlacedObject => isMovingPlacedObject;
    public bool HasPlacementSelection => GetSelectedPrefab() != null;

    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    private Vector3Int hoveredCell;
    private Quaternion currentRotation = Quaternion.identity;
    private readonly List<LineRenderer> runtimeLines = new List<LineRenderer>();
    private GameObject ghostObject;
    private GameObject ghostPrefab;
    private MaterialPropertyBlock ghostBlock;
    private GameObject selectedPrefabOverride;
    private Transform ghostParent;
    private Vector2Int selectedFootprint = Vector2Int.one;
    private Vector3 selectedPlacementOffset = Vector3.zero;
    private Vector2Int selectedOccupancyCellOffset = Vector2Int.zero;
    private bool hasGhostPreviewCells;
    private Vector3Int ghostPreviewCell;
    private Vector2Int ghostPreviewFootprint = Vector2Int.one;
    private bool ghostPreviewCanPlace;
    private Transform outlinedPlacedRoot;
    private readonly List<LineRenderer> placedOutlineLines = new List<LineRenderer>();
    private GameObject placedOutlineRoot;
    private readonly List<GameObject> occupiedCellVisuals = new List<GameObject>();
    private GameObject occupiedCellVisualRoot;
    private Material occupiedCellVisualMaterial;
    private bool isMovingPlacedObject;
    private GameObject movingPlacedObject;
    private Vector3Int movingOriginalCell;
    private Vector2Int movingOriginalFootprint = Vector2Int.one;
    private Vector3 movingOriginalPosition;
    private Quaternion movingOriginalRotation = Quaternion.identity;
    private Transform movingOriginalParent;
    private int ignorePlacementClickUntilFrame;

    private void Update()
    {
        if (UseInputToggle && Input.GetKeyDown(ToggleInventoryKey))
        {
            PlacementModeActive = !PlacementModeActive;
        }

        if (!PlacementModeActive)
        {
            if (isMovingPlacedObject)
            {
                CancelMovePlacedObject();
            }

            SetGridVisible(false);
            SetGhostVisible(false);
            hasGhostPreviewCells = false;
            ClearPlacedObjectOutline();
            SetOccupiedCellVisualsVisible(false);
            return;
        }

        UpdatePlacedObjectOutline();

        EnsureGridLines();
        UpdateGridLinePositions();
        SetGridVisible(true);
        UpdateOccupiedCellVisuals();

        if (RotateWithQAndE)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                currentRotation *= Quaternion.Euler(0f, -90f, 0f);
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                currentRotation *= Quaternion.Euler(0f, 90f, 0f);
            }
        }

        if (RotateWithR && Input.GetKeyDown(KeyCode.R))
        {
            currentRotation *= Quaternion.Euler(0f, RRotationStep, 0f);
        }

        if (isMovingPlacedObject && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
        {
            CancelMovePlacedObject();
            return;
        }

        var hasPlacementSelection = GetSelectedPrefab() != null;

        if (TryGetHoveredCell(out hoveredCell))
        {
            UpdateGhost(hoveredCell);
            if (Input.GetMouseButtonDown(0))
            {
                if (Time.frameCount <= ignorePlacementClickUntilFrame)
                {
                    return;
                }

                if (!hasPlacementSelection && TryGetClickedPlacedObject(out var _))
                {
                    return;
                }

                if (!hasPlacementSelection)
                {
                    return;
                }

                if (hasPlacementSelection)
                {
                    TryPlaceAtCell(hoveredCell);
                }
            }
        }
        else
        {
            SetGhostVisible(false);
            hasGhostPreviewCells = false;
        }
    }

    private bool TryGetHoveredCell(out Vector3Int cell)
    {
        cell = default;
        var ray = Camera.main == null ? default : Camera.main.ScreenPointToRay(Input.mousePosition);
        if (ray.direction == Vector3.zero)
        {
            return false;
        }

        if (!Physics.Raycast(ray, out var hit, 500f, PlacementSurfaceMask))
        {
            return false;
        }

        var origin = GetGridOrigin();
        var toHit = hit.point - origin;
        var cellX = Mathf.FloorToInt(Vector3.Dot(toHit, transform.right) / CellSize);
        var cellZ = Mathf.FloorToInt(Vector3.Dot(toHit, transform.forward) / CellSize);

        if (cellX < 0 || cellZ < 0 || cellX >= GridSize.x || cellZ >= GridSize.y)
        {
            return false;
        }

        cell = new Vector3Int(cellX, 0, cellZ);
        return true;
    }

    private void UpdatePlacedObjectOutline()
    {
        if (!ShowPlacedObjectOutline || Camera.main == null)
        {
            ClearPlacedObjectOutline();
            return;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f))
        {
            ClearPlacedObjectOutline();
            return;
        }

        var targetRoot = GetPlacedObjectRoot(hit.transform);
        if (targetRoot == null)
        {
            ClearPlacedObjectOutline();
            return;
        }

        if (!TryGetRenderBounds(targetRoot, out var bounds))
        {
            ClearPlacedObjectOutline();
            return;
        }

        outlinedPlacedRoot = targetRoot;
        EnsurePlacedOutlineLines();
        UpdatePlacedOutlineLines(bounds);
    }

    private Transform GetPlacedObjectRoot(Transform hovered)
    {
        if (hovered == null)
        {
            return null;
        }

        if (PlacedParent == null)
        {
            if (hovered == transform)
            {
                return null;
            }

            var owningGrid = hovered.GetComponentInParent<GridSystem>();
            if (owningGrid == this)
            {
                return null;
            }

            return hovered.root;
        }

        var current = hovered;
        while (current != null)
        {
            if (current.parent == PlacedParent)
            {
                return current;
            }

            if (current == PlacedParent)
            {
                return null;
            }

            current = current.parent;
        }

        return null;
    }

    private bool TryGetRenderBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!found)
            {
                bounds = renderers[i].bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return found;
    }

    private void EnsurePlacedOutlineLines()
    {
        if (placedOutlineLines.Count == 12)
        {
            for (int i = 0; i < placedOutlineLines.Count; i++)
            {
                placedOutlineLines[i].enabled = true;
                placedOutlineLines[i].startColor = PlacedObjectOutlineColor;
                placedOutlineLines[i].endColor = PlacedObjectOutlineColor;
                placedOutlineLines[i].startWidth = PlacedObjectOutlineWidth;
                placedOutlineLines[i].endWidth = PlacedObjectOutlineWidth;
            }
            return;
        }

        if (placedOutlineRoot == null)
        {
            placedOutlineRoot = new GameObject("PlacedHoverOutline");
            placedOutlineRoot.transform.SetParent(transform, worldPositionStays: true);
        }

        var lineMaterial = GridMaterial != null ? GridMaterial : new Material(Shader.Find("Sprites/Default"));
        while (placedOutlineLines.Count < 12)
        {
            var lineObj = new GameObject("OutlineLine");
            lineObj.transform.SetParent(placedOutlineRoot.transform, worldPositionStays: true);
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.material = lineMaterial;
            lr.startColor = PlacedObjectOutlineColor;
            lr.endColor = PlacedObjectOutlineColor;
            lr.startWidth = PlacedObjectOutlineWidth;
            lr.endWidth = PlacedObjectOutlineWidth;
            placedOutlineLines.Add(lr);
        }
    }

    private void UpdatePlacedOutlineLines(Bounds bounds)
    {
        if (placedOutlineLines.Count < 12)
        {
            return;
        }

        var min = bounds.min;
        var max = bounds.max;
        var p000 = new Vector3(min.x, min.y, min.z);
        var p001 = new Vector3(min.x, min.y, max.z);
        var p010 = new Vector3(min.x, max.y, min.z);
        var p011 = new Vector3(min.x, max.y, max.z);
        var p100 = new Vector3(max.x, min.y, min.z);
        var p101 = new Vector3(max.x, min.y, max.z);
        var p110 = new Vector3(max.x, max.y, min.z);
        var p111 = new Vector3(max.x, max.y, max.z);

        SetLine(0, p000, p001);
        SetLine(1, p001, p101);
        SetLine(2, p101, p100);
        SetLine(3, p100, p000);

        SetLine(4, p010, p011);
        SetLine(5, p011, p111);
        SetLine(6, p111, p110);
        SetLine(7, p110, p010);

        SetLine(8, p000, p010);
        SetLine(9, p001, p011);
        SetLine(10, p101, p111);
        SetLine(11, p100, p110);
    }

    private void SetLine(int index, Vector3 start, Vector3 end)
    {
        if (index < 0 || index >= placedOutlineLines.Count || placedOutlineLines[index] == null)
        {
            return;
        }

        placedOutlineLines[index].enabled = true;
        placedOutlineLines[index].SetPosition(0, start);
        placedOutlineLines[index].SetPosition(1, end);
    }

    private void ClearPlacedObjectOutline()
    {
        outlinedPlacedRoot = null;
        for (int i = 0; i < placedOutlineLines.Count; i++)
        {
            if (placedOutlineLines[i] != null)
            {
                placedOutlineLines[i].enabled = false;
            }
        }
    }

    private void TryPlaceAtCell(Vector3Int cell)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null)
        {
            return;
        }

        var effectiveFootprint = GetRotatedFootprint(selectedFootprint);
        var centerAnchorOffset = GetFootprintCenterAnchorOffset(effectiveFootprint);

        GetPlacementOffsets(out var cellOffset, out var worldRemainderOffset);
        var placementCell = new Vector3Int(
            cell.x + cellOffset.x - centerAnchorOffset.x,
            0,
            cell.z + cellOffset.z - centerAnchorOffset.z);

        var ignoreOccupiedCells = ShouldIgnoreOccupiedCellsForPrefab(prefab);

        if (!CanPlaceFootprint(placementCell, effectiveFootprint, ignoreOccupiedCells))
        {
            return;
        }

        var footprintVisualOffset = GetFootprintVisualOffset(effectiveFootprint);
        var worldPos = GetCellWorldPosition(placementCell) + footprintVisualOffset + worldRemainderOffset;
        var worldRot = currentRotation;
        if (!ApplySpecialPlacementPose(prefab, ref worldPos, ref worldRot))
        {
            return;
        }

        GameObject instance;
        if (isMovingPlacedObject && movingPlacedObject != null)
        {
            instance = movingPlacedObject;
            var parent = PlacedParent != null ? PlacedParent : movingOriginalParent;
            if (parent != null)
            {
                instance.transform.SetParent(parent, worldPositionStays: true);
            }

            instance.transform.position = worldPos;
            instance.transform.rotation = worldRot;
            instance.SetActive(true);
        }
        else
        {
            instance = Instantiate(prefab, worldPos, worldRot);
            if (PlacedParent != null)
            {
                instance.transform.SetParent(PlacedParent, worldPositionStays: true);
            }

            ItemPlaced?.Invoke(prefab);
            ItemPlacedWithInstance?.Invoke(prefab, instance);
        }

        MarkFootprintOccupied(placementCell, effectiveFootprint);
        RegisterPlacedObject(instance, prefab, placementCell, effectiveFootprint, selectedFootprint, selectedPlacementOffset);

        if (isMovingPlacedObject)
        {
            ClearMoveState();
            ClearSelection();
        }
    }

    public bool TryGetPlacedObjectData(GameObject placedObject, out PlaceableObjectData data)
    {
        return TryGetPlaceableInstanceData(placedObject, out data);
    }

    public GameObject GetPlacedObjectRootFromHit(Transform hovered)
    {
        var root = GetPlacedObjectRoot(hovered);
        return root != null ? root.gameObject : null;
    }

    public bool BeginMovePlacedObject(GameObject placedObject)
    {
        if (!TryGetPlaceableInstanceData(placedObject, out var data))
        {
            return false;
        }

        if (isMovingPlacedObject)
        {
            CancelMovePlacedObject();
        }

        movingPlacedObject = data.gameObject;
        movingOriginalCell = data.OccupiedCell;
        movingOriginalFootprint = NormalizeFootprint(data.OccupiedFootprint);
        movingOriginalPosition = movingPlacedObject.transform.position;
        movingOriginalRotation = movingPlacedObject.transform.rotation;
        movingOriginalParent = movingPlacedObject.transform.parent;

        UnmarkFootprintOccupied(movingOriginalCell, movingOriginalFootprint);
        movingPlacedObject.SetActive(false);

        isMovingPlacedObject = true;
        ignorePlacementClickUntilFrame = Time.frameCount + 1;
        currentRotation = Quaternion.Euler(0f, Mathf.Round(movingOriginalRotation.eulerAngles.y / 90f) * 90f, 0f);
        selectedPrefabOverride = data.SourcePrefab != null ? data.SourcePrefab : movingPlacedObject;
        selectedFootprint = NormalizeFootprint(data.BaseFootprint);
        selectedPlacementOffset = data.PlacementOffset;
        selectedOccupancyCellOffset = data.OccupancyCellOffset;
        PlacementModeActive = true;
        RefreshGhost();
        return true;
    }

    public void CancelMovePlacedObject()
    {
        if (!isMovingPlacedObject)
        {
            return;
        }

        if (movingPlacedObject != null)
        {
            if (movingOriginalParent != null)
            {
                movingPlacedObject.transform.SetParent(movingOriginalParent, worldPositionStays: true);
            }

            movingPlacedObject.transform.position = movingOriginalPosition;
            movingPlacedObject.transform.rotation = movingOriginalRotation;
            movingPlacedObject.SetActive(true);
            MarkFootprintOccupied(movingOriginalCell, movingOriginalFootprint);
        }

        ClearMoveState();
        ClearSelection();
    }

    public bool RemovePlacedObject(GameObject placedObject)
    {
        if (!TryGetPlaceableInstanceData(placedObject, out var data))
        {
            return false;
        }

        if (isMovingPlacedObject && movingPlacedObject == data.gameObject)
        {
            ClearMoveState();
        }

        UnmarkFootprintOccupied(data.OccupiedCell, data.OccupiedFootprint);
        Destroy(data.gameObject);
        return true;
    }

    public void SelectItem(int index)
    {
        SelectedIndex = index;
        selectedPrefabOverride = null;
        selectedFootprint = Vector2Int.one;
        selectedPlacementOffset = Vector3.zero;
        selectedOccupancyCellOffset = Vector2Int.zero;
        RefreshGhost();
    }

    public void ClearSelection()
    {
        SelectedIndex = -1;
        selectedPrefabOverride = null;
        selectedFootprint = Vector2Int.one;
        selectedPlacementOffset = Vector3.zero;
        selectedOccupancyCellOffset = Vector2Int.zero;
        DestroyGhost();
    }

    public void SelectPrefab(GameObject prefab)
    {
        selectedPrefabOverride = prefab;
        selectedFootprint = Vector2Int.one;
        selectedPlacementOffset = Vector3.zero;
        selectedOccupancyCellOffset = Vector2Int.zero;
        RefreshGhost();
    }

    public void SelectItem(int index, Vector2Int footprint)
    {
        SelectedIndex = index;
        selectedPrefabOverride = null;
        selectedFootprint = NormalizeFootprint(footprint);
        selectedPlacementOffset = Vector3.zero;
        selectedOccupancyCellOffset = Vector2Int.zero;
        RefreshGhost();
    }

    public void SelectPrefab(GameObject prefab, Vector2Int footprint)
    {
        selectedPrefabOverride = prefab;
        selectedFootprint = NormalizeFootprint(footprint);
        selectedPlacementOffset = Vector3.zero;
        selectedOccupancyCellOffset = Vector2Int.zero;
        RefreshGhost();
    }

    public void SelectItem(int index, Vector2Int footprint, Vector3 placementOffset)
    {
        SelectedIndex = index;
        selectedPrefabOverride = null;
        selectedFootprint = NormalizeFootprint(footprint);
        selectedPlacementOffset = placementOffset;
        selectedOccupancyCellOffset = Vector2Int.zero;
        RefreshGhost();
    }

    public void SelectPrefab(GameObject prefab, Vector2Int footprint, Vector3 placementOffset)
    {
        selectedPrefabOverride = prefab;
        selectedFootprint = NormalizeFootprint(footprint);
        selectedPlacementOffset = placementOffset;
        selectedOccupancyCellOffset = Vector2Int.zero;
        RefreshGhost();
    }

    public void SelectItem(int index, Vector2Int footprint, Vector3 placementOffset, Vector2Int occupancyCellOffset)
    {
        SelectedIndex = index;
        selectedPrefabOverride = null;
        selectedFootprint = NormalizeFootprint(footprint);
        selectedPlacementOffset = placementOffset;
        selectedOccupancyCellOffset = occupancyCellOffset;
        RefreshGhost();
    }

    public void SelectPrefab(GameObject prefab, Vector2Int footprint, Vector3 placementOffset, Vector2Int occupancyCellOffset)
    {
        selectedPrefabOverride = prefab;
        selectedFootprint = NormalizeFootprint(footprint);
        selectedPlacementOffset = placementOffset;
        selectedOccupancyCellOffset = occupancyCellOffset;
        RefreshGhost();
    }

    private GameObject GetSelectedPrefab()
    {
        if (selectedPrefabOverride != null)
        {
            return selectedPrefabOverride;
        }

        if (PlaceablePrefabs != null && PlaceablePrefabs.Count > 0)
        {
            if (SelectedIndex < 0)
            {
                return null;
            }

            if (SelectedIndex >= PlaceablePrefabs.Count)
            {
                SelectedIndex = Mathf.Clamp(SelectedIndex, 0, PlaceablePrefabs.Count - 1);
            }
            return PlaceablePrefabs[SelectedIndex];
        }

        return ObjectToPlace;
    }

    private Vector3 GetCellWorldPosition(Vector3Int cell)
    {
        var origin = GetGridOrigin();
        var right = transform.right;
        var forward = transform.forward;
        return origin
            + right * (cell.x * CellSize + (CellSize * 0.5f))
            + forward * (cell.z * CellSize + (CellSize * 0.5f))
            + transform.up * GridHeightOffset;
    }

    private void OnDrawGizmos()
    {
        if (!PlacementModeActive)
        {
            return;
        }

        Gizmos.color = GridColor;
        var origin = GetGridOrigin();
        var right = transform.right;
        var forward = transform.forward;

        for (int x = 0; x <= GridSize.x; x++)
        {
            var start = origin + right * (x * CellSize);
            var end = start + forward * (GridSize.y * CellSize);
            Gizmos.DrawLine(start, end);
        }

        for (int z = 0; z <= GridSize.y; z++)
        {
            var start = origin + forward * (z * CellSize);
            var end = start + right * (GridSize.x * CellSize);
            Gizmos.DrawLine(start, end);
        }

        if (Application.isPlaying)
        {
            DrawDebugCellOverlay();

            var cellPos = GetCellWorldPosition(hoveredCell);
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(cellPos, new Vector3(CellSize, 0.02f, CellSize));
        }
    }

    private void DrawDebugCellOverlay()
    {
        if (!ShowDebugCellOverlay)
        {
            return;
        }

        var ySize = Mathf.Max(0.01f, GridLineWidth);
        var cellVisualSize = new Vector3(CellSize * 0.9f, ySize, CellSize * 0.9f);

        if (!hasGhostPreviewCells)
        {
            return;
        }

        for (int x = 0; x < ghostPreviewFootprint.x; x++)
        {
            for (int z = 0; z < ghostPreviewFootprint.y; z++)
            {
                var c = new Vector3Int(ghostPreviewCell.x + x, 0, ghostPreviewCell.z + z);
                var inBounds = c.x >= 0 && c.z >= 0 && c.x < GridSize.x && c.z < GridSize.y;
                var blocked = !inBounds || occupiedCells.Contains(c);
                Gizmos.color = blocked ? GhostCellBlockedColor : GhostCellColor;
                Gizmos.DrawCube(GetCellWorldPosition(c), cellVisualSize);
            }
        }
    }

    private void UpdateOccupiedCellVisuals()
    {
        if (!ShowDebugCellOverlay || !ShowOccupiedCellsInGameView)
        {
            SetOccupiedCellVisualsVisible(false);
            return;
        }

        EnsureOccupiedCellVisualRoot();
        EnsureOccupiedCellVisualMaterial();
        SetOccupiedCellVisualsVisible(true);

        var needed = occupiedCells.Count;
        while (occupiedCellVisuals.Count < needed)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "OccupiedCellVisual";
            quad.transform.SetParent(occupiedCellVisualRoot.transform, worldPositionStays: true);

            var col = quad.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            var renderer = quad.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = occupiedCellVisualMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            occupiedCellVisuals.Add(quad);
        }

        var rotation = Quaternion.LookRotation(transform.up, transform.forward);
        var scale = new Vector3(CellSize * 0.9f, CellSize * 0.9f, 1f);
        var index = 0;
        foreach (var cell in occupiedCells)
        {
            var visual = occupiedCellVisuals[index++];
            visual.SetActive(true);
            visual.transform.position = GetCellWorldPosition(cell) + transform.up * 0.005f;
            visual.transform.rotation = rotation;
            visual.transform.localScale = scale;

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = OccupiedCellColor;
            }
        }

        for (int i = index; i < occupiedCellVisuals.Count; i++)
        {
            occupiedCellVisuals[i].SetActive(false);
        }
    }

    private void EnsureOccupiedCellVisualRoot()
    {
        if (occupiedCellVisualRoot != null)
        {
            return;
        }

        occupiedCellVisualRoot = new GameObject("OccupiedCellOverlay");
        occupiedCellVisualRoot.transform.SetParent(transform, worldPositionStays: true);
    }

    private void EnsureOccupiedCellVisualMaterial()
    {
        if (occupiedCellVisualMaterial != null)
        {
            return;
        }

        occupiedCellVisualMaterial = new Material(Shader.Find("Sprites/Default"));
        occupiedCellVisualMaterial.color = OccupiedCellColor;
    }

    private void SetOccupiedCellVisualsVisible(bool visible)
    {
        if (occupiedCellVisualRoot != null)
        {
            occupiedCellVisualRoot.SetActive(visible);
        }
    }

    private void DrawRuntimeGrid()
    {
        EnsureGridLines();
        SetGridVisible(true);
    }

    private void EnsureGridLines()
    {
        if (runtimeLines.Count > 0)
        {
            UpdateLineColors();
            UpdateGridLinePositions();
            return;
        }

        var lineCount = (GridSize.x + 1) + (GridSize.y + 1);
        for (int i = 0; i < lineCount; i++)
        {
            runtimeLines.Add(CreateLineRenderer(Vector3.zero, Vector3.zero));
        }

        UpdateGridLinePositions();
    }

    private void UpdateGridLinePositions()
    {
        var expectedCount = (GridSize.x + 1) + (GridSize.y + 1);
        if (runtimeLines.Count != expectedCount)
        {
            for (int i = runtimeLines.Count - 1; i >= 0; i--)
            {
                if (runtimeLines[i] != null)
                {
                    Destroy(runtimeLines[i].gameObject);
                }
            }
            runtimeLines.Clear();
            EnsureGridLines();
            return;
        }

        var origin = GetGridOrigin();
        var right = transform.right;
        var forward = transform.forward;
        var heightOffset = transform.up * GridHeightOffset;

        var index = 0;
        for (int x = 0; x <= GridSize.x; x++)
        {
            var start = origin + right * (x * CellSize) + heightOffset;
            var end = start + forward * (GridSize.y * CellSize);
            SetLinePositions(runtimeLines[index++], start, end);
        }

        for (int z = 0; z <= GridSize.y; z++)
        {
            var start = origin + forward * (z * CellSize) + heightOffset;
            var end = start + right * (GridSize.x * CellSize);
            SetLinePositions(runtimeLines[index++], start, end);
        }
    }

    private LineRenderer CreateLineRenderer(Vector3 start, Vector3 end)
    {
        var lineObj = new GameObject("GridLine");
        lineObj.transform.SetParent(transform, worldPositionStays: true);
        var lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = GridLineWidth;
        lr.endWidth = GridLineWidth;
        lr.useWorldSpace = true;
        if (GridMaterial != null)
        {
            lr.material = GridMaterial;
        }
        lr.startColor = GridColor;
        lr.endColor = GridColor;
        return lr;
    }

    private void SetLinePositions(LineRenderer lr, Vector3 start, Vector3 end)
    {
        if (lr == null)
        {
            return;
        }
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }

    private void SetGridVisible(bool isVisible)
    {
        if (runtimeLines.Count == 0)
        {
            return;
        }

        for (int i = 0; i < runtimeLines.Count; i++)
        {
            if (runtimeLines[i] != null)
            {
                runtimeLines[i].enabled = isVisible;
            }
        }
    }

    private void RefreshGhost()
    {
        if (!ShowGhost)
        {
            SetGhostVisible(false);
            return;
        }

        var prefab = GetSelectedPrefab();
        if (prefab == null)
        {
            DestroyGhost();
            return;
        }

        if (ghostObject == null || ghostPrefab != prefab)
        {
            DestroyGhost();
            ghostPrefab = prefab;
            EnsureGhostParent();
            ghostObject = Instantiate(prefab);
            ghostObject.name = prefab.name + "_Ghost";
            ghostObject.transform.SetParent(ghostParent, worldPositionStays: true);
            ghostObject.transform.localScale = prefab.transform.localScale;
            DisableColliders(ghostObject);
            DisableGhostBehaviours(ghostObject);
            ApplyGhostVisuals(ghostObject);
        }
    }

    private void UpdateGhost(Vector3Int cell)
    {
        if (!ShowGhost)
        {
            return;
        }

        RefreshGhost();
        if (ghostObject == null)
        {
            return;
        }

        var effectiveFootprint = GetRotatedFootprint(selectedFootprint);
        var centerAnchorOffset = GetFootprintCenterAnchorOffset(effectiveFootprint);

        GetPlacementOffsets(out var cellOffset, out var worldRemainderOffset);
        var placementCell = new Vector3Int(
            cell.x + cellOffset.x - centerAnchorOffset.x,
            0,
            cell.z + cellOffset.z - centerAnchorOffset.z);
        placementCell += GetRotatedCellOffset(selectedOccupancyCellOffset);
        placementCell += GetRotatedCellOffset(selectedOccupancyCellOffset);

        var footprintVisualOffset = GetFootprintVisualOffset(effectiveFootprint);
        var ignoreOccupiedCells = ShouldIgnoreOccupiedCellsForPrefab(GetSelectedPrefab());
        var canPlace = CanPlaceFootprint(placementCell, effectiveFootprint, ignoreOccupiedCells);

        ghostPreviewCell = placementCell;
        ghostPreviewFootprint = effectiveFootprint;
        ghostPreviewCanPlace = canPlace;
        hasGhostPreviewCells = true;

        if (!canPlace)
        {
            SetGhostVisible(false);
            return;
        }

        var ghostPosition = GetCellWorldPosition(placementCell) + footprintVisualOffset + worldRemainderOffset;
        var ghostRotation = currentRotation;
        if (!ApplySpecialPlacementPose(GetSelectedPrefab(), ref ghostPosition, ref ghostRotation))
        {
            SetGhostVisible(false);
            ghostPreviewCanPlace = false;
            return;
        }
        ghostObject.transform.position = ghostPosition;
        ghostObject.transform.rotation = ghostRotation;
        SetGhostVisible(true);
        ApplyGhostColor(GhostValidColor);
    }

    private bool ApplySpecialPlacementPose(GameObject prefab, ref Vector3 position, ref Quaternion rotation)
    {
        if (prefab == null)
        {
            return false;
        }

        var upgrader = prefab.GetComponent<ValueUpgrader>();
        if (upgrader == null)
        {
            upgrader = prefab.GetComponentInChildren<ValueUpgrader>(true);
        }

        if (upgrader == null)
        {
            return true;
        }

        if (upgrader.TryGetSnapPose(position, out var snappedPosition, out var snappedRotation))
        {
            position = snappedPosition;
            rotation = snappedRotation;
            return true;
        }

        return false;
    }

    private Vector3 GetPlacementOffsetWorld()
    {
        return currentRotation * selectedPlacementOffset;
    }

    private void GetPlacementOffsets(out Vector3Int cellOffset, out Vector3 worldRemainderOffset)
    {
        var worldOffset = GetPlacementOffsetWorld();

        var rightCells = Vector3.Dot(worldOffset, transform.right) / CellSize;
        var forwardCells = Vector3.Dot(worldOffset, transform.forward) / CellSize;

        var cellX = Mathf.RoundToInt(rightCells);
        var cellZ = Mathf.RoundToInt(forwardCells);
        cellOffset = new Vector3Int(cellX, 0, cellZ);

        worldRemainderOffset = worldOffset
            - transform.right * (cellX * CellSize)
            - transform.forward * (cellZ * CellSize);
    }

    private Vector3 GetFootprintVisualOffset(Vector2Int footprint)
    {
        var size = NormalizeFootprint(footprint);
        var rightOffset = (size.x - 1) * 0.5f * CellSize;
        var forwardOffset = (size.y - 1) * 0.5f * CellSize;
        return transform.right * rightOffset + transform.forward * forwardOffset;
    }

    private Vector3Int GetFootprintCenterAnchorOffset(Vector2Int footprint)
    {
        var size = NormalizeFootprint(footprint);
        return new Vector3Int((size.x - 1) / 2, 0, (size.y - 1) / 2);
    }

    private void SetGhostVisible(bool isVisible)
    {
        if (ghostObject != null)
        {
            ghostObject.SetActive(isVisible);
        }
    }

    private void DestroyGhost()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
        ghostPrefab = null;
    }

    private void EnsureGhostParent()
    {
        if (ghostParent != null)
        {
            return;
        }

        var parentObj = new GameObject("GhostRoot");
        ghostParent = parentObj.transform;
        ghostParent.SetParent(transform, worldPositionStays: true);

        var gridScale = transform.lossyScale;
        ghostParent.localScale = new Vector3(
            gridScale.x != 0f ? 1f / gridScale.x : 1f,
            gridScale.y != 0f ? 1f / gridScale.y : 1f,
            gridScale.z != 0f ? 1f / gridScale.z : 1f);
    }

    private void DisableColliders(GameObject root)
    {
        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void DisableGhostBehaviours(GameObject root)
    {
        var droppers = root.GetComponentsInChildren<Dropper>(true);
        for (int i = 0; i < droppers.Length; i++)
        {
            droppers[i].enabled = false;
        }
    }

    private void ApplyGhostVisuals(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var overrideMaterial = GetGhostMaterialOverride(root);
        for (int i = 0; i < renderers.Length; i++)
        {
            var materialToUse = overrideMaterial != null ? overrideMaterial : GhostMaterial;
            if (materialToUse != null)
            {
                renderers[i].material = materialToUse;
            }
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }
        ApplyGhostColor(GhostValidColor);
    }

    private Material GetGhostMaterialOverride(GameObject root)
    {
        var droppers = root.GetComponentsInChildren<Dropper>(true);
        for (int i = 0; i < droppers.Length; i++)
        {
            if (droppers[i].GhostPreviewMaterial != null)
            {
                return droppers[i].GhostPreviewMaterial;
            }
        }
        return null;
    }

    private void ApplyGhostColor(Color color)
    {
        if (ghostBlock == null)
        {
            ghostBlock = new MaterialPropertyBlock();
        }

        var renderers = ghostObject != null
            ? ghostObject.GetComponentsInChildren<Renderer>(true)
            : null;
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i].sharedMaterial;
            if (mat != null && mat.HasProperty("_Color"))
            {
                renderers[i].GetPropertyBlock(ghostBlock);
                var tinted = new Color(color.r, color.g, color.b, color.a * GhostAlpha);
                ghostBlock.SetColor("_Color", tinted);
                renderers[i].SetPropertyBlock(ghostBlock);
            }
        }
    }

    private bool CanPlaceFootprint(Vector3Int cell, Vector2Int footprint, bool ignoreOccupiedCells = false)
    {
        var size = NormalizeFootprint(footprint);
        if (!IsFootprintWithinBounds(cell, size))
        {
            return false;
        }

        if (ignoreOccupiedCells)
        {
            return true;
        }

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                var c = new Vector3Int(cell.x + x, 0, cell.z + z);
                if (occupiedCells.Contains(c))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool ShouldIgnoreOccupiedCellsForPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        return prefab.GetComponent<ValueUpgrader>() != null
            || prefab.GetComponentInChildren<ValueUpgrader>(true) != null;
    }

    private void MarkFootprintOccupied(Vector3Int cell, Vector2Int footprint)
    {
        var size = NormalizeFootprint(footprint);
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                occupiedCells.Add(new Vector3Int(cell.x + x, 0, cell.z + z));
            }
        }
    }

    private void UnmarkFootprintOccupied(Vector3Int cell, Vector2Int footprint)
    {
        var size = NormalizeFootprint(footprint);
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                occupiedCells.Remove(new Vector3Int(cell.x + x, 0, cell.z + z));
            }
        }
    }

    private bool TryGetClickedPlacedObject(out GameObject placedObject)
    {
        placedObject = null;
        if (Camera.main == null)
        {
            return false;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 600f);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var root = GetPlacedObjectRoot(hits[i].transform);
            if (root == null)
            {
                continue;
            }

            placedObject = root.gameObject;
            return true;
        }

        return false;
    }

    private void RegisterPlacedObject(
        GameObject placedObject,
        GameObject sourcePrefab,
        Vector3Int occupiedCell,
        Vector2Int occupiedFootprint,
        Vector2Int baseFootprint,
        Vector3 placementOffset)
    {
        if (placedObject == null)
        {
            return;
        }

        var data = placedObject.GetComponent<PlaceableObjectData>();
        if (data == null)
        {
            data = placedObject.AddComponent<PlaceableObjectData>();
        }

        data.SourcePrefab = sourcePrefab;
        data.BaseFootprint = NormalizeFootprint(baseFootprint);
        data.PlacementOffset = placementOffset;
        data.OccupancyCellOffset = selectedOccupancyCellOffset;
        data.DisplayName = sourcePrefab != null ? sourcePrefab.name : placedObject.name;
        data.SetPlacementData(occupiedCell, NormalizeFootprint(occupiedFootprint));
    }

    private bool TryGetPlaceableInstanceData(GameObject placedObject, out PlaceableObjectData data)
    {
        data = null;
        if (placedObject == null)
        {
            return false;
        }

        data = placedObject.GetComponent<PlaceableObjectData>();
        if (data == null)
        {
            data = placedObject.GetComponentInParent<PlaceableObjectData>();
        }

        if (data == null)
        {
            data = placedObject.AddComponent<PlaceableObjectData>();
            var guessedPrefab = GuessSourcePrefab(placedObject);
            var guessedCell = GetNearestCellFromWorldPosition(placedObject.transform.position);
            data.SourcePrefab = guessedPrefab;
            data.DisplayName = guessedPrefab != null ? guessedPrefab.name : placedObject.name;
            data.BaseFootprint = Vector2Int.one;
            data.PlacementOffset = Vector3.zero;
            data.SetPlacementData(guessedCell, Vector2Int.one);
            RegisterPlacedObject(
                placedObject,
                data.SourcePrefab,
                data.OccupiedCell,
                data.OccupiedFootprint,
                data.BaseFootprint,
                data.PlacementOffset);
        }

        if (PlacedParent != null && !data.transform.IsChildOf(PlacedParent))
        {
            return false;
        }

        return true;
    }

    private GameObject GuessSourcePrefab(GameObject placedObject)
    {
        if (placedObject == null)
        {
            return null;
        }

        var candidateName = placedObject.name.Replace("(Clone)", "").Trim();
        if (PlaceablePrefabs != null)
        {
            for (int i = 0; i < PlaceablePrefabs.Count; i++)
            {
                var prefab = PlaceablePrefabs[i];
                if (prefab != null && prefab.name == candidateName)
                {
                    return prefab;
                }
            }
        }

        if (ObjectToPlace != null && ObjectToPlace.name == candidateName)
        {
            return ObjectToPlace;
        }

        return null;
    }

    private Vector3Int GetNearestCellFromWorldPosition(Vector3 worldPosition)
    {
        var origin = GetGridOrigin();
        var toWorld = worldPosition - origin;
        var x = Mathf.RoundToInt(Vector3.Dot(toWorld, transform.right) / CellSize - 0.5f);
        var z = Mathf.RoundToInt(Vector3.Dot(toWorld, transform.forward) / CellSize - 0.5f);
        x = Mathf.Clamp(x, 0, Mathf.Max(0, GridSize.x - 1));
        z = Mathf.Clamp(z, 0, Mathf.Max(0, GridSize.y - 1));
        return new Vector3Int(x, 0, z);
    }

    private void ClearMoveState()
    {
        isMovingPlacedObject = false;
        movingPlacedObject = null;
        movingOriginalCell = default;
        movingOriginalFootprint = Vector2Int.one;
        movingOriginalPosition = Vector3.zero;
        movingOriginalRotation = Quaternion.identity;
        movingOriginalParent = null;
    }

    private bool IsFootprintWithinBounds(Vector3Int cell, Vector2Int footprint)
    {
        return cell.x >= 0
            && cell.z >= 0
            && cell.x + footprint.x <= GridSize.x
            && cell.z + footprint.y <= GridSize.y;
    }

    private Vector2Int NormalizeFootprint(Vector2Int footprint)
    {
        return new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
    }

    private Vector2Int GetRotatedFootprint(Vector2Int footprint)
    {
        var size = NormalizeFootprint(footprint);
        var yawSteps = Mathf.RoundToInt(currentRotation.eulerAngles.y / 90f) % 4;
        if (yawSteps < 0)
        {
            yawSteps += 4;
        }

        if (yawSteps % 2 == 1)
        {
            return new Vector2Int(size.y, size.x);
        }

        return size;
    }

    private Vector3Int GetRotatedCellOffset(Vector2Int cellOffset)
    {
        var offsetX = cellOffset.x;
        var offsetZ = cellOffset.y;

        var yawSteps = Mathf.RoundToInt(currentRotation.eulerAngles.y / 90f) % 4;
        if (yawSteps < 0)
        {
            yawSteps += 4;
        }

        switch (yawSteps)
        {
            case 1:
                return new Vector3Int(offsetZ, 0, -offsetX);
            case 2:
                return new Vector3Int(-offsetX, 0, -offsetZ);
            case 3:
                return new Vector3Int(-offsetZ, 0, offsetX);
            default:
                return new Vector3Int(offsetX, 0, offsetZ);
        }
    }

    private void UpdateLineColors()
    {
        for (int i = 0; i < runtimeLines.Count; i++)
        {
            if (runtimeLines[i] != null)
            {
                runtimeLines[i].startColor = GridColor;
                runtimeLines[i].endColor = GridColor;
                runtimeLines[i].startWidth = GridLineWidth;
                runtimeLines[i].endWidth = GridLineWidth;
                if (GridMaterial != null)
                {
                    runtimeLines[i].material = GridMaterial;
                }
            }
        }
    }

    private Vector3 GetGridOrigin()
    {
        if (!AlignToSurfaceBounds)
        {
            return transform.position;
        }

        if (TryGetComponent<Collider>(out var col))
        {
            var bounds = col.bounds;
            var topCenter = bounds.center + transform.up * bounds.extents.y;
            return topCenter
                - transform.right * (GridSize.x * CellSize * 0.5f)
                - transform.forward * (GridSize.y * CellSize * 0.5f);
        }

        if (TryGetComponent<Renderer>(out var rend))
        {
            var bounds = rend.bounds;
            var topCenter = bounds.center + transform.up * bounds.extents.y;
            return topCenter
                - transform.right * (GridSize.x * CellSize * 0.5f)
                - transform.forward * (GridSize.y * CellSize * 0.5f);
        }

        return transform.position;
    }
}
