using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitGroupManager : MonoBehaviour
{
    [Header("Grouping Settings")]
    [SerializeField] private int maxGroups = 9;
    [SerializeField] private bool persistGroupsOnSceneChange = false;

    [Header("Debugging")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool showGroupNumbersInHierarchy = true;

    // Event for notifying about group selection
    public event Action<List<Unit>> OnGroupSelected;

    // Dictionary to store unit groups (1-9)
    public Dictionary<int, List<Unit>> unitGroups;

    // Reference to sound manager if you want to play sounds on group operations
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip groupSavedSound;
    [SerializeField] private AudioClip groupSelectedSound;

    private void Awake()
    {
        try
        {
            // Initialize the dictionary
            unitGroups = new Dictionary<int, List<Unit>>();

            // Initialize empty groups for all slots
            for (int i = 1; i <= maxGroups; i++)
            {
                unitGroups[i] = new List<Unit>();
            }

            if (debugMode)
            {
                Debug.Log("UnitGroupManager initialized with " + maxGroups + " group slots");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing UnitGroupManager: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        // Clean up any references
        if (!persistGroupsOnSceneChange)
        {
            ClearAllGroups();
        }
    }

    /// <summary>
    /// Saves the current list of units to the specified group number
    /// </summary>
    /// <param name="groupNumber">Group number (1-9)</param>
    /// <param name="units">Units to save in the group</param>
    public void SaveGroup(int groupNumber, List<Unit> units)
    {
        try
        {
            // Validate group number
            if (groupNumber < 1 || groupNumber > maxGroups)
            {
                Debug.LogWarning($"Invalid group number: {groupNumber}. Must be between 1 and {maxGroups}");
                return;
            }

            // Filter out null units
            List<Unit> validUnits = units.Where(u => u != null).ToList();

            // Clear existing group and add new units
            unitGroups[groupNumber] = new List<Unit>(validUnits);

            if (debugMode)
            {
                Debug.Log($"Saved {validUnits.Count} units to group {groupNumber}");

                // If we want to show group numbers in hierarchy
                if (showGroupNumbersInHierarchy)
                {
                    foreach (var unit in validUnits)
                    {
                        // Add group number to gameObject name if it doesn't have it already
                        if (!unit.gameObject.name.Contains($"[G{groupNumber}]"))
                        {
                            // Remove any existing group markers
                            string baseName = unit.gameObject.name;
                            for (int i = 1; i <= maxGroups; i++)
                            {
                                baseName = baseName.Replace($"[G{i}]", "");
                            }

                            // Add new group marker
                            unit.gameObject.name = $"{baseName.Trim()}[G{groupNumber}]";
                        }
                    }
                }
            }

            // Play sound if available
            if (audioSource != null && groupSavedSound != null)
            {
                audioSource.PlayOneShot(groupSavedSound);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving group {groupNumber}: {e.Message}");
        }
    }

    /// <summary>
    /// Selects all units in the specified group
    /// </summary>
    /// <param name="groupNumber">Group number (1-9)</param>
    public void SelectGroup(int groupNumber)
    {
        try
        {
            // Validate group number
            if (groupNumber < 1 || groupNumber > maxGroups)
            {
                Debug.LogWarning($"Invalid group number: {groupNumber}. Must be between 1 and {maxGroups}");
                return;
            }

            // Get the group
            List<Unit> group = unitGroups[groupNumber];

            // Filter out destroyed units
            List<Unit> validUnits = group.Where(u => u != null).ToList();

            // If there are any valid units, notify listeners
            if (validUnits.Count > 0)
            {
                OnGroupSelected?.Invoke(validUnits);

                if (debugMode)
                {
                    int missingUnits = group.Count - validUnits.Count;
                    Debug.Log($"Selected group {groupNumber} with {validUnits.Count} units" +
                              (missingUnits > 0 ? $" ({missingUnits} units were destroyed)" : ""));
                }

                // Update the group if some units were destroyed
                if (validUnits.Count != group.Count)
                {
                    unitGroups[groupNumber] = validUnits;
                }

                // Play sound if available
                if (audioSource != null && groupSelectedSound != null)
                {
                    audioSource.PlayOneShot(groupSelectedSound);
                }
            }
            else
            {
                if (debugMode)
                {
                    Debug.Log($"Group {groupNumber} is empty or all units have been destroyed");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error selecting group {groupNumber}: {e.Message}");
        }
    }

    /// <summary>
    /// Adds a unit to an existing group
    /// </summary>
    public void AddUnitToGroup(int groupNumber, Unit unit)
    {
        try
        {
            if (groupNumber < 1 || groupNumber > maxGroups || unit == null)
            {
                return;
            }

            if (!unitGroups[groupNumber].Contains(unit))
            {
                unitGroups[groupNumber].Add(unit);

                if (debugMode)
                {
                    Debug.Log($"Added unit {unit.gameObject.name} to group {groupNumber}");

                    if (showGroupNumbersInHierarchy)
                    {
                        // Add group number to gameObject name
                        if (!unit.gameObject.name.Contains($"[G{groupNumber}]"))
                        {
                            unit.gameObject.name = $"{unit.gameObject.name}[G{groupNumber}]";
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error adding unit to group {groupNumber}: {e.Message}");
        }
    }

    /// <summary>
    /// Removes a unit from a group
    /// </summary>
    public void RemoveUnitFromGroup(int groupNumber, Unit unit)
    {
        try
        {
            if (groupNumber < 1 || groupNumber > maxGroups || unit == null)
            {
                return;
            }

            if (unitGroups[groupNumber].Contains(unit))
            {
                unitGroups[groupNumber].Remove(unit);

                if (debugMode)
                {
                    Debug.Log($"Removed unit {unit.gameObject.name} from group {groupNumber}");

                    if (showGroupNumbersInHierarchy)
                    {
                        // Remove group number from gameObject name
                        unit.gameObject.name = unit.gameObject.name.Replace($"[G{groupNumber}]", "");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error removing unit from group {groupNumber}: {e.Message}");
        }
    }

    /// <summary>
    /// Removes a unit from all groups
    /// </summary>
    public void RemoveUnitFromAllGroups(Unit unit)
    {
        try
        {
            if (unit == null) return;

            for (int i = 1; i <= maxGroups; i++)
            {
                if (unitGroups[i].Contains(unit))
                {
                    unitGroups[i].Remove(unit);

                    if (debugMode && showGroupNumbersInHierarchy)
                    {
                        // Remove group number from gameObject name
                        unit.gameObject.name = unit.gameObject.name.Replace($"[G{i}]", "");
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log($"Removed unit {unit.gameObject.name} from all groups");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error removing unit from all groups: {e.Message}");
        }
    }

    /// <summary>
    /// Clears all units from a group
    /// </summary>
    public void ClearGroup(int groupNumber)
    {
        try
        {
            if (groupNumber < 1 || groupNumber > maxGroups)
            {
                return;
            }

            if (debugMode)
            {
                Debug.Log($"Cleared group {groupNumber} with {unitGroups[groupNumber].Count} units");

                if (showGroupNumbersInHierarchy)
                {
                    // Remove group numbers from gameObject names
                    foreach (var unit in unitGroups[groupNumber])
                    {
                        if (unit != null)
                        {
                            unit.gameObject.name = unit.gameObject.name.Replace($"[G{groupNumber}]", "");
                        }
                    }
                }
            }

            unitGroups[groupNumber].Clear();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error clearing group {groupNumber}: {e.Message}");
        }
    }

    /// <summary>
    /// Clears all groups
    /// </summary>
    public void ClearAllGroups()
    {
        try
        {
            for (int i = 1; i <= maxGroups; i++)
            {
                ClearGroup(i);
            }

            if (debugMode)
            {
                Debug.Log("Cleared all groups");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error clearing all groups: {e.Message}");
        }
    }

    /// <summary>
    /// Returns a list of all units in a group
    /// </summary>
    public List<Unit> GetGroupUnits(int groupNumber)
    {
        if (groupNumber < 1 || groupNumber > maxGroups)
        {
            Debug.LogWarning($"Invalid group number: {groupNumber}. Must be between 1 and {maxGroups}");
            return new List<Unit>();
        }

        // Return a copy to prevent external modification
        return new List<Unit>(unitGroups[groupNumber]);
    }

    /// <summary>
    /// Checks if a unit is in a specific group
    /// </summary>
    public bool IsUnitInGroup(int groupNumber, Unit unit)
    {
        if (groupNumber < 1 || groupNumber > maxGroups || unit == null)
        {
            return false;
        }

        return unitGroups[groupNumber].Contains(unit);
    }

    /// <summary>
    /// Gets all group numbers a unit belongs to
    /// </summary>
    public List<int> GetUnitGroups(Unit unit)
    {
        List<int> groups = new List<int>();

        if (unit == null) return groups;

        for (int i = 1; i <= maxGroups; i++)
        {
            if (unitGroups[i].Contains(unit))
            {
                groups.Add(i);
            }
        }

        return groups;
    }

    /// <summary>
    /// Checks if a group has any units
    /// </summary>
    public bool IsGroupEmpty(int groupNumber)
    {
        if (groupNumber < 1 || groupNumber > maxGroups)
        {
            return true;
        }

        return unitGroups[groupNumber].Count == 0;
    }

    /// <summary>
    /// Double-tap functionality: if selecting same group twice within the time window, center camera on group
    /// </summary>
    private int lastSelectedGroup = -1;
    private float lastSelectTime = 0f;
    [SerializeField] private float doubleTapTime = 0.5f;
    [SerializeField] private Transform cameraTransform;

    public void HandleGroupDoubleSelect(int groupNumber)
    {
        try
        {
            float currentTime = Time.time;

            if (groupNumber == lastSelectedGroup && (currentTime - lastSelectTime) < doubleTapTime)
            {
                // Double tap detected, center camera if possible
                CenterCameraOnGroup(groupNumber);
            }

            lastSelectedGroup = groupNumber;
            lastSelectTime = currentTime;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling double select: {e.Message}");
        }
    }

    private void CenterCameraOnGroup(int groupNumber)
    {
        try
        {
            if (cameraTransform == null || IsGroupEmpty(groupNumber))
            {
                return;
            }

            // Calculate average position of all units in the group
            Vector3 groupCenter = Vector3.zero;
            List<Unit> units = unitGroups[groupNumber];
            int validUnits = 0;

            foreach (var unit in units)
            {
                if (unit != null)
                {
                    groupCenter += unit.transform.position;
                    validUnits++;
                }
            }

            if (validUnits > 0)
            {
                groupCenter /= validUnits;

                // Move camera to position above group
                cameraTransform.position = new Vector3(
                    groupCenter.x,
                    cameraTransform.position.y,
                    groupCenter.z - 10 // Adjust based on your camera setup
                );

                if (debugMode)
                {
                    Debug.Log($"Centered camera on group {groupNumber}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error centering camera on group: {e.Message}");
        }
    }
}