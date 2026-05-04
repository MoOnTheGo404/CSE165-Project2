using UnityEngine;
using TMPro;

public class WayfindingArrow : MonoBehaviour
{
    [Header("References")]
    [Tooltip("CheckpointManager that owns the track. Used to query the current target.")]
    public CheckpointManager checkpointManager;
    [Tooltip("The mesh to rotate so it points at the next checkpoint. Should be a child of this object.")]
    public Transform arrowMesh;
    [Tooltip("TextMeshPro label that displays 'Next: #N, Xm'.")]
    public TMP_Text distanceLabel;

    [Header("Display Options")]
    [Tooltip("Hide the arrow when the track is finished (no more targets).")]
    public bool hideWhenComplete = true;
    [Tooltip("Optional prefix for the label.")]
    public string labelPrefix = "Next:";

    private void Update()
    {
        if (checkpointManager == null || arrowMesh == null) return;

        if (!checkpointManager.TryGetCurrentTargetPosition(out Vector3 targetWorldPos))
        {
            // No target -> track is done, or hasn't loaded yet.
            if (hideWhenComplete && distanceLabel != null) distanceLabel.text = "Finished!";
            arrowMesh.gameObject.SetActive(!hideWhenComplete);
            return;
        }

        arrowMesh.gameObject.SetActive(true);

        // Direction from the arrow's WORLD position to the target's WORLD position.
        Vector3 toTarget = targetWorldPos - arrowMesh.position;

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            // Rotate the arrow mesh to face the target (its +Z forward axis points at the checkpoint).
            arrowMesh.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
        }

        // Update the distance label.
        if (distanceLabel != null)
        {
            float distanceMeters = toTarget.magnitude;
            int targetNumber = checkpointManager.CurrentIndex + 1; // 1-based for display
            distanceLabel.text = $"{labelPrefix} #{targetNumber}, {distanceMeters:F0}m";
        }
    }
}
