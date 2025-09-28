using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Managing.Scened;
using FishNet;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Transporting;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Board Setup")]
    public Transform[] boardTiles; // assign in MainGameScene!

    private readonly Dictionary<int, PlayerPawn> _playerPawns = new();
    private readonly Dictionary<int, int> _playerSlots = new();
    
    private readonly HashSet<int> _hudSent = new();

    private int _nextSlotIndex = 0;
    private bool _mainSceneLoadedServer = false;
    public int PlayerCount => _playerPawns.Count;
    public List<PlayerPawn> GetPlayersSnapshot()
    {
        
        return new List<PlayerPawn>(_playerPawns.Values);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public int TileCount => boardTiles?.Length ?? 0;

    public Vector3 GetTilePosition(int index)
    {
        if (boardTiles == null || index < 0 || index >= boardTiles.Length)
            return Vector3.zero;
        return boardTiles[index].position;
    }

    
    /* SERVER lifecycle for scene load */
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        InstanceFinder.SceneManager.OnLoadEnd += HandleSceneLoadEnd_Server;
        
        InstanceFinder.ServerManager.OnRemoteConnectionState += OnPlayerRemoteConnectionState_Server;
    }

    public override void OnStopServer()
    {
        // Cleanup subscriptions
        if (InstanceFinder.SceneManager != null)
            InstanceFinder.SceneManager.OnLoadEnd -= HandleSceneLoadEnd_Server;

        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnPlayerRemoteConnectionState_Server;

        base.OnStopServer();
    }

    private void HandleSceneLoadEnd_Server(SceneLoadEndEventArgs args)
    {
        if (!InstanceFinder.IsServer) return;
        
        bool loadedMain = false;
        foreach (var s in args.LoadedScenes)
        {
            if (s.name == "MainGameScene") { loadedMain = true; break; }
        }
        if (!loadedMain) return;

        _mainSceneLoadedServer = true;

        foreach (var kvp in InstanceFinder.ServerManager.Clients)
        {
            var conn = kvp.Value;
            if (conn?.FirstObject == null) continue;
            if (conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lp))
                MovePawnToStart(conn, lp);
        }
    }

    private void OnPlayerRemoteConnectionState_Server(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Started || !_mainSceneLoadedServer) return;
        if (conn?.FirstObject == null) return;
        if (conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lp))
            MovePawnToStart(conn, lp); // same path
    }

    [Server]
    private void MovePawnToStart(NetworkConnection conn, NetworkLobbyPlayer lobbyPlayer)
    {
        int connId = conn.ClientId;

        if (conn.FirstObject == null || !conn.FirstObject.TryGetComponent(out PlayerPawn pawn))
        {
            Debug.LogWarning($"[Server] No pawn for connection {connId}.");
            return;
        }
        
        _playerPawns[connId] = pawn;
        
        pawn.playerName.Value = lobbyPlayer.playerName.Value;
        if (pawn.money.Value == 0) pawn.money.Value = 1500; 

        
        int slot = GetOrAssignSlot(connId);
        _playerSlots[connId] = slot; 
        
        if (_hudSent.Add(connId))
            Rpc_AddOrUpdateHUD(slot, pawn.playerName.Value, pawn.money.Value);

        Debug.Log($"[Server] Placed '{pawn.playerName.Value}' at tile 0, slot {slot}, ${pawn.money.Value}");
    }

    [Server]
    public int GetSlotForPlayer(int connectionId)
    {
        return _playerSlots.TryGetValue(connectionId, out int slot) ? slot : -1;
    }
    
    [ObserversRpc]
    private void Rpc_AddOrUpdateHUD(int slotIndex, string name, int money)
    {
        Instance.StartCoroutine(WaitHUD(slotIndex, name, money));
    }

    private IEnumerator WaitHUD(int slot, string name, int money)
    {
        while (GameHUD.Instance == null)
            yield return null;
        
    }
    
    private readonly Dictionary<int, int> _slotByConn = new(); // connId -> slot

    [Server]
    private int GetOrAssignSlot(int connId)
    {
        if (_slotByConn.TryGetValue(connId, out int s))
            return s;

        // Assign next slot by join order (0,1,2,3...)
        s = _slotByConn.Count % 4;   // or % maxPanels
        _slotByConn[connId] = s;
        return s;
    }
    
    
}
