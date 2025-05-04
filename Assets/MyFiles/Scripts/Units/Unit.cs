using Mirror;
using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

public class Unit : NetworkBehaviour
{
    [Header("Unit Settings")]
    [SerializeField] private UnityEvent onSelected = null;
    [SerializeField] private UnityEvent onDeselect = null;
    [SerializeField] private GameObject selectionIndicator;

    [Header("Debugging")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Color debugSelectedColor = Color.green;
    [SerializeField] private Color debugGroupColor = Color.blue;

    public bool IsSelected { get; private set; }

    // Unit type and properties (useful for RTS games)
    [Header("Unit Properties")]
    [SerializeField] private string unitType = "Generic";
    [SerializeField] private int unitCost = 100;
    [SerializeField] private float maxHealth = 100f;

    // Health sync variable
    [SyncVar(hook = nameof(OnHealthChanged))]
    private float currentHealth;

    // Events
    public event Action<float> OnHealthUpdated;
    public event Action OnUnitDestroyed;
    public event Action OnUnitSelected;
    public event Action OnUnitDeselected;

    // Cache components
    private UnitGroupManager groupManager;

    #region Initialization

    private void Awake()
    {
        try
        {
            // Initialize health
            currentHealth = maxHealth;

            // Find group manager
            groupManager = FindFirstObjectByType<UnitGroupManager>();

            // Set selection indicator initial state
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in Unit.Awake: {e.Message}");
        }
    }

    public override void OnStartServer()
    {
        // Server-side initialization if needed
        currentHealth = maxHealth;
    }

    #endregion

    #region Client

    /// <summary>
    /// Selects the unit (client-side)
    /// </summary>
    [Client]
    public void Select()
    {
        try
        {
            if (!isOwned) { return; }

            IsSelected = true;
            onSelected?.Invoke();
            OnUnitSelected?.Invoke();

            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(true);
            }

            if (debugMode)
            {
                Debug.Log($"Unit selected: {gameObject.name}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error selecting unit: {e.Message}");
        }
    }

    /// <summary>
    /// Deselects the unit (client-side)
    /// </summary>
    [Client]
    public void Deselect()
    {
        try
        {
            if (!isOwned) { return; }

            IsSelected = false;
            onDeselect?.Invoke();
            OnUnitDeselected?.Invoke();

            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
            }

            if (debugMode)
            {
                Debug.Log($"Unit deselected: {gameObject.name}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error deselecting unit: {e.Message}");
        }
    }

    /// <summary>
    /// Get unit's current health percentage
    /// </summary>
    public float GetHealthPercent()
    {
        return Mathf.Clamp01(currentHealth / maxHealth);
    }

    #endregion

    #region Server

    /// <summary>
    /// Apply damage to the unit (server-side)
    /// </summary>
    [Server]
    public void TakeDamage(float amount)
    {
        try
        {
            if (currentHealth <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);

            if (debugMode)
            {
                Debug.Log($"Unit {gameObject.name} took {amount} damage. Current health: {currentHealth}");
            }

            if (currentHealth <= 0)
            {
                HandleDeath();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in TakeDamage: {e.Message}");
        }
    }

    /// <summary>
    /// Heal the unit (server-side)
    /// </summary>
    [Server]
    public void Heal(float amount)
    {
        try
        {
            if (currentHealth <= 0) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            if (debugMode)
            {
                Debug.Log($"Unit {gameObject.name} healed for {amount}. Current health: {currentHealth}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in Heal: {e.Message}");
        }
    }

    /// <summary>
    /// Handle unit death
    /// </summary>
    [Server]
    private void HandleDeath()
    {
        try
        {
            if (debugMode)
            {
                Debug.Log($"Unit {gameObject.name} died");
            }

            // Broadcast death event
            RpcHandleDeath();

            // Schedule actual destruction with a delay to allow for death animations
            Invoke(nameof(DestroyUnit), 2f);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in HandleDeath: {e.Message}");
        }
    }

    [ClientRpc]
    private void RpcHandleDeath()
    {
        try
        {
            // Trigger client-side death handling
            OnUnitDestroyed?.Invoke();

            // Remove from all groups
            if (isOwned && groupManager != null)
            {
                groupManager.RemoveUnitFromAllGroups(this);
            }

            // Trigger visual death effects, animations, etc.
            // Add your visual effects here
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in RpcHandleDeath: {e.Message}");
        }
    }

    [Server]
    private void DestroyUnit()
    {
        // Destroy the unit GameObject
        NetworkServer.Destroy(gameObject);
    }

    /// <summary>
    /// Callback for health sync var
    /// </summary>
    private void OnHealthChanged(float oldValue, float newValue)
    {
        // Notify subscribers about health change
        OnHealthUpdated?.Invoke(newValue);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Get the unit type
    /// </summary>
    public string GetUnitType()
    {
        return unitType;
    }

    /// <summary>
    /// Get the unit cost
    /// </summary>
    public int GetUnitCost()
    {
        return unitCost;
    }

    /// <summary>
    /// Check if unit belongs to any group
    /// </summary>
    public bool BelongsToAnyGroup()
    {
        if (groupManager == null) return false;
        return groupManager.GetUnitGroups(this).Count > 0;
    }

    /// <summary>
    /// Check if unit belongs to specific group
    /// </summary>
    public bool BelongsToGroup(int groupNumber)
    {
        if (groupManager == null) return false;
        return groupManager.IsUnitInGroup(groupNumber, this);
    }

    /// <summary>
    /// Get all groups the unit belongs to
    /// </summary>
    public List<int> GetGroups()
    {
        if (groupManager == null) return new List<int>();
        return groupManager.GetUnitGroups(this);
    }

    #endregion

    #region Visualization

    private void OnDrawGizmos()
    {
        if (!debugMode || !Application.isPlaying) return;

        // Draw debug gizmo for selected unit
        if (IsSelected)
        {
            Gizmos.color = debugSelectedColor;
            Gizmos.DrawWireSphere(transform.position, 1.1f);
        }

        // Draw debug gizmo for grouped unit
        if (BelongsToAnyGroup())
        {
            Gizmos.color = debugGroupColor;
            Gizmos.DrawWireCube(transform.position, new Vector3(1.2f, 0.2f, 1.2f));
        }
    }

    #endregion
}