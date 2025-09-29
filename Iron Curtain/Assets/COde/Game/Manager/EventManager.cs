using System.Collections.Generic;
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

    [Header("Event Databases (Optional)")]
    public List<GameEventSO> tileEvents = new List<GameEventSO>();
    public List<GameEventSO> mainEvents = new List<GameEventSO>();

    private void Awake()
    {
        Instance = this;
    }

    // === TILE EVENT (match your call: TriggerTileEvent(this, data.description)) ===
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn, string desc)
    {
        waitingForAll = true;
        playersReady = 0;
        totalPlayers = GameManager.Instance.Players.Count;

        // only to the pawn who triggered
        TargetShowEvent(pawn.Owner, desc, true);
    }

   
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (tileEvents == null || tileEvents.Count == 0)
            return;

        var e = tileEvents[Random.Range(0, tileEvents.Count)];
        waitingForAll = true;
        playersReady = 0;
        totalPlayers = GameManager.Instance.Players.Count;

        TargetShowEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);
        ApplyEventToPawn(e, pawn); // only to the pawn who triggered
    }

    // === MAIN EVENT (global, every 3 rounds) ===
    [Server]
    public void TriggerMainEvent(int round)
    {
        waitingForAll = true;
        playersReady = 0;
        totalPlayers = GameManager.Instance.Players.Count;

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

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowEvent(conn, msg, true);

        if (e != null)
            ApplyEventToAll(e);
    }

    // === Client popup ===
    [ObserversRpc]
    private void TargetShowEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.Show(message, pauseAll);
    }

    // === Players click "Continue" on the event popup ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerReady(NetworkConnection conn = null)
    {
        if (!waitingForAll) return;

        playersReady++;
        Debug.Log($"Player ready: {playersReady}/{totalPlayers}");

        if (playersReady >= totalPlayers)
        {
            waitingForAll = false;
            Debug.Log("All players acknowledged event. Resuming game!");
            ResumeAfterEvent();
        }
    }

    // === Resume game: show stock UI → enable End Turn for the current pawn ===
    [Server]
    private void ResumeAfterEvent()
    {
        var pawn = TurnManager.Instance.GetCurrentPawn();
        if (pawn != null)
        {
            // These must be public TargetRpcs on PlayerPawn
            pawn.TargetOpenStockUI(pawn.Owner);
        }
    }

    // === Apply event effects ===
    [Server]
private void ApplyEventToPawn(GameEventSO e, PlayerPawn pawn)
{
    if (e == null || pawn == null) return;

    foreach (var effect in e.effects)
    {
        // === Instant money ===
        if (effect.moneyDelta != 0)
        {
            pawn.AddMoney(effect.moneyDelta);
            Debug.Log($"{pawn.playerName.Value} money changed by {effect.moneyDelta} from event {e.eventName}");
        }

        // === Stock effect ===
        if (effect.targetType == TargetType.Stock && !string.IsNullOrEmpty(effect.targetName))
        {
            if (pawn.stockPortfolio.ContainsKey(effect.targetName))
            {
                var rec = pawn.stockPortfolio[effect.targetName];
                rec.multiplier *= effect.multiplier;
                rec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;

                Debug.Log($"{pawn.playerName.Value}'s {effect.targetName} stock multiplier x{rec.multiplier} until round {rec.multiplierExpiresAt}");
            }
        }

        // === Factory effect ===
        if (effect.targetType == TargetType.Factory && !string.IsNullOrEmpty(effect.targetName))
        {
            if (pawn.factoryPortfolio.ContainsKey(effect.targetName))
            {
                var rec = pawn.factoryPortfolio[effect.targetName];
                rec.multiplier *= effect.multiplier;
                rec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;

                Debug.Log($"{pawn.playerName.Value}'s {effect.targetName} factory multiplier x{rec.multiplier} until round {rec.multiplierExpiresAt}");
            }
        }

        // === Global effect (money + multipliers for all) ===
        if (effect.targetType == TargetType.Global)
        {
            foreach (var p in GameManager.Instance.Players)
            {
                // Global instant money
                if (effect.moneyDelta != 0)
                {
                    p.AddMoney(effect.moneyDelta);
                    Debug.Log($"{p.playerName.Value} global money change {effect.moneyDelta} from {e.eventName}");
                }

                // Apply global stock multiplier
                foreach (var kvp in p.stockPortfolio)
                {
                    var rec = kvp.Value;
                    rec.multiplier *= effect.multiplier;
                    rec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                }

                // Apply global factory multiplier
                foreach (var kvp in p.factoryPortfolio)
                {
                    var rec = kvp.Value;
                    rec.multiplier *= effect.multiplier;
                    rec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                }
            }
        }
    }
}

[Server]
private void ApplyEventToAll(GameEventSO e)
{
    if (e == null) return;
    foreach (var pawn in GameManager.Instance.Players)
        ApplyEventToPawn(e, pawn);
}

}