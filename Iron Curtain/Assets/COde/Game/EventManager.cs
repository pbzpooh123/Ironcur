using FishNet;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

public class EventManager : NetworkBehaviour
{
    public static EventManager Instance;

    private int playersReady = 0;
    private int totalPlayers = 0;
    private bool waitingForAll = false;

    private void Awake()
    {
        Instance = this;
    }

    // === Tile Event ===
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn, string desc)
    {
        TargetShowEvent(pawn.Owner, desc, false);
    }

    // === Main Event (every 3 rounds) ===
    [Server]
    public void TriggerMainEvent(int round)
    {
        waitingForAll = true;
        playersReady = 0;
        totalPlayers = GameManager.Instance.Players.Count;

        string msg = $"🌍 Main Event at Round {round}!";
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowEvent(conn, msg, true);
    }

    // === Client-side popup ===
    [TargetRpc]
    private void TargetShowEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        EventUI.Instance.Show(message, pauseAll);
    }

    // === Called by UI button ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerReady(NetworkConnection conn = null)
    {
        if (!waitingForAll) return;

        playersReady++;
        Debug.Log($"Player ready: {playersReady}/{totalPlayers}");

        if (playersReady >= totalPlayers)
        {
            waitingForAll = false;
            Debug.Log("✅ All players acknowledged event. Resuming game!");
        }
    }

   
}
