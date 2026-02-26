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
        if (ForceTriggerCollider && triggerCollider != null)
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
        if (other == null || LinkedEndpoint == null || !IsAffected(other))
        {
            return;
        }

        var target = ResolveTarget(other);
        if (target == null)
        {
            return;
        }

        var endpointId = GetInstanceID();
        var cooldown = GetOrAddCooldown(target);
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
        var cooldown = target != null ? target.GetComponent<TeleporterCooldownState>() : null;
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
        return ownCollider != null ? ownCollider.bounds.center : transform.position;
    }

    private Vector3 GetExitDirection()
    {
        var direction = transform.forward;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.001f ? Vector3.forward : direction.normalized;
    }

    private GameObject ResolveTarget(Collider other)
    {
        return other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : (other.transform.root != null ? other.transform.root.gameObject : other.gameObject);
    }

    private bool IsAffected(Collider other)
    {
        return (AffectedLayers.value & (1 << other.gameObject.layer)) != 0
            && (string.IsNullOrEmpty(RequiredTag) || other.CompareTag(RequiredTag));
    }

    private TeleporterCooldownState GetOrAddCooldown(GameObject target)
    {
        var cooldown = target.GetComponent<TeleporterCooldownState>();
        return cooldown != null ? cooldown : target.AddComponent<TeleporterCooldownState>();
    }
}

public class TeleporterCooldownState : MonoBehaviour
{
    public float NextAllowedTeleportTime;
    public int LockedEndpointId = -1;
}
