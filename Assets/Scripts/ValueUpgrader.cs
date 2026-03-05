using System.Collections.Generic;
using UnityEngine;

public class ValueUpgrader : MonoBehaviour
{
    [Header("Upgrade")]
    public float Multiplier = 2f;
    public float PerObjectCooldown = 0.2f;

    [Header("Filtering")]
    public LayerMask AffectedLayers = ~0;
    public string RequiredTag = "";

    [Header("Snap To Conveyor")]
    public bool SnapToNearestConveyorOnStart = true;
    public float SnapRadius = 2f;
    public float SnapVerticalOffset = 0.1f;
    public Vector3 SnapLocalOffset = Vector3.zero;
    public bool RequireConveyorSnapPoint = true;
    public float SnapYawOffset = 90f;
    public bool PreventStacking = true;
    public float StackCheckRadius = 0.25f;

    [Header("Laser")]
    public Collider LaserObjectCollider;

    private readonly Dictionary<int, float> nextAllowedUpgradeTime = new Dictionary<int, float>();

    private void Awake()
    {
        if (LaserObjectCollider == null)
        {
            LaserObjectCollider = GetComponent<Collider>();
        }

        if (LaserObjectCollider != null)
        {
            LaserObjectCollider.isTrigger = true;

            if (LaserObjectCollider.gameObject != gameObject)
            {
                var relay = LaserObjectCollider.GetComponent<ValueUpgraderTriggerRelay>();
                if (relay == null)
                {
                    relay = LaserObjectCollider.gameObject.AddComponent<ValueUpgraderTriggerRelay>();
                }

                relay.Owner = this;
            }
        }
    }

    private void Start()
    {
        if (SnapToNearestConveyorOnStart)
        {
            TrySnapToNearestConveyor();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessTrigger(other);
    }

    public void ProcessTrigger(Collider other)
    {
        if (other == null || !IsAffected(other))
        {
            return;
        }

        var target = ResolveTarget(other);
        if (target == null)
        {
            return;
        }

        var targetId = target.GetInstanceID();
        if (nextAllowedUpgradeTime.TryGetValue(targetId, out var nextTime) && Time.time < nextTime)
        {
            return;
        }

        var sellValue = target.GetComponentInChildren<SellValue>(true);
        if (sellValue == null)
        {
            return;
        }

        var oldValue = Mathf.Max(0, sellValue.Coins);
        var upgraded = Mathf.RoundToInt(oldValue * Mathf.Max(0f, Multiplier));
        sellValue.Coins = Mathf.Max(oldValue, upgraded);
        nextAllowedUpgradeTime[targetId] = Time.time + Mathf.Max(0f, PerObjectCooldown);
    }

    public bool TrySnapToNearestConveyor()
    {
        if (!TryGetSnapPose(transform.position, out var snappedPosition, out var snappedRotation))
        {
            return false;
        }

        transform.position = snappedPosition;
        transform.rotation = snappedRotation;
        return true;
    }

    public bool TryGetSnapPose(Vector3 probePosition, out Vector3 snappedPosition, out Quaternion snappedRotation)
    {
        snappedPosition = probePosition;
        snappedRotation = transform.rotation;

        if (!TryFindNearestConveyor(probePosition, out var nearest))
        {
            return false;
        }

        var direction = nearest.Direction.sqrMagnitude > 0.001f
            ? nearest.Direction.normalized
            : Vector3.forward;
        if (nearest.UseLocalDirection)
        {
            direction = nearest.transform.TransformDirection(direction).normalized;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            snappedRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, SnapYawOffset, 0f);
        }

        if (nearest.UpgraderSnapPoint != null)
        {
            if (nearest.IsUpgraderSlotFilled(this))
            {
                return false;
            }

            snappedPosition = nearest.UpgraderSnapPoint.position + snappedRotation * SnapLocalOffset;
            if (PreventStacking && !IsSnapSpotFree(snappedPosition))
            {
                return false;
            }
            return true;
        }

        if (RequireConveyorSnapPoint)
        {
            return false;
        }

        var conveyorCollider = nearest.GetComponent<Collider>();
        if (conveyorCollider == null)
        {
            return false;
        }

        var bounds = conveyorCollider.bounds;
        snappedPosition = bounds.center + Vector3.up * (bounds.extents.y + SnapVerticalOffset);
        snappedPosition += snappedRotation * SnapLocalOffset;

        if (PreventStacking && !IsSnapSpotFree(snappedPosition))
        {
            return false;
        }

        return true;
    }

