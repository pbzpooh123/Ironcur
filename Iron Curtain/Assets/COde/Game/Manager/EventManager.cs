using System.Collections.Generic;
using FishNet;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

public class EventManager : NetworkBehaviour
{
    public static EventManager Instance;

    private int playersReady = 0;
    private int requiredReady = 0;
    private bool waitingForAcks = false;

    [Header("Event Databases (Optional)")]
    public List<GameEventSO> tileEvents = new List<GameEventSO>();
    public List<GameEventSO> mainEvents = new List<GameEventSO>();

    private void Awake()
    {
        Instance = this;
    }

    /* ================= TILE EVENTS ================= */

    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (tileEvents == null || tileEvents.Count == 0 || pawn == null)
            return;

        var e = tileEvents[Random.Range(0, tileEvents.Count)];

        waitingForAcks = true;
        playersReady = 0;
        requiredReady = 1; // only the triggering pawn must ack

        TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}");
        ApplyEventToPawn(e, pawn);
    }

    /* ================== MAIN EVENTS ================== */

    [Server]
    public void TriggerMainEvent(int round)
    {
        waitingForAcks = true;
        playersReady = 0;

        int totalPlayers = (GameManager.Instance != null) ? GameManager.Instance.Players.Count : 0;
        requiredReady = Mathf.Max(1, totalPlayers);

        string msg;
        GameEventSO e = null;

        if (mainEvents != null && mainEvents.Count > 0)
        {
            e = mainEvents[Random.Range(0, mainEvents.Count)];
            msg = $"{e.eventName}\n\n{e.description}";
        }
        else
        {
            msg = $"🌍 Main Event at Round {round}!";
        }

        // Show popup to everyone
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, msg, true);

        // Apply effects to everyone (per pawn!)
        if (e != null)
            ApplyEventToAll(e);
    }

    /* =============== Popup RPCs =============== */

    [TargetRpc]
    private void TargetShowMainEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.MaineventShow(message, pauseAll);
    }

    [TargetRpc]
    private void TargetShowSideEvent(NetworkConnection conn, string message)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.SideeventShow(message);
    }

    /* ============ Player clicked OK on popup ============ */
    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerReady(NetworkConnection conn = null)
    {
        if (!waitingForAcks) return;

        playersReady++;
        Debug.Log($"[EventManager] Ack {playersReady}/{requiredReady}");

        if (playersReady >= requiredReady)
        {
            waitingForAcks = false;
            Debug.Log("[EventManager] All required acks received. Resuming.");
            ResumeAfterEvent();
        }
    }

    /* ================= Resume game flow ================= */

    [Server]
    private void ResumeAfterEvent()
    {
        var pawn = TurnManager.Instance.GetCurrentPawn();
        if (pawn != null)
        {
            pawn.TargetOpenStockUI(pawn.Owner);
            pawn.TargetEnableEndTurn(pawn.Owner, true);
        }
    }

    /* ================= Apply Effects ================= */

    [Server]
    private void ApplyEventToPawn(GameEventSO e, PlayerPawn pawn)
    {
        if (e == null || pawn == null) return;

        foreach (var effect in e.effects)
        {
            // 1) Instant / Random money
            int totalMoney = effect.moneyDelta;
            if (effect.randomMoneyMax > effect.randomMoneyMin)
            {
                totalMoney += Random.Range(effect.randomMoneyMin, effect.randomMoneyMax + 1);
            }
            if (totalMoney != 0)
                pawn.AddMoney(totalMoney);

            // 2) Skip turn
            if (effect.skipTurn)
                TurnManager.Instance.MarkSkipTurn(pawn, Mathf.Max(1, effect.duration));

            // 3) Per-type logic
            switch (effect.targetType)
            {
                case TargetType.Factory:
                    if (!string.IsNullOrEmpty(effect.targetName) &&
                        pawn.factoryPortfolio.TryGetValue(effect.targetName, out var facRec))
                    {
                        // stack multiplier if != 1
                        if (!Mathf.Approximately(effect.multiplier, 1f))
                        {
                            facRec.multiplier *= effect.multiplier;
                            facRec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                            pawn.factoryPortfolio[effect.targetName] = facRec; // write-back if struct
                        }
                    }
                    break;

                case TargetType.Ownership:
                    if (!string.IsNullOrEmpty(effect.targetName) &&
                        pawn.factoryPortfolio.TryGetValue(effect.targetName, out var ownRec))
                    {
                        ownRec.sharePercent += effect.ownershipDelta;
                        ownRec.sharePercent = Mathf.Clamp(ownRec.sharePercent, 0, 100);
                        pawn.factoryPortfolio[effect.targetName] = ownRec; // write-back
                    }
                    break;

                case TargetType.Global:
                    // Apply to THIS pawn only. ApplyEventToAll() will iterate all players.
                    // Multiplier applies to all of THIS pawn's companies.
                    if (!Mathf.Approximately(effect.multiplier, 1f) || effect.duration > 0)
                    {
                        var keys = new List<string>(pawn.factoryPortfolio.Keys);
                        foreach (var key in keys)
                        {
                            var gRec = pawn.factoryPortfolio[key];
                            gRec.multiplier *= effect.multiplier;
                            gRec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                            pawn.factoryPortfolio[key] = gRec; // write-back
                        }
                    }
                    break;

                default:
                    break;
            }
        }
    }

    [Server]
    private void ApplyEventToAll(GameEventSO e)
    {
        if (e == null || GameManager.Instance == null) return;

        // Apply the same logic for each player individually.
        foreach (var pawn in GameManager.Instance.Players)
            ApplyEventToPawn(e, pawn);
    }
}
