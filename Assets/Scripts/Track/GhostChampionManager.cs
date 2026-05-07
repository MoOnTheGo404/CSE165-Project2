using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GhostChampionManager : MonoBehaviour
{
    [Serializable]
    public class GhostFrame
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class GhostRun
    {
        public float totalTime;
        public List<GhostFrame> frames = new List<GhostFrame>();
    }

    [Header("References")]
    public GameManager gameManager;
    public Transform droneTransform;
    public GameObject ghostVisual;

    [Header("Recording")]
    public float samplesPerSecond = 90f;

    [Header("Runtime")]
    [SerializeField] private bool hasBestRun = false;
    [SerializeField] private float bestTime = 0f;
    [SerializeField] private int currentFrameCount = 0;
    [SerializeField] private int bestFrameCount = 0;

    public bool HasBestRun => hasBestRun;

    [Header("Startup Options")]
    public bool clearSavedGhostOnStartup = true;

    private GhostRun currentRun = new GhostRun();
    private GhostRun bestRun = new GhostRun();

    private float sampleTimer = 0f;
    private string savePath;

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "ghost_best_run.json");

        if (clearSavedGhostOnStartup && File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("GhostChampion: cleared saved ghost on startup.");
        }

        if (ghostVisual != null)
        {
            DisableGhostPhysics();
            ghostVisual.SetActive(false);
        }

        LoadBestRun();
    }

    private void Update()
    {
        if (gameManager == null || droneTransform == null)
            return;

        if (gameManager.State == GameManager.GameState.Racing)
        {
            RecordRun();
            ReplayGhost();
        }
        else
        {
            if (ghostVisual != null)
                ghostVisual.SetActive(false);
        }
    }

    private void DisableGhostPhysics()
    {
        if (ghostVisual == null) return;

        Collider[] colliders = ghostVisual.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = ghostVisual.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.useGravity = false;
        }

        Debug.Log($"GhostChampion: disabled {colliders.Length} ghost colliders and {rigidbodies.Length} ghost rigidbodies.");
    }

    public void StartNewRun()
    {
        currentRun = new GhostRun();
        sampleTimer = 0f;
        currentFrameCount = 0;

        if (ghostVisual != null)
            ghostVisual.SetActive(false);

        Debug.Log("GhostChampion: started new recording.");
    }

    public void FinishRun()
    {
        if (currentRun.frames.Count < 2)
        {
            Debug.LogWarning("GhostChampion: not enough frames to save.");
            return;
        }

        currentRun.totalTime = gameManager.ElapsedTime;

        bool isNewBest = !hasBestRun || currentRun.totalTime < bestRun.totalTime;

        if (isNewBest)
        {
            bestRun = currentRun;
            hasBestRun = true;
            bestTime = bestRun.totalTime;
            bestFrameCount = bestRun.frames.Count;

            SaveBestRun();

            Debug.Log($"GhostChampion: NEW BEST saved. Time={bestTime:F2}, Frames={bestFrameCount}");
        }
        else
        {
            Debug.Log($"GhostChampion: run complete, not best. Current={currentRun.totalTime:F2}, Best={bestRun.totalTime:F2}");
        }
    }

    private void RecordRun()
    {
        sampleTimer += Time.deltaTime;
        float interval = 1f / samplesPerSecond;

        while (sampleTimer >= interval)
        {
            sampleTimer -= interval;

            GhostFrame frame = new GhostFrame
            {
                time = gameManager.ElapsedTime,
                position = droneTransform.position,
                rotation = droneTransform.rotation
            };

            currentRun.frames.Add(frame);
            currentFrameCount = currentRun.frames.Count;
        }
    }

    private void ReplayGhost()
    {
        if (!hasBestRun || ghostVisual == null || bestRun.frames.Count < 2)
        {
            if (ghostVisual != null)
                ghostVisual.SetActive(false);
            return;
        }

        float t = gameManager.ElapsedTime;

        if (t > bestRun.totalTime)
        {
            ghostVisual.SetActive(false);
            return;
        }

        if (!TryGetFrames(t, out GhostFrame a, out GhostFrame b))
        {
            ghostVisual.SetActive(false);
            return;
        }

        ghostVisual.SetActive(true);

        float duration = b.time - a.time;
        float lerp = duration > 0f ? (t - a.time) / duration : 0f;

        ghostVisual.transform.position = Vector3.Lerp(a.position, b.position, lerp);
        ghostVisual.transform.rotation = Quaternion.Slerp(a.rotation, b.rotation, lerp);
    }

    private bool TryGetFrames(float time, out GhostFrame a, out GhostFrame b)
    {
        a = null;
        b = null;

        for (int i = 0; i < bestRun.frames.Count - 1; i++)
        {
            GhostFrame current = bestRun.frames[i];
            GhostFrame next = bestRun.frames[i + 1];

            if (time >= current.time && time <= next.time)
            {
                a = current;
                b = next;
                return true;
            }
        }

        return false;
    }

    private void SaveBestRun()
    {
        string json = JsonUtility.ToJson(bestRun, true);
        File.WriteAllText(savePath, json);
        Debug.Log($"GhostChampion: saved to {savePath}");
    }

    private void LoadBestRun()
    {
        if (!File.Exists(savePath))
        {
            hasBestRun = false;
            Debug.Log("GhostChampion: no saved ghost yet.");
            return;
        }

        string json = File.ReadAllText(savePath);
        bestRun = JsonUtility.FromJson<GhostRun>(json);

        if (bestRun != null && bestRun.frames != null && bestRun.frames.Count >= 2)
        {
            hasBestRun = true;
            bestTime = bestRun.totalTime;
            bestFrameCount = bestRun.frames.Count;
            Debug.Log($"GhostChampion: loaded best run. Time={bestTime:F2}, Frames={bestFrameCount}");
        }
        else
        {
            hasBestRun = false;
            Debug.LogWarning("GhostChampion: saved file invalid.");
        }
    }

    public void ClearBestRun()
    {
        if (File.Exists(savePath))
            File.Delete(savePath);

        hasBestRun = false;
        bestRun = new GhostRun();

        if (ghostVisual != null)
            ghostVisual.SetActive(false);

        Debug.Log("GhostChampion: cleared saved best run.");
    }
}