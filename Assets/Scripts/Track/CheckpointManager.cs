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
        LoadAndSpawn();
    }

    public void LoadAndSpawn()
    {
        foreach (var cp in checkpoints)
            if (cp != null) Destroy(cp.gameObject);
        checkpoints.Clear();
        currentIndex = 0;

        string path = !string.IsNullOrEmpty(externalPath)
            ? externalPath
            : Path.Combine(Application.streamingAssetsPath, streamingAssetsFilename);

        Debug.Log($"CheckpointManager: loading track from {path}");
        List<Vector3> positions = XYZParser.ParseFile(path);

        if (positions.Count == 0)
        {
            Debug.LogError("CheckpointManager: no checkpoints loaded.");
            return;
        }

        if (checkpointPrefab == null)
        {
            Debug.LogError("CheckpointManager: checkpointPrefab is not assigned.");
            return;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject go = Instantiate(checkpointPrefab, positions[i], Quaternion.identity, transform);
            go.name = $"Checkpoint_{i}";

            go.transform.localScale = Vector3.one * (visualRadiusMeters * 2f);

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

        Debug.Log($"CheckpointManager: spawned {checkpoints.Count} checkpoints.");
        UpdateCheckpointHighlights();
    }

    public void OnCheckpointEntered(Checkpoint cp)
    {
        if (cp.Index != currentIndex) return;

        Debug.Log($"Checkpoint {cp.Index} reached!");
        CheckpointReached?.Invoke(cp.Index);

        cp.gameObject.SetActive(false);

        currentIndex++;
        UpdateCheckpointHighlights();

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
        // use for the wayfinding system.
    }
}