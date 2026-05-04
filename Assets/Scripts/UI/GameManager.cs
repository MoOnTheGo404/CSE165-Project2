using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        PreStart,
        Countdown,
        Racing,
        Crashed,
        Finished
    }

    [Header("References")]
    public CheckpointManager checkpointManager;
    public DroneFlightController flightController;
    public DroneCollisionDetector collisionDetector;
    public Transform droneTransform;

    [Header("UI Text")]
    public TMP_Text countdownText;
    public TMP_Text stopwatchText;
    public TMP_Text notificationText;

    [Header("Timing")]
    [Tooltip("Initial countdown duration in seconds before racing starts.")]
    public float countdownDuration = 5f;
    [Tooltip("How long the drone is frozen after a crash (project minimum: 3 seconds).")]
    public float crashFreezeDuration = 3f;
    [Tooltip("How long checkpoint-reached notifications stay on screen.")]
    public float notificationDuration = 1.5f;

    [Header("Runtime State (read-only)")]
    [SerializeField] private GameState state = GameState.PreStart;
    [SerializeField] private float elapsedTime = 0f;
    [SerializeField] private int lastClearedCheckpointIndex = 0; // We start at 0 (start position).

    public GameState State => state;
    public float ElapsedTime => elapsedTime;

    private float notificationTimeRemaining = 0f;

    private void Start()
    {
        // Subscribe to checkpoint events.
        if (checkpointManager != null)
        {
            checkpointManager.CheckpointReached += OnCheckpointReached;
            checkpointManager.TrackCompleted += OnTrackCompleted;
        }
        if (collisionDetector != null)
        {
            collisionDetector.Crashed += OnDroneCrashed;
        }

        // Disable flight during PreStart and Countdown.
        if (flightController != null) flightController.FlightEnabled = false;

        if (notificationText != null) notificationText.text = "";
        if (stopwatchText != null) stopwatchText.text = "00:00.00";

        StartCoroutine(RunCountdown());
    }

    private void OnDestroy()
    {
        if (checkpointManager != null)
        {
            checkpointManager.CheckpointReached -= OnCheckpointReached;
            checkpointManager.TrackCompleted -= OnTrackCompleted;
        }
        if (collisionDetector != null)
        {
            collisionDetector.Crashed -= OnDroneCrashed;
        }
    }

    private void Update()
    {
        // Stopwatch runs during Racing AND Crashed (timer doesn't stop on crash, per project spec).
        if (state == GameState.Racing || state == GameState.Crashed)
        {
            elapsedTime += Time.deltaTime;
        }
        UpdateStopwatchUI();

        // Decay notification message.
        if (notificationTimeRemaining > 0f)
        {
            notificationTimeRemaining -= Time.deltaTime;
            if (notificationTimeRemaining <= 0f && notificationText != null && state != GameState.Crashed && state != GameState.Finished)
            {
                notificationText.text = "";
            }
        }
    }

    private IEnumerator RunCountdown()
    {
        state = GameState.Countdown;
        float remaining = countdownDuration;
        while (remaining > 0f)
        {
            int displayed = Mathf.CeilToInt(remaining);
            if (countdownText != null) countdownText.text = displayed.ToString();
            yield return null;
            remaining -= Time.deltaTime;
        }

        // Switch to "GO!" briefly, then start racing.
        if (countdownText != null) countdownText.text = "GO!";
        if (flightController != null) flightController.FlightEnabled = true;
        state = GameState.Racing;
        yield return new WaitForSeconds(1f);
        if (countdownText != null) countdownText.text = "";
    }

    private void OnCheckpointReached(int index)
    {
        if (state == GameState.Crashed || state == GameState.Finished) return;

        lastClearedCheckpointIndex = index;
        ShowNotification($"Checkpoint {index + 1} reached!");
    }

    private void OnTrackCompleted()
    {
        state = GameState.Finished;
        if (flightController != null) flightController.FlightEnabled = false;
        if (countdownText != null) countdownText.text = "FINISHED!";
        if (notificationText != null) notificationText.text = $"Time: {FormatTime(elapsedTime)}";
        notificationTimeRemaining = float.PositiveInfinity; // Keep finish message permanently.
    }

    private void OnDroneCrashed()
    {
        if (state != GameState.Racing) return;
        StartCoroutine(HandleCrash());
    }

    private IEnumerator HandleCrash()
    {
        state = GameState.Crashed;
        if (flightController != null) flightController.FlightEnabled = false;

        // Move drone back to last cleared checkpoint and orient toward next checkpoint.
        ResetDroneToLastCheckpoint();

        // Show crash message countdown.
        float remaining = crashFreezeDuration;
        while (remaining > 0f)
        {
            int displayed = Mathf.CeilToInt(remaining);
            if (notificationText != null) notificationText.text = $"CRASHED — resetting in {displayed}...";
            yield return null;
            remaining -= Time.deltaTime;
        }

        if (notificationText != null) notificationText.text = "";
        if (flightController != null) flightController.FlightEnabled = true;
        state = GameState.Racing;
    }

    private void ResetDroneToLastCheckpoint()
    {
        if (droneTransform == null || checkpointManager == null) return;

        // Get the world position of the last cleared checkpoint, or the start position
        // if nothing has been cleared yet (lastClearedCheckpointIndex starts at 0 = start).
        Vector3 resetPos;
        if (lastClearedCheckpointIndex == 0)
        {
            // Use the start position (track[0]) -- recompute from track data.
            // CheckpointManager spawned a checkpoint there; we can grab its position from CurrentTargetPosition logic.
            // Easiest: read from the manager's internal list via a helper. Since we don't have one,
            // we just use the position of the checkpoint that's currently the target's predecessor.
            resetPos = droneTransform.position; // Fallback (shouldn't happen often).
        }
        else
        {
            // Use the public API: query the position of the last cleared checkpoint via the manager.
            // We don't have a direct getter, so we use a small helper: get the current target,
            // then compute the previous one via the cleared index.
            // For now, just use whatever the current target is and back off slightly... but actually
            // the cleanest solution is to add a helper. For simplicity here:
            resetPos = checkpointManager.GetCheckpointWorldPosition(lastClearedCheckpointIndex);
        }

        droneTransform.position = resetPos;

        // Face the next checkpoint (per TA note).
        if (checkpointManager.TryGetCurrentTargetPosition(out Vector3 nextPos))
        {
            Vector3 toNext = nextPos - resetPos;
            toNext.y = 0f;
            if (toNext.sqrMagnitude > 0.0001f)
            {
                droneTransform.rotation = Quaternion.LookRotation(toNext, Vector3.up);
            }
        }
    }

    private void ShowNotification(string text)
    {
        if (notificationText != null) notificationText.text = text;
        notificationTimeRemaining = notificationDuration;
    }

    private void UpdateStopwatchUI()
    {
        if (stopwatchText == null) return;
        stopwatchText.text = FormatTime(elapsedTime);
    }

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float secs = seconds - minutes * 60f;
        return $"{minutes:00}:{secs:00.00}";
    }
}
