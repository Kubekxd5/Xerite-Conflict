using UnityEngine;
using Mirror; // <-- Add Mirror namespace

// This script should be attached to any NetworkBehaviour object
// that is meant to reveal Fog of War for its owner.
[RequireComponent(typeof(NetworkIdentity))] // Ensure it has a NetworkIdentity
public class NetworkedFogRevealer : NetworkBehaviour
{
    [Tooltip("The vision range for this revealer. Set to -1 to use the default from FogOfWarManager.")]
    [SerializeField] private float visionRange = -1f;

    // Cache the FogOfWarManager instance for performance
    private FogOfWarManager fogManager;

    private void Start()
    {
        // Try to find the manager early, though registration happens later
        fogManager = FogOfWarManager.Instance;
        if (fogManager == null)
        {
            Debug.LogError("NetworkedFogRevealer could not find a FogOfWarManager. Is one in the scene?", gameObject);
            // Don't disable yet, maybe the manager spawns later, but log the error.
        }
    }

    // This method is called on the client when this object is spawned
    // and its initial state is synchronized.
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Check if this unit is owned by the local client
        // isOwned is the property to use on the client to check for authority.
        // hasAuthority is an alias for isOwned.
        if (isOwned)
        {
            // This unit belongs to the local player on this client.
            // Register it with the FogOfWarManager so its vision reveals fog.
            if (fogManager != null)
            {
                float rangeToUse = visionRange < 0 ? fogManager.defaultVisionRange : visionRange;
                fogManager.RegisterRevealer(transform, rangeToUse);
                if (fogManager.debugMode) Debug.Log($"Client {authority} registered revealer for owned unit: {name}", gameObject);
            }
            else
            {
                // If manager was not found in Start, try finding it now just in case
                fogManager = FogOfWarManager.Instance;
                if (fogManager != null)
                {
                    float rangeToUse = visionRange < 0 ? fogManager.defaultVisionRange : visionRange;
                    fogManager.RegisterRevealer(transform, rangeToUse);
                    if (fogManager.debugMode) Debug.Log($"Client {authority} registered revealer for owned unit (found late): {name}", gameObject);
                }
                else
                {
                    Debug.LogError($"Client {authority}: FogOfWarManager not found when trying to register revealer for owned unit: {name}. Fog may not reveal correctly.", gameObject);
                }
            }
        }
    }

    // This method is called on the client when this object is destroyed or despawned
    public override void OnStopClient()
    {
        base.OnStopClient();

        // If this unit was owned by the local client, unregister it from the manager
        // No need to check isOwned here, OnStopClient is called regardless of ownership,
        // but the registration check in OnStartClient ensures we only unregister if we previously registered.
        // We can simply try to unregister.
        if (fogManager != null)
        {
            if (fogManager.debugMode) Debug.Log($"Client {authority} unregistering revealer for unit: {name}", gameObject);
            fogManager.UnregisterRevealer(transform);
        }
        else
        {
            // Try finding the manager one last time in case it was a late find scenario
            fogManager = FogOfWarManager.Instance;
            if (fogManager != null)
            {
                if (fogManager.debugMode) Debug.Log($"Client {authority} unregistering revealer for unit (found late): {name}", gameObject);
                fogManager.UnregisterRevealer(transform);
            }
            else
            {
                // This might happen if the manager is destroyed before the unit, which shouldn't
                // typically occur in a well-managed scene lifecycle.
                Debug.LogError($"Client {authority}: FogOfWarManager not found when trying to unregister revealer for unit: {name}. Potential cleanup issue.", gameObject);
            }
        }
    }

    // OnDestroy is called just before the object is destroyed.
    // OnStopClient is often sufficient for NetworkBehaviours, but OnDestroy can be a fallback
    // or used for non-network related cleanup. For unregistering, OnStopClient is preferred
    // because it aligns with the network lifecycle (object being removed from network).
    // private void OnDestroy()
    // {
    //     if (fogManager != null)
    //     {
    //          // This might cause issues if OnStopClient was already called.
    //          // Prefer OnStopClient for NetworkBehaviour lifecycle events.
    //         // fogManager.UnregisterRevealer(transform);
    //     }
    // }

    // You could add server-side logic here if needed, but for a revealer, it's typically client-side.
    // public override void OnStartServer() { }
    // public override void OnStopServer() { }
}