using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Interface for units that can participate in formations.
/// Provides a clean way for FormationManager to interact with units.
/// </summary>
public interface IFormationMember
{
    /// <summary>
    /// Check if the unit is currently moving
    /// </summary>
    bool IsMoving();

    /// <summary>
    /// Check if the unit has reached its destination
    /// </summary>
    bool HasReachedDestination();

    /// <summary>
    /// Get the NavMeshAgent component
    /// </summary>
    NavMeshAgent GetAgent();

    /// <summary>
    /// Get the unit's default movement speed
    /// </summary>
    float GetDefaultSpeed();

    /// <summary>
    /// Check if the unit is currently in formation
    /// </summary>
    bool IsInFormation();

    /// <summary>
    /// Get the unit's assigned index in the formation
    /// </summary>
    int GetFormationIndex();

    /// <summary>
    /// Get the unit's offset position in the formation
    /// </summary>
    Vector3 GetFormationOffset();

    /// <summary>
    /// Check if unit is matching speed with group
    /// </summary>
    bool IsMatchingSpeed();

    /// <summary>
    /// Command the unit to move to a position
    /// </summary>
    void CmdMove(Vector3 position);

    /// <summary>
    /// Command the unit to move as part of a formation
    /// </summary>
    void CmdMoveInFormation(Vector3 centerPosition, Vector3 offset, int index, float formationSpacing, float formationTightness, bool matchSpeed);

    /// <summary>
    /// Command the unit to stop moving
    /// </summary>
    void CmdStop();

    /// <summary>
    /// Command the unit to match speed with group
    /// </summary>
    void CmdMatchSpeedWithGroup(float minSpeed);

    /// <summary>
    /// Command to set unit speed
    /// </summary>
    void CmdSetSpeed(float speed);
}