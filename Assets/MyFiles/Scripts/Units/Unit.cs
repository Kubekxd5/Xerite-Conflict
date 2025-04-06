using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Unit : NetworkBehaviour
{
    [SerializeField] private UnityEvent onSelected = null;
    [SerializeField] private UnityEvent onDeselect = null;

    public bool IsSelected { get; private set; }

    #region Client

    [Client]
    public void Select()
    {
        if (!isOwned) { return; }

        IsSelected = true;
        onSelected?.Invoke();
    }

    [Client]
    public void Deselect()
    {
        if (!isOwned) { return; }

        IsSelected = false;
        onDeselect?.Invoke();
    }

    #endregion
}