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
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartTurn();
    }

    private void StartTurn()
    {
        if (!IsServer) return;
        if (GameManager.Instance == null) return;

        // snapshot + count
        var players = GameManager.Instance.GetPlayersSnapshot();
        int playerCount = GameManager.Instance.PlayerCount;
        if (playerCount == 0) return;

        if (currentPlayerIndex.Value >= playerCount)
            currentPlayerIndex.Value = 0;

        PlayerPawn currentPlayer = players[currentPlayerIndex.Value];
        currentPlayer.TargetStartTurn(currentPlayer.Owner);

        Debug.Log($"[TurnManager] Turn started for {currentPlayer.playerName.Value}");
    }

    [Server]
    public void EndTurn()
    {
        if (GameManager.Instance == null) return;

        var players = GameManager.Instance.GetPlayersSnapshot();
        int playerCount = GameManager.Instance.PlayerCount;
        if (playerCount == 0) return;

        int nextIndex = currentPlayerIndex.Value + 1;

        if (nextIndex >= playerCount)
        {
            nextIndex = 0;
            turnCount.Value++;

            Debug.Log($"[TurnManager] Completed a full cycle. TurnCount={turnCount.Value}");

            // A “round” = everyone has taken one turn.
            if (turnCount.Value % playerCount == 0)
            {
                roundCount.Value++;
                RpcUpdateRoundUI(roundCount.Value); // update client UI
                Debug.Log($"[TurnManager] Round {roundCount.Value} completed!");

                // trigger investment payouts
                MarketManager.Instance?.ProcessPayouts();
            }
        }

        currentPlayerIndex.Value = nextIndex;

        // main event every 3 rounds
        if (roundCount.Value > 0 && (roundCount.Value % 3 == 0))
            EventManager.Instance?.TriggerMainEvent(roundCount.Value);

        // simple game-over guard
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
        if (GameManager.Instance == null) return null;

        var players = GameManager.Instance.GetPlayersSnapshot();
        int playerCount = GameManager.Instance.PlayerCount;
        if (playerCount == 0) return null;

        return players[Mathf.Clamp(currentPlayerIndex.Value, 0, playerCount - 1)];
    }
}
