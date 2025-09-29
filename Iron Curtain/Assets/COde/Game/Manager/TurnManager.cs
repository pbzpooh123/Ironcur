using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    private List<PlayerPawn> turnOrder = new List<PlayerPawn>(); // shuffled order

    public readonly SyncVar<int> currentPlayerIndex = new();
    public readonly SyncVar<int> turnCount = new();
    public readonly SyncVar<int> roundCount = new();

    public TMP_Text roundtext;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartGame();
    }

    [Server]
    private void StartGame()
    {
        var players = GameManager.Instance.Players;
        if (players.Count == 0) return;

        // Shuffle turn order
        turnOrder = new List<PlayerPawn>(players);
        ShuffleList(turnOrder);

        currentPlayerIndex.Value = 0;

        // Tell everyone their order
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var pawn = turnOrder[i];
            pawn.TargetSetTurnOrder(pawn.Owner, i); 
        }

        StartTurn();
    }

    private void StartTurn()
    {
        if (!IsServer) return;
        if (turnOrder.Count == 0) return;

        if (currentPlayerIndex.Value >= turnOrder.Count)
            currentPlayerIndex.Value = 0;

        PlayerPawn currentPlayer = turnOrder[currentPlayerIndex.Value];
        if (currentPlayer != null)
        {
            currentPlayer.TargetStartTurn(currentPlayer.Owner);
            Debug.Log($"[TurnManager] Turn started for {currentPlayer.playerName.Value}");
        }
        else
        {
            Debug.LogWarning("[TurnManager] Current player is null.");
        }
    }

    [Server]
    public void EndTurn()
    {
        if (turnOrder.Count == 0) return;

        int nextIndex = currentPlayerIndex.Value + 1;

        if (nextIndex >= turnOrder.Count)
        {
            nextIndex = 0;
            turnCount.Value++;

            Debug.Log($"[TurnManager] Completed a full cycle. TurnCount={turnCount.Value}");

            // A round = everyone has played once
            if (turnCount.Value % turnOrder.Count == 0)
            {
                roundCount.Value++;
                RpcUpdateRoundUI(roundCount.Value);
                Debug.Log($"[TurnManager] Round {roundCount.Value} completed!");

                // Trigger investments payout
                MarketManager.Instance?.ProcessPayouts();
            }
        }

        currentPlayerIndex.Value = nextIndex;

        // Trigger global events every 3 rounds
        if (roundCount.Value > 0 && (roundCount.Value % 3 == 0))
        {
            EventManager.Instance?.TriggerMainEvent(roundCount.Value);
        }

        // Simple game-over guard
        if (roundCount.Value >= 15)
        {
            Debug.Log("Game Over! Count money and decide winner.");
            return;
        }

        StartTurn();
    }

    [ObserversRpc]
    private void RpcUpdateRoundUI(int round)
    {
        if (roundtext != null)
            roundtext.text = $"Round : {round}";
    }

    public PlayerPawn GetCurrentPawn()
    {
        if (turnOrder.Count == 0) return null;
        return turnOrder[Mathf.Clamp(currentPlayerIndex.Value, 0, turnOrder.Count - 1)];
    }

    // === Utility ===
    public void ShuffleList<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            int k = rng.Next(n--);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}
