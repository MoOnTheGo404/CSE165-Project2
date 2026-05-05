using System.Collections;
using UnityEngine;

public class BeaconManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("CheckpointManager that owns the track.")]
    public CheckpointManager checkpointManager;
    [Tooltip("Beacon prefab (a tall, glowing cylinder).")]
    public GameObject beaconPrefab;

    [Header("Beacon Placement")]
    [Tooltip("Vertical offset added to the checkpoint's position (so the beacon's center sits below the checkpoint, sticking down to ground and up past the checkpoint).")]
    public float verticalOffset = -25f;
    [Tooltip("If true, the beacon is hidden when the track is finished.")]
    public bool hideWhenComplete = true;

    private GameObject beaconInstance;

    private void Start()
    {
        if (checkpointManager == null)
        {
            Debug.LogError("BeaconManager: no CheckpointManager assigned.");
            enabled = false;
            return;
        }

        if (beaconPrefab == null)
        {
            Debug.LogError("BeaconManager: no beaconPrefab assigned.");
            enabled = false;
            return;
        }

        // Spawn the beacon once. We'll move it as the target changes.
        beaconInstance = Instantiate(beaconPrefab);
        beaconInstance.name = "Beacon (active target)";

        // Subscribe to checkpoint progress events so we can re-position the beacon.
        checkpointManager.CheckpointReached += OnCheckpointReached;
        checkpointManager.TrackCompleted += OnTrackCompleted;

        // Wait one frame so CheckpointManager finishes spawning before we try to read targets.
        StartCoroutine(InitialPlacement());
    }

    private void OnDestroy()
    {
        if (checkpointManager != null)
        {
            checkpointManager.CheckpointReached -= OnCheckpointReached;
            checkpointManager.TrackCompleted -= OnTrackCompleted;
        }
    }

    private IEnumerator InitialPlacement()
    {
        // Wait until the CheckpointManager has loaded its track.
        while (checkpointManager.TotalCheckpoints == 0)
        {
            yield return null;
        }
        UpdateBeaconPosition();
    }

    private void OnCheckpointReached(int index)
    {
        UpdateBeaconPosition();
    }

    private void OnTrackCompleted()
    {
        if (hideWhenComplete && beaconInstance != null)
        {
            beaconInstance.SetActive(false);
        }
    }

    private void UpdateBeaconPosition()
    {
        if (beaconInstance == null) return;
        if (!checkpointManager.TryGetCurrentTargetPosition(out Vector3 targetPos))
        {
            beaconInstance.SetActive(false);
            return;
        }

        beaconInstance.SetActive(true);
        // Offset vertically so the beacon's vertical center is below the checkpoint;
        // its top extends well above the checkpoint for easy visibility.
        Vector3 placement = targetPos;
        placement.y += verticalOffset;
        beaconInstance.transform.position = placement;
        beaconInstance.transform.rotation = Quaternion.identity;
    }
}
