using FishNet;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Connection; // <-- add this
using System.Collections.Generic;
using UnityEngine;

public class NetworkManagerLobby : MonoBehaviour
{
    public static NetworkManagerLobby Instance;

    private NetworkManager _networkManager;

    [Header("Lobby State")]
    [Tooltip("6-char alphanum room code. Host sets this on server start.")]
    public string roomCode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _networkManager = InstanceFinder.NetworkManager;
        if (_networkManager == null)
            Debug.LogError("NetworkManagerLobby: InstanceFinder.NetworkManager is null.");
    }

    private void OnEnable()
    {
        if (_networkManager == null) return;

        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState; // signature below
    }

    private void OnDisable()
    {
        if (_networkManager == null) return;

        _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        // Fires when the SERVER starts/stops.
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                roomCode = GenerateRoomCode();

            BroadcastRoomCode(roomCode);
            BroadcastRoster();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            roomCode = string.Empty;
        }
    }

    // ✅ Correct signature for OnRemoteConnectionState:
    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        // Fires when a CLIENT connects/disconnects to the server.
        if (args.ConnectionState == RemoteConnectionState.Started ||
            args.ConnectionState == RemoteConnectionState.Stopped)
        {
            // Defer one frame so FirstObject has spawned/despawned.
            StartCoroutine(DeferBroadcastOneFrame());
        }
    }

    private System.Collections.IEnumerator DeferBroadcastOneFrame()
    {
        yield return null; // next frame
        BroadcastRoster();
    }

    [Server]
    public void BroadcastRoster()
    {
        if (!_networkManager || !_networkManager.IsServer)
            return;

        List<string> roster = BuildRoster();

        foreach (var kvp in _networkManager.ServerManager.Clients)
        {
            var c = kvp.Value;
            if (c == null) continue;

            NetworkObject first = c.FirstObject;
            if (first == null) continue;

            if (first.TryGetComponent(out NetworkLobbyPlayer player))
            {
                player.Target_UpdatePlayerList(c, roster);
                if (!string.IsNullOrEmpty(roomCode))
                    player.Target_SetRoomCode(c, roomCode);
            }
        }
    }

    [Server]
    public void BroadcastRoomCode(string code)
    {
        if (!_networkManager || !_networkManager.IsServer)
            return;

        foreach (var kvp in _networkManager.ServerManager.Clients)
        {
            var c = kvp.Value;
            if (c == null) continue;

            NetworkObject first = c.FirstObject;
            if (first == null) continue;

            if (first.TryGetComponent(out NetworkLobbyPlayer player))
                player.Target_SetRoomCode(c, code);
        }
    }

    private List<string> BuildRoster()
    {
        var result = new List<string>();

        foreach (var kvp in _networkManager.ServerManager.Clients)
        {
            var c = kvp.Value;
            if (c == null || c.FirstObject == null) continue;

            if (c.FirstObject.TryGetComponent(out NetworkLobbyPlayer p))
            {
                string line = $"{p.playerName.Value}" +
                              (p.isReady.Value ? " ✅" : " ❌");
                result.Add(line);
            }
        }

        return result;
    }

    public bool AllPlayersReady()
    {
        if (_networkManager == null) return false;

        foreach (var kvp in _networkManager.ServerManager.Clients)
        {
            var c = kvp.Value;
            if (c == null || c.FirstObject == null) continue;

            if (c.FirstObject.TryGetComponent(out NetworkLobbyPlayer p))
            {
                if (!p.isReady.Value)
                    return false;
            }
        }
        return true;
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(6);
        for (int i = 0; i < 6; i++)
            sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        return sb.ToString();
    }

    [Server]
    public void OnPlayerReadyStateChanged()
    {
        BroadcastRoster();
    }
}
