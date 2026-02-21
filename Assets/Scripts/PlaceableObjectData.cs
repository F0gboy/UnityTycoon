using UnityEngine;

public class PlaceableObjectData : MonoBehaviour
{
    public GameObject SourcePrefab;
    public string DisplayName;
    public Vector2Int BaseFootprint = Vector2Int.one;
    public Vector3 PlacementOffset = Vector3.zero;
    public Vector3Int OccupiedCell;
    public Vector2Int OccupiedFootprint = Vector2Int.one;

    public void SetPlacementData(Vector3Int cell, Vector2Int occupiedFootprint)
    {
        OccupiedCell = cell;
        OccupiedFootprint = new Vector2Int(Mathf.Max(1, occupiedFootprint.x), Mathf.Max(1, occupiedFootprint.y));
    }
}
