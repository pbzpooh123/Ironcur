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

    private List<PlayerPawn> turnOrder = new List<PlayerPawn>();

    public readonly SyncVar<int> currentPlayerIndex = new();
    public readonly SyncVar<int> turnCount = new();
    public readonly SyncVar<int> roundCount = new();

    public TMP_Text roundtext;

    private TurnPhase _phase = TurnPhase.None;

    private readonly Dictionary<PlayerPawn, int> _skipTurns = new();
    private readonly Dictionary<PlayerPawn, int> _extraRolls = new();

    // Main event single-fire guard.
    private int _lastMainEventRoundFired = -1;
    
    private const int maxrounds = 13;
    private bool _gameEnded = false;
    public bool IsGameEnded => _gameEnded;

    private void Awake()
    {
        if (_gameEnded) return;
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(DelayedStart());
        roundCount.Value = 1;
        RpcUpdateRoundUI(roundCount.Value);
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
    
    // --- Phase gate checks used by PlayerPawn ---
    [Server] public bool CanRoll(PlayerPawn pawn)          => IsCurrentPawn(pawn) && _phase == TurnPhase.Rolling;
    [Server] public bool CanEndTurn(PlayerPawn pawn)       => IsCurrentPawn(pawn) && _phase == TurnPhase.EndReady;
    [Server] public bool InProposalPhaseFor(PlayerPawn p)  => IsCurrentPawn(p)    && _phase == TurnPhase.Proposal;
    [Server] public bool InReviewPhaseFor(PlayerPawn p)    => IsCurrentPawn(p)    && _phase == TurnPhase.Review;

    [Server]
    private void SetPhase(TurnPhase phase)
    {
        _phase = phase;
        Debug.Log($"[TurnManager] Phase -> {_phase}");

        // Broadcast to ALL clients so they can set UI properly
        var pawn = GetCurrentPawn();
        string currentName = (pawn != null) ? pawn.playerName.Value : "";
        RpcSetTurnState(currentPlayerIndex.Value, _phase, currentName);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcSetTurnState(int index, TurnPhase phase, string currentPlayerName)
    {
        // Optional: debug
        // Debug.Log($"[Client] TurnState -> idx={index}, phase={phase}, current={currentPlayerName}");

        var tu = GameObject.FindObjectOfType<TurnUI>();
        if (tu == null) return;

        // Find local-owned pawn
        PlayerPawn local = null;
        foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
        {
            if (p != null && p.IsOwner)
            {
                local = p;
                break;
            }
        }

        // Default lock-down
        tu.SetRollInteractable(false);
        tu.SetEndTurnInteractable(false);

        if (local == null || string.IsNullOrEmpty(currentPlayerName))
            return;

        bool isMyTurn = (local.playerName.Value == currentPlayerName);

        switch (phase)
        {
            case TurnPhase.Review:
                // Only review UI for the owner. No buttons here.
                break;

            case TurnPhase.Rolling:
                tu.SetRollInteractable(isMyTurn);
                break;

            case TurnPhase.Proposal:
                // Proposal UI for current player. No buttons here.
                break;

            case TurnPhase.EndReady:
                tu.SetEndTurnInteractable(isMyTurn);
                break;
        }
    }

    [Server]
    public bool IsCurrentPawn(PlayerPawn pawn)
    {
        return pawn != null && turnOrder.Count > 0
               && turnOrder[Mathf.Clamp(currentPlayerIndex.Value, 0, turnOrder.Count - 1)] == pawn;
    }
    

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
    }

    [Server] private int  GetExtraRolls(PlayerPawn pawn) => (pawn != null && _extraRolls.TryGetValue(pawn, out int v)) ? v : 0;
    [Server] private void ConsumeOneExtraRoll(PlayerPawn pawn)
    {
        if (pawn != null && _extraRolls.TryGetValue(pawn, out int v) && v > 0)
            _extraRolls[pawn] = v - 1;
    }

    /* -------------------- Turn lifecycle -------------------- */
    

    [Server]
    private bool IsJailed(PlayerPawn pawn) => (pawn != null && pawn.jailTurnsLeft.Value > 0);

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

        // Consume 1 turn of jail time at start (this turn counts as jailed if >0).
        bool jailedThisTurn = IsJailed(currentPlayer);
        if (jailedThisTurn)
            currentPlayer.jailTurnsLeft.Value = Mathf.Max(0, currentPlayer.jailTurnsLeft.Value - 1);

        MarketManager.Instance.BeginTurnFor(currentPlayer);

        if (!_extraRolls.ContainsKey(currentPlayer))
            _extraRolls[currentPlayer] = 0;

        currentPlayer.TargetStartTurn(currentPlayer.Owner);
        Debug.Log($"[TurnManager] Turn started for {currentPlayer.playerName.Value} (jailed={jailedThisTurn})");

        // === If jailed: NO Review, NO Roll, NO Proposal → directly EndReady ===
        if (jailedThisTurn)
        {
            // Make sure no extra roll from previous effects is used this turn
            _extraRolls[currentPlayer] = 0;

            SetPhase(TurnPhase.EndReady);
            currentPlayer.TargetEnableRoll(currentPlayer.Owner, false);
            currentPlayer.TargetEnableEndTurn(currentPlayer.Owner, true);

            // optional toast: "You are jailed this turn. You cannot act."
            return;
        }

        // If not jailed, do normal Review check:
        if (MarketManager.Instance.HasProposalsForOwner(currentPlayer))
        {
            SetPhase(TurnPhase.Review);
            MarketManager.Instance.ShowReviewForPawn(currentPlayer);
            return;
        }

        ProceedToRoll();
    }


    [Server]
    public void ProceedToRoll()
    {
        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        SetPhase(TurnPhase.Rolling);
        // Still okay to hint current pawn UI directly
        pawn.TargetEnableRoll(pawn.Owner, true);
        pawn.TargetEnableEndTurn(pawn.Owner, false);
    }

    [Server]
    public void ServerOnTileActionComplete(PlayerPawn pawn)
    {
        if (!IsCurrentPawn(pawn))
        {
            Debug.LogWarning("[TurnManager] ServerOnTileActionComplete called for non-current pawn.");
            return;
        }

        int extra = GetExtraRolls(pawn);
        Debug.Log($"[TurnManager] Tile action complete for {pawn.playerName.Value}. ExtraRolls={extra}");

        if (extra > 0)
        {
            ConsumeOneExtraRoll(pawn);
            Debug.Log($"[TurnManager] Consumed one extra roll. Remaining={GetExtraRolls(pawn)} → ProceedToRoll()");
            ProceedToRoll();  // This will SetPhase(Rolling) and enable Roll, disable EndTurn
        }
        else
        {
            Debug.Log("[TurnManager] No extra roll → ProceedToProposal()");
            ProceedToProposal(); // This opens ProposalUI and eventually enables EndTurn
        }
    }


    [Server]
    public void ProceedToProposal()
    {
        var pawn = GetCurrentPawn();
        if (pawn == null) return;
        
        if (IsJailed(pawn))
        {
            // Cannot propose while jailed; go directly to EndReady
            SetPhase(TurnPhase.EndReady);
            pawn.TargetEnableRoll(pawn.Owner, false);
            pawn.TargetEnableEndTurn(pawn.Owner, true);
            Debug.Log($"[TurnManager] {pawn.playerName.Value} is jailed → skipping Proposal, enabling EndTurn.");
            return;
        }

        SetPhase(TurnPhase.Proposal);
        MarketManager.Instance.ShowProposalForPawn(pawn);
    }

    [Server] public void OnOwnerFinishedReview() => ProceedToRoll();

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
        if (turnOrder.Count == 0 || _gameEnded) return;

        // Count every turn taken
        turnCount.Value++;

        int nextIndex = currentPlayerIndex.Value + 1;
        bool wrapped = nextIndex >= turnOrder.Count;
        if (wrapped) nextIndex = 0;

        if (wrapped)
        {
            // A new round begins now
            roundCount.Value++;
            Debug.Log($"[TurnManager] New round started: {roundCount.Value}");

            if (roundCount.Value > maxrounds)
            {
                EndMatch($"Completed Round {maxrounds}");
                return;
            }

            // Normal between-round processing
            MarketManager.Instance?.ProcessPayouts();
            MarketManager.Instance?.OnRoundAdvanced(roundCount.Value);

            // Fire Main Event only if NOT ended and round is eligible
            if ((roundCount.Value % 3 == 0) && _lastMainEventRoundFired != roundCount.Value)
            {
                _lastMainEventRoundFired = roundCount.Value;
                currentPlayerIndex.Value = nextIndex;   // set next player before pausing for event
                EventManager.Instance?.TriggerMainEvent(roundCount.Value);
                SetPhase(TurnPhase.None);
                return; // EventManager will call ServerStartTurnAfterMainEvent()
            }
        }

        currentPlayerIndex.Value = nextIndex;
        SetPhase(TurnPhase.None);
        StartTurn();
    }

    /// <summary>
    /// Called by EventManager AFTER main event finishes.
    /// </summary>
    [Server]
    public void ServerStartTurnAfterMainEvent()
    {
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
    
    [Server]
    private void EndMatch(string reason = "Reached final round.")
    {
        if (_gameEnded) return;
        _gameEnded = true;

        // Notify systems (now implemented below)
        MarketManager.Instance?.OnMatchEnded();
        EventManager.Instance?.OnMatchEnded();

        // Lock phases and tell clients to freeze buttons / show banner
        SetPhase(TurnPhase.None);
        RpcOnGameEnded(reason);
    }


    [ObserversRpc(BufferLast = true)]
    private void RpcOnGameEnded(string reason)
    {
        // Freeze roll/end buttons on any local TurnUI
        var tu = GameObject.FindObjectOfType<TurnUI>();
        if (tu != null)
        {
            tu.SetRollInteractable(false);
            tu.SetEndTurnInteractable(false);
            tu.ShowToast($"Game Over: {reason}", 5f);
        }

        // If you have a dedicated end screen, call it here instead:
        // EndScreen.Show(finalScores);
    }
    
}
