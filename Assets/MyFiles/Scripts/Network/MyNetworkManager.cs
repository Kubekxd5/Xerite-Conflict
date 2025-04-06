using Mirror;
using UnityEngine;

public class MyNetworkManager : NetworkManager
{
    [SerializeField] private GameObject unitSpawnerPrefab = null;

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        var connTransform = conn.identity.transform;
        GameObject unitSpawnerInstance = Instantiate(unitSpawnerPrefab, connTransform.position, connTransform.rotation);
        
        NetworkServer.Spawn(unitSpawnerInstance, conn);
    }   
}
