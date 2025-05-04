using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class FogOfWar : MonoBehaviour
{
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

    [Header("Vision Settings")]
    [Tooltip("Units with this tag will reveal fog")]
    [SerializeField] private string unitTagToTrack = "Player";

    [Range(1f, 50f)]
    [Tooltip("How far units can see (in world units)")]
    [SerializeField] private float defaultVisionRange = 10f;

    [Range(0.1f, 2f)]
    [Tooltip("How quickly vision updates")]
    [SerializeField] private float visionUpdateInterval = 0.2f;

    [Header("Advanced Settings")]
    [SerializeField] private bool useShaderSmoothing = true;
    [SerializeField] private bool rememberExploredAreas = true;
    [SerializeField] private bool debugMode = false;

    // Internal variables
    private Texture2D fogTexture;
    private Color[] fogColors;
    private MeshRenderer meshRenderer;
    private Material fogMaterial;
    private bool isInitialized = false;
    private List<FogRevealer> fogRevealers = new List<FogRevealer>();
    private bool updateFogNextFrame = false;
    private float lastUpdateTime = 0f;

    // Class to keep track of units that reveal fog
    [System.Serializable]
    public class FogRevealer
    {
        public Transform transform;
        public float visionRange;
        public bool isActive;

        public FogRevealer(Transform t, float range, bool active = true)
        {
            transform = t;
            visionRange = range;
            isActive = active;
        }
    }

    private void Awake()
    {
        try
        {
            Initialize();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Fog of War: {e.Message}");
            enabled = false;
        }
    }

    private void Initialize()
    {
        // Get required components
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            throw new NullReferenceException("MeshRenderer component is missing!");
        }

        // Create fog texture
        fogTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
        fogTexture.filterMode = useShaderSmoothing ? FilterMode.Bilinear : FilterMode.Point;
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        // Initialize fog color array
        fogColors = new Color[resolution.x * resolution.y];
        for (int i = 0; i < fogColors.Length; i++)
        {
            fogColors[i] = new Color(0, 0, 0, maxFogOpacity);
        }
        fogTexture.SetPixels(fogColors);
        fogTexture.Apply();

        // Setup material
        try
        {
            fogMaterial = new Material(Shader.Find("Unlit/Transparent"));
            if (fogMaterial == null)
            {
                throw new NullReferenceException("Failed to create fog material!");
            }
            fogMaterial.mainTexture = fogTexture;
            meshRenderer.material = fogMaterial;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error setting up fog material: {e.Message}");
            throw;
        }

        isInitialized = true;

        // Initial fog update
        UpdateFogTexture();

        // Start vision update coroutine
        StartCoroutine(UpdateVisionSources());
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Check if it's time to update fog
        if (Time.time - lastUpdateTime >= visionUpdateInterval || updateFogNextFrame)
        {
            UpdateFogTexture();
            lastUpdateTime = Time.time;
            updateFogNextFrame = false;
        }

        // Debug visualization
        if (debugMode)
        {
            DrawDebugVisualization();
        }
    }

    private void OnEnable()
    {
        if (isInitialized)
        {
            FindAllFogRevealers();
        }
    }

    private IEnumerator UpdateVisionSources()
    {
        WaitForSeconds wait = new WaitForSeconds(visionUpdateInterval);

        while (true)
        {
            yield return wait;

            try
            {
                // Search for new units with the specified tag
                FindAllFogRevealers();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error updating vision sources: {e.Message}");
            }
        }
    }

    private void FindAllFogRevealers()
    {
        try
        {
            // Clear inactive revealers from list
            fogRevealers.RemoveAll(revealer => revealer.transform == null);

            // Find all game objects with the specified tag
            GameObject[] units = GameObject.FindGameObjectsWithTag(unitTagToTrack);

            // Check if any are new and add them to our list
            foreach (GameObject unit in units)
            {
                bool isAlreadyTracked = false;

                foreach (FogRevealer revealer in fogRevealers)
                {
                    if (revealer.transform == unit.transform)
                    {
                        isAlreadyTracked = true;
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error finding fog revealers: {e.Message}");
        }
    }

    private void UpdateFogTexture()
    {
        if (!isInitialized || fogTexture == null || fogColors == null) return;

        try
        {
            // Create a temporary array for new fog values
            float[,] fogValues = new float[resolution.x, resolution.y];

            // For each cell in the grid
            for (int x = 0; x < resolution.x; x++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    // Get current world position of this cell
                    Vector3 worldPos = GridToWorldPosition(x, y);
                    float minDistance = float.MaxValue;

                    // Find the minimum distance to any revealer
                    foreach (FogRevealer revealer in fogRevealers)
                    {
                        if (revealer.transform == null || !revealer.isActive)
                            continue;

                        float distance = Vector3.Distance(worldPos, revealer.transform.position);
                        float visionRange = revealer.visionRange;

                        // Only consider if within vision range
                        if (distance < visionRange)
                        {
                            minDistance = Mathf.Min(minDistance, distance / visionRange);
                        }
                    }

                    // Calculate fog value based on distance
                    float fogValue;
                    if (minDistance < 1.0f)
                    {
                        // Area is at least partially revealed
                        fogValue = minDistance;
                    }
                    else
                    {
                        // Area is not in view
                        fogValue = 1.0f;
                    }

                    fogValues[x, y] = fogValue;
                }
            }

            // Update the fog texture based on the calculated values and fading
            for (int x = 0; x < resolution.x; x++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    int index = y * resolution.x + x;
                    float currentAlpha = fogColors[index].a;
                    float targetAlpha;

                    if (fogValues[x, y] < 1.0f)
                    {
                        // Area is being revealed
                        targetAlpha = Mathf.Lerp(minFogOpacity, maxFogOpacity, fogValues[x, y]);
                        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, revealSpeed);
                    }
                    else if (rememberExploredAreas)
                    {
                        // Area was previously explored but not in view now
                        float exploredAlpha = Mathf.Lerp(maxFogOpacity, 0.5f, 0.7f); // Semi-transparent for explored areas
                        targetAlpha = currentAlpha < maxFogOpacity ? exploredAlpha : maxFogOpacity;
                        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, obscureSpeed);
                    }
                    else
                    {
                        // Area is out of view and should fade back to fog
                        targetAlpha = maxFogOpacity;
                        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, obscureSpeed);
                    }

                    fogColors[index] = new Color(0, 0, 0, currentAlpha);
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
        // Convert grid coordinates to world position
        float worldX = (x / (float)resolution.x) * gridSize.x - (gridSize.x / 2) + transform.position.x;
        float worldZ = (y / (float)resolution.y) * gridSize.y - (gridSize.y / 2) + transform.position.z;

        return new Vector3(worldX, transform.position.y, worldZ);
    }

    private Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
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
        // Draw grid bounds
        Vector3 center = transform.position;
        Vector3 size = new Vector3(gridSize.x, 0.1f, gridSize.y);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);

        // Draw vision ranges for each revealer
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        foreach (FogRevealer revealer in fogRevealers)
        {
            if (revealer.transform != null && revealer.isActive)
            {
                Gizmos.DrawWireSphere(revealer.transform.position, revealer.visionRange);
            }
        }
    }

    // Public methods for external control

    /// <summary>
    /// Registers a new fog revealer manually
    /// </summary>
    /// <param name="transform">The transform of the revealer</param>
    /// <param name="visionRange">How far this revealer can see</param>
    /// <returns>Index of the added revealer for later reference</returns>
    public int AddFogRevealer(Transform transform, float visionRange = -1)
    {
        if (transform == null)
        {
            Debug.LogWarning("Attempted to add null transform as fog revealer");
            return -1;
        }

        try
        {
            float range = visionRange < 0 ? defaultVisionRange : visionRange;
            fogRevealers.Add(new FogRevealer(transform, range));
            updateFogNextFrame = true;
            return fogRevealers.Count - 1;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error adding fog revealer: {e.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Removes a fog revealer by its transform
    /// </summary>
    public bool RemoveFogRevealer(Transform transform)
    {
        if (transform == null) return false;

        try
        {
            int index = fogRevealers.FindIndex(r => r.transform == transform);
            if (index >= 0)
            {
                fogRevealers.RemoveAt(index);
                updateFogNextFrame = true;
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error removing fog revealer: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Set active state of a specific fog revealer
    /// </summary>
    public bool SetFogRevealerActive(Transform transform, bool active)
    {
        if (transform == null) return false;

        try
        {
            int index = fogRevealers.FindIndex(r => r.transform == transform);
            if (index >= 0)
            {
                fogRevealers[index].isActive = active;
                updateFogNextFrame = true;
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error setting fog revealer state: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reveals the entire map (for debugging or end-game scenarios)
    /// </summary>
    public void RevealEntireMap()
    {
        try
        {
            for (int i = 0; i < fogColors.Length; i++)
            {
                fogColors[i] = new Color(0, 0, 0, minFogOpacity);
            }
            fogTexture.SetPixels(fogColors);
            fogTexture.Apply();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error revealing entire map: {e.Message}");
        }
    }

    /// <summary>
    /// Reset the fog to cover the entire map
    /// </summary>
    public void ResetFog()
    {
        try
        {
            for (int i = 0; i < fogColors.Length; i++)
            {
                fogColors[i] = new Color(0, 0, 0, maxFogOpacity);
            }
            fogTexture.SetPixels(fogColors);
            fogTexture.Apply();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error resetting fog: {e.Message}");
        }
    }

    /// <summary>
    /// Checks if a world position is currently visible (not fogged)
    /// </summary>
    public bool IsPositionVisible(Vector3 worldPosition)
    {
        try
        {
            Vector2Int gridPos = WorldToGridPosition(worldPosition);
            int index = gridPos.y * resolution.x + gridPos.x;

            if (index >= 0 && index < fogColors.Length)
            {
                // If alpha is closer to minFogOpacity than maxFogOpacity, consider it visible
                float threshold = minFogOpacity + (maxFogOpacity - minFogOpacity) * 0.5f;
                return fogColors[index].a < threshold;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error checking position visibility: {e.Message}");
            return false;
        }
    }
}