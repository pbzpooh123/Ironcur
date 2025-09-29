using FishNet;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using System.Collections.Generic;
using FishNet.Transporting;
using UnityEngine;

public class NetworkManagerLobby : MonoBehaviour
{
    public static NetworkManagerLobby Instance;

    private NetworkManager _networkManager;

    [Header("Lobby State")]
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
    }

    private void OnDisable()
    {
        if (_networkManager == null) return;

        _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                roomCode = GenerateRoomCode();

            UpdateLobbyUI();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            roomCode = string.Empty;
        }
    }

    public void UpdateLobbyUI()
    {
        List<string> playerList = GetPlayerList();

        foreach (var kvp in _networkManager.ServerManager.Clients)
        {
            var conn = kvp.Value;
            if (conn == null || conn.FirstObject == null) continue;

            if (conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer player))
            {
                player.UpdatedPlayerList(playerList);
                player.TargetReceiveRoomCode(conn, roomCode);
            }
        }
    }

    public List<string> GetPlayerList()
    {
        List<string> players = new List<string>();

        foreach (var kvp in _networkManager.ServerManager.Clients)
        {
            var conn = kvp.Value;
            if (conn == null || conn.FirstObject == null) continue;

            if (conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer player))
            {
                string details = $"{player.playerName.Value} | {player.business.Value} | {player.country.Value}";
                if (player.isReady.Value)
                    details += " ✅";
                else
                    details += " ❌";

                players.Add(details);
            }
        }

        return players;
    }

    public bool AllPlayersReady()
    {
        foreach (var kvp in _networkManager.ServerManager.Clients)
        {
            var conn = kvp.Value;
            if (conn == null || conn.FirstObject == null) continue;

            if (conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer player))
            {
                if (!player.isReady.Value)
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
}
