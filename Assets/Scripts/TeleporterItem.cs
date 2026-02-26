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

    private void Awake()
    {
        if (Endpoint == null)
        {
            Endpoint = GetComponentInChildren<TeleporterEndpoint>(true);
        }
    }

    public TeleporterEndpoint GetEndpoint()
    {
        if (Endpoint == null)
        {
            Endpoint = GetComponentInChildren<TeleporterEndpoint>(true);
        }

        return Endpoint;
    }

    public GameObject GetInventoryOwnerPrefab(GameObject fallback)
    {
        if (InventoryOwnerPrefab != null)
        {
            return InventoryOwnerPrefab;
        }

        return fallback;
    }

    public GameObject GetTeleporterBPrefab()
    {
        return TeleporterBPrefab;
    }

    public Vector2Int GetTeleporterBFootprint()
    {
        return new Vector2Int(Mathf.Max(1, TeleporterBFootprint.x), Mathf.Max(1, TeleporterBFootprint.y));
    }
}
