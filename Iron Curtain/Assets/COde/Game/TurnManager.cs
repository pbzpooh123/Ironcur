using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;
    
    public readonly SyncVar<int> currentPlayerIndex = new();
    public readonly SyncVar<int> turnCount = new();
    public readonly SyncVar<int> roundCount = new();
    
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

        var players = GameManager.Instance.Players;
        if (players.Count == 0) return;

        PlayerPawn currentPlayer = players[currentPlayerIndex.Value];
        currentPlayer.TargetStartTurn(currentPlayer.Owner); 
    }

    [Server]
    public void EndTurn()
    {
        var players = GameManager.Instance.Players;
        if (players.Count == 0) return;

        // Update using .Value
        currentPlayerIndex.Value = (currentPlayerIndex.Value + 1) % players.Count;
        turnCount.Value++;

        if (turnCount.Value % players.Count == 0)
        {
            roundCount.Value++;
            // TODO: stock market tick here
        }

        if (roundCount.Value >= 15)
        {
            Debug.Log("Game Over! Count money and declare winner.");
            return;
        }

        StartTurn();
    }
}