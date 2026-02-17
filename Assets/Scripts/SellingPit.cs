using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SellingPit : MonoBehaviour
{
    [Header("Sell Settings")]
    public int DefaultSellValue = 1;
    public LayerMask AffectedLayers = ~0;
    public string RequiredTag = "";
    public bool UseTrigger = true;

    private Collider pitCollider;

    private void Awake()
    {
        pitCollider = GetComponent<Collider>();
        if (pitCollider != null)
        {
            pitCollider.isTrigger = UseTrigger;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!UseTrigger)
        {
            return;
        }

        TrySell(other, other.attachedRigidbody);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (UseTrigger)
        {
            return;
        }

        TrySell(collision.collider, collision.rigidbody);
    }

    private void TrySell(Collider other, Rigidbody rb)
    {
        if (other == null || !IsAffected(other))
        {
            return;
        }

        var target = ResolveTarget(other, rb);
        if (target == null)
        {
            return;
        }

        var value = GetSellValue(target);
        if (value > 0 && InventoryUI.Instance != null)
        {
            InventoryUI.Instance.AddCoins(value);
        }

        Destroy(target);
    }

    private GameObject ResolveTarget(Collider other, Rigidbody rb)
    {
        if (rb != null)
        {
            return rb.gameObject;
        }

        return other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
    }

    private int GetSellValue(GameObject target)
    {
        var sellValue = target.GetComponentInChildren<SellValue>(true);
        if (sellValue != null)
        {
            return Mathf.Max(0, sellValue.Coins);
        }

        return Mathf.Max(0, DefaultSellValue);
    }

    private bool IsAffected(Collider other)
    {
        if ((AffectedLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(RequiredTag) && !other.CompareTag(RequiredTag))
        {
            return false;
        }

        return true;
    }
}
