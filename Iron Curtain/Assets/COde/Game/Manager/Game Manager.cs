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

    /* Called by host when pressing Start from lobby. Keep it simple and replace scenes. */
    [Server]
    public static void StartGame()
    {
        var data = new SceneLoadData("MainGameScene")
        {
            ReplaceScenes = ReplaceOption.All   // <— simple & version-safe
        };
        InstanceFinder.SceneManager.LoadGlobalScenes(data);
    }

    /* SERVER lifecycle for scene load */
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Listen for when MainGameScene finishes loading on the SERVER.
        InstanceFinder.SceneManager.OnLoadEnd += HandleSceneLoadEnd_Server;

        // Also track late joins (clients connecting AFTER scene change).
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

        // Only respond when our MainGameScene finished loading.
        bool loadedMain = false;
        foreach (var s in args.LoadedScenes)
        {
            if (s.name == "MainGameScene") { loadedMain = true; break; }
        }
        if (!loadedMain) return;

        _mainSceneLoadedServer = true;

        // Place each connected player and create HUD once.
        int idx = 0;
        foreach (var kvp in InstanceFinder.ServerManager.Clients)
        {
            NetworkConnection conn = kvp.Value;
            if (conn == null || conn.FirstObject == null) continue;

            if (conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                MovePawnToStart(conn, lobbyPlayer, idx);
                idx++;
            }
        }
    }

    private void OnPlayerRemoteConnectionState_Server(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (!InstanceFinder.IsServer) return;

        // Late join: when a client connects after the main scene is active, place them too.
        if (args.ConnectionState == RemoteConnectionState.Started && _mainSceneLoadedServer)
        {
            if (conn != null && conn.FirstObject != null &&
                conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                int idx = _nextSlotIndex; // give them the next slot
                MovePawnToStart(conn, lobbyPlayer, idx);
            }
        }
    }

    [Server]
    private void MovePawnToStart(NetworkConnection conn, NetworkLobbyPlayer lobbyPlayer, int preferredSlotIndex)
    {
        int connectionId = conn.ClientId;

        // Find pawn owned by this connection
        if (conn.FirstObject == null || !conn.FirstObject.TryGetComponent(out PlayerPawn pawn))
        {
            Debug.LogWarning($"[Server] No pawn found for connection {connectionId}.");
            return;
        }

        // Server-authoritative spawn at tile 0 (or choose per-player start point here)
        Vector3 startPos = GetTilePosition(0);
        pawn.transform.SetPositionAndRotation(startPos, Quaternion.identity);

        // Optionally set game logic state (tile index) on server
        // pawn.PlaceAtTile(0); // if you implemented a server method that sets SyncVar + position

        // Keep a reference
        _playerPawns[connectionId] = pawn;

        // Assign persistent slot for HUD
        if (!_playerSlots.TryGetValue(connectionId, out int assignedSlot))
        {
            assignedSlot = preferredSlotIndex % 4; // or however many slots you have
            _playerSlots[connectionId] = assignedSlot;
            _nextSlotIndex = assignedSlot + 1;
        }

        // Send HUD exactly once
        if (!_hudSent.Contains(connectionId))
        {
            _hudSent.Add(connectionId);

            // Ensure we pass correct data. If PlayerPawn doesn't have business/country, pass "".
            string name = lobbyPlayer.playerName.Value;

            lobbyPlayer.TargetSetHUD(
                lobbyPlayer.Owner,
                assignedSlot,
                name,
                0
            );

            Debug.Log($"[Server] HUD slot {assignedSlot} -> {name}");
        }
    }

    [Server]
    public int GetSlotForPlayer(int connectionId)
    {
        return _playerSlots.TryGetValue(connectionId, out int slot) ? slot : -1;
    }
}
