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
    public Transform[] boardTiles; // ช่องบนกระดาน (วาง empty GameObject ตามลำดับช่อง)

    private Dictionary<int, PlayerPawn> playerPawns = new Dictionary<int, PlayerPawn>();

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

    // 🚀 เรียกตอน Host กด Start Game ใน Lobby
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

    // ✅ เรียกโดย FishNet หลัง Scene โหลดเสร็จ
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("✅ BoardGameScene loaded. Moving players to start...");

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
        {
            if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                MovePawnToStart(conn.ClientId, lobbyPlayer);
            }
        }

        // ✅ Handle late joins
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

    [Server]
    private void MovePawnToStart(int connectionId, NetworkLobbyPlayer lobbyPlayer)
    {
        if (!InstanceFinder.ServerManager.Clients.TryGetValue(connectionId, out NetworkConnection conn))
            return;

        // If player already has a pawn (their FirstObject), move it
        if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out PlayerPawn pawn))
        {
            int startTile = 0; // ✅ starting waypoint index
            pawn.transform.position = GetTilePosition(startTile);

            // ✅ Copy lobby data → pawn
            pawn.playerName.Value = lobbyPlayer.playerName.Value;
            pawn.business.Value   = lobbyPlayer.business.Value;
            pawn.country.Value    = lobbyPlayer.country.Value;

            playerPawns[connectionId] = pawn;

            Debug.Log($"✅ Moved {pawn.playerName.Value} to Tile {startTile}.");
        }
        else
        {
            Debug.LogWarning($"❌ No pawn found for connection {connectionId}, can’t move.");
        }
    }
}
