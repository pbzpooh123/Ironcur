using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class LobbySpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public NetworkObject lobbyPlayerPrefab; // assign your NetworkLobbyPlayer prefab

    private void OnEnable()
    {
        var nm = InstanceFinder.NetworkManager;
        if (nm != null)
            nm.ServerManager.OnRemoteConnectionState += OnRemoteConnState;
    }

    private void OnDisable()
    {
        var nm = InstanceFinder.NetworkManager;
        if (nm != null)
            nm.ServerManager.OnRemoteConnectionState -= OnRemoteConnState;
    }

    private void OnRemoteConnState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        if (lobbyPlayerPrefab == null)
        {
            Debug.LogError("[LobbySpawner] Lobby player prefab not assigned.");
            return;
        }

        // Server spawns a lobby player for THIS connection immediately.
        var no = Instantiate(lobbyPlayerPrefab);
        InstanceFinder.ServerManager.Spawn(no, conn);
    }
}
