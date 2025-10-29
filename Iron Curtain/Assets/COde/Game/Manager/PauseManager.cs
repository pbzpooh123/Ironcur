using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class PauseManager : NetworkBehaviour
{
    public static PauseManager Instance;

    // Server truth
    public readonly SyncVar<bool> isPaused = new();
    private readonly HashSet<int> _votes = new(); // (optional) ready for future vote system

    private void Awake() => Instance = this;

    [ServerRpc(RequireOwnership = false)]
    public void CmdSetPaused(bool paused)
    {
        // keep it simple: host/server decides
        isPaused.Value = paused;
        RpcOnPauseState(paused);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcOnPauseState(bool paused)
    {
        // Optional: dim a pause overlay, mute sfx, etc.
        PauseUI.Instance?.SetPaused(paused);
    }

    public static bool IsPaused() => Instance != null && Instance.isPaused.Value;
}
