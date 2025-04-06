using System;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingUnitSpawner : NetworkBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject unitPrefab = null;
    [SerializeField] private Transform spawnPoint = null;
    
    #region Server

    [Command]
    private void CmdSpawnUnit()
    {
        try
        {
            GameObject unitSpawn = Instantiate(unitPrefab, spawnPoint.position, spawnPoint.rotation);
            unitSpawn.name = "Unit_" + gameObject.name;
            
            NetworkServer.Spawn(unitSpawn, connectionToClient);
            
            Debug.Log("A unit has been spawned: " + unitSpawn.name);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            throw;
        }
    }

    #endregion

    #region Client
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if(eventData.button != PointerEventData.InputButton.Left) { return; }
        if (!isOwned) { return; }
        CmdSpawnUnit();
    }

    #endregion
}
