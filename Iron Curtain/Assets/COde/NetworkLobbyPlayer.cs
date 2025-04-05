using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections.Generic;

public class NetworkLobbyPlayer : NetworkBehaviour
{
     public string playerName;
     public string business;
     public string country;
    public bool isReady;

    private LobbyUI lobbyUI;

    public override void OnStartClient()
    {
        base.OnStartClient();
        Invoke(nameof(FindLobbyUI), 0.5f);
    }

    void FindLobbyUI()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();

        if (lobbyUI == null)
        {
            Debug.LogError("LobbyUI still not found! Check if it exists in the scene.");
            return;
        }

        if (IsOwner)
        {
            SetPlayerInfo(
                PlayerPrefs.GetString("PlayerName"),
                PlayerPrefs.GetString("Business"),
                PlayerPrefs.GetString("Country")
            );

            RequestRoomCode();
        }
    }

    [ServerRpc]
    public void SetPlayerInfo(string newName, string newBusiness, string newCountry)
    {
        playerName = newName;
        business = newBusiness;
        country = newCountry;

        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    [ServerRpc]
    public void RequestRoomCode()
    {
        TargetReceiveRoomCode(Owner, NetworkManagerLobby.Instance.roomCode);
    }

    [TargetRpc]
    public void TargetReceiveRoomCode(NetworkConnection conn, string code)
    {
        LobbyUI ui = FindObjectOfType<LobbyUI>();
        if (ui != null)
        {
            ui.SetRoomCode(code);
        }
    }

    [ObserversRpc]
    public void UpdatedPlayerList(List<string> playerDetails)

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

    [ServerRpc]
    public void JoinRoom(string enteredCode)
    {
        if (NetworkManagerLobby.Instance.roomCode == enteredCode)
        {
            TargetShowWaitingPanel(Owner);
        }
        else
        {
            Debug.Log("Invalid Room Code!");
        }
    }

    [TargetRpc]
    public void TargetShowWaitingPanel(NetworkConnection conn)
    {
        MainMenuUI ui = FindObjectOfType<MainMenuUI>();
        if (ui != null)
        {
            ui.lobbyPanel.SetActive(true);
            ui.mainMenuPanel.SetActive(false);
        }
    }

    [ServerRpc]
    public void ToggleReady()
    {
        isReady = !isReady;
        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    private void OnReadyStatusChanged(bool oldVal, bool newVal, bool asServer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        lobbyUI?.UpdatePlayerList(NetworkManagerLobby.Instance.GetPlayerList());
    }
}
