using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkLobbyPlayer : NetworkBehaviour
{
    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<bool> isReady = new();
    public readonly SyncVar<float> profit = new();
    
    private LobbyUI lobbyUI;

    public override void OnStartClient()
    {
        base.OnStartClient();
        isReady.OnChange += OnReadyStatusChanged;
        Invoke(nameof(FindLobbyUI), 0.5f);
    }

    private void FindLobbyUI()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();

        if (IsOwner)
        {
            SetPlayerInfo(PlayerPrefs.GetString("PlayerName", "Player"));
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
        FindObjectOfType<LobbyUI>()?.SetRoomCode(code);
    }

    [ObserversRpc]
    public void UpdatedPlayerList(List<string> playerDetails)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        lobbyUI?.UpdatePlayerList(playerDetails);
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
        var ui = FindObjectOfType<MainMenuUI>();
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
   
    [ObserversRpc]
    public void TargetSetHUD(NetworkConnection conn, int slotIndex, string name, int profit)
    {
        StartCoroutine(WaitForHUD(slotIndex, name, profit));
    }

    private System.Collections.IEnumerator WaitForHUD(int slotIndex, string name, int profit)
    {
        // Wait until GameHUD is present
        while (GameHUD.Instance == null)
            yield return null;

        var panel = GameHUD.Instance.CreatePlayerPanel(slotIndex, name, profit);

        // Find the local pawn instead of using conn.FirstObject
        foreach (var pawn in FindObjectsOfType<PlayerPawn>())
        {
            if (pawn.playerName.Value == name)  // match by name (or connectionId if you store it)
            {
                pawn.infoPanel = panel;
                break;
            }
        }
    }
}
