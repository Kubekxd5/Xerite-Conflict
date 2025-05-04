using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using UnityEngine.AI; // Keep this if UnitMovement still uses NavMeshAgent when not directly controlled

/// <summary>
/// Allows direct control of a unit by the player with an FPS camera.
/// Movement uses the unit's inherent speed from UnitMovement.
/// </summary>
[RequireComponent(typeof(UnitSelection))]
public class UnitDirectControl : MonoBehaviour
{
    [Header("Control Settings")]
    [Tooltip("Key to start direct control of selected unit")]
    [SerializeField] private Key takeControlKey = Key.T;
    [Tooltip("Key to release control and return to normal unit selection")]
    [SerializeField] private Key releaseControlKey = Key.Escape;
    // directControlSpeed is no longer used for movement, as we'll use UnitMovement's speed
    // [Tooltip("Movement speed when directly controlling a unit")]
    // [SerializeField] private float directControlSpeed = 5f;
    [Tooltip("Rotation speed when directly controlling a unit")]
    [SerializeField] private float directControlRotationSpeed = 120f; // Still used for unit turning

    [Header("Camera Control")]
    [Tooltip("Offset from the unit's position for the FPS camera (should be around eye level)")]
    [SerializeField] private Vector3 fpsCameraOffset = new Vector3(0f, 1.7f, 0f);
    [Tooltip("Camera rotation sensitivity")]
    [SerializeField] private float cameraRotationSensitivity = 2f;
    [Tooltip("Vertical camera look limit (max angle up)")]
    [SerializeField] private float lookUpLimit = 80f;
    [Tooltip("Vertical camera look limit (max angle down)")]
    [SerializeField] private float lookDownLimit = -80f;


    [Header("UI")]
    [Tooltip("Optional UI element to show when in direct control mode")]
    [SerializeField] private GameObject directControlUI;
    [Tooltip("Optional text mesh to display above controlled unit")]
    [SerializeField] private GameObject controlIndicatorPrefab;

    [Header("Debugging")]
    [SerializeField] private bool debugMode = false;

    // References
    private UnitSelection unitSelection;
    private Camera mainCamera;
    private Unit controlledUnit;
    private UnitMovement controlledUnitMovement; // We need this to get the speed
    private Transform originalCameraParent;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private GameObject controlIndicator;

    // State tracking
    private bool isInDirectControl = false;
    private Vector3 inputDirection = Vector3.zero;
    private float cameraPitch = 0f; // For vertical camera rotation
    private float cameraYaw = 0f;   // For horizontal camera rotation (and unit rotation)

