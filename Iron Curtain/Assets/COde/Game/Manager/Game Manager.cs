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
            pawn.transform.position = GetTilePosition(0);

            pawn.playerName.Value = lobbyPlayer.playerName.Value;

            playerPawns[connectionId] = pawn;

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
            
            Debug.Log($"[Server] Assigning HUD slot {assignedSlot} to {pawn.playerName.Value}");
        }
        else
        {
            Debug.LogWarning($"[Server] No pawn found for connection {connectionId}.");
        }
       
    }
}