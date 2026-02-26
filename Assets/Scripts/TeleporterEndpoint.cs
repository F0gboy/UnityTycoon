using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TeleporterEndpoint : MonoBehaviour
{
    [Header("Link")]
    public TeleporterEndpoint LinkedEndpoint;
    public Transform ExitPoint;

    [Header("Teleport")]
    public float ExitForwardVelocity = 2f;
    public float ExitVerticalVelocity = 0.25f;
    public float PerObjectCooldown = 0.3f;

    [Header("Filtering")]
    public LayerMask AffectedLayers = ~0;
    public string RequiredTag = "";
    public bool ForceTriggerCollider = true;

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && ForceTriggerCollider)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTeleport(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryTeleport(other);
    }

    private void OnTriggerExit(Collider other)
    {
        ReleaseEndpointLock(other);
    }

    public void LinkBidirectional(TeleporterEndpoint other)
    {
        LinkedEndpoint = other;
        if (other != null)
        {
            other.LinkedEndpoint = this;
        }
    }

    private void OnDestroy()
    {
        if (LinkedEndpoint != null && LinkedEndpoint.LinkedEndpoint == this)
        {
            LinkedEndpoint.LinkedEndpoint = null;
        }
    }

    private void TryTeleport(Collider other)
    {
        if (other == null || !IsAffected(other))
        {
            return;
        }

        if (LinkedEndpoint == null)
        {
            return;
        }

        var target = ResolveTarget(other);
        if (target == null)
        {
            return;
        }

        var cooldown = target.GetComponent<TeleporterCooldownState>();
        if (cooldown == null)
        {
            cooldown = target.AddComponent<TeleporterCooldownState>();
        }

        var endpointId = GetInstanceID();
        if (cooldown.LockedEndpointId == endpointId)
        {
            return;
        }

        if (Time.time < cooldown.NextAllowedTeleportTime)
        {
            return;
        }

        var destination = LinkedEndpoint.GetExitPosition();
        target.transform.position = destination;

        var rb = target.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            var launch = LinkedEndpoint.GetExitDirection() * Mathf.Max(0f, ExitForwardVelocity);
            launch.y += Mathf.Max(0f, ExitVerticalVelocity);
            rb.linearVelocity = launch;
        }

        cooldown.NextAllowedTeleportTime = Time.time + Mathf.Max(0.01f, PerObjectCooldown);
        cooldown.LockedEndpointId = LinkedEndpoint.GetInstanceID();
    }

    private void ReleaseEndpointLock(Collider other)
    {
        if (other == null)
        {
            return;
        }

        var target = ResolveTarget(other);
        if (target == null)
        {
            return;
        }

        var cooldown = target.GetComponent<TeleporterCooldownState>();
        if (cooldown == null)
        {
            return;
        }

        if (cooldown.LockedEndpointId == GetInstanceID())
        {
            cooldown.LockedEndpointId = -1;
        }
    }

    private Vector3 GetExitPosition()
    {
        if (ExitPoint != null)
        {
            return ExitPoint.position;
        }

        var ownCollider = triggerCollider != null ? triggerCollider : GetComponent<Collider>();
        if (ownCollider != null)
        {
            return ownCollider.bounds.center;
        }

        return transform.position;
    }

    private Vector3 GetExitDirection()
    {
        var direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return Vector3.forward;
        }

        return direction.normalized;
    }

    private GameObject ResolveTarget(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            return other.attachedRigidbody.gameObject;
        }

        return other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
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

public class TeleporterCooldownState : MonoBehaviour
{
    public float NextAllowedTeleportTime;
    public int LockedEndpointId = -1;
}
