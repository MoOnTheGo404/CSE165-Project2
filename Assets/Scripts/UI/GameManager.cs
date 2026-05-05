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
    [Tooltip("Vertical offset added to reset position to keep drone above terrain (meters).")]
    public float crashRespawnVerticalOffset = 5f;

    [Header("Runtime State (read-only)")]
    [SerializeField] private GameState state = GameState.PreStart;
    [SerializeField] private float elapsedTime = 0f;
    [SerializeField] private int lastClearedCheckpointIndex = -1; // -1 means "nothing cleared yet"
    [SerializeField] private bool anyCheckpointCleared = false;

    public GameState State => state;
    public float ElapsedTime => elapsedTime;

    private float notificationTimeRemaining = 0f;
    private Quaternion droneInitialRotation;
    private Vector3 droneInitialPosition;

    private void Start()
    {
        if (checkpointManager != null)
        {
            checkpointManager.CheckpointReached += OnCheckpointReached;
            checkpointManager.TrackCompleted += OnTrackCompleted;
        }
        if (collisionDetector != null)
        {
            collisionDetector.Crashed += OnDroneCrashed;
        }

        if (flightController != null) flightController.FlightEnabled = false;

        if (notificationText != null) notificationText.text = "";
        if (stopwatchText != null) stopwatchText.text = "00:00.00";

        // Cache the initial drone pose (set by CheckpointManager) so we can reset to it
        // if the drone crashes BEFORE clearing any checkpoints.
        StartCoroutine(CacheInitialPoseAndStartCountdown());
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
            if (notificationTimeRemaining <= 0f && notificationText != null
                && state != GameState.Crashed && state != GameState.Finished)
            {
                notificationText.text = "";
            }
        }
    }

    private IEnumerator CacheInitialPoseAndStartCountdown()
    {
        // Wait one frame so CheckpointManager has positioned the drone.
        yield return null;
        if (droneTransform != null)
        {
            droneInitialPosition = droneTransform.position;
            droneInitialRotation = droneTransform.rotation;
        }
        StartCoroutine(RunCountdown());
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
        anyCheckpointCleared = true;
        ShowNotification($"Checkpoint {index + 1} reached!");
    }

    private void OnTrackCompleted()
    {
        state = GameState.Finished;
        if (flightController != null) flightController.FlightEnabled = false;
        if (countdownText != null) countdownText.text = "FINISHED!";
        if (notificationText != null) notificationText.text = $"Time: {FormatTime(elapsedTime)}";
        notificationTimeRemaining = float.PositiveInfinity;
    }

    private void OnDroneCrashed()
    {
        // Only handle crashes during active racing — ignore crashes during countdown,
        // during another crash freeze, or after finish.
        if (state != GameState.Racing) return;
        StartCoroutine(HandleCrash());
    }

    private IEnumerator HandleCrash()
    {
        state = GameState.Crashed;
        if (flightController != null) flightController.FlightEnabled = false;

        // CRITICAL: disable the collision detector during the freeze, otherwise it'll
        // immediately detect the terrain we just respawned on top of and re-trigger a crash.
        if (collisionDetector != null) collisionDetector.enabled = false;

        ResetDroneToLastCheckpoint();

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
        if (collisionDetector != null) collisionDetector.enabled = true;
        state = GameState.Racing;
    }

    private void ResetDroneToLastCheckpoint()
    {
        if (droneTransform == null) return;

        Vector3 resetPos;
        Vector3 facingTarget = droneTransform.position + droneTransform.forward; // fallback

        if (anyCheckpointCleared && checkpointManager != null)
        {
            // Use the last cleared checkpoint's position.
            resetPos = checkpointManager.GetCheckpointWorldPosition(lastClearedCheckpointIndex);
        }
        else
        {
            // No checkpoints cleared yet — go back to the original spawn position.
            resetPos = droneInitialPosition;
        }

        // Lift the drone slightly above the reset point so it doesn't immediately re-crash.
        resetPos.y += crashRespawnVerticalOffset;
        droneTransform.position = resetPos;

        // Face the next checkpoint per the TA's note.
        if (checkpointManager != null && checkpointManager.TryGetCurrentTargetPosition(out Vector3 nextPos))
        {
            Vector3 toNext = nextPos - resetPos;
            toNext.y = 0f;
            if (toNext.sqrMagnitude > 0.0001f)
            {
                droneTransform.rotation = Quaternion.LookRotation(toNext, Vector3.up);
            }
            else
            {
                droneTransform.rotation = droneInitialRotation;
            }
        }
        else
        {
            droneTransform.rotation = droneInitialRotation;
        }

        // Tell the flight controller to reset its internal state (e.g., velocity smoothing).
        if (flightController != null) flightController.ResetCalibration();
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
