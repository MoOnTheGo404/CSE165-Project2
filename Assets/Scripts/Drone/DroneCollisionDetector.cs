using System;
using UnityEngine;


public class DroneCollisionDetector : MonoBehaviour
{
    [Tooltip("Tag used by checkpoints. Collisions with these are ignored here.")]
    public string checkpointTag = "Untagged"; // Checkpoints don't have a special tag in our setup; we filter by component type instead.

    /// <summary>Fired when the drone hits a non-checkpoint collider.</summary>
    public event Action Crashed;

    private void OnTriggerEnter(Collider other)
    {
        // Ignore the checkpoints (they have a Checkpoint component on them).
        if (other.GetComponent<Checkpoint>() != null) return;

        // Anything else is "terrain or buildings" -> crash.
        Debug.Log($"Drone collided with: {other.name}");
        Crashed?.Invoke();
    }
}
