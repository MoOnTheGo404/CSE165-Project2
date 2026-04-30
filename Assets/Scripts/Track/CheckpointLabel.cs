using UnityEngine;
using TMPro;

public class CheckpointLabel : MonoBehaviour
{
    [Tooltip("The TextMeshPro child showing the checkpoint number.")]
    public TMP_Text label;

    private Transform cameraTransform;

    private void Start()
    {
        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    public void SetNumber(int displayNumber)
    {
        if (label != null)
            label.text = displayNumber.ToString();
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            else return;
        }

        if (label == null) return;

        Vector3 toCamera = cameraTransform.position - label.transform.position;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude < 0.0001f) return;

        Quaternion worldRotation = Quaternion.LookRotation(-toCamera, Vector3.up);
        label.transform.rotation = worldRotation;
    }
}