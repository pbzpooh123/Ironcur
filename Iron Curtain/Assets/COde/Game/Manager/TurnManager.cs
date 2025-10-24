using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using System.Threading.Tasks;


public enum TurnPhase
{
    None,
    Review,
    Rolling,
    TileEventPending, 
    Proposal,
    EndReady
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

    private const int maxrounds = 25;
    private bool _gameEnded = false;
    public readonly SyncVar<int> remainingRounds = new();
    public bool IsGameEnded => _gameEnded;

    // === NEW: are we waiting for the tile panel "Ready"? ===
    private bool _tileActionAwaitingAck = false;
    [SerializeField] private bool enableDevHotkeys = true;
    [SerializeField] private bool AutoBailoutAtTurnStart = true;
    private int RoundsRemaining() => Mathf.Max(0, maxrounds - (roundCount.Value - 1));
    private int RoundsUntilNextMainEvent()
    {
        int mod = roundCount.Value % 3;
        return (mod == 0) ? 0 : (3 - mod);
    }

    private void Awake()
    {
        if (_gameEnded) return;
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        roundCount.Value = 1;
        remainingRounds.Value = maxrounds;
        RpcUpdateRoundUI(
        RoundsRemaining(),
        RoundsUntilNextMainEvent(),
        EventManager.Instance ? EventManager.Instance.PeekNextMainEventName() : "—"
        );
        StartCoroutine(DelayedStart());
        EventManager.Instance?.ServerSelectTimelineAndAnnounce();

    }

    private System.Collections.IEnumerator DelayedStart()
    {
        yield return null;
        StartGame();
    }

