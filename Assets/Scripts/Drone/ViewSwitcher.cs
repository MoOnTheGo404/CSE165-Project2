using UnityEngine;

public class ViewSwitcher : MonoBehaviour
{
    public enum ViewMode
    {
        FirstPerson = 0,
        Cockpit = 1,
        ThirdPerson = 2
    }

    [Header("References")]
    [Tooltip("Right OVRHand (used for thumbs-up detection).")]
    public OVRHand rightHand;
    [Tooltip("Right OVRSkeleton (needed to read finger bone positions for thumbs-up).")]
    public OVRSkeleton rightSkeleton;
    [Tooltip("The Cockpit GameObject (child of Drone). Enabled in Cockpit view.")]
    public GameObject cockpit;
    [Tooltip("The DroneBody GameObject (child of Drone). Enabled in ThirdPerson view.")]
    public GameObject droneBody;
    [Tooltip("The OVRCameraRig (child of Drone). Camera position is moved between views.")]
    public Transform cameraRig;

    [Header("Third-Person Camera Offset")]
    [Tooltip("Offset from the drone in 3rd-person view (drone-local space). Negative Z = behind, positive Y = above.")]
    public Vector3 thirdPersonOffset = new Vector3(0f, 2f, -5f);

    [Header("Gesture Detection")]
    [Tooltip("How long to hold thumbs-up before cycling (seconds).")]
    public float gestureHoldTime = 0.6f;
    [Tooltip("Cooldown after switching, prevents accidental rapid cycling (seconds).")]
    public float postSwitchCooldown = 1.0f;
    [Tooltip("How extended the thumb must be (1 = fully extended, 0 = tucked).")]
    [Range(0f, 1f)] public float thumbExtendedThreshold = 0.7f;
    [Tooltip("How curled the other 4 fingers must be (1 = tightly fisted, 0 = open).")]
    [Range(0f, 1f)] public float fingersCurledThreshold = 0.6f;

    [Header("Runtime State (read-only)")]
    [SerializeField] private ViewMode currentMode = ViewMode.FirstPerson;
    [SerializeField] private float gestureProgress = 0f;
    [SerializeField] private float cooldownTimer = 0f;

    private Vector3 firstPersonRigLocalPos;
    private Quaternion firstPersonRigLocalRot;

    public ViewMode CurrentMode => currentMode;

    private void Start()
    {
        if (cameraRig != null)
        {
            firstPersonRigLocalPos = cameraRig.localPosition;
            firstPersonRigLocalRot = cameraRig.localRotation;
        }
        ApplyMode(currentMode);
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        if (rightHand == null || !rightHand.IsTracked || rightSkeleton == null)
        {
            gestureProgress = 0f;
            return;
        }

        bool gestureHeld = IsThumbsUp();

        if (gestureHeld)
        {
            gestureProgress += Time.deltaTime;
            if (gestureProgress >= gestureHoldTime)
            {
                CycleMode();
                gestureProgress = 0f;
                cooldownTimer = postSwitchCooldown;
            }
        }
        else
        {
            gestureProgress = 0f;
        }
    }

    private bool IsThumbsUp()
    {
        // Get pinch strength for each finger. Pinch strength is high when the finger curls toward the thumb.
        // For Index/Middle/Ring/Pinky, high pinch strength means curled (= fist).
        float indexCurl  = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        float middleCurl = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        float ringCurl   = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
        float pinkyCurl  = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);

        bool fingersCurled =
            indexCurl  > fingersCurledThreshold &&
            middleCurl > fingersCurledThreshold &&
            ringCurl   > fingersCurledThreshold &&
            pinkyCurl  > fingersCurledThreshold;

        // Thumb extended: the thumb's pinch strength should be LOW (not pinching).
        // OVRHand doesn't have a "thumb extended" measure directly, but if the thumb isn't pinching
        // and the other fingers are curled, that's a thumbs-up.
        float thumbCurl = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb);
        bool thumbExtended = thumbCurl < (1f - thumbExtendedThreshold);

        return fingersCurled && thumbExtended;
    }

    private void CycleMode()
    {
        int next = ((int)currentMode + 1) % 3;
        currentMode = (ViewMode)next;
        ApplyMode(currentMode);
        Debug.Log($"ViewSwitcher: switched to {currentMode}");
    }

    private void ApplyMode(ViewMode mode)
    {
        switch (mode)
        {
            case ViewMode.FirstPerson:
                if (cockpit != null) cockpit.SetActive(false);
                if (droneBody != null) droneBody.SetActive(false);
                if (cameraRig != null)
                {
                    cameraRig.localPosition = firstPersonRigLocalPos;
                    cameraRig.localRotation = firstPersonRigLocalRot;
                }
                break;

            case ViewMode.Cockpit:
                if (cockpit != null) cockpit.SetActive(true);
                if (droneBody != null) droneBody.SetActive(false);
                if (cameraRig != null)
                {
                    cameraRig.localPosition = firstPersonRigLocalPos;
                    cameraRig.localRotation = firstPersonRigLocalRot;
                }
                break;

            case ViewMode.ThirdPerson:
                if (cockpit != null) cockpit.SetActive(false);
                if (droneBody != null) droneBody.SetActive(true);
                if (cameraRig != null)
                {
                    cameraRig.localPosition = thirdPersonOffset;
                    cameraRig.localRotation = firstPersonRigLocalRot;
                }
                break;
        }
    }
}
