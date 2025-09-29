using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
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
    public void TargetReceiveRoomCode(FishNet.Connection.NetworkConnection conn, string code)
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
    public void TargetShowWaitingPanel(FishNet.Connection.NetworkConnection conn)
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

    /* ---------------- HUD BROADCAST TO ALL ---------------- */
    
    [ObserversRpc]
    public void TargetSetHUD(int slotIndex, string name, int initialMoney, int ownerConnectionId)
    {
        StartCoroutine(WaitForHUD(slotIndex, name, initialMoney, ownerConnectionId));
    }

    private IEnumerator WaitForHUD(int slotIndex, string name, int initialMoney, int ownerConnectionId)
    {
        while (GameHUD.Instance == null)
            yield return null;
        
        var panel = GameHUD.Instance.CreatePlayerPanel(slotIndex, name, initialMoney);
        
        foreach (var pawn in FindObjectsOfType<PlayerPawn>())
        {
            if (pawn.Owner != null && pawn.Owner.ClientId == ownerConnectionId)
            {
                pawn.infoPanel = panel;
                panel.UpdateMoney(pawn.money.Value);
                break;
            }
        }
    }
}
