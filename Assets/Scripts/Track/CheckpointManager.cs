using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    [Header("Track Source")]
    [Tooltip("Filename of the XYZ track inside StreamingAssets. Leave blank to use externalPath.")]
    public string streamingAssetsFilename = "track.xyz";

    [Tooltip("Optional absolute path (e.g. a thumbdrive). If set, takes priority over StreamingAssets.")]
    public string externalPath = "";

    [Header("Checkpoint Visuals")]
    [Tooltip("Prefab for a checkpoint. Should have a SphereCollider (trigger) and a Checkpoint component.")]
    public GameObject checkpointPrefab;

    [Tooltip("Reach radius in METERS. 30 feet ≈ 9.144 meters per project spec.")]
    public float reachRadiusMeters = 9.144f;

    [Tooltip("Visual sphere radius in meters (just for the visible mesh; collider uses reachRadiusMeters).")]
    public float visualRadiusMeters = 9.144f;

    [Header("Drone Reference")]
    [Tooltip("The drone Transform. Auto-positioned behind checkpoint 1 at start.")]
    public Transform droneTransform;

    [Tooltip("How far behind checkpoint 1 to spawn the drone (meters).")]
    public float spawnBackOffsetMeters = 15f;

    [Header("Runtime State (read-only)")]
    [SerializeField] private int currentIndex = 0;

    private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();

    public event Action<int> CheckpointReached;
    public event Action TrackCompleted;

    public int CurrentIndex => currentIndex;
    public int TotalCheckpoints => checkpoints.Count;
    public Checkpoint CurrentCheckpoint =>
        (currentIndex >= 0 && currentIndex < checkpoints.Count) ? checkpoints[currentIndex] : null;

    private void Start()
    {
        StartCoroutine(LoadAndSpawnAsync());
    }

    public void LoadAndSpawn()
    {
        // Synchronous version — kept for backwards compatibility but should not be called on Android.
        StartCoroutine(LoadAndSpawnAsync());
    }

    private System.Collections.IEnumerator LoadAndSpawnAsync()
    {
        // Clear any previously-spawned checkpoints (in case of reload).
        foreach (var cp in checkpoints)
            if (cp != null) Destroy(cp.gameObject);
        checkpoints.Clear();
        currentIndex = 0;

        // Resolve which file to load.
        string path = !string.IsNullOrEmpty(externalPath)
            ? externalPath
            : System.IO.Path.Combine(Application.streamingAssetsPath, streamingAssetsFilename);

        Debug.Log($"CheckpointManager: loading track from {path}");

        List<Vector3> positions = null;
        yield return XYZParser.ParseFileAsync(path, result => positions = result);

        if (positions == null || positions.Count == 0)
        {
            Debug.LogError("CheckpointManager: no checkpoints loaded.");
            yield break;
        }

        if (checkpointPrefab == null)
        {
            Debug.LogError("CheckpointManager: checkpointPrefab is not assigned.");
            yield break;
        }

        // Spawn a checkpoint GameObject for each parsed position.
        for (int i = 0; i < positions.Count; i++)
        {
            GameObject go = Instantiate(checkpointPrefab, transform);
            go.transform.position = positions[i];
            go.transform.rotation = Quaternion.identity;
            go.name = $"Checkpoint_{i}";

            // Set visual scale (sphere mesh diameter = 2 * radius).
            go.transform.localScale = Vector3.one * (visualRadiusMeters * 2f);

            // Set the trigger collider radius for the reach detection.
            SphereCollider sc = go.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                sc.radius = 0.5f * (reachRadiusMeters / visualRadiusMeters);
            }

            Checkpoint cp = go.GetComponent<Checkpoint>();
            if (cp == null) cp = go.AddComponent<Checkpoint>();
            cp.Initialize(i, this);

            CheckpointLabel labelComponent = go.GetComponent<CheckpointLabel>();
            if (labelComponent != null)
                labelComponent.SetNumber(i + 1);

            checkpoints.Add(cp);
        }

        // Initial target is checkpoint 0 (= the first sphere labeled "1"). The drone
        // spawns behind it, so the player must fly forward through it.
        currentIndex = 0;

        // Position the drone BEHIND checkpoint 1, facing toward it (and toward checkpoint 2).
        if (droneTransform != null && positions.Count > 1)
        {
            Vector3 toNext = positions[1] - positions[0];
            toNext.y = 0f;
            Vector3 forwardDir = toNext.sqrMagnitude > 0.0001f ? toNext.normalized : Vector3.forward;

            // Spawn position: checkpoint 1 minus the forward direction times the offset.
            droneTransform.position = positions[0] - forwardDir * spawnBackOffsetMeters;
            droneTransform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        }
        else if (droneTransform != null && positions.Count > 0)
        {
            // Fallback for single-checkpoint tracks.
            droneTransform.position = positions[0];
            droneTransform.rotation = Quaternion.identity;
        }

        Debug.Log($"CheckpointManager: spawned {checkpoints.Count} checkpoints.");
        UpdateCheckpointHighlights();
    }

    public void OnCheckpointEntered(Checkpoint cp)
    {
        // Enforce order: only the current checkpoint counts.
        if (cp.Index != currentIndex) return;

        Debug.Log($"Checkpoint {cp.Index} reached!");

        // Hide the reached checkpoint.
        cp.gameObject.SetActive(false);

        // Advance the pointer FIRST, so listeners querying for the next target see the right value.
        currentIndex++;
        UpdateCheckpointHighlights();

        // Now fire the event so listeners (BeaconManager, etc.) refresh based on the new state.
        CheckpointReached?.Invoke(cp.Index);

        if (currentIndex >= checkpoints.Count)
        {
            Debug.Log("Track completed!");
            TrackCompleted?.Invoke();
        }
    }

    public bool TryGetCurrentTargetPosition(out Vector3 worldPos)
    {
        if (CurrentCheckpoint != null)
        {
            worldPos = CurrentCheckpoint.transform.position;
            return true;
        }
        worldPos = Vector3.zero;
        return false;
    }

    private void UpdateCheckpointHighlights()
    {
        for (int i = 0; i < checkpoints.Count; i++)
        {
            if (checkpoints[i] == null) continue;
            Renderer r = checkpoints[i].GetComponentInChildren<Renderer>();
            if (r == null) continue;

            // Current target: bright yellow. Others: cyan (the original color).
            Color targetColor = (i == currentIndex) ? new Color(1f, 1f, 0f, 0.5f) : new Color(0f, 1f, 1f, 0.4f);
            r.material.color = targetColor;
        }
    }

    public Vector3 GetCheckpointWorldPosition(int index)
    {
        if (index >= 0 && index < checkpoints.Count && checkpoints[index] != null)
        {
            return checkpoints[index].transform.position;
        }
        Debug.LogWarning($"GetCheckpointWorldPosition: invalid index {index}");
        return Vector3.zero;
    }
}