    private void Awake()
    {
        try
        {
            unitSelection = GetComponent<UnitSelection>();
            mainCamera = Camera.main;

            if (unitSelection == null)
            {
                Debug.LogError("UnitSelection component not found. Required for UnitDirectControl.");
                enabled = false;
                return;
            }

            if (mainCamera == null)
            {
                Debug.LogError("Main camera not found. Required for UnitDirectControl.");
                enabled = false;
                return;
            }

            // Store original camera state
            originalCameraParent = mainCamera.transform.parent;
            originalCameraPosition = mainCamera.transform.position;
            originalCameraRotation = mainCamera.transform.rotation;

            // Setup UI if available
            if (directControlUI != null)
            {
                directControlUI.SetActive(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UnitDirectControl.Awake: {e.Message}");
            enabled = false;
        }
    }

    private void Update()
    {
        try
        {
            // Check for control toggle key
            if (Keyboard.current[takeControlKey].wasPressedThisFrame && !isInDirectControl)
            {
                TryTakeControlOfSelectedUnit();
            }

            // Check for release control key
            if (Keyboard.current[releaseControlKey].wasPressedThisFrame && isInDirectControl)
            {
                ReleaseControl();
            }

            // Handle direct control if active
            if (isInDirectControl && controlledUnit != null && controlledUnitMovement != null)
            {
                HandleDirectControl();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UnitDirectControl.Update: {e.Message}");
        }
    }

    private void LateUpdate()
    {
        // Update camera position and rotation in LateUpdate for smooth follow
        if (isInDirectControl && controlledUnit != null && mainCamera != null)
        {
            // Position the camera at the unit's position with the defined offset
            mainCamera.transform.position = controlledUnit.transform.position + fpsCameraOffset;

            // Apply the camera rotation based on pitch and yaw
            mainCamera.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        }
    }

    private void TryTakeControlOfSelectedUnit()
    {
        try
        {
            List<Unit> selectedUnits = unitSelection.GetSelectedUnits();

            // Only allow direct control if exactly 1 unit is selected
            if (selectedUnits.Count != 1)
            {
                if (debugMode)
                {
                    Debug.Log($"Cannot take control: {selectedUnits.Count} units selected. Need exactly 1.");
                }
                return;
            }

            Unit unitToControl = selectedUnits[0];

            // Make sure unit is owned by local player
            if (!unitToControl.isOwned)
            {
                if (debugMode)
                {
                    Debug.Log("Cannot take control: Unit is not owned by local player.");
                }
                return;
            }

            // Make sure unit has a movement component
            UnitMovement unitMovement = unitToControl.GetComponent<UnitMovement>();
            if (unitMovement == null)
            {
                if (debugMode)
                {
                    Debug.Log("Cannot take control: Unit doesn't have UnitMovement component.");
                }
                return;
            }

            // Take control of the unit
            TakeControl(unitToControl, unitMovement);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in TryTakeControlOfSelectedUnit: {e.Message}");
        }
    }

    private void TakeControl(Unit unit, UnitMovement unitMovement)
    {
        try
        {
            // Store references
            controlledUnit = unit;
            controlledUnitMovement = unitMovement;

            // Stop any current movement via NavMeshAgent
            if (controlledUnitMovement.GetAgent().enabled)
            {
                controlledUnitMovement.CmdStop();
            }


            // Disable NavMeshAgent control temporarily
            if (controlledUnitMovement.GetComponent<UnitMovement>().enabled)
            {
                controlledUnitMovement.enabled = false; // Disable the movement script to prevent conflicts
            }


            // Enable direct control mode
            isInDirectControl = true;

            // Hide default RTS camera rig if you have one
            // You might need to adjust this based on your camera setup
            if (originalCameraParent != null && originalCameraParent.name == "RTSCameraRig") // Example name
            {
                originalCameraParent.gameObject.SetActive(false);
            }


            // Setup camera for FPS view
            if (mainCamera != null)
            {
                // Detach camera from any parent rig
                mainCamera.transform.parent = null;

                // Initialize camera pitch and yaw based on current camera rotation
                Vector3 currentEuler = mainCamera.transform.eulerAngles;
                cameraYaw = currentEuler.y;
                // Ensure pitch is within a reasonable range and handles wrap-around
                cameraPitch = currentEuler.x;
                if (cameraPitch > 180) cameraPitch -= 360; // Handle angles > 180
                cameraPitch = Mathf.Clamp(cameraPitch, lookDownLimit, lookUpLimit); // Clamp to limits


                // Position and rotate camera in LateUpdate
                // Initial positioning is done in LateUpdate
            }


            // Create control indicator
            if (controlIndicatorPrefab != null)
            {
                // Instantiate indicator not parented initially, will parent later if needed or keep separate
                controlIndicator = Instantiate(controlIndicatorPrefab, controlledUnit.transform.position + Vector3.up * 2f, Quaternion.identity);
                if (controlIndicator != null)
                {
                    controlIndicator.transform.parent = controlledUnit.transform; // Parent to unit if desired
                }
            }

            // Show UI if available
            if (directControlUI != null)
            {
                directControlUI.SetActive(true);
            }

            // Lock and hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;


            if (debugMode)
            {
                Debug.Log($"Taking direct control of unit: {controlledUnit.gameObject.name}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in TakeControl: {e.Message}");
            // Try to clean up if we fail
            ReleaseControl();
        }
    }

    private void ReleaseControl()
    {
        try
        {
            if (!isInDirectControl) return;

            // Re-enable NavMeshAgent if it exists
            if (controlledUnitMovement != null && controlledUnitMovement.TryGetComponent<NavMeshAgent>(out NavMeshAgent agent))
            {
                // Only re-enable if it was originally enabled or you want it always on after release
                // For simplicity, we'll just enable it here. You might need more complex state saving.
                agent.enabled = true;
                controlledUnitMovement.enabled = true; // Re-enable the movement script
            }


            // Reset camera to original state
            if (mainCamera != null)
            {
                mainCamera.transform.parent = originalCameraParent;
                mainCamera.transform.position = originalCameraPosition;
                mainCamera.transform.rotation = originalCameraRotation;

                // Re-enable original RTS camera rig if you hid it
                if (originalCameraParent != null && originalCameraParent.name == "RTSCameraRig") // Example name
                {
                    originalCameraParent.gameObject.SetActive(true);
                }
            }

            // Clean up indicator
            if (controlIndicator != null)
            {
                Destroy(controlIndicator);
                controlIndicator = null;
            }

            // Hide UI if available
            if (directControlUI != null)
            {
                directControlUI.SetActive(false);
            }

            // Unlock and show cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Reset control state
            isInDirectControl = false;
            controlledUnit = null;
            controlledUnitMovement = null;
            inputDirection = Vector3.zero;
            cameraPitch = 0f; // Reset pitch
            cameraYaw = 0f;   // Reset yaw


            if (debugMode)
            {
                Debug.Log("Released direct control of unit");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in ReleaseControl: {e.Message}");
        }
    }

    private void HandleDirectControl()
    {
        // Get movement input
        inputDirection = Vector3.zero;
        if (Keyboard.current[Key.W].isPressed) inputDirection.z += 1f;
        if (Keyboard.current[Key.S].isPressed) inputDirection.z -= 1f;
        if (Keyboard.current[Key.A].isPressed) inputDirection.x -= 1f;
        if (Keyboard.current[Key.D].isPressed) inputDirection.x += 1f;

        // Handle camera rotation with mouse
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * cameraRotationSensitivity;
            cameraYaw += mouseDelta.x;
            cameraPitch -= mouseDelta.y; // Invert Y for typical FPS look

            // Clamp vertical camera rotation
            cameraPitch = Mathf.Clamp(cameraPitch, lookDownLimit, lookUpLimit);

            // Note: Camera rotation is applied in LateUpdate
        }


        // Handle unit movement based on camera direction and input
        if (inputDirection != Vector3.zero)
        {
            MoveControlledUnit();
            if (controlledUnitMovement != null)
            {
                controlledUnitMovement.CmdSetRun(true); // Assume CmdSetRun controls the running animation
            }
        }
        else
        {
            // Stop running animation if not moving
            if (controlledUnitMovement != null)
            {
                controlledUnitMovement.CmdSetRun(false);
            }
        }

        // Unit rotation should follow the camera's yaw
        if (controlledUnit != null)
        {
            Quaternion targetUnitRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            controlledUnit.transform.rotation = Quaternion.RotateTowards(
               controlledUnit.transform.rotation,
               targetUnitRotation,
               directControlRotationSpeed * Time.deltaTime
           );
        }


        // Sync position with server if using networking - This part needs to interact with your UnitMovement's networking
        if (controlledUnit is NetworkBehaviour networkBehaviour)
        {
            // Instead of directly moving the transform and syncing,
            // you should ideally call a command on the controlledUnitMovement
            // or the unit itself to handle movement server-side or through Mirror's
            // NetworkTransform if configured for client authority for this mode.

            // Example (assuming UnitMovement has a command for direct movement):
            // controlledUnitMovement.CmdMoveDirect(moveDirection * GetUnitSpeed() * Time.deltaTime);
            // The GetUnitSpeed() part is crucial here.

            // If using NetworkTransform with client authority:
            // The NetworkTransform on the unit should handle syncing
            // as the client is directly moving the transform.
        }
    }

    private void MoveControlledUnit()
    {
        try
        {
            // Normalize input and adjust for camera direction (only yaw)
            Vector3 normalizedInput = inputDirection.normalized;
            Transform cameraTransform = mainCamera.transform;

            // Get forward and right vectors from the camera's yaw rotation
            Vector3 forward = Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.right;


            // Calculate movement direction relative to camera's horizontal facing
            Vector3 moveDirection = forward * normalizedInput.z + right * normalizedInput.x;
            moveDirection.y = 0; // Ensure movement is horizontal
            moveDirection.Normalize(); // Normalize after potentially zeroing Y


            if (moveDirection != Vector3.zero)
            {
                // Get the movement speed from the UnitMovement script
                float currentMoveSpeed = 0f;
                // *** IMPORTANT: Replace this line with how you get speed from UnitMovement ***
                // Example assuming a public float field 'moveSpeed':
                // currentMoveSpeed = controlledUnitMovement.moveSpeed;
                // Example assuming a public method 'GetMoveSpeed()':
                // currentMoveSpeed = controlledUnitMovement.GetMoveSpeed();
                // For now, using a placeholder or a default if speed cannot be retrieved
                if (controlledUnitMovement != null)
                {
                    // Attempt to get speed, adjust this line
                    // Assuming UnitMovement has a public float field or property named 'MoveSpeed'
                    System.Reflection.FieldInfo speedField = controlledUnitMovement.GetType().GetField("moveSpeed");
                    System.Reflection.PropertyInfo speedProperty = controlledUnitMovement.GetType().GetProperty("moveSpeed");

                    if (speedField != null && speedField.FieldType == typeof(float))
                    {
                        currentMoveSpeed = (float)speedField.GetValue(controlledUnitMovement);
                    }
                    else if (speedProperty != null && speedProperty.PropertyType == typeof(float))
                    {
                        currentMoveSpeed = (float)speedProperty.GetValue(controlledUnitMovement);
                    }
                    else
                    {
                        if (debugMode)
                        {
                            Debug.LogWarning("Could not find a 'moveSpeed' field or property in UnitMovement. Using directControlSpeed fallback.");
                        }
                        // Fallback to a default or the old directControlSpeed if UnitMovement speed isn't accessible
                        // currentMoveSpeed = directControlSpeed; // Use the old serialized field value if you still want a fallback
                        currentMoveSpeed = 5f; // Default fallback speed
                    }
                }
                else
                {
                    // Fallback if UnitMovement reference is somehow lost
                    // currentMoveSpeed = directControlSpeed; // Use the old serialized field value
                    currentMoveSpeed = 5f; // Default fallback speed
                }
                // *** END IMPORTANT SECTION ***


                // Apply movement using the unit's speed
                // If UnitMovement has a method for applying movement, call it here instead
                // controlledUnitMovement.Move(moveDirection, currentMoveSpeed * Time.deltaTime); // Example


                // Direct transform movement (ensure networking syncs this if using client authority)
                controlledUnit.transform.position += moveDirection * currentMoveSpeed * Time.deltaTime;


                // Rotation is handled in HandleDirectControl based on cameraYaw
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in MoveControlledUnit: {e.Message}");
        }
    }

    private void OnDisable()
    {
        // Make sure we release control if script is disabled
        if (isInDirectControl)
        {
            ReleaseControl();
        }
    }

    // Public methods for external access

    public bool IsInDirectControl()
    {
        return isInDirectControl;
    }

    public Unit GetControlledUnit()
    {
        return controlledUnit;
    }

    public void ForceReleaseControl()
    {
        ReleaseControl();
    }
}