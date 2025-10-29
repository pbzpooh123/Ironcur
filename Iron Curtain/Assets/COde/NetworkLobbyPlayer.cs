using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Managing;
using UnityEngine;

public class NetworkLobbyPlayer : NetworkBehaviour
{
    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<bool> isReady = new();
    public readonly SyncVar<float> profit = new();

    // NEW: chosen color (unique)
    public readonly SyncVar<int> colorIndex = new();

    private LobbyUI lobbyUI;

    public override void OnStartServer()
     {
        base.OnStartServer();
           colorIndex.Value = -1; 
     }

   public override void OnStartClient()
    {
        base.OnStartClient();

        isReady.OnChange += OnReadyStatusChanged;
        colorIndex.OnChange += (oldV, newV, asServer) => ApplyMyColor(newV);

        if (IsOwner)
        {
            string n  = PlayerPrefs.GetString("PlayerName", "Player");
            int ci    = PlayerPrefs.GetInt("ColorIndex", 0);
            CmdSetProfile(n, ci);       
            RequestRoomCode();           
        }
    }

    private void FindLobbyUIAndPushProfile()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();

        if (IsOwner)
        {
            string n = PlayerPrefs.GetString("PlayerName", "Player");
            RequestRoomCode();
        }
    }

   private void ApplyMyColor(int idx)
    {
        PlayerPawn mine = null;
        foreach (var pawn in FindObjectsOfType<PlayerPawn>())
            if (pawn != null && pawn.Owner == Owner) { mine = pawn; break; }
        if (mine == null) return;

        if (IsOwner)
        {
            mine.ApplyColorIndex(idx); 
        }
        else
        {
            mine.ApplyColor(idx);
        }
    }

    /* ---------------- Profile + color (unique) ---------------- */

    [ServerRpc]
    public void CmdSetProfile(string newName, int desiredColorIndex)
    {
        playerName.Value = string.IsNullOrWhiteSpace(newName) ? "Player" : newName;
        NetworkManagerLobby.Instance.UpdateLobbyUI();
        if (ColorLockManager.Instance != null)
        {
            ColorLockManager.Instance.CmdPick(desiredColorIndex, playerName.Value, Owner);
        }
    }

    /* ---------------- Room code + lobby list ---------------- */

    [ServerRpc] public void RequestRoomCode()
        => TargetReceiveRoomCode(Owner, NetworkManagerLobby.Instance.roomCode);

    [TargetRpc]
    public void TargetReceiveRoomCode(FishNet.Connection.NetworkConnection conn, string code)
    {
        FindObjectOfType<LobbyUI>()?.SetRoomCode(code);
    }

    [ObserversRpc]
    public void UpdatedPlayerList(List<string> names, List<bool> readies, List<int> connIds)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        lobbyUI?.UpdatePlayerList(names, readies, connIds);
    }

    [ServerRpc]
    public void JoinRoom(string enteredCode)
    {
        if (NetworkManagerLobby.Instance.roomCode == enteredCode)
            TargetShowWaitingPanel(Owner);
        else
            Debug.Log("Invalid Room Code!");
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

    [ServerRpc] public void SetReady(bool value)
    {
        isReady.Value = value;
        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    private void OnReadyStatusChanged(bool oldVal, bool newVal, bool asServer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    /* ---------------- Game HUD bridge (unchanged) ---------------- */

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