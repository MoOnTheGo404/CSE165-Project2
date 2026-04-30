using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class Checkpoint : MonoBehaviour
{
    public int Index { get; private set; }
    public CheckpointManager Manager { get; set; }

    public void Initialize(int index, CheckpointManager manager)
    {
        Index = index;
        Manager = manager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Drone")) return;
        if (Manager == null) return;
        Manager.OnCheckpointEntered(this);
    }
}