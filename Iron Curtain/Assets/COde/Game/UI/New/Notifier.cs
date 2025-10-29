using FishNet.Object;
using UnityEngine;

public class Notifier : NetworkBehaviour
{
    public static Notifier Instance;

    private void Awake() => Instance = this;

    /* === Server-side helpers ===
       Call from server game logic:
         Notifier.Instance.ToastAll("..");
         Notifier.Instance.ToastTo(pawn.Owner, "..");
    */
    [Server]
    public void ToastAll(string msg, ToastKind kind = ToastKind.Info) 
        => RpcToastAll(msg, kind);

    [Server]
    public void ToastTo(FishNet.Connection.NetworkConnection conn, string msg, ToastKind kind = ToastKind.Info) 
        => TargetToast(conn, msg, kind);

    /* === Wire to clients === */
    [ObserversRpc(BufferLast = true)]
    private void RpcToastAll(string msg, ToastKind kind)
        => ToastTray.Instance?.Enqueue(msg, kind);

    [TargetRpc]
    private void TargetToast(FishNet.Connection.NetworkConnection conn, string msg, ToastKind kind)
        => ToastTray.Instance?.Enqueue(msg, kind);
}

/* Severity / style */
public enum ToastKind { Info, Success, Warning, Error }
