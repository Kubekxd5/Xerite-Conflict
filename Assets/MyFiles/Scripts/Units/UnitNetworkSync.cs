using System;
using UnityEngine;
using Mirror;
using UnityEngine.AI;

/// <summary>
/// Handles network synchronization for direct unit control
/// </summary>
[RequireComponent(typeof(UnitMovement))]
public class UnitNetworkSync : NetworkBehaviour
{
    [Header("Sync Settings")]
    [Tooltip("How frequently to send sync messages")]
    [SerializeField] private float syncInterval = 0.1f;
    [Tooltip("Distance threshold for position correction")]
    [SerializeField] private float positionThreshold = 0.1f;
    [Tooltip("Angle threshold for rotation correction (degrees)")]
    [SerializeField] private float rotationThreshold = 5f;
    [Tooltip("How quickly to lerp to correct position")]
    [SerializeField] private float positionLerpSpeed = 10f;
    [Tooltip("How quickly to slerp to correct rotation")]
    [SerializeField] private float rotationLerpSpeed = 5f;

    [Header("Debugging")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Color debugSyncColor = Color.cyan;

    // Component references
    private UnitMovement unitMovement;
    private NavMeshAgent agent;

    // Sync variables
    [SyncVar(hook = nameof(OnSyncedPositionChanged))]
    private Vector3 syncedPosition;

    [SyncVar(hook = nameof(OnSyncedRotationChanged))]
    private Quaternion syncedRotation;

    [SyncVar]
    private bool isInDirectControl = false;

    // Local variables
    private float nextSyncTime;
    private bool isCorrectingPosition = false;

    private void Awake()
    {
        try
        {
            unitMovement = GetComponent<UnitMovement>();
            agent = GetComponent<NavMeshAgent>();

            if (unitMovement == null || agent == null)
            {
                Debug.LogError("Required components missing from UnitNetworkSync");
                enabled = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UnitNetworkSync.Awake: {e.Message}");
            enabled = false;
        }
    }

    private void Update()
    {
        if (!isOwned) return;

        try
        {
            // Send position updates to server when in direct control
            if (isInDirectControl && Time.time >= nextSyncTime)
            {
                CmdSyncTransform(transform.position, transform.rotation);
                nextSyncTime = Time.time + syncInterval;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UnitNetworkSync.Update: {e.Message}");
        }
    }

    #region Server Commands

    [Command]
    public void CmdEnterDirectControl()
    {
        try
        {
            isInDirectControl = true;

            // Disable NavMeshAgent on the server
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.enabled = false;
            }

            if (debugMode)
            {
                Debug.Log($"Server: Unit {gameObject.name} entered direct control");
            }

            // Broadcast to all clients
            RpcEnterDirectControl();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdEnterDirectControl: {e.Message}");
        }
    }

    [Command]
    public void CmdExitDirectControl()
    {
        try
        {
            isInDirectControl = false;

            // Re-enable NavMeshAgent on the server
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            if (debugMode)
            {
                Debug.Log($"Server: Unit {gameObject.name} exited direct control");
            }

            // Broadcast to all clients
            RpcExitDirectControl();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdExitDirectControl: {e.Message}");
        }
    }

    [Command]
    public void CmdSyncTransform(Vector3 position, Quaternion rotation)
    {
        try
        {
            // Update the SyncVars, which will trigger the hooks on clients
            syncedPosition = position;
            syncedRotation = rotation;

            // Update the server representation directly
            if (isInDirectControl)
            {
                transform.position = position;
                transform.rotation = rotation;
            }

            if (debugMode)
            {
                Debug.DrawLine(transform.position, transform.position + Vector3.up * 2f, debugSyncColor, syncInterval);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CmdSyncTransform: {e.Message}");
        }
    }

    #endregion

    #region Client RPCs

    [ClientRpc]
    private void RpcEnterDirectControl()
    {
        try
        {
            // Skip on the authority client (owner) - they're already handling this
            if (isOwned) return;

            // Non-authority clients need to disable their NavMeshAgent
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.enabled = false;
            }

            if (debugMode)
            {
                Debug.Log($"Client: Unit {gameObject.name} entered direct control");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in RpcEnterDirectControl: {e.Message}");
        }
    }

    [ClientRpc]
    private void RpcExitDirectControl()
    {
        try
        {
            // Skip on the authority client (owner) - they're already handling this
            if (isOwned) return;

            // Non-authority clients need to re-enable their NavMeshAgent
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            if (debugMode)
            {
                Debug.Log($"Client: Unit {gameObject.name} exited direct control");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in RpcExitDirectControl: {e.Message}");
        }
    }

    #endregion

    #region SyncVar Hooks

    private void OnSyncedPositionChanged(Vector3 oldPosition, Vector3 newPosition)
    {
        try
        {
            // Skip corrections on the authority client (owner)
            if (isOwned) return;

            // Apply the position update on clients
            StartPositionCorrection(newPosition);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in OnSyncedPositionChanged: {e.Message}");
        }
    }

    private void OnSyncedRotationChanged(Quaternion oldRotation, Quaternion newRotation)
    {
        try
        {
            // Skip corrections on the authority client (owner)
            if (isOwned) return;

            // Apply the rotation update on clients
            transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * rotationLerpSpeed);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in OnSyncedRotationChanged: {e.Message}");
        }
    }

    #endregion

    #region Position Correction

    private void StartPositionCorrection(Vector3 targetPosition)
    {
        try
        {
            // Only correct if the error is significant
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > positionThreshold)
            {
                if (debugMode)
                {
                    Debug.DrawLine(transform.position, targetPosition, Color.red, syncInterval);
                }

                isCorrectingPosition = true;
                StartCoroutine(CorrectPosition(targetPosition));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in StartPositionCorrection: {e.Message}");
        }
    }

    private System.Collections.IEnumerator CorrectPosition(Vector3 targetPosition)
    {
        float elapsedTime = 0;
        Vector3 startPosition = transform.position;

        while (elapsedTime < syncInterval && Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / syncInterval * positionLerpSpeed);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        isCorrectingPosition = false;
    }

    #endregion

    // Public methods for external access

    public bool IsUnderDirectControl()
    {
        return isInDirectControl;
    }
}