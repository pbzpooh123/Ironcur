using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public enum TurnPhase
{
    None,
    Review,
    Rolling,
    TileEventPending,
    Proposal,
    MainEvent,
    EndReady
}

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    /* ---------- Turn State ---------- */
    private List<PlayerPawn> turnOrder = new List<PlayerPawn>();

    public readonly SyncVar<int> currentPlayerIndex = new();
    public readonly SyncVar<int> turnCount = new();
    public readonly SyncVar<int> roundCount = new();

    public TMP_Text roundtext;

    private TurnPhase _phase = TurnPhase.None;

    private readonly Dictionary<PlayerPawn, int> _skipTurns = new();
    private readonly Dictionary<PlayerPawn, int> _extraRolls = new();

    private int _lastMainEventRoundFired = -1;

    private const int maxrounds = 25;
    private bool _gameEnded = false;
    public readonly SyncVar<int> remainingRounds = new();
    public bool IsGameEnded => _gameEnded;

    private bool _tileActionAwaitingAck = false;
    [SerializeField] private bool enableDevHotkeys = true;
    [SerializeField] private bool AutoBailoutAtTurnStart = true;

    // guard for server auto-end
    private bool _endTurnScheduled = false;

    private int RoundsRemaining() => Mathf.Max(0, maxrounds - (roundCount.Value - 1));

    [Server] public bool IsPhase(TurnPhase phase) => _phase == phase;

    /* ---------- Timers (per-phase + per-turn) ---------- */

    [Header("Per-Phase Durations (seconds)")]
    [SerializeField] private int defaultPhaseSeconds = 5;
    [SerializeField] private int reviewSeconds       = 10;
    [SerializeField] private int rollingSeconds      = 10;
    [SerializeField] private int tileEventSeconds    = 15;
    [SerializeField] private int proposalSeconds = 25;
    [SerializeField] private int mainEventSeconds   = 20;
    [SerializeField] private int endReadySeconds     = 5;

    [Header("Whole-Turn Duration (seconds)")]
    [SerializeField] private int turnDurationSeconds = 60;

    // live counters synced for clients
    public readonly SyncVar<int> phaseSecondsLeft = new();
    public readonly SyncVar<TurnPhase> phaseSync  = new();
    public readonly SyncVar<int> turnSecondsLeft  = new();

    private Coroutine _phaseTimerCo;
    private Coroutine _turnTimerCo;

    [Server]
    public bool IsCurrentAndInProposal(PlayerPawn pawn)
        => pawn != null && IsCurrentPawn(pawn) && IsPhase(TurnPhase.Proposal);

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

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (IsServerStarted)
                EndMatch("Ended by DEV hotkey (P).");
            else
                CmdDevEndMatch();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdDevEndMatch(FishNet.Connection.NetworkConnection caller = null)
    {
        if (!enableDevHotkeys) return;
        EndMatch("Ended by DEV hotkey (P).");
    }

    /* ---------- Game Start / Turn Order ---------- */

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

    /* ---------- Phase Gates used by PlayerPawn ---------- */

    [Server] public bool CanRoll(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.Rolling;
    [Server] public bool CanEndTurn(PlayerPawn pawn) => IsCurrentPawn(pawn) && _phase == TurnPhase.EndReady;
    [Server] public bool InProposalPhaseFor(PlayerPawn p) => IsCurrentPawn(p) && _phase == TurnPhase.Proposal;
    [Server] public bool InReviewPhaseFor(PlayerPawn p) => IsCurrentPawn(p) && _phase == TurnPhase.Review;

    /* ---------- Phase switching (starts timer) ---------- */

    [Server]
    private void SetPhase(TurnPhase phase)
    {
        _phase = phase;

        var pawn = GetCurrentPawn();
        int currentOwnerCid = (pawn?.Owner != null) ? pawn.Owner.ClientId : -1;

        RpcSetTurnState(_phase, currentOwnerCid);

        // (Re)start phase timer whenever we enter a real phase
        StopPhaseTimer();
        if (phase != TurnPhase.None)
            StartPhaseTimer(phase);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcSetTurnState(TurnPhase phase, int currentOwnerCid)
    {
        GameHUD.Instance?.SetCurrentTurnByCid(currentOwnerCid);
        TurnUI.Instance?.ApplyTurnState(phase, currentOwnerCid);
    }

    /* ---------- Helpers ---------- */

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

    /* ---------- Extra rolls ---------- */

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

    /* ---------- Turn lifecycle ---------- */

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
            const int MAX_TRIES = 10;
            while (currentPlayer.money.Value < 0 && tries < MAX_TRIES)
            {
                currentPlayer.ForceBailoutOnce();
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

        bool jailedThisTurn = IsJailed(currentPlayer);
        if (jailedThisTurn)
            currentPlayer.jailTurnsLeft.Value = Mathf.Max(0, currentPlayer.jailTurnsLeft.Value - 1);

        MarketManager.Instance.BeginTurnFor(currentPlayer);

        if (!_extraRolls.ContainsKey(currentPlayer))
            _extraRolls[currentPlayer] = 0;

        currentPlayer.TargetStartTurn(currentPlayer.Owner);
        Debug.Log($"[TurnManager] Turn started for {currentPlayer.playerName.Value} (jailed={jailedThisTurn})");

        // Start whole-turn timer
        StartTurnTimer(currentPlayer);

        if (jailedThisTurn)
        {
            _extraRolls[currentPlayer] = 0;
            ServerEnterEndReadyAndAutoEnd();
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

    /* ---------- Tile action bracket ---------- */

    [Server]
    public void ServerBeginTileAction(PlayerPawn pawn)
    {
        if (!IsCurrentPawn(pawn)) return;
        _tileActionAwaitingAck = true;
        SetPhase(TurnPhase.TileEventPending);

        pawn.TargetEnableRoll(pawn.Owner, false);
        pawn.TargetEnableEndTurn(pawn.Owner, false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdTileActionReady(FishNet.Connection.NetworkConnection caller = null)
    {
        if (caller == null) return;
        var pawn = GetCurrentPawn();
        if (pawn == null || pawn.Owner != caller) return;
        if (!_tileActionAwaitingAck || _phase != TurnPhase.TileEventPending) return;

        _tileActionAwaitingAck = false;
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
            ProceedToRoll();
        }
        else
        {
            foreach (var tile in FindObjectsOfType<TileData>())
                tile.RefreshVisuals();

            ProceedToProposal();
        }
    }

    [Server]
    public void ProceedToProposal()
    {
        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        if (EventManager.Instance != null && EventManager.Instance.IsProposalBlockedNow())
        {
            Debug.Log($"[TurnManager] Proposals disabled → auto end for {pawn.playerName.Value}.");
            ServerEnterEndReadyAndAutoEnd();
            return;
        }

        if (IsJailed(pawn))
        {
            Debug.Log($"[TurnManager] {pawn.playerName.Value} is jailed (defensive) → auto end.");
            ServerEnterEndReadyAndAutoEnd();
            return;
        }

        SetPhase(TurnPhase.Proposal);
        MarketManager.Instance.ShowProposalForPawn(pawn);
    }

    [Server] public void OnOwnerFinishedReview() => ProceedToRoll();

    [Server]
    public void OnPlayerFinishedProposal()
    {
        ServerEnterEndReadyAndAutoEnd();
    }

    /* ---------- End turn (auto or manual) ---------- */

    [Server]
    private void ServerEnterEndReadyAndAutoEnd()
    {
        if (_endTurnScheduled || _gameEnded) return;

        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        _endTurnScheduled = true;

        SetPhase(TurnPhase.EndReady);
        pawn.TargetEnableRoll(pawn.Owner, false);
        pawn.TargetEnableEndTurn(pawn.Owner, false);

        StartCoroutine(CoAutoEndTurn());
    }

    private System.Collections.IEnumerator CoAutoEndTurn()
    {
        yield return new WaitForSeconds(0.35f);
        _endTurnScheduled = false;
        EndTurn();
    }

    [Server]
    public void EndTurn()
    {
        if (turnOrder.Count == 0 || _gameEnded) return;

        // stop timers as we leave this turn
        StopTurnTimer();
        StopPhaseTimer();

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
                return;
            }
        }

        currentPlayerIndex.Value = nextIndex;
        StartTurn();
    }

    [Server]
    public void ServerBeginMainEvent()
    {
        SetPhase(TurnPhase.MainEvent);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcUpdateRoundUI(int roundsLeft, int eventIn, string nextEventName)
    {
        if (roundtext != null)
            roundtext.text = $"รอบที่ยังคงเหลือ: {roundsLeft}";

        TurnUI.Instance?.SetNextMainEventETA(eventIn);
    }

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

    /* ---------- Utility ---------- */

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

    /* ---------- Match End / Results ---------- */

    private int _resultsReady = 0;
    private int _resultsRequired = 0;
    private HashSet<int> _readyClientIds;

    [Server]
    private void EndMatch(string reason = "Reached final round.")
    {
        if (_gameEnded) return;
        _gameEnded = true;

        StopTurnTimer();
        StopPhaseTimer();

        SetPhase(TurnPhase.None);
        RpcOnGameEnded(reason);

        MarketManager.Instance?.OnMatchEnded();
        EventManager.Instance?.OnMatchEnded();
        if (roundtext != null) roundtext.text = "เกมจบแล้ว";

        var players = GameManager.Instance.Players;

        // ===== เจ้าพ่อพอร์ตหุ้น (Portfolio King) =====
        PlayerPawn portfolioKing = null;
        int bestPortfolioValue = -1;

        if (MarketManager.Instance != null)
        {
            foreach (var p in players)
            {
                if (p == null) continue;
                int v = MarketManager.Instance.ComputePortfolioValue(p);
                if (v > bestPortfolioValue)
                {
                    bestPortfolioValue = v;
                    portfolioKing = p;
                }
            }
        }

        if (portfolioKing != null)
        {
            Debug.Log($"[Awards] เจ้าพ่อพอร์ตหุ้น = {portfolioKing.playerName.Value} (พอร์ต = {bestPortfolioValue})");
        }

        int n = players.Count;
        string[] names = new string[n];
        int[] moneys = new int[n];
        int[] bailouts = new int[n];
        int[] finals = new int[n];

        for (int i = 0; i < n; i++)
        {
            var p = players[i];
            names[i]   = string.IsNullOrWhiteSpace(p.playerName.Value) ? $"Player {i + 1}" : p.playerName.Value;
            moneys[i]  = p.money.Value;
            bailouts[i]= p.bailoutMarks.Value;
            finals[i]  = Mathf.Max(0, p.money.Value - 100 * p.bailoutMarks.Value);
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
        }
    }

    [Server]
    private void ServerRequestClientScoreSubmissions()
    {
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null || p.Owner == null) continue;
            string name = string.IsNullOrWhiteSpace(p.playerName.Value) ? "Player" : p.playerName.Value;
            long score  = p.money.Value - (100L * p.bailoutMarks.Value);
            p.TargetSubmitToLeaderboard(p.Owner, score, name);
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcShowFinalResults(string[] names, int[] moneys, int[] bailouts, int[] finals)
    {
        MatchResultsUI.Instance?.Show(names, moneys, bailouts, finals);
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
            _ = SubmitScoresThenGoToLeaderboard();
    }

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
                string name = string.IsNullOrWhiteSpace(p.playerName.Value) ? "Player" : p.playerName.Value;
                long score  = Mathf.Max(0, p.money.Value - 100 * p.bailoutMarks.Value);
                tasks.Add(svc.SubmitMyScoreAsync(score, name));
            }

            try { await Task.WhenAll(tasks); }
            catch (Exception ex)
            {
                Debug.LogError($"[UGS] One or more leaderboard submissions failed: {ex}");
            }
        }
        else
        {
            Debug.LogWarning("[UGS] Leaderboard service not present; loading scene anyway.");
            await Task.Delay(500);
        }

        RpcGoToLeaderboard();
    }

    /* ---------- Per-turn timer ---------- */

    [Server]
    private void StartTurnTimer(PlayerPawn pawn)
    {
        StopTurnTimer();
        turnSecondsLeft.Value = Mathf.Max(5, turnDurationSeconds);
    }

    [Server]
    private void StopTurnTimer()
    {
        if (_turnTimerCo != null)
        {
            StopCoroutine(_turnTimerCo);
            _turnTimerCo = null;
        }
    }


    /* ---------- Per-phase timer ---------- */

   private int GetPhaseSeconds(TurnPhase phase)
    {
        return phase switch
        {
            TurnPhase.Review            => reviewSeconds      > 0 ? reviewSeconds      : defaultPhaseSeconds,
            TurnPhase.Rolling           => rollingSeconds     > 0 ? rollingSeconds     : defaultPhaseSeconds,
            TurnPhase.TileEventPending  => tileEventSeconds   > 0 ? tileEventSeconds   : defaultPhaseSeconds,
            TurnPhase.Proposal          => proposalSeconds    > 0 ? proposalSeconds    : defaultPhaseSeconds,
            TurnPhase.EndReady          => endReadySeconds    > 0 ? endReadySeconds    : defaultPhaseSeconds,
            TurnPhase.MainEvent         => mainEventSeconds   > 0 ? mainEventSeconds   : defaultPhaseSeconds,   // ← NEW
            _                           => defaultPhaseSeconds
        };
    }


    [Server]
    private void StartPhaseTimer(TurnPhase phase)
    {
        StopPhaseTimer();

        var pawn = GetCurrentPawn();
        if (pawn == null) return;

        phaseSync.Value = phase;
        phaseSecondsLeft.Value = GetPhaseSeconds(phase);
        _phaseTimerCo = StartCoroutine(CoPhaseTimer(pawn, phase));
    }

    [Server]
    private void StopPhaseTimer()
    {
        if (_phaseTimerCo != null)
        {
            StopCoroutine(_phaseTimerCo);
            _phaseTimerCo = null;
        }
    }

    private System.Collections.IEnumerator CoPhaseTimer(PlayerPawn pawn, TurnPhase phaseAtStart)
    {
        while (phaseSecondsLeft.Value > 0)
        {
            if (PauseManager.Instance != null && PauseManager.Instance.isPaused.Value)
            {
                yield return null;
                continue;
            }

            RpcPhaseTimerTick(phaseAtStart, phaseSecondsLeft.Value, pawn.Owner != null ? pawn.Owner.ClientId : -1);
            yield return new WaitForSeconds(1f);

            if (_phase != phaseAtStart) yield break;  // phase changed elsewhere

            phaseSecondsLeft.Value--;
        }

        // phase timed out
        RpcPhaseTimeout(phaseAtStart);
        ServerOnPhaseTimeout(pawn, phaseAtStart);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcPhaseTimerTick(TurnPhase phase, int seconds, int currentOwnerCid)
    {
        TurnUI.Instance?.SetPhaseTimer(phase, seconds, currentOwnerCid);
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcPhaseTimeout(TurnPhase phase)
    {
        UICloser.CloseForPhaseClient(phase);
    }

    [Server]
    private void ServerOnPhaseTimeout(PlayerPawn pawn, TurnPhase phaseTimedOut)
    {
        if (pawn == null || !IsCurrentPawn(pawn)) return;

        switch (phaseTimedOut)
        {
            case TurnPhase.Review:
                MarketManager.Instance?.ForceCloseReviewFor(pawn);
                ProceedToRoll();
                break;

            case TurnPhase.Rolling:
                ServerForceRoll(pawn);
                break;

            case TurnPhase.TileEventPending:
                ServerOnTileActionComplete(pawn);
                break;

            case TurnPhase.Proposal:
                MarketManager.Instance?.ForceCloseProposalFor(pawn, autoSkip: true);
                OnPlayerFinishedProposal();
                break;

            case TurnPhase.MainEvent:                 // ← NEW
                ServerStartTurnAfterMainEvent();      // resume match
                break;

            case TurnPhase.EndReady:
                EndTurn();
                break;
        }
    }


    [Server]
    private void ServerForceRoll(PlayerPawn pawn)
    {
        if (pawn == null || !IsCurrentPawn(pawn)) { ServerEnterEndReadyAndAutoEnd(); return; }

        int d1 = UnityEngine.Random.Range(1, 7);
        int d2 = UnityEngine.Random.Range(1, 7);
        int total = d1 + d2;
        pawn.lastRoll.Value = total;

        EventManager.Instance?.OnServerPlayerRolled(pawn, total);
        pawn.TargetShowDiceAndMove(pawn.Owner, d1, d2, total);
    }
}
