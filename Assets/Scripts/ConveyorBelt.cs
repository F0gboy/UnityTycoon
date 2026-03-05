using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConveyorBelt : MonoBehaviour
{
    [Header("Movement")]
    public float Speed = 2f;
    public Vector3 Direction = Vector3.forward;
    public bool UseLocalDirection = true;
    public bool OverrideHorizontalVelocity = true;

    [Header("Collider")]
    public bool UseTrigger = false;

    [Header("Filtering")]
    public LayerMask AffectedLayers = ~0;
    public string RequiredTag = "";

    [Header("Upgrader Slot")]
    public Transform UpgraderSnapPoint;
    public bool LimitToSingleUpgrader = true;
    public float UpgraderSlotRadius = 0.3f;

    private Collider beltTrigger;

    private void Awake()
    {
        beltTrigger = GetComponent<Collider>();
        if (beltTrigger != null)
        {
            beltTrigger.isTrigger = UseTrigger;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!UseTrigger)
        {
            return;
        }

        if (!IsAffected(other))
        {
            return;
        }

        MoveTarget(other, other.attachedRigidbody);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (UseTrigger)
        {
            return;
        }

        var other = collision.collider;
        if (!IsAffected(other))
        {
            return;
        }

        MoveTarget(other, collision.rigidbody);
    }

    private void MoveTarget(Collider other, Rigidbody rb)
    {
        var beltVelocity = GetBeltVelocity();
        if (rb != null && !rb.isKinematic)
        {
            if (OverrideHorizontalVelocity)
            {
                var v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(beltVelocity.x, v.y, beltVelocity.z);
            }
            else
            {
                rb.AddForce(beltVelocity, ForceMode.Acceleration);
            }
            return;
        }

        var target = other.transform.position + beltVelocity * Time.fixedDeltaTime;
        other.transform.position = target;
    }

    private Vector3 GetBeltVelocity()
    {
        var dir = Direction.sqrMagnitude > 0.001f ? Direction.normalized : Vector3.forward;
        if (UseLocalDirection)
        {
            dir = transform.TransformDirection(dir).normalized;
        }
        return dir * Speed;
    }

    private bool IsAffected(Collider other)
    {
        return (AffectedLayers.value & (1 << other.gameObject.layer)) != 0
            && (string.IsNullOrEmpty(RequiredTag) || other.CompareTag(RequiredTag));
    }

    public bool IsUpgraderSlotFilled(ValueUpgrader currentUpgrader = null)
    {
        if (!LimitToSingleUpgrader || UpgraderSnapPoint == null)
        {
            return false;
        }

        var radius = Mathf.Max(0.01f, UpgraderSlotRadius);
        var hits = Physics.OverlapSphere(UpgraderSnapPoint.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            var upgrader = hits[i].GetComponentInParent<ValueUpgrader>();
            if (upgrader == null || upgrader == currentUpgrader)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
