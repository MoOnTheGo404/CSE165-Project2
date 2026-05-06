using System.Collections;
using UnityEngine;

public class DroneAudioManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public CheckpointManager checkpointManager;
    public Transform droneTransform;
    public BeaconManager beaconManager;

    [Header("Visual Wayfinding To Disable During Spatial Audio Mode")]
    public GameObject wayfindingArrowObject;
    public GameObject beaconObject;

    [Header("Motor Audio")]
    public AudioSource motorSource;
    public AudioClip motorLoopClip;
    public float minMotorPitch = 0.7f;
    public float maxMotorPitch = 2.0f;
    public float maxSpeedForPitch = 25f;

    [Header("Sound Effects")]
    public AudioSource sfxSource;
    public AudioClip countdownClip;
    public AudioClip checkpointClip;
    public AudioClip crashClip;
    public AudioClip finishClip;

    [Header("Spatial Waypoint Audio")]
    public bool spatialAudioMode = true;
    public AudioSource waypointSpatialSource;
    public AudioClip waypointBeepClip;
    public float waypointBeepInterval = 1.0f;

    [Header("Wayfinding EC Toggle")]
    public KeyCode toggleSpatialAudioKey = KeyCode.T;
    private bool lastSpatialAudioMode;

    [Header("Countdown Timing")]
    public float countdownSoundLeadTime = 3f;
    private Coroutine countdownRoutine;

    private Vector3 lastDronePosition;
    private float currentSpeed;
    private float beepTimer;
    private GameManager.GameState lastState;

    private void Start()
    {
        if (droneTransform != null)
            lastDronePosition = droneTransform.position;

        if (checkpointManager != null)
        {
            checkpointManager.CheckpointReached += OnCheckpointReached;
            checkpointManager.TrackCompleted += OnTrackCompleted;
        }

        SetupMotorAudio();
        SetupSpatialAudio();
        ApplySpatialAudioMode();
        lastSpatialAudioMode = spatialAudioMode;

        if (gameManager != null)
            lastState = gameManager.State;
    }

    private void OnDestroy()
    {
        if (checkpointManager != null)
        {
            checkpointManager.CheckpointReached -= OnCheckpointReached;
            checkpointManager.TrackCompleted -= OnTrackCompleted;
        }
    }

    private void Update()
    {
        HandleSpatialAudioToggle();

        UpdateDroneSpeed();
        UpdateMotorAudio();
        UpdateStateSounds();
        UpdateWaypointSpatialAudio();
    }

    private void SetupMotorAudio()
    {
        if (motorSource == null || motorLoopClip == null) return;

        motorSource.clip = motorLoopClip;
        motorSource.loop = true;
        motorSource.playOnAwake = false;

        if (!motorSource.isPlaying)
            motorSource.Play();
    }

    private void SetupSpatialAudio()
    {
        if (waypointSpatialSource == null) return;

        waypointSpatialSource.loop = false;
        waypointSpatialSource.playOnAwake = false;

        // Full 3D spatial audio.
        waypointSpatialSource.spatialBlend = 1f;
        waypointSpatialSource.rolloffMode = AudioRolloffMode.Logarithmic;
        waypointSpatialSource.minDistance = 5f;
        waypointSpatialSource.maxDistance = 200f;
    }

   private void ApplySpatialAudioMode()
    {
        // Wayfinding EC ON:
        // spatial audio ON, visual wayfinding OFF.
        // Wayfinding EC OFF:
        // spatial audio OFF, visual wayfinding ON.
        bool visualWayfindingEnabled = !spatialAudioMode;

        if (wayfindingArrowObject != null)
            wayfindingArrowObject.SetActive(visualWayfindingEnabled);

        if (beaconObject != null)
            beaconObject.SetActive(visualWayfindingEnabled);

        if (beaconManager != null)
            beaconManager.SetBeaconVisible(visualWayfindingEnabled);

        if (waypointSpatialSource != null)
        {
            waypointSpatialSource.Stop();
            waypointSpatialSource.enabled = spatialAudioMode;
        }

        Debug.Log($"Wayfinding EC mode: {(spatialAudioMode ? "ON - spatial audio, visuals hidden" : "OFF - visual arrow/beacon, spatial audio hidden")}");
    }

    private void HandleSpatialAudioToggle()
    {
        // Keyboard toggle for editor/laptop testing.
        if (Input.GetKeyDown(toggleSpatialAudioKey))
        {
            SetSpatialAudioMode(!spatialAudioMode);
        }

        // Also allow live Inspector checkbox changes to apply immediately.
        if (spatialAudioMode != lastSpatialAudioMode)
        {
            ApplySpatialAudioMode();
            lastSpatialAudioMode = spatialAudioMode;
        }
    }

    private void UpdateDroneSpeed()
    {
        if (droneTransform == null)
        {
            currentSpeed = 0f;
            return;
        }

        currentSpeed = Vector3.Distance(droneTransform.position, lastDronePosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastDronePosition = droneTransform.position;
    }

    private void UpdateMotorAudio()
    {
        if (motorSource == null) return;

        bool shouldPlayMotor =
            gameManager != null &&
            (gameManager.State == GameManager.GameState.Racing ||
             gameManager.State == GameManager.GameState.Crashed);

        if (!shouldPlayMotor)
        {
            motorSource.volume = Mathf.Lerp(motorSource.volume, 0f, Time.deltaTime * 4f);
            return;
        }

        if (!motorSource.isPlaying)
            motorSource.Play();

        float speed01 = Mathf.Clamp01(currentSpeed / maxSpeedForPitch);
        motorSource.pitch = Mathf.Lerp(minMotorPitch, maxMotorPitch, speed01);
        motorSource.volume = Mathf.Lerp(motorSource.volume, 1f, Time.deltaTime * 4f);
    }

    private void UpdateStateSounds()
    {
        if (gameManager == null) return;

        GameManager.GameState currentState = gameManager.State;

        if (currentState == lastState) return;

        if (currentState == GameManager.GameState.Countdown)
        {
            if (countdownRoutine != null)
                StopCoroutine(countdownRoutine);

            countdownRoutine = StartCoroutine(PlayCountdownAligned());
        }
        else if (currentState == GameManager.GameState.Crashed)
        {
            PlaySfx(crashClip);
        }

        lastState = currentState;
    }

    private IEnumerator PlayCountdownAligned()
    {
        if (gameManager == null || countdownClip == null)
            yield break;

        // Example: game countdown = 5 sec, sound is 3 sec, so wait 2 sec.
        float waitTime = Mathf.Max(0f, gameManager.countdownDuration - countdownSoundLeadTime);

        yield return new WaitForSeconds(waitTime);

        if (gameManager.State == GameManager.GameState.Countdown)
            PlaySfx(countdownClip);
    }

    private void UpdateWaypointSpatialAudio()
    {
        if (!spatialAudioMode) return;
        if (checkpointManager == null || waypointSpatialSource == null || waypointBeepClip == null) return;
        if (gameManager == null || gameManager.State != GameManager.GameState.Racing) return;

        if (!checkpointManager.TryGetCurrentTargetPosition(out Vector3 targetPos))
            return;

        waypointSpatialSource.transform.position = targetPos;

        beepTimer -= Time.deltaTime;
        if (beepTimer <= 0f)
        {
            waypointSpatialSource.PlayOneShot(waypointBeepClip);
            beepTimer = waypointBeepInterval;
        }
    }

    private void OnCheckpointReached(int index)
    {
        PlaySfx(checkpointClip);
    }

    private void OnTrackCompleted()
    {
        PlaySfx(finishClip);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void SetSpatialAudioMode(bool enabled)
    {
        spatialAudioMode = enabled;
        ApplySpatialAudioMode();
        lastSpatialAudioMode = spatialAudioMode;
    }
}