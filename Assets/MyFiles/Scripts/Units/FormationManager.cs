using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class FormationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnitSelection unitSelection; // Assign your UnitSelection manager here
    [SerializeField] private LayerMask groundMask;        // Same ground mask as UnitMovement

    [Tooltip("Key to cycle through formation types")]
    [SerializeField] private Key cycleFormationKey = Key.Tab;

    [Header("Formation Settings")]
    [Tooltip("Default spacing between units in formation")]
    [SerializeField] private float formationSpacing = 1.5f;
    [Tooltip("How tight the formation should be (lower = tighter)")]
    [Range(0.5f, 3f)]
    [SerializeField] public float formationTightness = 1f;
    [Tooltip("Randomization amount for spread formation")]
    [Range(0f, 1f)]
    [SerializeField] private float spreadRandomness = 0.5f;
    [Tooltip("Current formation type to use when moving as a group")]
    [SerializeField] private FormationType currentFormation = FormationType.Rectangle;
    [Tooltip("Key to hold for formation movement")]
    [SerializeField] private Key formationKey = Key.LeftShift;

    [Header("Speed Matching Settings")]
    [Tooltip("Enable to make units match speed with the slowest in group by default")]
    [SerializeField] private bool matchSpeedWithSlowestDefault = false;
    [Tooltip("Key to hold to temporarily match speed with slowest unit")]
    [SerializeField] private Key speedMatchKey = Key.LeftAlt;
    [Tooltip("Minimum speed units will move at when speed matching")]
    [SerializeField] private float minMatchedSpeed = 1.0f;

    [Header("Debugging")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool showFormationPreview = true;
    [SerializeField] private Color formationPreviewColor = Color.green;
    [SerializeField] private float previewDisplayTime = 1.0f;

    // Formation types
    public enum FormationType
    {
        Rectangle,
        Arrow,
        Circle,
        Column,
        Row,
        SpreadAround,
        Random
    }

    private Camera mainCamera;
    private List<Vector3> previewPositions = new List<Vector3>();
    private Coroutine previewCoroutine;

    private void Awake()
    {
        unitSelection = FindFirstObjectByType<UnitSelection>();
        if (unitSelection == null)
        {
            Debug.LogError("FormationManager needs a reference to the UnitSelection manager!");
            // Optionally try to find it if not assigned
            unitSelection = FindFirstObjectByType<UnitSelection>();
            if (unitSelection == null)
            {
                enabled = false; // Disable if selection system is missing
                return;
            }
        }
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("FormationManager requires a camera tagged 'MainCamera'.");
            enabled = false;
        }
    }

    private void Update()
    {
        // Check for movement input only if the necessary components are available
        if (mainCamera == null || unitSelection == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            HandleMovementInput();
        }

        if (Keyboard.current[cycleFormationKey].wasPressedThisFrame)
        {
            CycleFormationType();
        }

        // Optional: Add input handling to change 'currentFormation'
        // e.g., using number keys to switch formation types
    }
    private void CycleFormationType()
    {
        int nextIndex = ((int)currentFormation + 1) % Enum.GetNames(typeof(FormationType)).Length;
        currentFormation = (FormationType)nextIndex;

        if (debugMode)
            Debug.Log($"Formation changed to: {currentFormation}");
    }

    private void HandleMovementInput()
    {
        List<Unit> selectedUnits = unitSelection.GetSelectedUnits();

        // Ignore if no units or only one unit is selected
        // Or ignore if the formation key isn't held down
        if (selectedUnits.Count <= 1 || !Keyboard.current[formationKey].isPressed)
        {
            // Let the individual UnitMovement handle its own input
            return;
        }

        // Perform raycast to find the target point on the ground
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundMask))
        {
            // We have multiple units selected and the formation key is pressed.
            // Initiate a formation move.
            InitiateFormationMovement(selectedUnits, hit.point);
        }
    }

    /// <summary>
    /// Orchestrates moving a list of units into a formation towards a center point.
    /// </summary>
    private void InitiateFormationMovement(List<Unit> units, Vector3 destinationCenter)
    {
        int unitCount = units.Count;
        if (unitCount == 0) return;

        // 1. Calculate Formation Offsets (Relative Positions)
        List<Vector3> baseOffsets = CalculateFormationPositions(currentFormation, unitCount);

        // 2. Determine Formation Orientation
        Vector3 currentCenter = CalculateUnitsCenter(units);
        Vector3 direction = (destinationCenter - currentCenter).normalized;

        // Prevent zero direction vector
        if (direction == Vector3.zero)
        {
            direction = CalculateAverageDirection(units);
        }

        Quaternion formationRotation = Quaternion.LookRotation(direction, Vector3.up);

        // 3. Determine if Speed Matching is needed
        bool shouldMatchSpeed = ShouldMatchSpeed();
        float minSpeed = CalculateMinimumSpeed(units);

        // 4. Assign Positions and Issue Commands
        previewPositions.Clear(); // For debug preview
        for (int i = 0; i < unitCount; i++)
        {
            Unit unit = units[i];
            IFormationMember formationMember = unit.GetComponent<IFormationMember>();

            if (formationMember != null)
            {
                // Get the base offset for this unit's index
                Vector3 baseOffset = (i < baseOffsets.Count) ? baseOffsets[i] : Vector3.zero;

                // Apply formation rotation to offset
                Vector3 rotatedOffset = formationRotation * baseOffset;

                // Calculate target position
                Vector3 targetPos = destinationCenter + rotatedOffset * formationTightness;

                // Add to preview positions for debugging
                if (showFormationPreview)
                {
                    if (NavMesh.SamplePosition(targetPos, out NavMeshHit previewHit, formationSpacing * 2f, NavMesh.AllAreas))
                    {
                        previewPositions.Add(previewHit.position);
                    }
                    else
                    {
                        previewPositions.Add(targetPos);
                    }
                }

                // Issue command to the unit through interface
                formationMember.CmdMoveInFormation(
                    destinationCenter,
                    baseOffset,
                    i,
                    formationSpacing,
                    formationTightness,
                    shouldMatchSpeed
                );

                // Set speed for group movement if needed
                if (shouldMatchSpeed)
                {
                    formationMember.CmdMatchSpeedWithGroup(minSpeed);
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning($"Selected unit {unit.name} doesn't implement IFormationMember.");
            }
        }

        if (debugMode)
        {
            Debug.Log($"Issued formation move command for {unitCount} units to {destinationCenter} " +
                      $"in {currentFormation} formation." +
                      (shouldMatchSpeed ? $" Matching speed to {minSpeed:F1}." : ""));
        }

        // Show formation preview for debugging
        if (showFormationPreview && previewPositions.Count > 0)
        {
            ShowFormationPreview();
        }
    }

    /// <summary>
    /// Calculates the average center position of all units
    /// </summary>
    private Vector3 CalculateUnitsCenter(List<Unit> units)
    {
        if (units.Count == 0) return Vector3.zero;

        Vector3 center = Vector3.zero;
        foreach (var unit in units)
        {
            center += unit.transform.position;
        }
        return center / units.Count;
    }

    /// <summary>
    /// Calculates average forward direction of units
    /// </summary>
    private Vector3 CalculateAverageDirection(List<Unit> units)
    {
        Vector3 direction = Vector3.forward;
        foreach (var unit in units)
        {
            direction += unit.transform.forward;
        }
        direction = direction.normalized;
        return direction != Vector3.zero ? direction : Vector3.forward;
    }

    /// <summary>
    /// Determines if speed matching should be enabled
    /// </summary>
    private bool ShouldMatchSpeed()
    {
        return matchSpeedWithSlowestDefault || Keyboard.current[speedMatchKey].isPressed;
    }

    /// <summary>
    /// Calculates the minimum speed among all units
    /// </summary>
    private float CalculateMinimumSpeed(List<Unit> units)
    {
        float minSpeed = float.MaxValue;

        foreach (Unit unit in units)
        {
            IFormationMember member = unit.GetComponent<IFormationMember>();
            if (member != null)
            {
                float speed = member.GetDefaultSpeed();
                if (speed < minSpeed)
                {
                    minSpeed = speed;
                }
            }
        }

        return Mathf.Max(minSpeed, minMatchedSpeed); // Enforce minimum
    }

    /// <summary>
    /// Calculates the local offset positions for the selected formation type.
    /// </summary>
    private List<Vector3> CalculateFormationPositions(FormationType type, int unitCount)
    {
        List<Vector3> offsets = new List<Vector3>();

        switch (type)
        {
            case FormationType.Rectangle:
                int columns = Mathf.CeilToInt(Mathf.Sqrt(unitCount));
                int rows = Mathf.CeilToInt((float)unitCount / columns);
                for (int i = 0; i < unitCount; i++)
                {
                    int row = i / columns;
                    int column = i % columns;
                    float x = (column - (columns - 1) / 2f) * formationSpacing;
                    float z = -(row - (rows - 1) / 2f) * formationSpacing;
                    offsets.Add(new Vector3(x, 0, z));
                }
                break;

            case FormationType.Row:
                for (int i = 0; i < unitCount; i++)
                {
                    float x = (i - (unitCount - 1) / 2f) * formationSpacing;
                    offsets.Add(new Vector3(x, 0, 0));
                }
                break;

            case FormationType.Column:
                for (int i = 0; i < unitCount; i++)
                {
                    float z = -(i - (unitCount - 1) / 2f) * formationSpacing;
                    offsets.Add(new Vector3(0, 0, z));
                }
                break;

            case FormationType.Circle:
                float radius = formationSpacing * Mathf.Sqrt(unitCount);
                for (int i = 0; i < unitCount; i++)
                {
                    float angle = (360f / unitCount) * i;
                    float rad = Mathf.Deg2Rad * angle;
                    offsets.Add(new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * radius);
                }
                break;

            case FormationType.Arrow:
                int mid = unitCount / 2;
                for (int i = 0; i < unitCount; i++)
                {
                    int offset = i - mid;
                    float z = Mathf.Abs(offset) * formationSpacing;
                    float x = offset * formationSpacing;
                    offsets.Add(new Vector3(x, 0, -z));
                }
                break;

            case FormationType.SpreadAround:
                for (int i = 0; i < unitCount; i++)
                {
                    Vector2 circle = UnityEngine.Random.insideUnitCircle * formationSpacing * unitCount * 0.2f;
                    offsets.Add(new Vector3(circle.x, 0, circle.y));
                }
                break;

            case FormationType.Random:
                for (int i = 0; i < unitCount; i++)
                {
                    Vector2 rnd = UnityEngine.Random.insideUnitCircle * formationSpacing * spreadRandomness;
                    offsets.Add(new Vector3(rnd.x, 0, rnd.y));
                }
                break;
        }

        return offsets;
    }

    /// <summary>
    /// Displays debug spheres at target formation positions
    /// </summary>
    private void ShowFormationPreview()
    {
        if (previewCoroutine != null)
            StopCoroutine(previewCoroutine);

        previewCoroutine = StartCoroutine(DrawPreviewSpheres());
    }

    private IEnumerator DrawPreviewSpheres()
    {
        float timer = previewDisplayTime;
        while (timer > 0f)
        {
            foreach (var pos in previewPositions)
            {
                Debug.DrawRay(pos + Vector3.up * 0.1f, Vector3.up * 0.5f, formationPreviewColor);
            }
            timer -= Time.deltaTime;
            yield return null;
        }
    }
}
