using UnityEngine;


public class CameraRelativeUI : MonoBehaviour
{
    [Tooltip("Local offset from the camera (X = right, Y = up, Z = forward).")]
    public Vector3 offsetFromCamera = new Vector3(0f, 0.3f, 1.5f);

    [Tooltip("If true, the UI faces the camera each frame (billboarding). If false, faces the camera's forward direction.")]
    public bool billboard = true;

    [Tooltip("Smoothing factor for position (higher = snappier). Set to 0 for no smoothing.")]
    public float positionSmoothing = 10f;

    private Transform cameraTransform;

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            else return;
        }

        // Compute target world position using the camera's local axes.
        Vector3 targetPosition = cameraTransform.position
            + cameraTransform.right   * offsetFromCamera.x
            + cameraTransform.up      * offsetFromCamera.y
            + cameraTransform.forward * offsetFromCamera.z;

        if (positionSmoothing > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothing * Time.deltaTime);
        }
        else
        {
            transform.position = targetPosition;
        }

        // Always face the camera so text reads correctly.
        if (billboard)
        {
            // Stand the UI upright, just rotate around Y to face the camera horizontally.
            Vector3 toCamera = transform.position - cameraTransform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            }
        }
        else
        {
            transform.rotation = cameraTransform.rotation;
        }
    }
}
