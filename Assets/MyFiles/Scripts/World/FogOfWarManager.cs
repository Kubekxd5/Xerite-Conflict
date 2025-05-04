using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror; // <-- Add Mirror namespace

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class FogOfWarManager : NetworkBehaviour // <-- Inherit from NetworkBehaviour
{
    public static FogOfWarManager Instance { get; private set; } // <-- Singleton Instance

    [Header("Fog Settings")]
    [Tooltip("The dimensions of the fog grid in world units")]
    [SerializeField] private Vector2 gridSize = new Vector2(100f, 100f);

    [Tooltip("Number of cells in the fog grid (higher = more detailed)")]
    [SerializeField] private Vector2Int resolution = new Vector2Int(128, 128);

    [Range(0.001f, 1.0f)]
    [Tooltip("Speed at which fog reveals areas (0.001 = very slow, 1 = instant)")]
    [SerializeField] private float revealSpeed = 0.1f;

    [Range(0.001f, 1.0f)]
    [Tooltip("Speed at which fog returns to areas out of vision (0.001 = very slow, 1 = instant)")]
    [SerializeField] private float obscureSpeed = 0.05f;

    [Range(0f, 1f)]
    [Tooltip("The minimum opacity of the fog (0 = completely transparent, 1 = opaque)")]
    [SerializeField] private float minFogOpacity = 0f;

    [Range(0f, 1f)]
    [Tooltip("The maximum opacity of the fog (0 = completely transparent, 1 = opaque)")]
    [SerializeField] private float maxFogOpacity = 0.9f;

    [Range(0f, 1f)]
    [Tooltip("The opacity of fog in previously explored areas")]
    [SerializeField] private float exploredFogOpacity = 0.5f; // New field for explored state opacity

    [Header("Vision Settings")]
    // Removed unitTagToTrack - revealers will register manually
    [Range(1f, 50f)]
    [Tooltip("Default vision range for revealers that don't specify their own")]
    [SerializeField] public float defaultVisionRange = 10f; // Still useful as a fallback

    [Range(0.01f, 1f)] // Reduced min interval for potentially smoother updates
    [Tooltip("How quickly vision updates")]
    [SerializeField] private float visionUpdateInterval = 0.2f;

    [Header("Advanced Settings")]
    [SerializeField] private bool useShaderSmoothing = true;
    [SerializeField] private bool rememberExploredAreas = true;
    [SerializeField] public bool debugMode = false;

    // Internal variables
    private Texture2D fogTexture;
    private Color[] fogColors;
    private MeshRenderer meshRenderer;
    private Material fogMaterial;
    private bool isInitialized = false;

    // Store the target alpha value for each pixel (0 = fully visible, 1 = fully fogged)
    private float[] targetAlphaValues;
    // Store whether a pixel has ever been seen
    private bool[] hasBeenExplored;


    // List of revealers currently active and owned by *this client*
    private List<FogRevealer> activeLocalRevealers = new List<FogRevealer>();

    private float lastUpdateTime = 0f;

    // Class to keep track of units that reveal fog
    public class FogRevealer
    {
        public Transform transform;
        public float visionRange;
        // isActive is handled by registration/deregistration now
        // public bool isActive; // Not needed explicitly here, transform == null means inactive/destroyed

        public FogRevealer(Transform t, float range)
        {
            transform = t;
            visionRange = range;
        }
    }

    private void Awake()
    {
        // Implement singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple FogOfWarManager instances found! Destroying this one.", gameObject);
            Destroy(gameObject);
            return; // Prevent further execution in this duplicate
        }

        // Initialization is deferred until OnStartClient
        // try
        // {
        //     Initialize(); // Moved to OnStartClient
        // }
        // catch (Exception e)
        // {
        //     Debug.LogError($"Failed to initialize Fog of War: {e.Message}");
        //     enabled = false;
        // }
    }

    public override void OnStartClient() // <-- Initialize on client start
    {
        if (isInitialized) return; // Prevent double initialization

        try
        {
            Initialize();
            Debug.Log("FogOfWarManager Initialized on Client.", gameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Fog of War on client: {e.Message}");
            // Optionally disable the component if initialization fails
            enabled = false;
        }
    }

    public override void OnStopClient() // <-- Clean up on client stop
    {
        // Clean up resources if necessary
        if (fogTexture != null)
        {
            Destroy(fogTexture);
            fogTexture = null;
        }
        if (fogMaterial != null)
        {
            Destroy(fogMaterial); // Important to destroy dynamically created materials
            fogMaterial = null;
        }
        fogColors = null;
        targetAlphaValues = null;
        hasBeenExplored = null;
        activeLocalRevealers.Clear(); // Clear the list

        isInitialized = false;
        Debug.Log("FogOfWarManager Stopped on Client.", gameObject);

        // Clear singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Server doesn't need the visual FOW logic
    public override void OnStartServer() { }
    public override void OnStopServer() { }


    private void Initialize()
    {
        // Get required components - only need MeshRenderer/Filter on the client rendering fog
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError("MeshRenderer component is missing on FogOfWarManager!", gameObject);
            // Don't throw here, just disable on this client if it's missing
            enabled = false;
            return;
        }
        // Ensure a MeshFilter exists too (likely added by RequireComponent, but good practice)
        if (GetComponent<MeshFilter>() == null)
        {
            Debug.LogError("MeshFilter component is missing on FogOfWarManager!", gameObject);
            enabled = false;
            return;
        }


        // Create fog texture
        fogTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
        fogTexture.filterMode = useShaderSmoothing ? FilterMode.Bilinear : FilterMode.Point;
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        // Initialize fog color array, target alpha, and explored state
        int totalPixels = resolution.x * resolution.y;
        fogColors = new Color[totalPixels];
        targetAlphaValues = new float[totalPixels]; // Target alpha (0-1, 0=visible, 1=fogged)
        hasBeenExplored = new bool[totalPixels];

        for (int i = 0; i < totalPixels; i++)
        {
            fogColors[i] = new Color(0, 0, 0, maxFogOpacity);
            targetAlphaValues[i] = maxFogOpacity; // Start fully fogged
            hasBeenExplored[i] = false; // Start not explored
        }

        fogTexture.SetPixels(fogColors);
        fogTexture.Apply();

        // Setup material
        try
        {
            // Use a standard unlit shader that supports transparency
            fogMaterial = new Material(Shader.Find("Unlit/Transparent"));
            if (fogMaterial == null)
            {
                Debug.LogError("Failed to find 'Unlit/Transparent' shader. Make sure it's included in your project!", gameObject);
                enabled = false;
                return;
            }
            fogMaterial.mainTexture = fogTexture;
            meshRenderer.material = fogMaterial;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error setting up fog material: {e.Message}", gameObject);
            enabled = false;
            return;
        }

        isInitialized = true;

        // Start the periodic update coroutine
        StartCoroutine(UpdateFogPeriodically());
    }

    // Use a coroutine for periodic updates, more efficient than checking Time.time in Update
    private IEnumerator UpdateFogPeriodically()
    {
        WaitForSeconds wait = new WaitForSeconds(visionUpdateInterval);

        // This coroutine should only run on the client that displays fog
        while (isInitialized && Application.isPlaying)
        {
            yield return wait;

            // The core fog calculation and texture update
            UpdateFogTexture();
        }
    }


    // Update method is now very simple, just for debug visualization
    private void Update()
    {
        // Debug visualization only in editor or if debugMode is true
        if (debugMode)
        {
            DrawDebugVisualization();
        }
    }

    private void OnDestroy()
    {
        // Ensure singleton instance is cleared if this is the one being destroyed
        if (Instance == this)
        {
            Instance = null;
        }

        // Clean up dynamically created assets if the object is destroyed
        if (fogTexture != null)
        {
            Destroy(fogTexture);
            fogTexture = null;
        }
        if (fogMaterial != null)
        {
            // Only destroy if not an instance from a shared resource etc.
            // Standard practice is to destroy materials created with 'new'
            Destroy(fogMaterial);
            fogMaterial = null;
        }
    }

    // Optimized Fog Texture Update
    private void UpdateFogTexture()
    {
        // Only run this logic on clients
        if (!isInitialized || !isClient || fogTexture == null || fogColors == null) return;

        try
        {
            int totalPixels = resolution.x * resolution.y;

            // Reset target alpha values to fully fogged
            for (int i = 0; i < totalPixels; i++)
            {
                // Keep currently explored areas at their explored opacity target
                if (rememberExploredAreas && hasBeenExplored[i])
                {
                    targetAlphaValues[i] = exploredFogOpacity;
                }
                else
                {
                    targetAlphaValues[i] = maxFogOpacity;
                }
            }

            // Iterate through revealers and mark areas as visible
            foreach (FogRevealer revealer in activeLocalRevealers)
            {
                // Ensure revealer is still valid (not destroyed)
                if (revealer.transform == null)
                    continue;

                Vector3 revealerWorldPos = revealer.transform.position;
                float visionRange = revealer.visionRange;
                float visionRangeSq = visionRange * visionRange; // Use squared distance for faster checks

                // Convert world position to grid center
                Vector2Int centerGrid = WorldToGridPosition(revealerWorldPos);

                // Calculate the grid area affected by this revealer
                // We iterate a square bounding box for simplicity, distance check handles the circle
                int minX = Mathf.Max(0, centerGrid.x - Mathf.CeilToInt(visionRange / (gridSize.x / resolution.x)));
                int maxX = Mathf.Min(resolution.x - 1, centerGrid.x + Mathf.CeilToInt(visionRange / (gridSize.x / resolution.x)));
                int minY = Mathf.Max(0, centerGrid.y - Mathf.CeilToInt(visionRange / (gridSize.y / resolution.y)));
                int maxY = Mathf.Min(resolution.y - 1, centerGrid.y + Mathf.CeilToInt(visionRange / (gridSize.y / resolution.y)));

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        int index = y * resolution.x + x;
                        Vector3 pixelWorldPos = GridToWorldPosition(x, y);

                        // Check distance using squared values
                        float distanceSq = (pixelWorldPos - revealerWorldPos).sqrMagnitude;

                        if (distanceSq < visionRangeSq)
                        {
                            // Area is within vision range
                            float distanceRatio = Mathf.Sqrt(distanceSq) / visionRange; // Recalculate normalized distance for smooth fade
                            float visibleAlpha = Mathf.Lerp(minFogOpacity, maxFogOpacity, distanceRatio);

                            // Set target alpha to the minimum (most visible) found so far for this pixel
                            targetAlphaValues[index] = Mathf.Min(targetAlphaValues[index], visibleAlpha);

                            // Mark as explored if it's currently visible
                            hasBeenExplored[index] = true;
                        }
                    }
                }
            }

            // Apply fading based on target alpha values
            for (int i = 0; i < totalPixels; i++)
            {
                float currentAlpha = fogColors[i].a;
                float targetAlpha = targetAlphaValues[i];

                float speed = (targetAlpha < currentAlpha) ? revealSpeed : obscureSpeed;

                // Only lerp if there's a significant difference or if it's not already at max fog (to allow obscureSpeed fade-in)
                if (Mathf.Abs(currentAlpha - targetAlpha) > 0.001f || targetAlpha == maxFogOpacity)
                {
                    fogColors[i].a = Mathf.Lerp(currentAlpha, targetAlpha, speed);
                }
                else if (rememberExploredAreas && hasBeenExplored[i] && currentAlpha > exploredFogOpacity + 0.001f && targetAlphaValues[i] >= maxFogOpacity)
                {
                    // Special case: Fade from currently revealed (or partially fogged) towards explored state
                    fogColors[i].a = Mathf.Lerp(currentAlpha, exploredFogOpacity, obscureSpeed);
                }
                // If not explored and not currently visible, ensure it fades towards max opacity
                else if (!hasBeenExplored[i] && targetAlphaValues[i] >= maxFogOpacity && currentAlpha < maxFogOpacity - 0.001f)
                {
                    fogColors[i].a = Mathf.Lerp(currentAlpha, maxFogOpacity, obscureSpeed);
                }
            }


            // Apply changes to texture
            fogTexture.SetPixels(fogColors);
            fogTexture.Apply();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error updating fog texture: {e.Message}");
        }
    }


    private Vector3 GridToWorldPosition(int x, int y)
    {
        // Ensure transform is valid before using it
        if (transform == null) return Vector3.zero;

        // Convert grid coordinates to world position (center of the pixel)
        float worldX = (x + 0.5f) / resolution.x * gridSize.x - (gridSize.x / 2) + transform.position.x;
        float worldZ = (y + 0.5f) / resolution.y * gridSize.y - (gridSize.y / 2) + transform.position.z;

        // Use the height of the FOW plane itself
        return new Vector3(worldX, transform.position.y, worldZ);
    }

    private Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        // Ensure transform is valid before using it
        if (transform == null) return Vector2Int.zero;

        // Convert world position to grid coordinates
        int x = Mathf.FloorToInt((worldPosition.x - transform.position.x + gridSize.x / 2) / gridSize.x * resolution.x);
        int y = Mathf.FloorToInt((worldPosition.z - transform.position.z + gridSize.y / 2) / gridSize.y * resolution.y);

        // Clamp to grid bounds
        x = Mathf.Clamp(x, 0, resolution.x - 1);
        y = Mathf.Clamp(y, 0, resolution.y - 1);

        return new Vector2Int(x, y);
    }

    private void DrawDebugVisualization()
    {
        // Only draw gizmos if the manager is active and initialized
        if (!enabled || !isInitialized) return;

        // Draw grid bounds
        Vector3 center = transform.position;
        Vector3 size = new Vector3(gridSize.x, 0.1f, gridSize.y); // Use a small height for visibility
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);

        // Draw vision ranges for each active local revealer
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        foreach (FogRevealer revealer in activeLocalRevealers)
        {
            // Ensure the revealer transform is still valid
            if (revealer.transform != null)
            {
                Gizmos.DrawWireSphere(revealer.transform.position, revealer.visionRange);
            }
        }

        // Optional: Visualize fog texture (e.g., using Gizmos.DrawGUITexture)
        // This is more complex and often done separately if needed.
    }

    // Public methods for NetworkedFogRevealer to register/unregister

    /// <summary>
    /// Registers a new fog revealer. Should be called by a NetworkedFogRevealer
    /// on a client that owns it.
    /// </summary>
    /// <param name="transform">The transform of the revealer</param>
    /// <param name="visionRange">How far this revealer can see</param>
    public void RegisterRevealer(Transform transform, float visionRange = -1)
    {
        if (transform == null)
        {
            Debug.LogWarning("Attempted to register null transform as fog revealer");
            return;
        }

        // Check if it's already in the list
        bool isAlreadyTracked = false;
        foreach (FogRevealer revealer in activeLocalRevealers)
        {
            if (revealer.transform == transform)
            {
                isAlreadyTracked = true;
                // Update vision range if it changed? Or require re-registration?
                // revealer.visionRange = visionRange < 0 ? defaultVisionRange : visionRange;
                break;
            }
        }

        if (!isAlreadyTracked)
        {
            float range = visionRange < 0 ? defaultVisionRange : visionRange;
            activeLocalRevealers.Add(new FogRevealer(transform, range));
            // Trigger an immediate update if a revealer is added/removed? Maybe not necessary with periodic update.
            // updateFogNextFrame = true;
            if (debugMode) Debug.Log($"Registered revealer: {transform.name}", transform.gameObject);
        }
    }

    /// <summary>
    /// Removes a fog revealer. Should be called by a NetworkedFogRevealer
    /// when it is destroyed or loses local ownership.
    /// </summary>
    public void UnregisterRevealer(Transform transform)
    {
        if (transform == null)
        {
            Debug.LogWarning("Attempted to unregister null transform as fog revealer");
            return;
        }

        // Remove the revealer with the matching transform
        int removedCount = activeLocalRevealers.RemoveAll(r => r.transform == transform);

        if (debugMode && removedCount > 0) Debug.Log($"Unregistered {removedCount} revealer(s): {transform.name}", transform.gameObject);

        // If a revealer was removed, the fog needs to update to potentially obscure the area
        // updateFogNextFrame = true; // Not necessary with periodic update
    }


    // Public methods for external control (potentially called by local player scripts)

    /// <summary>
    /// Reveals the entire map (for debugging or end-game scenarios).
    /// Only affects the local client's fog view.
    /// </summary>
    [ClientRpc] // Can be called on server, executes on clients
    public void RpcRevealEntireMap()
    {
        // Only execute on the client
        if (!isClient || !isInitialized) return;

        try
        {
            int totalPixels = resolution.x * resolution.y;
            for (int i = 0; i < totalPixels; i++)
            {
                fogColors[i] = new Color(0, 0, 0, minFogOpacity);
                targetAlphaValues[i] = minFogOpacity; // Update target as well
                hasBeenExplored[i] = true; // Mark all as explored
            }
            fogTexture.SetPixels(fogColors);
            fogTexture.Apply();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error revealing entire map on client: {e.Message}");
        }
    }

    /// <summary>
    /// Reset the fog to cover the entire map.
    /// Only affects the local client's fog view.
    /// </summary>
    [ClientRpc] // Can be called on server, executes on clients
    public void RpcResetFog()
    {
        // Only execute on the client
        if (!isClient || !isInitialized) return;

        try
        {
            int totalPixels = resolution.x * resolution.y;
            for (int i = 0; i < totalPixels; i++)
            {
                fogColors[i] = new Color(0, 0, 0, maxFogOpacity);
                targetAlphaValues[i] = maxFogOpacity; // Update target as well
                hasBeenExplored[i] = false; // Reset explored state
            }
            fogTexture.SetPixels(fogColors);
            fogTexture.Apply();
            // We might need to force a fog update after reset if revealers are already present
            UpdateFogTexture();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error resetting fog on client: {e.Message}");
        }
    }

    /// <summary>
    /// Checks if a world position is currently visible (not fogged) for this client.
    /// Only works on the client.
    /// </summary>
    /// <param name="worldPosition"></param>
    /// <returns>True if the position is visible, false otherwise or if not a client.</returns>
    public bool IsPositionVisible(Vector3 worldPosition)
    {
        if (!isClient || !isInitialized || fogColors == null) return false;

        try
        {
            Vector2Int gridPos = WorldToGridPosition(worldPosition);
            int index = gridPos.y * resolution.x + gridPos.x;

            if (index >= 0 && index < fogColors.Length)
            {
                // If alpha is closer to minFogOpacity than the combined range to max/explored opacity, consider it visible.
                // A simple threshold based on maxOpacity is usually sufficient for "visible vs not visible".
                // Check if the current alpha is close to the minimum opacity (fully visible)
                return fogColors[index].a <= minFogOpacity + (maxFogOpacity - minFogOpacity) * 0.2f; // Use a small tolerance
            }
            return false; // Out of bounds or not initialized
        }
        catch (Exception e)
        {
            Debug.LogError($"Error checking position visibility: {e.Message}");
            return false;
        }
    }

    // The AddFogRevealer, RemoveFogRevealer (by index), and SetFogRevealerActive
    // public methods are no longer needed as the NetworkedFogRevealer handles registration/deregistration
    // directly with RegisterRevealer/UnregisterRevealer based on network events.
    // The FindAllFogRevealers coroutine is also removed as units self-register.
}