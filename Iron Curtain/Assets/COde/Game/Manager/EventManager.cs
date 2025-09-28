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

    // === TILE EVENT (custom desc) ===
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn, string desc)
    {
        waitingForAll = true;
        playersReady = 0;
        totalPlayers = GameManager.Instance != null ? GameManager.Instance.PlayerCount : 0;

        TargetShowEvent(pawn.Owner, desc, true);
    }

    // === TILE EVENT (random) ===
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (tileEvents == null || tileEvents.Count == 0)
            return;

        var e = tileEvents[Random.Range(0, tileEvents.Count)];
        waitingForAll = true;
        playersReady = 0;
        totalPlayers = GameManager.Instance != null ? GameManager.Instance.PlayerCount : 0;

        TargetShowEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);
        ApplyEventToPawn(e, pawn);
    }

    // === MAIN EVENT (global) ===
    [Server]
    public void TriggerMainEvent(int round)
    {
        waitingForAll = true;
        playersReady = 0;
        totalPlayers = GameManager.Instance != null ? GameManager.Instance.PlayerCount : 0;

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

    [TargetRpc]
    private void TargetShowEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.Show(message, pauseAll);
    }

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

    [Server]
    private void ResumeAfterEvent()
    {
        var pawn = TurnManager.Instance.GetCurrentPawn();
        if (pawn != null)
            pawn.TargetOpenStockUI(pawn.Owner);
    }

    // === Apply event effects ===
    [Server]
    private void ApplyEventToPawn(GameEventSO e, PlayerPawn pawn)
    {
        if (e == null || pawn == null) return;

        foreach (var effect in e.effects)
        {
            if (effect.moneyDelta != 0)
            {
                pawn.AddMoney(effect.moneyDelta);
                Debug.Log($"{pawn.playerName.Value} money changed by {effect.moneyDelta} from event {e.eventName}");
            }

            if (effect.targetType == TargetType.Stock && !string.IsNullOrEmpty(effect.targetName))
            {
                if (pawn.stockPortfolio.TryGetValue(effect.targetName, out var rec))
                {
                    rec.multiplier *= effect.multiplier;
                    rec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                    pawn.stockPortfolio[effect.targetName] = rec;
                }
            }

            if (effect.targetType == TargetType.Factory && !string.IsNullOrEmpty(effect.targetName))
            {
                if (pawn.factoryPortfolio.TryGetValue(effect.targetName, out var rec))
                {
                    rec.multiplier *= effect.multiplier;
                    rec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                    pawn.factoryPortfolio[effect.targetName] = rec;
                }
            }

            if (effect.targetType == TargetType.Global && GameManager.Instance != null)
            {
                foreach (var p in GameManager.Instance.GetPlayersSnapshot())
                {
                    if (effect.moneyDelta != 0)
                        p.AddMoney(effect.moneyDelta);

                    foreach (var kvp in p.stockPortfolio)
                    {
                        var r = kvp.Value;
                        r.multiplier *= effect.multiplier;
                        r.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                        p.stockPortfolio[kvp.Key] = r;
                    }

                    foreach (var kvp in p.factoryPortfolio)
                    {
                        var r = kvp.Value;
                        r.multiplier *= effect.multiplier;
                        r.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                        p.factoryPortfolio[kvp.Key] = r;
                    }
                }
            }
        }
    }

    [Server]
    private void ApplyEventToAll(GameEventSO e)
    {
        if (e == null || GameManager.Instance == null) return;
        foreach (var pawn in GameManager.Instance.GetPlayersSnapshot())
            ApplyEventToPawn(e, pawn);
    }
}
