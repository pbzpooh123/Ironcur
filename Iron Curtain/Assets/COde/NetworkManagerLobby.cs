using FishNet;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManagerLobby : MonoBehaviour
{
    public static NetworkManagerLobby Instance;

    private NetworkManager networkManager;
    public string roomCode;
    private List<string> playerDetails = new List<string>();

    private void Awake()
    {
        Instance = this;
        networkManager = InstanceFinder.NetworkManager;
    }

    private void Start()
    {
        // Hook to start host or client depending on setup
        networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
            networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
    }

    private void ServerManager_OnServerConnectionState(FishNet.Transporting.ServerConnectionStateArgs obj)
    {
        if (obj.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
        {
            roomCode = GenerateRoomCode(); // Host only
        }
    }

    public void UpdateLobbyUI()
    {
        playerDetails.Clear();

        foreach (var conn in networkManager.ServerManager.Clients)
        {
            if (conn.Value == null || conn.Value.FirstObject == null)
                continue;

            NetworkLobbyPlayer player = conn.Value.FirstObject.GetComponent<NetworkLobbyPlayer>();
            if (player != null)
            {
                playerDetails.Add($"{player.playerName} - {player.business} ({player.country})");
            }
        }

        foreach (var conn in networkManager.ServerManager.Clients)
        {
            if (conn.Value == null || conn.Value.FirstObject == null)
                continue;

            NetworkLobbyPlayer player = conn.Value.FirstObject.GetComponent<NetworkLobbyPlayer>();
            if (player != null)
            {
                player.UpdatedPlayerList(GetPlayerList());
            }
        }
    }

    private string GenerateRoomCode()
    {
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 6; i++)
            code += chars[Random.Range(0, chars.Length)];
        return code;
    }

    public bool AllPlayersReady()
    {
        foreach (var conn in networkManager.ServerManager.Clients)
        {
            if (conn.Value == null || conn.Value.FirstObject == null)
                continue;

            var player = conn.Value.FirstObject.GetComponent<NetworkLobbyPlayer>();
            if (player != null && !player.isReady.Value)
                return false;
        }
        return true;
    }

    public List<string> GetPlayerList()
    {
        List<string> result = new List<string>();
        foreach (var conn in networkManager.ServerManager.Clients)
        {
            if (conn.Value == null || conn.Value.FirstObject == null)
                continue;

            var player = conn.Value.FirstObject.GetComponent<NetworkLobbyPlayer>();
            if (player != null)
                result.Add($"{player.playerName.Value} - {player.business.Value} ({player.country.Value})" +
                           (player.isReady.Value ? " ✅" : " ❌"));

        }
        return result;
    }
}
