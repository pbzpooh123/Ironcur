using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;

public class ColorLockManager : NetworkBehaviour
{
    public static ColorLockManager Instance;

    [Header("Must match lobby color button count")]
    public int slotCount = 6;

    private int[] _ownerCid;
    private string[] _ownerName;
    private readonly Dictionary<int,int> _cidToSlot = new();

    private void Awake() => Instance = this;

    public override void OnStartServer()
    {
        base.OnStartServer();
        _ownerCid = new int[slotCount];
        _ownerName = new string[slotCount];
        for (int i = 0; i < slotCount; i++) { _ownerCid[i] = -1; _ownerName[i] = ""; }

        InstanceFinder.ServerManager.OnRemoteConnectionState += OnClientState;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnClientState;
    }

    private void OnClientState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
            ReleaseByCid(conn.ClientId);
    }

    [Server]
    private void ReleaseByCid(int cid)
    {
        if (_cidToSlot.TryGetValue(cid, out int slot))
        {
            if (slot >= 0 && slot < _ownerCid.Length && _ownerCid[slot] == cid)
            {
                _ownerCid[slot] = -1;
                _ownerName[slot] = "";
            }
            _cidToSlot.Remove(cid);
            RpcSync(_ownerName, _ownerCid);

            var conn = connOf(cid);
            if (conn != null) // may already be null at Stopped
            {
                var lp = conn.FirstObject?.GetComponent<NetworkLobbyPlayer>();
                if (lp != null) lp.colorIndex.Value = -1;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestSnapshot(NetworkConnection conn = null)
    {
        if (conn == null) return;
        TargetSnapshot(conn, _ownerName, _ownerCid);
    }

    [TargetRpc]
    private void TargetSnapshot(NetworkConnection conn, string[] names, int[] cids)
    {
        LobbyColorBinder.ApplyOwners(cids);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPick(int slotIndex, string playerName, NetworkConnection conn = null)
    {
        if (conn == null) return;

        // auto-pick next free if requested one is taken
        if (slotIndex < 0 || slotIndex >= _ownerCid.Length)
            slotIndex = 0;

        if (_ownerCid[slotIndex] >= 0)
        {
            int alt = FindNextFreeSlot(slotIndex);
            if (alt == -1)
            {
                // no colors left – bail
                TargetColorAssigned(conn, -1);
                return;
            }
            slotIndex = alt;
        }

        // free previous if moving
        if (_cidToSlot.TryGetValue(conn.ClientId, out int prev) && prev >= 0)
        {
            _ownerCid[prev] = -1;
            _ownerName[prev] = "";
        }

        _ownerCid[slotIndex] = conn.ClientId;
        _ownerName[slotIndex] = string.IsNullOrWhiteSpace(playerName) ? $"P{conn.ClientId}" : playerName.Trim();
        _cidToSlot[conn.ClientId] = slotIndex;

        // Set the lobby SyncVar
        var lp = conn.FirstObject?.GetComponent<NetworkLobbyPlayer>();
        if (lp != null) lp.colorIndex.Value = slotIndex;

        // NEW: If a pawn already exists, push now (so you don’t rely on later pulls)
        ApplyColorToExistingPawn(conn.ClientId, slotIndex);

        RpcSync(_ownerName, _ownerCid);
        TargetColorAssigned(conn, slotIndex); // persist to PlayerPrefs
    }

    [TargetRpc]
    private void TargetColorAssigned(NetworkConnection conn, int slotIndex)
    {
        PlayerPrefs.SetInt("ColorIndex", slotIndex);
        PlayerPrefs.Save();
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcSync(string[] names, int[] cids)
    {
         LobbyColorBinder.ApplyOwners(cids);
    }

    [Server]
    public bool TryGetSlotForCid(int cid, out int slotIndex)
    {
        return _cidToSlot.TryGetValue(cid, out slotIndex);
    }

    private NetworkConnection connOf(int cid)
    {
        InstanceFinder.ServerManager.Clients.TryGetValue(cid, out var conn);
        return conn;
    }

    [Server]
    private int FindNextFreeSlot(int startIdx)
    {
        if (_ownerCid == null || _ownerCid.Length == 0) return -1;
        int n = _ownerCid.Length;
        for (int i = 0; i < n; i++)
        {
            int k = (startIdx + i) % n;
            if (_ownerCid[k] < 0) return k;
        }
        return -1;
    }

    [Server]
    private void ApplyColorToExistingPawn(int cid, int slotIndex)
    {
        // If a PlayerPawn for this owner already exists (e.g., in game scene), set its SyncVar now.
        foreach (var netObj in InstanceFinder.ServerManager.Objects.Spawned.Values)
        {
            if (netObj.Owner != null && netObj.Owner.ClientId == cid &&
                netObj.TryGetComponent(out PlayerPawn pawn))
            {
                pawn.colorIndex.Value = PlayerColors.ClampOrUnset(slotIndex);
                break;
            }
        }
    }

    
}