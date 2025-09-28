using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using UnityEngine;

public partial class NetworkLobbyPlayer : NetworkBehaviour
{
    public static NetworkLobbyPlayer Local;

    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<bool>   isReady    = new();
    public readonly SyncVar<float>  profit     = new();
    [System.NonSerialized] public bool HudSpawned;

    private LobbyUI _lobbyUI; // <-- add this

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsOwner) Local = this;

        // Was: Invoke(nameof(FindLobbyUI), 0.25f);
        // Now we call the actual init you implemented:
        Invoke(nameof(CacheLobbyUIAndInitOwner), 0.25f);
    }

    public override void OnStopClient()
    {
        if (IsOwner && Local == this) Local = null;
        base.OnStopClient();
    }

    private void CacheLobbyUIAndInitOwner()
    {
        _lobbyUI = FindObjectOfType<LobbyUI>();

        if (IsOwner)
        {
            SetPlayerInfo(
                PlayerPrefs.GetString("PlayerName", "Player")
            );

            RequestRoomCode();
        }
    }

    /* --------------------- Server RPCs --------------------- */

    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerInfo(string newName)
    {
        playerName.Value = newName;
        NetworkManagerLobby.Instance?.BroadcastRoster();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestRoomCode()
    {
        var code = NetworkManagerLobby.Instance?.roomCode ?? string.Empty;
        Target_SetRoomCode(Owner, code);
    }

    [ServerRpc(RequireOwnership = false)]
    public void JoinRoom(string enteredCode)
    {
        string want = (enteredCode ?? string.Empty).Trim().ToUpperInvariant();
        string have = (NetworkManagerLobby.Instance?.roomCode ?? string.Empty).Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(want) && want == have)
        {
            Target_ShowWaitingPanel(Owner);
            NetworkManagerLobby.Instance?.BroadcastRoster();
        }
        else
        {
            Debug.Log("JoinRoom: Invalid room code.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleReady()
    {
        isReady.Value = !isReady.Value;
        NetworkManagerLobby.Instance?.OnPlayerReadyStateChanged(); // server will BroadcastRoster()
        Debug.Log($"[Server] {playerName.Value} ready = {isReady.Value}");
    }

    /* --------------------- Target RPCs (UI pushes) --------------------- */

    [TargetRpc]
    public void Target_SetRoomCode(NetworkConnection target, string code)
    {
        if (_lobbyUI == null) _lobbyUI = FindObjectOfType<LobbyUI>();
        _lobbyUI?.SetRoomCode(code);
    }

    [TargetRpc]
    public void Target_UpdatePlayerList(NetworkConnection target, List<string> roster)
    {
        if (_lobbyUI == null) _lobbyUI = FindObjectOfType<LobbyUI>();
        _lobbyUI?.UpdatePlayerList(roster);
    }

    [TargetRpc]
    public void Target_ShowWaitingPanel(NetworkConnection target)
    {
        var menu = FindObjectOfType<MainMenuUI>();
        if (menu != null)
        {
            menu.lobbyPanel.SetActive(true);
            if (menu.mainMenuPanel != null) menu.mainMenuPanel.SetActive(false);
        }
    }

    /* --------------------- HUD hookup (unchanged) --------------------- */

    [TargetRpc]
    public void TargetSetHUD(NetworkConnection conn, int slotIndex, string name, int profitAmount)
    {
        StartCoroutine(WaitForHUD(slotIndex, name, profitAmount, conn));
    }

    private IEnumerator WaitForHUD(int slotIndex, string name, int profitAmount, NetworkConnection conn)
    {
        while (GameHUD.Instance == null)
            yield return null;

        var panel = GameHUD.Instance.CreatePlayerPanel(slotIndex, name, profitAmount);

        if (conn?.FirstObject != null && conn.FirstObject.TryGetComponent(out PlayerPawn pawn))
        {
            pawn.infoPanel = panel;
            pawn.AddMoney(500);
        }
    }
}
