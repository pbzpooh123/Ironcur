using System;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    public readonly SyncVar<int> currentPlayerIndex = new();
    public readonly SyncVar<int> turnCount = new();
    public readonly SyncVar<int> roundCount = new();

    public TMP_Text roundtext;

    private void Awake()
    {
        Instance = this;
        roundtext.text = $"Round : {roundCount.Value}";
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartTurn();
    }

    private void StartTurn()
    {
        if (!IsServer) return;

        var players = GameManager.Instance.Players;
        if (players.Count == 0) return;
        
        if (currentPlayerIndex.Value >= players.Count)
            currentPlayerIndex.Value = 0;

        PlayerPawn currentPlayer = players[currentPlayerIndex.Value];
        currentPlayer.TargetStartTurn(currentPlayer.Owner);

        Debug.Log($"[TurnManager] Turn started for {currentPlayer.playerName.Value}");
    }

    [Server]
    public void EndTurn()
    {
        var players = GameManager.Instance.Players;
        if (players.Count == 0) return;

        int nextIndex = currentPlayerIndex.Value + 1;

        // ถ้าถึงผู้เล่นคนสุดท้ายที่จอย → new round
        if (nextIndex >= players.Count)
        {
            nextIndex = 0;
            turnCount.Value++;

            Debug.Log($"[TurnManager] Completed a full cycle of turns. TurnCount={turnCount.Value}");

            if (turnCount.Value % players.Count == 0) 
            {
                roundCount.Value++;
                roundtext.text = $"Round : {roundCount.Value}";
                Debug.Log($"[TurnManager] Round {roundCount.Value} completed!");
            }
        }

        currentPlayerIndex.Value = nextIndex;

        if (roundCount.Value % 3 == 0)
        {
                EventManager.Instance.TriggerMainEvent(roundCount.Value);
                // Game pauses until all ready
        }

        if (roundCount.Value >= 15)
        {
            Debug.Log("Game Over! Count money and decide winner.");
            return;
        }

        StartTurn();
    }
}