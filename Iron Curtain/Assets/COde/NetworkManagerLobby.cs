using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManagerLobby : NetworkManager
{
   
    private List<string> playerDetails = new List<string>();
    public static NetworkManagerLobby instance;
    public  string roomCode;

    public override void Awake()
    {
        base.Awake();
        instance = this;
    }
    
    public override void OnStartHost()
    {
        base.OnStartHost();
        roomCode = GenerateRoomCode(); // only generated on host
    }
    
    public void UpdateLobbyUI()
    {
        playerDetails.Clear();

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null)
            {
                NetworkLobbyPlayer player = conn.identity.GetComponent<NetworkLobbyPlayer>();
                if (player != null)
                {
                    playerDetails.Add($"{player.playerName} - {player.business} ({player.country})");
                    player.RpcUpdatePlayerList(GetPlayerList());
                }
            }
        }

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null)
            {
                NetworkLobbyPlayer player = conn.identity.GetComponent<NetworkLobbyPlayer>();
                if (player != null)
                {
                    player.RpcUpdatePlayerList(playerDetails);
                    player.RpcUpdatePlayerList(GetPlayerList());
                }
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
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null)
            {
                var player = conn.identity.GetComponent<NetworkLobbyPlayer>();
                if (player != null && !player.isReady)
                    return false;
            }
        }
        return true;
    }

    public List<string> GetPlayerList()
    {
        List<string> result = new List<string>();
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null)
            {
                var player = conn.identity.GetComponent<NetworkLobbyPlayer>();
                if (player != null)
                    result.Add($"{player.playerName} - {player.business} ({player.country})" +
                               (player.isReady ? " ✅" : " ❌"));
            }
        }
        return result;
    }


   
}