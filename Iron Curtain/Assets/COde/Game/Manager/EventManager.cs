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

    /// <summary>
    /// Custom text tile event (no data). Only the pawn sees it. Wait for 1 ack.
    /// </summary>
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn, string desc)
    {
        waitingForAcks = true;
        playersReady = 0;
        requiredReady = 1; // only this pawn must acknowledge

        TargetShowEvent(pawn.Owner, desc, true);
        // No effects here, just a popup.
    }

    /// <summary>
    /// Random tile event from database. Only the pawn sees it. Wait for 1 ack.
    /// </summary>
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (tileEvents == null || tileEvents.Count == 0)
            return;

        var e = tileEvents[Random.Range(0, tileEvents.Count)];

        waitingForAcks = true;
        playersReady = 0;
        requiredReady = 1;

        TargetShowEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);
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
            TargetShowEvent(conn, msg, true);

        // Apply effects to everyone
        if (e != null)
            ApplyEventToAll(e);
    }

    /* =============== Popup RPC to specific client =============== */

    [TargetRpc]
    private void TargetShowEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.Show(message, pauseAll);
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
            // After popup close → let current pawn do stock invest, then end turn.
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
            // 1) direct money
            if (effect.moneyDelta != 0)
                pawn.AddMoney(effect.moneyDelta);

            // 2) random money
            if (effect.randomMoneyMax > effect.randomMoneyMin)
            {
                int rnd = Random.Range(effect.randomMoneyMin, effect.randomMoneyMax + 1);
                pawn.AddMoney(rnd);
            }

            // 3) skip turn
            if (effect.skipTurn)
            {
                // duration = number of future turns to skip
                TurnManager.Instance.MarkSkipTurn(pawn, Mathf.Max(1, effect.duration));
            }

            // 4) multipliers
            if (effect.multiplier != 1f && effect.duration > 0)
            {
                switch (effect.targetType)
                {
                    case TargetType.Stock:
                        if (!string.IsNullOrEmpty(effect.targetName) &&
                            pawn.stockPortfolio.TryGetValue(effect.targetName, out var srec))
                        {
                            srec.multiplier *= effect.multiplier;
                            srec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                            pawn.stockPortfolio[effect.targetName] = srec; // write-back if struct
                        }
                        break;

                    case TargetType.Factory:
                        if (!string.IsNullOrEmpty(effect.targetName) &&
                            pawn.factoryPortfolio.TryGetValue(effect.targetName, out var frec))
                        {
                            frec.multiplier *= effect.multiplier;
                            frec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                            pawn.factoryPortfolio[effect.targetName] = frec; // write-back if struct
                        }
                        break;

                    case TargetType.Global:
                        // Global = apply to all stocks and factories this pawn holds
                        {
                            // Stocks
                            var stockKeys = new List<string>(pawn.stockPortfolio.Keys);
                            foreach (var key in stockKeys)
                            {
                                var r = pawn.stockPortfolio[key];
                                r.multiplier *= effect.multiplier;
                                r.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                                pawn.stockPortfolio[key] = r;
                            }
                            // Factories
                            var factoryKeys = new List<string>(pawn.factoryPortfolio.Keys);
                            foreach (var key in factoryKeys)
                            {
                                var r = pawn.factoryPortfolio[key];
                                r.multiplier *= effect.multiplier;
                                r.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                                pawn.factoryPortfolio[key] = r;
                            }
                        }
                        break;
                }
            }
        }
    }

    [Server]
    private void ApplyEventToAll(GameEventSO e)
    {
        if (e == null || GameManager.Instance == null) return;
        foreach (var pawn in GameManager.Instance.Players)
            ApplyEventToPawn(e, pawn);
    }
}
