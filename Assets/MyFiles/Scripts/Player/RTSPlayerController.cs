using Mirror;
using UnityEngine;

public class RTSPlayerController : NetworkBehaviour
{
    [Header("Fog of War Settings")]
    [SerializeField] private GameObject fogOfWarPrefab;
    [SerializeField] private Vector3 fogOfWarPosition = new Vector3(0, 50f, 0);
    [SerializeField] private float fogInitDelay = 0.5f;

    [Header("Debug Options")]
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private bool alwaysRevealMapForDebugging = false;

    // Fog of War component reference for this player
    private GameObject fogOfWarInstance;

    // Track initialization state
    private bool isFogInitialized = false;

    // This gets called when this player object is created on the client that owns it
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (verboseLogging) Debug.Log($"[RTSPlayerController] OnStartLocalPlayer - Starting FOW initialization");
    }
}