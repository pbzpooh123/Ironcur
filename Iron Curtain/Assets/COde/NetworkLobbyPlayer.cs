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

    public override void OnStartClient()
    {
        base.OnStartClient();

        // keep UI synced
        isReady.OnChange += OnReadyStatusChanged;

        // apply color on change (tints pawn for everyone)
        colorIndex.OnChange += (oldV, newV, asServer) => ApplyMyColor(newV);

        Invoke(nameof(FindLobbyUIAndPushProfile), 0.5f);
    }

    private void FindLobbyUIAndPushProfile()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();

        if (IsOwner)
        {
            string n = PlayerPrefs.GetString("PlayerName", "Player");
            int ci = PlayerPrefs.GetInt("ColorIndex", 0);
            CmdSetProfile(n, ci); // server will enforce uniqueness
            RequestRoomCode();
        }
    }

   private void ApplyMyColor(int idx)
    {
        
        foreach (var pawn in FindObjectsOfType<PlayerPawn>())
        {
            if (pawn != null && pawn.Owner == Owner)
            {
                pawn.ApplyColorIndex(idx); 
                break;
            }
        }
    }

    /* ---------------- Profile + color (unique) ---------------- */

    [ServerRpc]
    public void CmdSetProfile(string newName, int desiredColorIndex)
    {
        playerName.Value = string.IsNullOrWhiteSpace(newName) ? "Player" : newName;

        int picked = GetUniqueColor(desiredColorIndex);
        if (picked < 0)
        {
            // no color left, tell client to pick again (keeps previous)
            TargetColorDenied(Owner);
            NetworkManagerLobby.Instance.UpdateLobbyUI();
            return;
        }

        colorIndex.Value = picked;

        // for host/server pawn, apply immediately on server too
        ApplyMyColor(colorIndex.Value);

        TargetColorAssigned(Owner, colorIndex.Value);
        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    // Check other players' selected colors and pick the first free one
    private int GetUniqueColor(int desired)
    {
        desired = PlayerColors.Clamp(desired);

        var taken = new HashSet<int>();
        foreach (var kv in NetworkManager.ServerManager.Clients)
        {
            var no = kv.Value?.FirstObject;
            if (no == null) continue;
            var lp = no.GetComponent<NetworkLobbyPlayer>();
            if (lp == null) continue;
            if (lp.colorIndex.Value >= 0 && lp.colorIndex.Value < PlayerColors.Palette.Length)
                taken.Add(lp.colorIndex.Value);
        }

        if (!taken.Contains(desired))
            return desired;

        // find first free color
        for (int i = 0; i < PlayerColors.Palette.Length; i++)
            if (!taken.Contains(i))
                return i;

        // none free
        return -1;
    }

    [TargetRpc]
    private void TargetColorAssigned(FishNet.Connection.NetworkConnection conn, int idx)
    {
        PlayerPrefs.SetInt("ColorIndex", idx);
        PlayerPrefs.Save();
        // (Optional) flash a “Color reserved” UI message here
    }

    [TargetRpc]
    private void TargetColorDenied(FishNet.Connection.NetworkConnection conn)
    {
        // (Optional) show UI prompt “Color taken. Please pick another.”
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