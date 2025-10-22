// DebugServerBridge.cs
using FishNet.Object;
using FishNet.Connection;
using UnityEngine;

public class DebugServerBridge : NetworkBehaviour
{
    public static DebugServerBridge Instance;

    private void Awake() => Instance = this;

    // Multiplies payouts for *everyone* who owns anything, for N rounds.
    [ServerRpc(RequireOwnership = false)]
    public void CmdBoostAllPayouts(float multiplier, int durationRounds)
    {
        if (MarketManager.Instance == null) return;
        multiplier = Mathf.Max(0f, multiplier);              // no negatives
        durationRounds = Mathf.Max(1, durationRounds);
        MarketManager.Instance.ServerBoostAllPayouts(multiplier, durationRounds);
        Debug.Log($"[Debug] BoostAllPayouts x{multiplier} for {durationRounds} rounds.");
    }

    // Multiplies payouts only for the caller's portfolio.
    [ServerRpc(RequireOwnership = false)]
    public void CmdBoostMine(float multiplier, int durationRounds, NetworkConnection conn = null)
    {
        if (MarketManager.Instance == null || conn == null) return;

        var pawn = GameManager.Instance?.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;

        multiplier = Mathf.Max(0f, multiplier);
        durationRounds = Mathf.Max(1, durationRounds);

        // priceDeltaPercent = 0 (just testing payout multiplier)
        MarketManager.Instance.ServerBoostAllCompaniesOwnedBy(
            pawn, priceDeltaPercent: 0f, payoutMultiplier: multiplier, durationRounds: durationRounds
        );

        Debug.Log($"[Debug] BoostMine {pawn.playerName.Value}: x{multiplier} for {durationRounds} rounds.");
    }
}
