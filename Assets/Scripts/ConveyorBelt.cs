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
                var v = rb.velocity;
                rb.velocity = new Vector3(beltVelocity.x, v.y, beltVelocity.z);
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
