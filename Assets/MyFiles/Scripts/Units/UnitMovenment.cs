using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Mirror;

[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(NavMeshAgent))]
public class UnitMovement : NetworkBehaviour, IFormationMember
{
    [Header("References")]
    [SerializeField] private Animator unitAnimator = null;
    [SerializeField] private NavMeshAgent agent = null;
    [SerializeField] private Transform unitTransform = null;
    [SerializeField] private LayerMask groundMask;

    [Header("Movement Settings")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float runningThreshold = 0.5f;
    [Tooltip("Default movement speed of this unit")]
    [SerializeField] public float defaultSpeed = 3.5f;

    [Header("Advanced Movement")]
    [Tooltip("Enable to make units avoid each other")]
    [SerializeField] private bool enableCollisionAvoidance = true;
    [Tooltip("Enable to make units navigate around obstacles dynamically")]
    [SerializeField] private bool enableObstacleAvoidance = true;
    [Tooltip("Enable smoothing when starting/stopping")]
    [SerializeField] private bool useMovementSmoothing = true;
    [Tooltip("Time to reach full speed")]
    [Range(0.1f, 2f)]
    [SerializeField] private float accelerationTime = 0.3f;
    [Tooltip("How quickly units adjust their speed to match others")]
    [Range(0.1f, 5f)]
    [SerializeField] private float speedMatchingRate = 1f;

    [Header("Debugging")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool showPathLines = false;
    [SerializeField] private Color debugPathColor = Color.yellow;
    [SerializeField] private float pathDisplayDuration = 3f;

    // Sync variables
    [SyncVar]
    private bool isRunning;

    [SyncVar]
    private Vector3 targetDestination;

    [SyncVar]
    private bool isMatchingSpeed = false;

    // Formation data (implementing IFormationMember)
    private bool _isInFormation = false;
    private Vector3 _formationOffset = Vector3.zero;
    private int _formationIndex = -1;

    // Cache references
    private Unit unit;
    private Camera mainCamera;
    private Vector3 previousPosition;
    private float stopDistance;

    // Movement tracking
    private bool movementComplete = false;
    private float originalSpeed;
    private float targetSpeed;

    // Pathfinding
    private NavMeshPath currentPath;
    private Coroutine pathDisplayCoroutine;

    private void Awake()
    {
        try
        {
            // Get required components
            unit = GetComponent<Unit>();

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (unitTransform == null)
            {
                unitTransform = transform;
            }

            // Store the default stop distance and speed
            stopDistance = agent.stoppingDistance;
            originalSpeed = defaultSpeed;
            agent.speed = originalSpeed;

            // Initialize path object
            currentPath = new NavMeshPath();

            // Store initial position for movement detection
            previousPosition = transform.position;

            // Configure NavMeshAgent settings based on inspector values
            ConfigureNavMeshAgent();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UnitMovement.Awake: {e.Message}");
        }
    }

    private void ConfigureNavMeshAgent()
    {
        if (agent != null)
        {
            agent.obstacleAvoidanceType = enableObstacleAvoidance ?
                ObstacleAvoidanceType.HighQualityObstacleAvoidance :
                ObstacleAvoidanceType.NoObstacleAvoidance;

            agent.avoidancePriority = enableCollisionAvoidance ? 50 : 99;
            agent.acceleration = useMovementSmoothing ? 8f / accelerationTime : 999f;
        }
    }

    #region Server Commands

    /// <summary>
    /// Command to move unit to a position
    /// </summary>
    [Command]
    public void CmdMove(Vector3 position)
    {
        try
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                if (debugMode)
                {
                    Debug.LogWarning($"Invalid destination position: {position}");
                }
                return;
            }

            targetDestination = hit.position;

            if (agent.CalculatePath(hit.position, currentPath) && currentPath.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetDestination(hit.position);
                _isInFormation = false;
                _formationIndex = -1;
                _formationOffset = Vector3.zero;
                agent.stoppingDistance = stopDistance;

                if (debugMode)
                {
                    Debug.Log($"Moving unit {gameObject.name} to {hit.position}");
                }

                if (showPathLines)
                {
                    RpcShowPath(currentPath.corners);
                }
            }
            else
            {
                if (debugMode)
                {
                    Debug.LogWarning($"Cannot find valid path to {hit.position}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdMove: {e.Message}");
        }
    }

    /// <summary>
    /// Command to move unit as part of a formation
    /// </summary>
    [Command]
    public void CmdMoveInFormation(Vector3 centerPosition, Vector3 offset, int index, float formationSpacing, float formationTightness, bool matchSpeed)
    {
        try
        {
            _isInFormation = true;
            _formationOffset = offset;
            _formationIndex = index;
            isMatchingSpeed = matchSpeed;

            Vector3 formationPosition = centerPosition + offset * formationSpacing * formationTightness;

            if (!NavMesh.SamplePosition(formationPosition, out NavMeshHit hit, formationSpacing, NavMesh.AllAreas))
            {
                if (debugMode)
                {
                    Debug.LogWarning($"Cannot find valid formation position near {formationPosition}");
                }

                // Try nearby positions
                for (float radius = formationSpacing * 0.5f; radius <= formationSpacing * 2f; radius += formationSpacing * 0.5f)
                {
                    Vector3 alternatePos = centerPosition + offset.normalized * radius;
                    if (NavMesh.SamplePosition(alternatePos, out hit, formationSpacing * 0.5f, NavMesh.AllAreas))
                    {
                        break;
                    }
                }

                if (hit.position == Vector3.zero)
                {
                    return; // Couldn't find valid position
                }
            }

            targetDestination = hit.position;
            agent.stoppingDistance = stopDistance * 0.5f;
            agent.SetDestination(hit.position);

            if (debugMode)
            {
                Debug.Log($"Moving unit {gameObject.name} in formation to {hit.position} with offset {offset}");
            }

            if (showPathLines && agent.CalculatePath(hit.position, currentPath))
            {
                RpcShowPath(currentPath.corners);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdMoveInFormation: {e.Message}");
        }
    }

    /// <summary>
    /// Command to stop unit movement
    /// </summary>
    [Command]
    public void CmdStop()
    {
        try
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            isMatchingSpeed = false;

            if (debugMode)
            {
                Debug.Log($"Stopped unit {gameObject.name}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdStop: {e.Message}");
        }
    }

    /// <summary>
    /// Command to update running state
    /// </summary>
    [Command]
    public void CmdSetRun(bool running)
    {
        isRunning = running;
    }

    /// <summary>
    /// Command to set agent speed
    /// </summary>
    [Command]
    public void CmdSetSpeed(float speed)
    {
        try
        {
            targetSpeed = speed;
            if (!useMovementSmoothing)
            {
                agent.speed = speed;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdSetSpeed: {e.Message}");
        }
    }

    /// <summary>
    /// Command to match speed with other units
    /// </summary>
    [Command]
    public void CmdMatchSpeedWithGroup(float minSpeed)
    {
        try
        {
            if (isMatchingSpeed)
            {
                targetSpeed = minSpeed;
                if (!useMovementSmoothing)
                {
                    agent.speed = minSpeed;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdMatchSpeedWithGroup: {e.Message}");
        }
    }

    #endregion

    #region Private Methods

    private void CheckMovementCompletion()
    {
        if (HasReachedDestination() && !movementComplete)
        {
            movementComplete = true;

            if (isMatchingSpeed)
            {
                CmdSetSpeed(originalSpeed);
                isMatchingSpeed = false;
            }

            CmdStop();
        }
    }

    private void UpdateMovementSpeed()
    {
        if (useMovementSmoothing && Mathf.Abs(agent.speed - targetSpeed) > 0.1f)
        {
            agent.speed = Mathf.Lerp(agent.speed, targetSpeed, Time.deltaTime * speedMatchingRate);
        }
        else
        {
            agent.speed = targetSpeed;
        }
    }

    #endregion

    #region ClientCallback

    [ClientCallback]
    private void Update()
    {
        try
        {
            UpdateAnimationState();
            UpdateMovementSpeed();

            if (!isOwned || !unit.IsSelected) { return; }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                HandleMovementInput();
            }

            CheckMovementCompletion();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in Update: {e.Message}");
        }
    }

    #endregion

    #region Client

    public override void OnStartAuthority()
    {
        try
        {
            // Get camera reference when we have authority
            mainCamera = Camera.main;

            if (mainCamera == null && debugMode)
            {
                Debug.LogWarning("Main camera not found. UnitMovement requires a camera tagged as 'MainCamera'.");
            }

            // Reset to default speed
            agent.speed = defaultSpeed;
            targetSpeed = defaultSpeed;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in OnStartAuthority: {e.Message}");
        }
    }

    [ClientRpc]
    private void RpcShowPath(Vector3[] pathPoints)
    {
        try
        {
            // Don't visualize path on non-owner clients
            if (!isOwned) return;

            // Cancel existing visualization
            if (pathDisplayCoroutine != null)
            {
                StopCoroutine(pathDisplayCoroutine);
            }

            // Start new visualization
            pathDisplayCoroutine = StartCoroutine(DisplayPathForDuration(pathPoints, pathDisplayDuration));
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in RpcShowPath: {e.Message}");
        }
    }

    private IEnumerator DisplayPathForDuration(Vector3[] pathPoints, float duration)
    {
        float endTime = Time.time + duration;

        while (Time.time < endTime)
        {
            // Draw the path in the scene view
            for (int i = 0; i < pathPoints.Length - 1; i++)
            {
                Debug.DrawLine(pathPoints[i], pathPoints[i + 1], debugPathColor);
            }

            yield return null;
        }
    }

    private void UpdateAnimationState()
    {
        try
        {
            if (unitAnimator != null)
            {
                // Check if we're moving based on actual position change, not just velocity
                float movementMagnitude = Vector3.Distance(transform.position, previousPosition) / Time.deltaTime;
                bool isMoving = movementMagnitude > runningThreshold;

                // Update running state to server
                if (isOwned && isMoving != isRunning)
                {
                    CmdSetRun(isMoving);
                }

                // Update animator
                unitAnimator.SetBool("isRunning", isRunning);

                // Store current position for next frame
                previousPosition = transform.position;

                if (isMoving)
                {
                    Vector3 direction = (agent.destination - transform.position).normalized;
                    if (direction != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(direction);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error updating animation state: {e.Message}");
        }
    }

    private void HandleMovementInput()
    {
        try
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundMask))
            {
                // Check if this is a formation move handled by FormationManager
                bool isFormationMove = Keyboard.current[Key.LeftShift].isPressed;
                UnitSelection selectionManager = FindFirstObjectByType<UnitSelection>();

                // Only issue individual move commands if this is not a formation move
                if (selectionManager == null ||
                    selectionManager.GetSelectedUnits().Count <= 1 ||
                    !isFormationMove)
                {
                    // Issue a standard move command for this single unit
                    CmdMove(hit.point);

                    // Preview movement client-side
                    if (agent != null)
                    {
                        agent.SetDestination(hit.point);
                        // Reset formation state
                        _isInFormation = false;
                        _formationIndex = -1;
                        _formationOffset = Vector3.zero;
                        agent.stoppingDistance = stopDistance;

                        // Reset speed if it was matched previously
                        if (isMatchingSpeed)
                        {
                            targetSpeed = defaultSpeed; // Client prediction
                        }
                        isMatchingSpeed = false;
                    }
                }
                // Let FormationManager handle formation moves
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling movement input in UnitMovement: {e.Message}");
        }
    }

    #endregion

    #region Public Methods (IFormationMember Implementation)

    /// <summary>
    /// Check if unit is currently moving
    /// </summary>
    public bool IsMoving()
    {
        return agent != null && !agent.isStopped && agent.velocity.magnitude > 0.1f;
    }

    /// <summary>
    /// Get the current destination
    /// </summary>
    public Vector3 GetDestination()
    {
        return agent != null ? agent.destination : transform.position;
    }

    /// <summary>
    /// Get remaining distance to destination
    /// </summary>
    public float GetRemainingDistance()
    {
        return agent != null ? agent.remainingDistance : 0f;
    }

    /// <summary>
    /// Check if unit has reached destination
    /// </summary>
    public bool HasReachedDestination()
    {
        return agent != null &&
               !agent.pathPending &&
               agent.remainingDistance <= agent.stoppingDistance &&
               (!agent.hasPath || agent.velocity.sqrMagnitude < 0.1f);
    }

    /// <summary>
    /// Get the NavMeshAgent component
    /// </summary>
    public NavMeshAgent GetAgent()
    {
        return agent;
    }

    /// <summary>
    /// Check if unit is in formation
    /// </summary>
    public bool IsInFormation()
    {
        return _isInFormation;
    }

    /// <summary>
    /// Get formation index
    /// </summary>
    public int GetFormationIndex()
    {
        return _formationIndex;
    }

    /// <summary>
    /// Get formation offset
    /// </summary>
    public Vector3 GetFormationOffset()
    {
        return _formationOffset;
    }

    /// <summary>
    /// Get unit's default speed
    /// </summary>
    public float GetDefaultSpeed()
    {
        return defaultSpeed;
    }

    /// <summary>
    /// Check if unit is matching speed
    /// </summary>
    public bool IsMatchingSpeed()
    {
        return isMatchingSpeed;
    }

    #endregion

    #region Visualization

    private void OnDrawGizmos()
    {
        if (!debugMode || !Application.isPlaying) return;

        // Draw destination point
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(agent.destination, 0.3f);

            // Draw speed info if matching
            if (isMatchingSpeed)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"Speed: {agent.speed:F1}");
#endif
            }
        }
    }

    #endregion
}