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

            // also clear the NetworkLobbyPlayer’s SyncVar
            var lp = connOf(cid)?.FirstObject?.GetComponent<NetworkLobbyPlayer>();
            if (lp != null) lp.colorIndex.Value = -1;
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
        if (slotIndex < 0 || slotIndex >= _ownerCid.Length) return;
        if (_ownerCid[slotIndex] >= 0) return; // already taken

        // free previous if moving
        if (_cidToSlot.TryGetValue(conn.ClientId, out int prev) && prev >= 0)
        {
            _ownerCid[prev] = -1;
            _ownerName[prev] = "";
        }

        _ownerCid[slotIndex] = conn.ClientId;
        _ownerName[slotIndex] = string.IsNullOrWhiteSpace(playerName) ? $"P{conn.ClientId}" : playerName.Trim();
        _cidToSlot[conn.ClientId] = slotIndex;

        // set the SyncVar on their lobby player so your pawn gets tinted later
        var lp = conn.FirstObject?.GetComponent<NetworkLobbyPlayer>();
        if (lp != null) lp.colorIndex.Value = slotIndex;

        RpcSync(_ownerName, _ownerCid);
        TargetColorAssigned(conn, slotIndex); // persist to PlayerPrefs client-side
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
}