    private bool IsSnapSpotFree(Vector3 snapPosition)
    {
        var hits = Physics.OverlapSphere(snapPosition, Mathf.Max(0.01f, StackCheckRadius));
        for (int i = 0; i < hits.Length; i++)
        {
            var upgrader = hits[i].GetComponentInParent<ValueUpgrader>();
            if (upgrader == null)
            {
                continue;
            }

            if (upgrader == this)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool TryFindNearestConveyor(Vector3 probePosition, out ConveyorBelt nearest)
    {
        if (TryGetConveyorUnderProbe(probePosition, out nearest))
        {
            return true;
        }

        nearest = null;
        var colliders = Physics.OverlapSphere(probePosition, Mathf.Max(0.01f, SnapRadius));
        var nearestSqr = float.MaxValue;
        var foundWithSnapPoint = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            var conveyor = GetBestConveyorFromTransform(colliders[i].transform);
            if (conveyor == null)
            {
                continue;
            }

            var hasSnapPoint = conveyor.UpgraderSnapPoint != null;
            var sqr = (conveyor.transform.position - probePosition).sqrMagnitude;

            if (hasSnapPoint && !foundWithSnapPoint)
            {
                foundWithSnapPoint = true;
                nearestSqr = sqr;
                nearest = conveyor;
                continue;
            }

            if (hasSnapPoint == foundWithSnapPoint && sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = conveyor;
            }
        }

        return nearest != null;
    }

    private bool TryGetConveyorUnderProbe(Vector3 probePosition, out ConveyorBelt conveyor)
    {
        conveyor = null;

        var start = probePosition + Vector3.up * 3f;
        var hits = Physics.RaycastAll(start, Vector3.down, 8f);
        if (hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        ConveyorBelt nearestWithoutSnap = null;
        for (int i = 0; i < hits.Length; i++)
        {
            var candidate = GetBestConveyorFromTransform(hits[i].transform);
            if (candidate == null)
            {
                continue;
            }

            if (candidate.UpgraderSnapPoint != null)
            {
                conveyor = candidate;
                return true;
            }

            if (nearestWithoutSnap == null)
            {
                nearestWithoutSnap = candidate;
            }
        }

        conveyor = nearestWithoutSnap;
        return conveyor != null;
    }

    private ConveyorBelt GetBestConveyorFromTransform(Transform source)
    {
        if (source == null)
        {
            return null;
        }

        // First prefer the conveyor in this exact parent chain.
        var direct = source.GetComponentInParent<ConveyorBelt>();
        if (direct != null)
        {
            return direct;
        }

        // Fallback: only search this transform's own branch, not the whole scene root.
        var inBranch = source.GetComponentsInChildren<ConveyorBelt>(true);
        if (inBranch == null || inBranch.Length == 0)
        {
            return null;
        }

        ConveyorBelt nearest = null;
        var nearestSqr = float.MaxValue;
        for (int i = 0; i < inBranch.Length; i++)
        {
            var conveyor = inBranch[i];
            var sqr = (conveyor.transform.position - source.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = conveyor;
            }
        }

        return nearest;
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
        return (AffectedLayers.value & (1 << other.gameObject.layer)) != 0
            && (string.IsNullOrEmpty(RequiredTag) || other.CompareTag(RequiredTag));
    }
}

public class ValueUpgraderTriggerRelay : MonoBehaviour
{
    public ValueUpgrader Owner;

    private void OnTriggerEnter(Collider other)
    {
        if (Owner != null)
        {
            Owner.ProcessTrigger(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (Owner != null)
        {
            Owner.ProcessTrigger(other);
        }
    }
}
