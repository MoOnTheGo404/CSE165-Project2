using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneFlightController : MonoBehaviour
{
    [Header("Hand References")]
    [Tooltip("OVRHand component on the LeftHandAnchor under OVRCameraRig.")]
    public OVRHand leftHand;
    [Tooltip("OVRHand component on the RightHandAnchor under OVRCameraRig.")]
    public OVRHand rightHand;

    [Header("Speed Limits")]
    [Tooltip("Max forward/back speed (m/s).")]
    public float maxForwardSpeed = 30f;
    [Tooltip("Max strafe (left/right) speed (m/s).")]
    public float maxStrafeSpeed = 15f;
    [Tooltip("Max vertical (up/down) speed (m/s).")]
    public float maxVerticalSpeed = 15f;
    [Tooltip("Max yaw rate (deg/s).")]
    public float maxYawRate = 60f;
    [Tooltip("Speed multiplier when right hand is pinching.")]
    public float boostMultiplier = 2f;

    [Header("Hand Input Mapping")]
    [Tooltip("Hand displacement below this radius (meters) is ignored. Prevents jitter drift.")]
    public float deadzoneRadius = 0.05f;
    [Tooltip("Hand displacement at or above this distance (meters) maps to full speed on that axis.")]
    public float maxDisplacement = 0.30f;

    [Header("Calibration")]
    [Tooltip("How long both palms must face down to capture neutral position (seconds).")]
    public float calibrationHoldTime = 1.0f;
    [Tooltip("Dot product threshold for 'palm facing down'. 1 = exactly down, lower = more lenient.")]
    public float palmDownThreshold = 0.7f;

    [Header("Runtime State (read-only)")]
    [SerializeField] private bool isCalibrated = false;
    [SerializeField] private bool flightEnabled = true;
    [SerializeField] private Vector3 leftHandNeutralLocal;
    [SerializeField] private Vector3 rightHandNeutralLocal;

    private Rigidbody rb;
    private Transform centerEye;
    private float calibrationProgress = 0f;

    public bool IsCalibrated => isCalibrated;
    public bool FlightEnabled
    {
        get => flightEnabled;
        set => flightEnabled = value;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Start()
    {
        
    }

    private void FixedUpdate()
    {
        if (leftHand == null || rightHand == null) return;
        if (!leftHand.IsTracked || !rightHand.IsTracked) return;

        Vector3 leftLocal  = transform.InverseTransformPoint(leftHand.transform.position);
        Vector3 rightLocal = transform.InverseTransformPoint(rightHand.transform.position);

        UpdateCalibration(leftLocal, rightLocal);

        if (!isCalibrated || !flightEnabled) return;

        Vector3 leftDelta  = leftLocal  - leftHandNeutralLocal;
        Vector3 rightDelta = rightLocal - rightHandNeutralLocal;

        float vertInput   = MapAxis(leftDelta.y);
        float strafeInput = MapAxis(leftDelta.x);

        float forwardInput = MapAxis(rightDelta.z);
        float yawInput     = MapAxis(rightDelta.x);

        bool boosting = rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        bool braking  = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (braking)
        {
            return;
        }

        float speedMul = boosting ? boostMultiplier : 1f;

        Vector3 localVelocity = new Vector3(
            strafeInput  * maxStrafeSpeed   * speedMul,
            vertInput    * maxVerticalSpeed * speedMul,
            forwardInput * maxForwardSpeed  * speedMul
        );

        Vector3 worldVelocity = transform.TransformDirection(localVelocity);

        rb.MovePosition(rb.position + worldVelocity * Time.fixedDeltaTime);

        float yawDelta = yawInput * maxYawRate * Time.fixedDeltaTime;
        Quaternion yawRotation = Quaternion.Euler(0f, yawDelta, 0f);
        rb.MoveRotation(rb.rotation * yawRotation);
    }

    private float MapAxis(float displacement)
    {
        float absD = Mathf.Abs(displacement);
        if (absD < deadzoneRadius) return 0f;

        float t = Mathf.InverseLerp(deadzoneRadius, maxDisplacement, absD);
        return Mathf.Sign(displacement) * Mathf.Clamp01(t);
    }

    private void UpdateCalibration(Vector3 leftLocal, Vector3 rightLocal)
    {
        Vector3 leftPalmDir  = -leftHand.transform.up;
        Vector3 rightPalmDir = -rightHand.transform.up;

        float leftDot  = Vector3.Dot(leftPalmDir,  Vector3.down);
        float rightDot = Vector3.Dot(rightPalmDir, Vector3.down);

        bool gestureActive = leftDot > palmDownThreshold && rightDot > palmDownThreshold;

        if (gestureActive)
        {
            calibrationProgress += Time.fixedDeltaTime;
            if (calibrationProgress >= calibrationHoldTime)
            {
                leftHandNeutralLocal  = leftLocal;
                rightHandNeutralLocal = rightLocal;
                isCalibrated = true;
                calibrationProgress = 0f;
                Debug.Log("Drone flight: calibrated. Left=" + leftHandNeutralLocal + " Right=" + rightHandNeutralLocal);
            }
        }
        else
        {
            calibrationProgress = 0f;
        }
    }

    public void ResetCalibration()
    {
        isCalibrated = false;
        calibrationProgress = 0f;
    }
}