using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;

public enum TurnPhase
{
    None,
    Review,           // Owner reviewing proposals at start of their turn
    Rolling,          // Current pawn may roll dice
    TileEventPending, // Waiting for EventManager to finish a tile event
    Proposal,         // Current pawn may propose on others' companies
    EndReady          // Current pawn can press EndTurn
}

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    private List<PlayerPawn> turnOrder = new List<PlayerPawn>(); // shuffled order

    public readonly SyncVar<int> currentPlayerIndex = new();
    public readonly SyncVar<int> turnCount = new();
    public readonly SyncVar<int> roundCount = new();

    public TMP_Text roundtext;

    // Current phase (server authoritative)
    private TurnPhase _phase = TurnPhase.None;

    // Skip turn tracking
    private readonly Dictionary<PlayerPawn, int> _skipTurns = new();

    // NEW: Extra rolls per pawn this turn
    private readonly Dictionary<PlayerPawn, int> _extraRolls = new();

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (Owner != null)
            GiveOwnership(Owner);

        StartCoroutine(DelayedStart());
    }

    private System.Collections.IEnumerator DelayedStart()
    {
        yield return null;
        StartGame();
    }

    [Server]
    private void StartGame()
    {
        var players = GameManager.Instance.Players;
        if (players.Count == 0) return;

        turnOrder = new List<PlayerPawn>(players);
        ShuffleList(turnOrder);

        currentPlayerIndex.Value = 0;

        for (int i = 0; i < turnOrder.Count; i++)
            turnOrder[i].TargetSetTurnOrder(turnOrder[i].Owner, i);

        StartCoroutine(DelayedFirstTurn());
    }

    private System.Collections.IEnumerator DelayedFirstTurn()
    {
        yield return new WaitForSeconds(1.0f);
        StartTurn();
    }

    [Server]
    private void SetPhase(TurnPhase phase)
    {
        _phase = phase;
        // Debug.Log($"[TurnManager] Phase -> {_phase}");
    }

    [Server]
    public bool IsCurrentPawn(PlayerPawn pawn)
    {
        return pawn != null && turnOrder.Count > 0 && turnOrder[Mathf.Clamp(currentPlayerIndex.Value,0,turnOrder.Count-1)] == pawn;
    }

    [Server]
    public bool CanRoll(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.Rolling;

    [Server]
    public bool CanEndTurn(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.EndReady;

    [Server]
    public bool InProposalPhaseFor(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.Proposal;

    [Server]
    public bool InReviewPhaseFor(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.Review;

    [Server]
    private bool ShouldSkip(PlayerPawn pawn)
    {
        if (pawn == null) return false;
        if (_skipTurns.TryGetValue(pawn, out int left) && left > 0)
        {
            _skipTurns[pawn] = left - 1;
            return true;
        }
        return false;
    }

    [Server]
    public void MarkSkipTurn(PlayerPawn pawn, int duration)
    {
        if (pawn == null) return;
        _skipTurns[pawn] = Mathf.Max(duration, 1);
    }

    /* -------------------- Extra roll API -------------------- */

    [Server]
    public void QueueExtraRoll(PlayerPawn pawn, int count)
    {
        if (pawn == null) return;
        if (!_extraRolls.ContainsKey(pawn))
            _extraRolls[pawn] = 0;
        _extraRolls[pawn] += Mathf.Max(1, count);
        // Debug.Log($"[TurnManager] Extra rolls for {pawn.playerName.Value} now = {_extraRolls[pawn]}");
    }

    [Server]
    private int GetExtraRolls(PlayerPawn pawn)
    {
        if (pawn == null) return 0;
        return _extraRolls.TryGetValue(pawn, out int v) ? v : 0;
    }

    [Server]
    private void ConsumeOneExtraRoll(PlayerPawn pawn)
    {
        if (pawn == null) return;
        if (_extraRolls.TryGetValue(pawn, out int v) && v > 0)
            _extraRolls[pawn] = v - 1;
    }

    /* -------------------- Turn lifecycle -------------------- */

    [Server]
    private void StartTurn()
    {
        if (turnOrder.Count == 0) return;

        if (currentPlayerIndex.Value >= turnOrder.Count)
            currentPlayerIndex.Value = 0;

        PlayerPawn currentPlayer = turnOrder[currentPlayerIndex.Value];

        if (ShouldSkip(currentPlayer))
        {
            Debug.Log($"[TurnManager] Skipping {currentPlayer.playerName.Value}'s turn");
            EndTurn();
            return;
        }

        // Reset "extra rolls" for safety if missing key
        if (!_extraRolls.ContainsKey(currentPlayer))
            _extraRolls[currentPlayer] = 0;

        // Reset "submitted this turn" per MarketManager
        MarketManager.Instance.BeginTurnFor(currentPlayer);

        // UI → it's your turn
        currentPlayer.TargetStartTurn(currentPlayer.Owner);
        Debug.Log($"[TurnManager] Turn started for {currentPlayer.playerName.Value}");

        // If owner has pending proposals → review phase
        if (MarketManager.Instance.HasProposalsForOwner(currentPlayer))
        {
            SetPhase(TurnPhase.Review);
            MarketManager.Instance.CmdRequestReviewUI(currentPlayer.Owner);
        }
        else
        {
            ProceedToRoll();
        }
    }

    [Server]
    public void ProceedToRoll()
    {
        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        SetPhase(TurnPhase.Rolling);
        pawn.TargetEnableRoll(pawn.Owner, true);
        pawn.TargetEnableEndTurn(pawn.Owner, false);
    }

    /// <summary>
    /// Called by EventManager or PlayerPawn/InvestmentUI when the tile action (including events) is fully done.
    /// Decides whether to give an extra roll or go to Proposal phase.
    /// </summary>
    [Server]
    public void ServerOnTileActionComplete(PlayerPawn pawn)
    {
        if (!IsCurrentPawn(pawn)) return;

        int extra = GetExtraRolls(pawn);
        if (extra > 0)
        {
            ConsumeOneExtraRoll(pawn);
            // Go back to Rolling
            ProceedToRoll();
        }
        else
        {
            // No extra roll → Proposal phase
            ProceedToProposal();
        }
    }

    [Server]
    public void ProceedToProposal()
    {
        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        SetPhase(TurnPhase.Proposal);
        // Ask client to open Proposal UI for current pawn
        MarketManager.Instance.CmdRequestProposalUI(pawn.Owner);
    }

    [Server]
    public void OnOwnerFinishedReview()
    {
        ProceedToRoll();
    }

    [Server]
    public void OnPlayerFinishedProposal()
    {
        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        SetPhase(TurnPhase.EndReady);
        pawn.TargetEnableRoll(pawn.Owner, false);
        pawn.TargetEnableEndTurn(pawn.Owner, true);
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

            if (turnCount.Value % turnOrder.Count == 0)
            {
                roundCount.Value++;
                RpcUpdateRoundUI(roundCount.Value);
                Debug.Log($"[TurnManager] Round {roundCount.Value} completed!");

                MarketManager.Instance?.ProcessPayouts();
                MarketManager.Instance?.OnRoundAdvanced(roundCount.Value);
            }
        }

        currentPlayerIndex.Value = nextIndex;

        if (roundCount.Value > 0 && (roundCount.Value % 3 == 0))
            EventManager.Instance?.TriggerMainEvent(roundCount.Value);

        if (roundCount.Value >= 15)
        {
            Debug.Log("Game Over! Count money and decide winner.");
            return;
        }

        SetPhase(TurnPhase.None);
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
