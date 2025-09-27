using System;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections.Generic;

public class NetworkLobbyPlayer : NetworkBehaviour
{
    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<bool> isReady = new();
    public readonly SyncVar<float> profit = new();
    
    
    
    private LobbyUI lobbyUI;

    public override void OnStartClient()
    {
        base.OnStartClient();
        Invoke(nameof(FindLobbyUI), 0.5f);
    }
    

    void FindLobbyUI()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();

        if (IsOwner)
        {
            SetPlayerInfo(
                PlayerPrefs.GetString("PlayerName")
            );

            RequestRoomCode();
        }
    }

    [ServerRpc]
    public void SetPlayerInfo(string newName)
    {
        playerName.Value = newName;
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
        isReady.Value = !isReady.Value;
        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    private void OnReadyStatusChanged(bool oldVal, bool newVal, bool asServer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        lobbyUI?.UpdatePlayerList(NetworkManagerLobby.Instance.GetPlayerList());
    }
   
    [TargetRpc]
    public void TargetSetHUD(NetworkConnection conn, int slotIndex, string name,  int profit)
    {
        var panel = GameHUD.Instance.CreatePlayerPanel(slotIndex, name, profit);
        
        if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out PlayerPawn pawn))
        {
            pawn.infoPanel = panel;
            pawn.AddMoney(500);
        }
        
    }

    
}
