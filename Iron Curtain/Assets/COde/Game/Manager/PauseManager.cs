using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

public class PauseManager : NetworkBehaviour
{
    public static PauseManager Instance;

    // Server truth
    public readonly SyncVar<bool> isPaused = new();
    private readonly HashSet<int> _votes = new(); // you can use this later if you want vote-pause

    private void Awake() => Instance = this;

    [ServerRpc(RequireOwnership = false)]
    public void CmdSetPaused(bool paused, NetworkConnection caller = null)
    {
        // Update server state
        isPaused.Value = paused;

        // Figure out who pressed the button
        string whoName = "";

        if (caller != null && GameManager.Instance != null && GameManager.Instance.Players != null)
        {
            var pawn = GameManager.Instance.Players.Find(p => p != null && p.Owner == caller);
            if (pawn != null)
                whoName = pawn.playerName.Value;
        }

        // Broadcast to all clients
        RpcOnPauseState(paused, whoName);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcOnPauseState(bool paused, string pausedByName)
    {
        PauseUI.Instance?.SetPaused(paused, pausedByName);
    }

    public static bool IsPaused() => Instance != null && Instance.isPaused.Value;
}