    private void Update()
    {
        if (!enableDevHotkeys) return;

        // Only react when the key is pressed this frame
        if (Input.GetKeyDown(KeyCode.Keypad9))
        {
            // If we're the server/host, end immediately.
            if (IsServerStarted)
            {
                EndMatch("Ended by DEV hotkey (Numpad 9).");
            }
            else
            {
                // Ask the server to end the match.
                CmdDevEndMatch();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdDevEndMatch(FishNet.Connection.NetworkConnection caller = null)
    {
        if (!enableDevHotkeys) return; // guard if disabled on server
        EndMatch("Ended by DEV hotkey (Numpad 9).");
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
    [Server] public bool CanRoll(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.Rolling;
    [Server] public bool CanEndTurn(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.EndReady;
    [Server] public bool InProposalPhaseFor(PlayerPawn p) => IsCurrentPawn(p) && _phase == TurnPhase.Proposal;
    [Server] public bool InReviewPhaseFor(PlayerPawn p) => IsCurrentPawn(p) && _phase == TurnPhase.Review;

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

            case TurnPhase.TileEventPending:
                // While a tile panel is open, no roll / no end turn.
                tu.SetRollInteractable(false);
                tu.SetEndTurnInteractable(false);
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

    [Server] private int GetExtraRolls(PlayerPawn pawn) => (pawn != null && _extraRolls.TryGetValue(pawn, out int v)) ? v : 0;
    [Server]
    private void ConsumeOneExtraRoll(PlayerPawn pawn)
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

        if (AutoBailoutAtTurnStart && currentPlayer.money.Value < 0)
        {
            int tries = 0;
            const int MAX_TRIES = 10; // safety cap
            while (currentPlayer.money.Value < 0 && tries < MAX_TRIES)
            {
                currentPlayer.ForceBailoutOnce(); // your +$100, +mark
                tries++;
            }
            if (tries > 0)
                Debug.Log($"[TurnManager] Auto-bailout x{tries} for {currentPlayer.playerName.Value} (money={currentPlayer.money.Value}).");
        }

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

        // If jailed: NO Review, NO Roll, NO Proposal → directly EndReady
        if (jailedThisTurn)
        {
            _extraRolls[currentPlayer] = 0;

            SetPhase(TurnPhase.EndReady);
            currentPlayer.TargetEnableRoll(currentPlayer.Owner, false);
            currentPlayer.TargetEnableEndTurn(currentPlayer.Owner, true);

            return;
        }

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
        pawn.TargetEnableRoll(pawn.Owner, true);
        pawn.TargetEnableEndTurn(pawn.Owner, false);
    }

    // === NEW: bracket any tile panel ===
    [Server]
    public void ServerBeginTileAction(PlayerPawn pawn)
    {
        if (!IsCurrentPawn(pawn)) return;
        _tileActionAwaitingAck = true;
        SetPhase(TurnPhase.TileEventPending);

        // lock roll/end on the active pawn while a tile panel is open
        pawn.TargetEnableRoll(pawn.Owner, false);
        pawn.TargetEnableEndTurn(pawn.Owner, false);
    }

    // Called by the local player when they press Ready/Close on that tile panel.
    [ServerRpc(RequireOwnership = false)]
    public void CmdTileActionReady(FishNet.Connection.NetworkConnection caller = null)
    {
        if (caller == null) return;
        var pawn = GetCurrentPawn();
        if (pawn == null || pawn.Owner != caller) return;
        if (!_tileActionAwaitingAck || _phase != TurnPhase.TileEventPending) return;

        _tileActionAwaitingAck = false;

        // Continue: extra rolls first, then proposal
        ServerOnTileActionComplete(pawn);
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
            ProceedToRoll(); // roll again; that tile should also call ServerBeginTileAction + CmdTileActionReady
        }
        else
        {
            foreach (var tile in FindObjectsOfType<TileData>())
                tile.RefreshVisuals();
            Debug.Log("[TurnManager] No extra roll → ProceedToProposal()");
            ProceedToProposal();
        }
    }

    [Server]
    public void ProceedToProposal()
    {
        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        // Block proposals for the round if the event is active
        if (EventManager.Instance != null && EventManager.Instance.IsProposalBlockedNow())
        {
            SetPhase(TurnPhase.EndReady);
            pawn.TargetEnableRoll(pawn.Owner, false);
            pawn.TargetEnableEndTurn(pawn.Owner, true);
            Debug.Log($"[TurnManager] Proposals disabled → EndTurn enabled for {pawn.playerName.Value}.");
            return;
        }

        if (IsJailed(pawn))
        {
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
            roundCount.Value++;

            MarketManager.Instance?.OnRoundAdvanced(roundCount.Value);
            EventManager.Instance?.ServerPruneSectorSurges();

            remainingRounds.Value = Mathf.Max(remainingRounds.Value - 1, 0);
            RpcUpdateRoundUI(
                remainingRounds.Value,
                RoundsUntilNextMainEvent(),
                EventManager.Instance ? EventManager.Instance.PeekNextMainEventName() : "—"
            );

            if (remainingRounds.Value <= 0)
            {
                EndMatch($"Completed {maxrounds} Turns");
                return;
            }

            MarketManager.Instance?.ProcessPayouts();

            if ((roundCount.Value % 3 == 0) && _lastMainEventRoundFired != roundCount.Value)
            {
                _lastMainEventRoundFired = roundCount.Value;
                currentPlayerIndex.Value = nextIndex;
                EventManager.Instance?.TriggerMainEvent(roundCount.Value);
                SetPhase(TurnPhase.None);
                return;
            }
        }
        currentPlayerIndex.Value = nextIndex;
        SetPhase(TurnPhase.None);
        StartTurn();
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcUpdateRoundUI(int roundsLeft, int eventIn, string nextEventName)
    {
        if (roundtext != null)
        {
            if (eventIn <= 0)
                roundtext.text = $"Rounds left: {roundsLeft}   (Main event this round: {nextEventName})";
            else
                roundtext.text = $"Rounds left: {roundsLeft}   (Main event in {eventIn} rounds)";
        }

        // Optional: also mirror to a dedicated label on TurnUI
        TurnUI.Instance?.SetNextMainEventName(nextEventName);
    }

    /// <summary> Called by EventManager AFTER main event finishes. </summary>
    [Server]
    public void ServerStartTurnAfterMainEvent()
    {
        RpcUpdateRoundUI(
            remainingRounds.Value,
            RoundsUntilNextMainEvent(),
            EventManager.Instance ? EventManager.Instance.PeekNextMainEventName() : "—"
        );
        SetPhase(TurnPhase.None);
        StartTurn();
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

    private int _resultsReady = 0;
    private int _resultsRequired = 0;

    [Server]
    private void EndMatch(string reason = "Reached final round.")
    {
        if (_gameEnded) return;
        _gameEnded = true;

        SetPhase(TurnPhase.None);
        RpcOnGameEnded(reason);

        MarketManager.Instance?.OnMatchEnded();
        EventManager.Instance?.OnMatchEnded();
        roundtext.text = "Game Ended";

        var players = GameManager.Instance.Players;
        int n = players.Count;
        string[] names = new string[n];
        int[] moneys = new int[n];
        int[] bailouts = new int[n];
        int[] finals = new int[n];

        for (int i = 0; i < n; i++)
        {
            var p = players[i];
            names[i] = string.IsNullOrWhiteSpace(p.playerName.Value) ? $"Player {i + 1}" : p.playerName.Value;
            moneys[i] = p.money.Value;
            bailouts[i] = p.bailoutMarks.Value;
            finals[i] = Mathf.Max(0, p.money.Value - 100 * p.bailoutMarks.Value);
        }

        RpcShowFinalResults(names, moneys, bailouts, finals);

        _resultsReady = 0;
        _resultsRequired = Mathf.Max(1, n);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcOnGameEnded(string reason)
    {
        var tu = GameObject.FindObjectOfType<TurnUI>();
        if (tu != null)
        {
            tu.SetRollInteractable(false);
            tu.SetEndTurnInteractable(false);
            tu.ShowToast($"Game Over: {reason}", 5f);
        }
    }

    [Server]
    private void ServerRequestClientScoreSubmissions()
    {
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null || p.Owner == null) continue;
            string name = string.IsNullOrWhiteSpace(p.playerName.Value) ? "Player" : p.playerName.Value;
            long score = p.money.Value - (100L * p.bailoutMarks.Value);


            p.TargetSubmitToLeaderboard(p.Owner, score, name);
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcShowFinalResults(string[] names, int[] moneys, int[] bailouts, int[] finals)
    {

        var ui = MatchResultsUI.Instance;
        if (ui != null)
            ui.Show(names, moneys, bailouts, finals);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdFinalResultsReady(FishNet.Connection.NetworkConnection conn = null)
    {

        if (conn == null) return;

        _readyClientIds ??= new HashSet<int>();
        if (_readyClientIds.Contains(conn.ClientId)) return;

        _readyClientIds.Add(conn.ClientId);
        _resultsReady++;

        if (_resultsReady >= _resultsRequired)
        {
             _ = SubmitScoresThenGoToLeaderboard();
        }
    }
    private HashSet<int> _readyClientIds;

    [ObserversRpc(BufferLast = true)]
    private void RpcGoToLeaderboard()
    {
        MatchResultsUI.Instance?.GoToLeaderboardScene();
    }

   private async Task SubmitScoresThenGoToLeaderboard()
{
    var svc = UGSLeaderboard.Instance;

    if (svc != null)
    {
        var tasks = new List<Task>();
        foreach (var p in GameManager.Instance.Players)
        {
            string name  = string.IsNullOrWhiteSpace(p.playerName.Value) ? "Player" : p.playerName.Value;
            long score   = Mathf.Max(0, p.money.Value - 100 * p.bailoutMarks.Value);
            tasks.Add(svc.SubmitMyScoreAsync(score, name));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UGS] One or more leaderboard submissions failed: {ex}");
        }
    }
    else
    {
        Debug.LogWarning("[UGS] Leaderboard service not present; loading scene anyway.");
        // tiny grace period so any client-side calls can race-in
        await Task.Delay(500);
    }

    RpcGoToLeaderboard();
}

}
