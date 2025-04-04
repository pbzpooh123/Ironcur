using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class NetworkLobbyPlayer : NetworkBehaviour
{
    [SyncVar] public string playerName;
    [SyncVar] public string business;
    [SyncVar] public string country;
    [SyncVar(hook = nameof(OnReadyStatusChanged))]
    public bool isReady;

    private LobbyUI lobbyUI;

    private void Start()
    {
        Invoke(nameof(FindLobbyUI), 0.5f); // Delayed to ensure LobbyUI is in scene
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
            CmdSetPlayerInfo(
                PlayerPrefs.GetString("PlayerName"),
                PlayerPrefs.GetString("Business"),
                PlayerPrefs.GetString("Country")
            );

            CmdRequestRoomCode(); // Ask server for room code
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

    [Command]
    void CmdRequestRoomCode()
    {
        TargetReceiveRoomCode(connectionToClient, NetworkManagerLobby.instance.roomCode);
    }

    [TargetRpc]
    void TargetReceiveRoomCode(NetworkConnection target, string code)
    {
        LobbyUI ui = FindObjectOfType<LobbyUI>();
        if (ui != null)
        {
            ui.SetRoomCode(code);
        }
    }

    [ClientRpc]
    public void RpcUpdatePlayerList(List<string> playerDetails)
    {
        if (lobbyUI == null)
        {
            lobbyUI = FindObjectOfType<LobbyUI>();
            if (lobbyUI == null)
            {
                Debug.LogError("NetworkLobbyPlayer: LobbyUI is still missing! Cannot update player list.");
                return;
            }
        }

        lobbyUI.UpdatePlayerList(playerDetails);
    }

    [Command]
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

    [ClientRpc]
    void RpcShowWaitingPanel()
    {
        MainMenuUI ui = FindObjectOfType<MainMenuUI>();
        if (ui != null)
        {
            ui.lobbyPanel.SetActive(true);
            ui.mainMenuPanel.SetActive(false);
        }
    }
    
    [Command]
    public void CmdToggleReady()
    {
        isReady = !isReady;
        NetworkManagerLobby.instance.UpdateLobbyUI();
    }

    void OnReadyStatusChanged(bool oldVal, bool newVal)
    {
        // Trigger a UI update whenever someone's ready state changes
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        lobbyUI?.UpdatePlayerList(NetworkManagerLobby.instance.GetPlayerList());
    }

}
