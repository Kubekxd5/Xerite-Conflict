using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float edgeScrollThreshold = 10f;
    [SerializeField] private Vector2 moveLimitsX = new Vector2(-100f, 100f);
    [SerializeField] private Vector2 moveLimitsZ = new Vector2(-100f, 100f);

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 50f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 100f;
    [SerializeField] private float minZoom = 10f;
    [SerializeField] private float maxZoom = 80f;

    [Header("Pitch (Camera Tilt)")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float pitchSpeed = 40f;
    [SerializeField] private float minPitch = 30f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false; // Przycisk do w³¹czania/wy³¹czania debugów

    private Vector3 inputDirection;

    private void Start()
    {
        // Null check dla cameraPivot na starcie
        if (cameraPivot == null)
        {
            Debug.LogError("Camera Pivot nie zosta³ przypisany w inspektorze!");
            enabled = false; // Wy³¹cz skrypt, aby unikn¹æ dalszych b³êdów
        }
        else if (enableDebugLogs)
        {
            Debug.Log("RTSCameraController zosta³ zainicjalizowany.");
        }
    }

    private void Update()
    {
        inputDirection = Vector3.zero;

        try
        {
            HandleEdgeScrolling();
            HandleKeyboardMovement();
            HandleRotationAndPitch();
            HandleZoom();
            MoveCamera();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Wyst¹pi³ b³¹d w Update: {e.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(e);
            }
        }
    }

    private void HandleEdgeScrolling()
    {
        try
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
        catch (System.Exception e)
        {
            Debug.LogError($"Wyst¹pi³ b³¹d w HandleEdgeScrolling: {e.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(e);
            }
        }
    }

    private void HandleKeyboardMovement()
    {
        try
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
        catch (System.Exception e)
        {
            Debug.LogError($"Wyst¹pi³ b³¹d w HandleKeyboardMovement: {e.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(e);
            }
        }
    }

    private void HandleRotationAndPitch()
    {
        try
        {
            if (cameraPivot == null) return; // Dodatkowy null check w trakcie dzia³ania

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

            if (enableDebugLogs && (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)))
            {
                Debug.Log("Obrót i/lub pochylenie kamery.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Wyst¹pi³ b³¹d w HandleRotationAndPitch: {e.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(e);
            }
        }
    }

    private void AdjustPitch(float delta)
    {
        try
        {
            if (cameraPivot == null) return;

            Vector3 currentEuler = cameraPivot.localEulerAngles;

            // Normalize X angle to [-180, 180]
            float currentPitch = currentEuler.x;
            if (currentPitch > 180f) currentPitch -= 360f;

            float newPitch = Mathf.Clamp(currentPitch + delta, minPitch, maxPitch);

            cameraPivot.localEulerAngles = new Vector3(newPitch, currentEuler.y, currentEuler.z);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Wyst¹pi³ b³¹d w AdjustPitch: {e.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(e);
            }
        }
    }

    private void HandleZoom()
    {
        try
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
        catch (System.Exception e)
        {
            Debug.LogError($"Wyst¹pi³ b³¹d w HandleZoom: {e.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(e);
            }
        }
    }

    private void MoveCamera()
    {
        try
        {
            Vector3 direction = transform.TransformDirection(inputDirection.normalized);
            direction.y = 0; // Prevent vertical drifting
            Vector3 targetPosition = transform.position + direction * moveSpeed * Time.deltaTime;

            // Ogranicz ruch w osi X
            targetPosition.x = Mathf.Clamp(targetPosition.x, moveLimitsX.x, moveLimitsX.y);
            // Ogranicz ruch w osi Z
            targetPosition.z = Mathf.Clamp(targetPosition.z, moveLimitsZ.x, moveLimitsZ.y);

            transform.position = targetPosition;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Wyst¹pi³ b³¹d w MoveCamera: {e.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(e);
            }
        }
    }
}