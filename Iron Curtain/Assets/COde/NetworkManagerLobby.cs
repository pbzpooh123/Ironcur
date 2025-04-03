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
        roomCode = GenerateRoomCode(); // Generate room code on host
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
    

   
}