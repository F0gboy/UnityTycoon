using UnityEngine;

public class Dropper : MonoBehaviour
{
    public GameObject DropPrefab;
    public Transform DropPoint;
    public float DropInterval = 1.0f;
    public Transform DroppedParent;
    public Material GhostPreviewMaterial;
    public float IdleDestroySeconds = 10f;

    private float nextDropTime;

    private void Update()
    {
        if (DropPrefab == null || DropPoint == null)
        {
            return;
        }

        if (Time.time >= nextDropTime)
        {
            SpawnDrop();
            nextDropTime = Time.time + DropInterval;
        }
    }

    private void SpawnDrop()
    {
        var instance = Instantiate(DropPrefab, DropPoint.position, DropPoint.rotation);
        if (DroppedParent != null)
        {
            instance.transform.SetParent(DroppedParent, worldPositionStays: true);
        }

        var autoDestroy = instance.GetComponent<AutoDestroyOnNoMovement>();
        if (autoDestroy == null)
        {
            autoDestroy = instance.AddComponent<AutoDestroyOnNoMovement>();
        }
        autoDestroy.IdleSeconds = IdleDestroySeconds;
    }
}
