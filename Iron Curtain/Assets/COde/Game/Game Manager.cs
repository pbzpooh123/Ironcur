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
    public GameObject pawnPrefab;  // Prefab หมากผู้เล่น (3D pawn)

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
        Debug.Log("✅ BoardGameScene loaded. Waiting for players...");

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
        {
            if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                SpawnPawnForPlayer(conn.ClientId, lobbyPlayer);
            }
        }

        // ✅ เผื่อกรณี player join ช้ากว่า
        InstanceFinder.ServerManager.OnRemoteConnectionState += OnPlayerJoined;
    }

    private void OnPlayerJoined(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            if (conn.FirstObject != null && conn.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobbyPlayer))
            {
                SpawnPawnForPlayer(conn.ClientId, lobbyPlayer);
            }
        }
    }

    [Server]
    private void SpawnPawnForPlayer(int connectionId, NetworkLobbyPlayer lobbyPlayer)
    {
        NetworkConnection conn = InstanceFinder.ServerManager.Clients[connectionId];
        GameObject pawnObj = Instantiate(pawnPrefab, GetTilePosition(0), Quaternion.identity);

        PlayerPawn pawn = pawnObj.GetComponent<PlayerPawn>();

        // ✅ ใช้ .Value สำหรับ SyncVar<T>
        pawn.playerName.Value = lobbyPlayer.playerName.Value;
        pawn.business.Value   = lobbyPlayer.business.Value;
        pawn.country.Value    = lobbyPlayer.country.Value;

        Spawn(pawnObj, conn); // FishNet spawn

        playerPawns[connectionId] = pawn;
        Debug.Log($"Spawned pawn for {pawn.playerName.Value} at Start Tile.");
    }

}
