using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ConveyorBelt))]
public class TeleporterItem : MonoBehaviour
{
    public enum TeleporterVariant
    {
        A,
        B
    }

    public string PairId = "TeleporterPair";
    public TeleporterVariant Variant = TeleporterVariant.A;
    public GameObject TeleporterBPrefab;
    public Vector2Int TeleporterBFootprint = new Vector2Int(3, 3);
    public GameObject InventoryOwnerPrefab;
    public TeleporterEndpoint Endpoint;

    private void Awake() => EnsureEndpoint();

    public TeleporterEndpoint GetEndpoint()
    {
        EnsureEndpoint();
        return Endpoint;
    }

    public GameObject GetInventoryOwnerPrefab(GameObject fallback) => InventoryOwnerPrefab != null ? InventoryOwnerPrefab : fallback;

    public GameObject GetTeleporterBPrefab() => TeleporterBPrefab;

    public Vector2Int GetTeleporterBFootprint() => new Vector2Int(Mathf.Max(1, TeleporterBFootprint.x), Mathf.Max(1, TeleporterBFootprint.y));

    private void EnsureEndpoint()
    {
        if (Endpoint == null)
        {
            Endpoint = GetComponentInChildren<TeleporterEndpoint>(true);
        }
    }
}
