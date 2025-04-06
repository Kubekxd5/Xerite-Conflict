using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float edgeScrollThreshold = 10f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 50f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 100f;
    [SerializeField] private float minZoom = 10f;
    [SerializeField] private float maxZoom = 80f;

    [Header("Pitch (Camera Tilt)")]
    [SerializeField] private Transform cameraPivot; // Assign the camera itself here in inspector
    [SerializeField] private float pitchSpeed = 40f;
    [SerializeField] private float minPitch = 30f;
    [SerializeField] private float maxPitch = 80f;

    private Vector3 inputDirection;

    private void Update()
    {
        inputDirection = Vector3.zero;

        HandleEdgeScrolling();
        HandleKeyboardMovement();
        HandleRotationAndPitch();
        HandleZoom();
        MoveCamera();
    }

    private void HandleEdgeScrolling()
    {
        Vector3 mousePos = Input.mousePosition;

        if (mousePos.x >= Screen.width - edgeScrollThreshold)
            inputDirection += Vector3.right;
        else if (mousePos.x <= edgeScrollThreshold)
            inputDirection += Vector3.left;

        if (mousePos.y >= Screen.height - edgeScrollThreshold)
            inputDirection += Vector3.forward;
        else if (mousePos.y <= edgeScrollThreshold)
            inputDirection += Vector3.back;
    }

    private void HandleKeyboardMovement()
    {
        if (Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.RightShift))
            inputDirection += Vector3.forward;
        if (Input.GetKey(KeyCode.DownArrow) && !Input.GetKey(KeyCode.RightShift))
            inputDirection += Vector3.back;
        if (Input.GetKey(KeyCode.LeftArrow) && !Input.GetKey(KeyCode.RightShift))
            inputDirection += Vector3.left;
        if (Input.GetKey(KeyCode.RightArrow) && !Input.GetKey(KeyCode.RightShift))
            inputDirection += Vector3.right;
    }

    private void HandleRotationAndPitch()
    {
        if (!Input.GetKey(KeyCode.RightShift)) return;

        // Horizontal camera rotation (Y axis)
        if (Input.GetKey(KeyCode.LeftArrow))
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime, Space.World);
        if (Input.GetKey(KeyCode.RightArrow))
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Vertical camera pitch (X axis)
        if (Input.GetKey(KeyCode.UpArrow))
            AdjustPitch(-pitchSpeed * Time.deltaTime);
        if (Input.GetKey(KeyCode.DownArrow))
            AdjustPitch(pitchSpeed * Time.deltaTime);
    }

    private void AdjustPitch(float delta)
    {
        Vector3 currentEuler = cameraPivot.localEulerAngles;

        // Normalize X angle to [-180, 180]
        float currentPitch = currentEuler.x;
        if (currentPitch > 180f) currentPitch -= 360f;

        float newPitch = Mathf.Clamp(currentPitch + delta, minPitch, maxPitch);

        cameraPivot.localEulerAngles = new Vector3(newPitch, currentEuler.y, currentEuler.z);
    }

    private void HandleZoom()
    {
        float scrollDelta = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            float targetY = transform.position.y - scrollDelta * zoomSpeed * Time.deltaTime;
            targetY = Mathf.Clamp(targetY, minZoom, maxZoom);

            Vector3 pos = transform.position;
            pos.y = targetY;
            transform.position = pos;
        }
    }

    private void MoveCamera()
    {
        Vector3 direction = transform.TransformDirection(inputDirection.normalized);
        direction.y = 0; // Prevent vertical drifting
        transform.position += direction * moveSpeed * Time.deltaTime;
    }
}
