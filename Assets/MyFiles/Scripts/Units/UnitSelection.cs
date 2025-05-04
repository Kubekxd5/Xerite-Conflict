using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(UnitGroupManager))]
public class UnitSelection : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform selectionBoxUI;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask unitMask;

    [Header("Debugging")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Color debugBoxColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] private Color debugRayColor = Color.red;
    [SerializeField] private float debugRayDuration = 1f;

    private Camera _mainCamera;
    private Vector2 startPos;
    public List<Unit> selectedUnits = new List<Unit>();
    private bool isDragging = false;
    private const float DragThreshold = 5f;
    private UnitGroupManager _groupManager;

    // Action mapping for easier testing and configuration
    private InputAction _leftMouseAction;
    private InputAction _rightMouseAction;
    private InputAction _shiftAction;
    private InputAction _ctrlAction;

    private void Awake()
    {
        try
        {
            _groupManager = GetComponent<UnitGroupManager>();

            // Initialize input actions
            _leftMouseAction = new InputAction("LeftMouse", binding: "<Mouse>/leftButton");
            _rightMouseAction = new InputAction("RightMouse", binding: "<Mouse>/rightButton");
            _shiftAction = new InputAction("Shift", binding: "<Keyboard>/leftShift");
            _ctrlAction = new InputAction("Ctrl", binding: "<Keyboard>/leftCtrl");

            _leftMouseAction.Enable();
            _rightMouseAction.Enable();
            _shiftAction.Enable();
            _ctrlAction.Enable();
        }
        catch (Exception e)
        {
            Debug.LogError($"UnitSelection initialization error: {e.Message}");
        }
    }

    private void Start()
    {
        try
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("Main camera not found. UnitSelection requires a camera tagged as 'MainCamera'.");
            }

            if (selectionBoxUI == null)
            {
                Debug.LogError("Selection box UI is not assigned in the inspector.");
            }
            else
            {
                selectionBoxUI.gameObject.SetActive(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"UnitSelection Start error: {e.Message}");
        }
    }

    private void OnEnable()
    {
        // Subscribe to group manager events if needed
        if (_groupManager != null)
        {
            _groupManager.OnGroupSelected += HandleGroupSelected;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        if (_groupManager != null)
        {
            _groupManager.OnGroupSelected -= HandleGroupSelected;
        }

        // Disable input actions
        _leftMouseAction?.Disable();
        _rightMouseAction?.Disable();
        _shiftAction?.Disable();
        _ctrlAction?.Disable();
    }

    private void Update()
    {
        try
        {
            if (_mainCamera == null) return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (_leftMouseAction.WasPressedThisFrame())
            {
                HandleLeftMouseDown(mousePosition);
            }

            if (_leftMouseAction.IsPressed() && isDragging)
            {
                UpdateSelectionBox(startPos, mousePosition);
            }

            if (_leftMouseAction.WasReleasedThisFrame())
            {
                HandleLeftMouseUp(mousePosition);
            }

            // Check for Ctrl+Number to save current selection as group
            if (_ctrlAction.IsPressed())
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (Keyboard.current[Key.Digit1 + i - 1].wasPressedThisFrame)
                    {
                        SaveSelectionAsGroup(i);
                        if (debugMode) Debug.Log($"Saved selection to group {i}");
                    }
                }
            }
            // Check for Number key to select a group
            else
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (Keyboard.current[Key.Digit1 + i - 1].wasPressedThisFrame)
                    {
                        SelectGroup(i);
                        if (debugMode) Debug.Log($"Selected group {i}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UnitSelection.Update: {e.Message}");
        }
    }

    private void HandleLeftMouseDown(Vector2 mousePosition)
    {
        startPos = mousePosition;
        isDragging = true;

        if (selectionBoxUI != null)
        {
            selectionBoxUI.gameObject.SetActive(true);
        }

        // Don't deselect if shift is held
        if (!_shiftAction.IsPressed())
        {
            DeselectAll();
        }
    }

    private void HandleLeftMouseUp(Vector2 mousePosition)
    {
        if (selectionBoxUI != null)
        {
            selectionBoxUI.gameObject.SetActive(false);
        }

        float dragDistance = Vector2.Distance(startPos, mousePosition);

        if (dragDistance > DragThreshold)
        {
            SelectUnitsInBox(startPos, mousePosition);
        }
        else
        {
            TrySelectSingleUnit(mousePosition);
        }

        isDragging = false;
    }

    private void TrySelectSingleUnit(Vector2 screenPos)
    {
        try
        {
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);

            if (debugMode)
            {
                Debug.DrawRay(ray.origin, ray.direction * 100f, debugRayColor, debugRayDuration);
            }

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, unitMask))
            {
                if (hit.collider.TryGetComponent<Unit>(out Unit unit) && unit.isOwned)
                {
                    selectedUnits.Add(unit);
                    unit.Select();

                    if (debugMode)
                    {
                        Debug.Log($"Selected unit: {unit.gameObject.name}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error selecting unit: {e.Message}");
        }
    }

    private void UpdateSelectionBox(Vector2 start, Vector2 end)
    {
        try
        {
            Vector2 lowerLeft = new Vector2(
                Mathf.Min(start.x, end.x),
                Mathf.Min(start.y, end.y)
            );

            Vector2 size = new Vector2(
                Mathf.Abs(start.x - end.x),
                Mathf.Abs(start.y - end.y)
            );

            if (selectionBoxUI != null)
            {
                selectionBoxUI.anchoredPosition = lowerLeft;
                selectionBoxUI.sizeDelta = size;
            }

            if (debugMode)
            {
                // Draw debug box in scene view
                Vector3 center = new Vector3(lowerLeft.x + size.x / 2, lowerLeft.y + size.y / 2, 0);
                Vector3 extents = new Vector3(size.x / 2, size.y / 2, 0);
                Debug.DrawLine(
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x, lowerLeft.y, 10)),
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x + size.x, lowerLeft.y, 10)),
                    debugBoxColor
                );
                Debug.DrawLine(
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x + size.x, lowerLeft.y, 10)),
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x + size.x, lowerLeft.y + size.y, 10)),
                    debugBoxColor
                );
                Debug.DrawLine(
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x + size.x, lowerLeft.y + size.y, 10)),
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x, lowerLeft.y + size.y, 10)),
                    debugBoxColor
                );
                Debug.DrawLine(
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x, lowerLeft.y + size.y, 10)),
                    _mainCamera.ScreenToWorldPoint(new Vector3(lowerLeft.x, lowerLeft.y, 10)),
                    debugBoxColor
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error updating selection box: {e.Message}");
        }
    }

    private void SelectUnitsInBox(Vector2 screenStart, Vector2 screenEnd)
    {
        try
        {
            Vector2 lower = Vector2.Min(screenStart, screenEnd);
            Vector2 upper = Vector2.Max(screenStart, screenEnd);

            Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            int selectedCount = 0;

            foreach (Unit unit in allUnits)
            {
                if (!unit.isOwned) continue;

                Vector3 screenPos = _mainCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPos.z < 0) continue; // Behind camera

                if (screenPos.x >= lower.x && screenPos.x <= upper.x &&
                    screenPos.y >= lower.y && screenPos.y <= upper.y)
                {
                    selectedUnits.Add(unit);
                    unit.Select();
                    selectedCount++;
                }
            }

            if (debugMode && selectedCount > 0)
            {
                Debug.Log($"Selected {selectedCount} units in box selection");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error selecting units in box: {e.Message}");
        }
    }

    private void DeselectAll()
    {
        try
        {
            foreach (var unit in selectedUnits)
            {
                if (unit != null)
                {
                    unit.Deselect();
                }
            }

            selectedUnits.Clear();

            if (debugMode)
            {
                Debug.Log("Deselected all units");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error deselecting units: {e.Message}");
        }
    }

    // Group Management Methods

    private void SaveSelectionAsGroup(int groupNumber)
    {
        if (_groupManager != null && selectedUnits.Count > 0)
        {
            _groupManager.SaveGroup(groupNumber, new List<Unit>(selectedUnits));
        }
    }

    private void SelectGroup(int groupNumber)
    {
        if (_groupManager != null)
        {
            _groupManager.SelectGroup(groupNumber);
        }
    }

    private void HandleGroupSelected(List<Unit> groupUnits)
    {
        try
        {
            // Deselect current selection first
            DeselectAll();

            // Select all units in the group
            foreach (Unit unit in groupUnits)
            {
                if (unit != null && unit.isOwned)
                {
                    selectedUnits.Add(unit);
                    unit.Select();
                }
            }

            if (debugMode)
            {
                Debug.Log($"Selected group with {groupUnits.Count} units");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling group selection: {e.Message}");
        }
    }

    // Public methods that might be useful for external classes

    public List<Unit> GetSelectedUnits()
    {
        return new List<Unit>(selectedUnits);
    }

    public void AddUnitToSelection(Unit unit)
    {
        if (unit != null && unit.isOwned && !selectedUnits.Contains(unit))
        {
            selectedUnits.Add(unit);
            unit.Select();
        }
    }

    public void RemoveUnitFromSelection(Unit unit)
    {
        if (unit != null && selectedUnits.Contains(unit))
        {
            selectedUnits.Remove(unit);
            unit.Deselect();
        }
    }

    // Visualization for debugging - only runs when debug mode is enabled
    private void OnDrawGizmos()
    {
        if (!debugMode || !Application.isPlaying) return;

        // Visualize selected units
        Gizmos.color = Color.green;
        foreach (var unit in selectedUnits)
        {
            if (unit != null)
            {
                Gizmos.DrawWireSphere(unit.transform.position, 1.2f);
            }
        }
    }
}