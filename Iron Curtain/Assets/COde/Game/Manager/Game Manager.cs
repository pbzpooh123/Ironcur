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
    public Transform[] boardTiles; 

    private Dictionary<int, PlayerPawn> playerPawns = new Dictionary<int, PlayerPawn>();
    private Dictionary<int, int> playerSlots = new Dictionary<int, int>(); 
    
    public List<PlayerPawn> Players => new List<PlayerPawn>(playerPawns.Values);
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public int TileCount => boardTiles.Length;

    public Vector3 GetTilePosition(int index)
    {
        if (index < 0 || index >= boardTiles.Length) return Vector3.zero;
        return boardTiles[index].position;
    }

    
    [Server]
    public void StartGame()
    {
        Debug.Log("Loading BoardGameScene...");

        SceneLoadData data = new SceneLoadData("BoardGameScene")
        {
            ReplaceScenes = ReplaceOption.All
        };

        InstanceFinder.SceneManager.LoadGlobalScenes(data);
    }

   
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("BoardGameScene loaded. Moving players to start...");

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
        {
            if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                MovePawnToStart(conn.ClientId, lobbyPlayer);
            }
        }

        // Handle late joins
        InstanceFinder.ServerManager.OnRemoteConnectionState += OnPlayerJoined;
    }

    private void OnPlayerJoined(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                MovePawnToStart(conn.ClientId, lobbyPlayer);
            }
        }
    }

    private int nextSlotIndex = 0;

    [Server]
    private void MovePawnToStart(int connectionId, NetworkLobbyPlayer lobbyPlayer)
    {
        if (!InstanceFinder.ServerManager.Clients.TryGetValue(connectionId, out NetworkConnection conn))
            return;

        if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out PlayerPawn pawn))
        {
            // IMPORTANT: assign ownership to the client
            if (pawn.Owner != conn)
            {
                pawn.GiveOwnership(conn);
                Debug.Log($"[Server] Ownership of {pawn.playerName.Value} pawn given to connection {connectionId}");
            }

            // Place pawn at tile 0
            pawn.transform.position = GetTilePosition(0);

            // Set sync vars
            pawn.playerName.Value = lobbyPlayer.playerName.Value;

            // Store references
            playerPawns[connectionId] = pawn;

            // Assign HUD slot
            if (!playerSlots.TryGetValue(connectionId, out int assignedSlot))
            {
                assignedSlot = nextSlotIndex % 4;
                playerSlots[connectionId] = assignedSlot;
                nextSlotIndex++;
            }

            lobbyPlayer.TargetSetHUD(
                lobbyPlayer.Owner,
                assignedSlot,
                pawn.playerName.Value,
                0
            );
        }
        else
        {
            Debug.LogWarning($"[Server] No pawn found for connection {connectionId}.");
        }
    }

    
    [ObserversRpc]
    private void RpcPlacePawnAtStart(int connId, Vector3 pos)
    {
        if (playerPawns.TryGetValue(connId, out PlayerPawn pawn))
        {
            pawn.transform.position = pos;
        }
    }
}