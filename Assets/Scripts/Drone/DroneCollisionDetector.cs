using System;
using UnityEngine;

/// <summary>
/// Detects collisions with terrain/buildings each frame using Physics.OverlapSphere.
/// Bypasses Unity's trigger-vs-static-collider interaction limitation that prevents
/// kinematic trigger colliders from firing OnTriggerEnter against MeshColliders.
/// </summary>
public class DroneCollisionDetector : MonoBehaviour
{
    [Tooltip("Radius of the crash-detection sphere (meters). Should match or slightly exceed the drone's visual size.")]
    public float crashRadius = 1.5f;
    [Tooltip("Cooldown after a detected crash before another can register (prevents repeat triggers).")]
    public float crashCooldown = 1.0f;

    public event Action Crashed;

    private float cooldownTimer = 0f;

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        // Sphere-cast at the drone's position. Returns all colliders overlapping the sphere.
        Collider[] hits = Physics.OverlapSphere(transform.position, crashRadius);

        foreach (var hit in hits)
        {
            // Ignore checkpoints — they're handled separately.
            if (hit.GetComponent<Checkpoint>() != null) continue;

            // Ignore the drone itself and any of its children (cockpit, drone body, hand meshes).
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            // Anything else = terrain or building.
            Debug.Log($"Drone collided with: {hit.name}");
            Crashed?.Invoke();
            cooldownTimer = crashCooldown;
            return;
        }
    }
}
