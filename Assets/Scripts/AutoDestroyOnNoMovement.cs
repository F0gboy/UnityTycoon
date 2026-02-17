using UnityEngine;

public class AutoDestroyOnNoMovement : MonoBehaviour
{
    public float IdleSeconds = 10f;
    public float MinSpeed = 0.02f;
    public float MinDistancePerSecond = 0.01f;

    private Rigidbody cachedRb;
    private Vector3 lastPosition;
    private float lastMoveTime;

    private void Awake()
    {
        cachedRb = GetComponent<Rigidbody>();
        lastPosition = transform.position;
        lastMoveTime = Time.time;
    }

    private void Update()
    {
        if (IsMoving())
        {
            lastMoveTime = Time.time;
        }

        if (Time.time - lastMoveTime >= IdleSeconds)
        {
            Destroy(gameObject);
        }
    }

    private bool IsMoving()
    {
        if (cachedRb != null && !cachedRb.isKinematic)
        {
            return cachedRb.linearVelocity.sqrMagnitude >= (MinSpeed * MinSpeed);
        }

        var delta = transform.position - lastPosition;
        lastPosition = transform.position;
        return (delta.sqrMagnitude / Mathf.Max(Time.deltaTime, 0.0001f)) >= (MinDistancePerSecond * MinDistancePerSecond);
    }
}
