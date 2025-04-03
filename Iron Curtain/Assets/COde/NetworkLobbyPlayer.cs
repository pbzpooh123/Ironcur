using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class NetworkLobbyPlayer : NetworkBehaviour
{
    [SyncVar] public string playerName;
    [SyncVar] public string business;
    [SyncVar] public string country;

    private LobbyUI lobbyUI;
    
    
    private void Start()
    {
        Invoke(nameof(FindLobbyUI), 0.5f); // Delayed call to avoid null issues
    }

    void FindLobbyUI()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();

        if (lobbyUI == null)
        {
            Debug.LogError("LobbyUI still not found! Check if it exists in the scene.");
            return;
        }

        if (isLocalPlayer)
        {
            CmdSetPlayerInfo(PlayerPrefs.GetString("PlayerName"),
                PlayerPrefs.GetString("Business"),
                PlayerPrefs.GetString("Country"));
        }
    }

    [Command]
    void CmdSetPlayerInfo(string newName, string newBusiness, string newCountry)
    {
        playerName = newName;
        business = newBusiness;
        country = newCountry;
        NetworkManagerLobby.instance.UpdateLobbyUI();
    }
    [Command] // Runs on the server
    public void CmdJoinRoom(string enteredCode)
    {
        if (NetworkManagerLobby.instance.roomCode == enteredCode)
        {
            Debug.Log("Player joined successfully.");
            RpcShowWaitingPanel();
        }
        else
        {
            Debug.Log("Invalid Room Code!");
        }
    }

    [ClientRpc] // Sends a message to the client
    void RpcShowWaitingPanel()
    {
        FindObjectOfType<MainMenuUI>().lobbyPanel.SetActive(true);
        FindObjectOfType<MainMenuUI>().mainMenuPanel.SetActive(false);
    }


    [ClientRpc]
    public void RpcUpdatePlayerList(List<string> playerDetails)
    {
        if (lobbyUI == null)
        {
            lobbyUI = FindObjectOfType<LobbyUI>(); // Try finding it again
            if (lobbyUI == null)
            {
                Debug.LogError("NetworkLobbyPlayer: LobbyUI is still missing! Cannot update player list.");
                return;
            }
        }

        lobbyUI.UpdatePlayerList(playerDetails);
    }
   

}