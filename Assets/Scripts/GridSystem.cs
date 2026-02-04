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

    [Header("Inventory / Placement Mode")]
    public KeyCode ToggleInventoryKey = KeyCode.F;
    public bool PlacementModeActive = false;
    public int SelectedIndex = 0;
    public bool UseInputToggle = true;

    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    private Vector3Int hoveredCell;
    private Quaternion currentRotation = Quaternion.identity;
    private readonly List<LineRenderer> runtimeLines = new List<LineRenderer>();

    private void Update()
    {
        if (UseInputToggle && Input.GetKeyDown(ToggleInventoryKey))
        {
            PlacementModeActive = !PlacementModeActive;
        }

        if (!PlacementModeActive)
        {
            SetGridVisible(false);
            return;
        }

        EnsureGridLines();
        UpdateGridLinePositions();
        SetGridVisible(true);

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

        if (!TryGetHoveredCell(out hoveredCell))
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceAtCell(hoveredCell);
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

    private void TryPlaceAtCell(Vector3Int cell)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null)
        {
            return;
        }

        if (occupiedCells.Contains(cell))
        {
            return;
        }

        var worldPos = GetCellWorldPosition(cell);
        Instantiate(prefab, worldPos, currentRotation, transform);
        occupiedCells.Add(cell);
    }

    public void SelectItem(int index)
    {
        SelectedIndex = index;
    }

    private GameObject GetSelectedPrefab()
    {
        if (PlaceablePrefabs != null && PlaceablePrefabs.Count > 0)
        {
            if (SelectedIndex < 0 || SelectedIndex >= PlaceablePrefabs.Count)
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
            var cellPos = GetCellWorldPosition(hoveredCell);
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(cellPos, new Vector3(CellSize, 0.02f, CellSize));
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
