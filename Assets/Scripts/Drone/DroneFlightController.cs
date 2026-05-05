using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class DroneFlightController : MonoBehaviour
{
    [Header("Hand References")]
    [Tooltip("Right OVRHand — the only hand needed for this scheme.")]
    public OVRHand rightHand;

    [Header("Speed Limits")]
    [Tooltip("Max speed (m/s). Used when pinch strength is 1.0.")]
    public float maxSpeed = 25f;
    [Tooltip("Speed multiplier on top of max when in 'boost' (kept for compatibility / tuning).")]
    public float boostMultiplier = 1.5f;
    [Tooltip("How quickly the drone smooths to its target velocity (higher = snappier).")]
    public float velocitySmoothing = 5f;
    [Tooltip("How quickly the drone yaws to face its flight direction (higher = snappier).")]
    public float yawSmoothing = 3f;

    [Header("Input Thresholds")]
    [Tooltip("Pinch strength must exceed this to count as throttle (filters jitter).")]
    [Range(0f, 1f)] public float pinchDeadzone = 0.05f;
    [Tooltip("How curled all fingers must be to register a 'fist' brake. 1 = full pinch.")]
    [Range(0f, 1f)] public float fistThreshold = 0.7f;

    [Header("Runtime State (read-only)")]
    [SerializeField] private bool flightEnabled = true;
    [SerializeField] private Vector3 currentVelocity;
    [SerializeField] private float lastThrottle;
    [SerializeField] private bool lastBraking;

    private Rigidbody rb;

    public bool FlightEnabled
    {
        get => flightEnabled;
        set
        {
            flightEnabled = value;
            if (!value)
            {
                currentVelocity = Vector3.zero;
            }
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (!flightEnabled || rightHand == null || !rightHand.IsTracked)
        {
            // Decay velocity smoothly when flight disabled or hand lost.
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, velocitySmoothing * Time.fixedDeltaTime);
            ApplyMovement();
            return;
        }

        // Read pinch strength on each finger.
        float indexPinch  = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        float middlePinch = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        float ringPinch   = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
        float pinkyPinch  = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);

        // Fist = all four non-thumb fingers curled.
        bool isFist =
            indexPinch  > fistThreshold &&
            middlePinch > fistThreshold &&
            ringPinch   > fistThreshold &&
            pinkyPinch  > fistThreshold;

        lastBraking = isFist;

        if (isFist)
        {
            // Brake: stop immediately.
            currentVelocity = Vector3.zero;
            ApplyMovement();
            return;
        }

        // Throttle = right index pinch strength (the most reliable signal from OVRHand).
        float throttle = Mathf.Max(0f, indexPinch - pinchDeadzone) / (1f - pinchDeadzone);
        throttle = Mathf.Clamp01(throttle);
        lastThrottle = throttle;

        // Flight direction = palm forward direction.
        // For Meta's hand convention, the right hand's palm normal is approximately the hand's -up direction.
        // Hand forward (+Z) is the direction from wrist to fingertips; that's what we want as flight direction.
        Vector3 palmForward = rightHand.transform.forward;

        // Compute target velocity in world space.
        Vector3 targetVelocity = palmForward.normalized * (throttle * maxSpeed);

        // Smooth toward target velocity (avoids jitter).
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, velocitySmoothing * Time.fixedDeltaTime);

        ApplyMovement();
        ApplyYaw(palmForward);
    }

    private void ApplyMovement()
    {
        rb.MovePosition(rb.position + currentVelocity * Time.fixedDeltaTime);
    }

    private void ApplyYaw(Vector3 desiredForward)
    {
        // Drone yaws (around world Y) to face the hand's horizontal direction.
        Vector3 horizontalForward = desiredForward;
        horizontalForward.y = 0f;
        if (horizontalForward.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(horizontalForward, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, yawSmoothing * Time.fixedDeltaTime));
    }

    public void ResetCalibration()
    {
        currentVelocity = Vector3.zero;
    }
}