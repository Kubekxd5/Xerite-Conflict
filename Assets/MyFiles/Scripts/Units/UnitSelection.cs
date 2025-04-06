using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UnitSelection : MonoBehaviour
{
    [SerializeField] private RectTransform selectionBoxUI;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask unitMask;

    private Camera _mainCamera;
    private Vector2 startPos;
    private List<Unit> selectedUnits = new List<Unit>();
    private bool isDragging = false;
    private const float DragThreshold = 5f;

    private void Start()
    {
        _mainCamera = Camera.main;
        selectionBoxUI.gameObject.SetActive(false);
    }

    private void Update()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleLeftMouseDown(mousePosition);
        }

        if (Mouse.current.leftButton.isPressed && isDragging)
        {
            UpdateSelectionBox(startPos, mousePosition);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            HandleLeftMouseUp(mousePosition);
        }
    }
    
    private void HandleLeftMouseDown(Vector2 mousePosition)
    {
        startPos = mousePosition;
        isDragging = true;
        selectionBoxUI.gameObject.SetActive(true);

        if (!Input.GetKey(KeyCode.LeftShift))
        {
            DeselectAll();
        }
    }

    private void HandleLeftMouseUp(Vector2 mousePosition)
    {
        selectionBoxUI.gameObject.SetActive(false);

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
        Ray ray = _mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, unitMask))
        {
            if (hit.collider.TryGetComponent<Unit>(out Unit unit) && unit.isOwned)
            {
                selectedUnits.Add(unit);
                unit.Select();
            }
        }
    }

    private void UpdateSelectionBox(Vector2 start, Vector2 end)
    {
        Vector2 lowerLeft = new Vector2(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y)
        );

        Vector2 size = new Vector2(
            Mathf.Abs(start.x - end.x),
            Mathf.Abs(start.y - end.y)
        );

        selectionBoxUI.anchoredPosition = lowerLeft;
        selectionBoxUI.sizeDelta = size;
    }

    private void SelectUnitsInBox(Vector2 screenStart, Vector2 screenEnd)
    {
        Vector2 lower = Vector2.Min(screenStart, screenEnd);
        Vector2 upper = Vector2.Max(screenStart, screenEnd);

        foreach (Unit unit in FindObjectsOfType<Unit>())
        {
            if (!unit.isOwned) continue;

            Vector3 screenPos = _mainCamera.WorldToScreenPoint(unit.transform.position);
            if (screenPos.z < 0) continue; // Behind camera

            if (screenPos.x >= lower.x && screenPos.x <= upper.x &&
                screenPos.y >= lower.y && screenPos.y <= upper.y)
            {
                selectedUnits.Add(unit);
                unit.Select();
            }
        }
    }

    private void DeselectAll()
    {
        foreach (var unit in selectedUnits)
        {
            unit.Deselect();
        }

        selectedUnits.Clear();
    }
}
