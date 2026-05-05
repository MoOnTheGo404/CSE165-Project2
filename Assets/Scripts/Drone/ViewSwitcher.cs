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
    public OVRHand leftHand;
    public OVRHand rightHand;
    public GameObject cockpit;
    public GameObject droneBody;
    public Transform cameraRig;

    [Header("Third-Person Camera Offset")]
    public Vector3 thirdPersonOffset = new Vector3(0f, 2f, -5f);

    [Header("Gesture Detection")]
    [Tooltip("How long to hold both pinches (seconds).")]
    public float gestureHoldTime = 1.0f;
    [Tooltip("Cooldown after switching to prevent rapid cycling (seconds).")]
    public float postSwitchCooldown = 1.5f;
    [Tooltip("Pinch strength threshold for 'pinching' (1 = full pinch).")]
    [Range(0f, 1f)] public float pinchThreshold = 0.7f;

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

        if (leftHand == null || rightHand == null || !leftHand.IsTracked || !rightHand.IsTracked)
        {
            gestureProgress = 0f;
            return;
        }

        float leftPinch  = leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        float rightPinch = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

        bool gestureHeld = leftPinch > pinchThreshold && rightPinch > pinchThreshold;

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
