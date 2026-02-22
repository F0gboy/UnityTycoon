using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConveyorBelt : MonoBehaviour
{
    [Header("Movement")]
    public float Speed = 2f;
    public Vector3 Direction = Vector3.forward;
    public bool UseLocalDirection = true;
    public bool OverrideHorizontalVelocity = true;
    public float HorizontalAcceleration = 25f;
    public float ExitImpulse = 0.75f;

    [Header("Collider")]
    public bool UseTrigger = false;

    [Header("Filtering")]
    public LayerMask AffectedLayers = ~0;
    public string RequiredTag = "";

    [Header("Upgrader Snap")]
    public Transform UpgraderSnapPoint;
    public float UpgraderSlotCheckRadius = 0.2f;

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

    private void OnTriggerExit(Collider other)
    {
        if (!UseTrigger)
        {
            return;
        }

        if (!IsAffected(other))
        {
            return;
        }

        ApplyExitImpulse(other.attachedRigidbody);
    }

    private void OnCollisionExit(Collision collision)
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

        ApplyExitImpulse(collision.rigidbody);
    }

    private void MoveTarget(Collider other, Rigidbody rb)
    {
        var beltVelocity = GetBeltVelocity();
        if (rb != null && !rb.isKinematic)
        {
            if (OverrideHorizontalVelocity)
            {
                var v = rb.linearVelocity;
                var current = new Vector3(v.x, 0f, v.z);
                var target = new Vector3(beltVelocity.x, 0f, beltVelocity.z);
                var next = Vector3.MoveTowards(current, target, HorizontalAcceleration * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector3(next.x, v.y, next.z);
            }
            else
            {
                rb.AddForce(beltVelocity, ForceMode.Acceleration);
            }
            return;
        }

        var nextPos = other.transform.position + beltVelocity * Time.fixedDeltaTime;
        other.transform.position = nextPos;
    }

    private void ApplyExitImpulse(Rigidbody rb)
    {
        if (rb == null || rb.isKinematic)
        {
            return;
        }

        if (ExitImpulse <= 0f)
        {
            return;
        }

        var dir = GetBeltVelocity();
        if (dir.sqrMagnitude < 0.001f)
        {
            return;
        }

        rb.AddForce(dir.normalized * ExitImpulse, ForceMode.VelocityChange);
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

    public bool IsUpgraderSlotFilled(ValueUpgrader requester = null)
    {
        if (UpgraderSnapPoint == null)
        {
            return false;
        }

        var hits = Physics.OverlapSphere(
            UpgraderSnapPoint.position,
            Mathf.Max(0.01f, UpgraderSlotCheckRadius));

        for (int i = 0; i < hits.Length; i++)
        {
            var upgrader = hits[i].GetComponentInParent<ValueUpgrader>();
            if (upgrader == null)
            {
                continue;
            }

            if (requester != null && upgrader == requester)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
