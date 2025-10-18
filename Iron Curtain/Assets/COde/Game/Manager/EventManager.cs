using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Connection;

public class EventManager : NetworkBehaviour
{
    public static EventManager Instance;

    private int playersReady = 0;
    private int requiredReady = 0;
    private bool waitingForAcks = false; // used for MAIN events only

    // Resume context to differentiate Tile vs Main
    private enum ResumeContext { None, Tile, Main }
    private ResumeContext _resume = ResumeContext.None;
    private PlayerPawn _resumeTilePawn = null;

    [Header("Event Databases")]
    // Tile Events are NOT timeline-bound.
    public List<GameEventSO> tileEvents = new();

    // Optional legacy list (used only if current timeline has no events)
    public List<GameEventSO> mainEvents = new();

    // ===== State for special modes =====
    private bool _compFixedPayoutMode = false;
    private int _compWinnerPayout = 0;
    private int _compOtherPayout = 0;

    // Bank Odd Fine (rule toggle)
    private bool _bankOddFineActive = false;
    private string _bankCompanyName = "Bank";
    private int _bankOddFineAmount = 500;
    private int _bankOddFineExpiresAtRound = -1;

    private void Awake() => Instance = this;

    [Header("Timelines (Main Events only)")]
    public List<TimelineSO> timelines = new(); // assign in inspector
    private int _currentTimelineIndex = -1;     // selected once per match
    private readonly Dictionary<int, PlayerPawn> _cidToPawn = new();

    // ==== ForcedRollTier state ====
    private bool _tierActive = false;
    private int _tierLowMax, _tierHighMin, _tierPay, _tierGain;
    private readonly HashSet<int> _tierParticipants = new();
    private readonly Dictionary<int, int> _tierRolls = new();

    // Defer tier start until everyone presses Ready on the main-event popup; keep UI until all Close
    private GameEventSO _pendingTierEvent = null;
    private bool _tierAwaitingCloses = false;
    private readonly HashSet<int> _tierClosed = new();

    /* ================= TIMELINE HELPERS ================= */

    [Server]
    private void SelectTimelineIfNeeded()
    {
        if (timelines == null || timelines.Count == 0) return;
        if (_currentTimelineIndex >= 0 && _currentTimelineIndex < timelines.Count) return;

        _currentTimelineIndex = Random.Range(0, timelines.Count);
        var name = timelines[_currentTimelineIndex]?.timelineName ?? "Unknown Era";
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, $"Timeline selected: {name}", false);
        RpcSetTimelineName(name);
    }

    [Server]
    private TimelineSO GetCurrentTimeline()
    {
        if (_currentTimelineIndex >= 0 &&
            timelines != null &&
            _currentTimelineIndex < timelines.Count)
            return timelines[_currentTimelineIndex];
        return null;
    }

    [Server]
    private GameEventSO PickMainEventFromTimeline()
    {
        var tl = GetCurrentTimeline();
        if (tl != null && tl.mainEvents != null && tl.mainEvents.Count > 0)
            return tl.mainEvents[Random.Range(0, tl.mainEvents.Count)];
        // Fallback only if timeline has zero events
        if (mainEvents != null && mainEvents.Count > 0)
            return mainEvents[Random.Range(0, mainEvents.Count)];
        return null;
    }

    /* ================= TILE EVENTS (NOT TIMELINE-BOUND) ================= */

    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (pawn == null)
        {
            TurnManager.Instance.ServerOnTileActionComplete(pawn);
            return;
        }

        GameEventSO e = null;
        if (tileEvents != null && tileEvents.Count > 0)
            e = tileEvents[Random.Range(0, tileEvents.Count)];

        if (e == null)
        {
            TurnManager.Instance.ServerOnTileActionComplete(pawn);
            return;
        }

        // Special named tile events
        var chooser = TurnManager.Instance?.GetCurrentPawn();

        if (e.eventName == "Fundraising for New Business Development")
        {
            // Bracket this tile action so proposal waits until we finish this flow.
            TurnManager.Instance.ServerBeginTileAction(pawn);

            _resume = ResumeContext.Tile;
            _resumeTilePawn = pawn;

            TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);
            StartCoroutine(CoBenefactorDonationTile(pawn, 100));
            return;
        }
        else if (e.eventName == "Your Business Gains Media Attention!")
        {
            TurnManager.Instance.ServerBeginTileAction(pawn);

            _resume = ResumeContext.Tile;
            _resumeTilePawn = pawn;

            StartMediaAttentionAllRoll(winPayout: 1000, otherPayout: 100);
            return;
        }
        else if (e.eventName == "Your Business Is Hit by a Cyber Attack!")
        {
            TurnManager.Instance.ServerBeginTileAction(pawn);

            _resume = ResumeContext.Tile;
            _resumeTilePawn = pawn;

            StartCyberAttackTargetSelect(chooser, 0.10f);
            return;
        }

        // ===== Default tile behaviour (simple side popup) =====
        // IMPORTANT: no per-tile waitingForAcks. The tile panel's Ready calls TurnManager.CmdTileActionReady().
        TurnManager.Instance.ServerBeginTileAction(pawn);

        _resume = ResumeContext.Tile;
        _resumeTilePawn = pawn;

        TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);

        // Apply effects immediately; proposal will NOT open until the player presses Ready on the panel.
        ApplyEventToPawn(e, pawn);
        // Now we wait for the client Ready → TurnManager.CmdTileActionReady() → TurnManager.ServerOnTileActionComplete(pawn)
    }

    /* ================= MAIN EVENTS (TIMELINE-BOUND) ================= */

    [Server]
    public void TriggerMainEvent(int round)
    {
        TickBankOddFineExpiration();

        // Ensure one timeline is chosen for the whole match
        SelectTimelineIfNeeded();

        // Pick ONLY from the selected timeline (with a safe fallback if timeline is empty)
        GameEventSO e = PickMainEventFromTimeline();
        string msg = (e != null) ? $"{e.eventName}\n\n{e.description}" : $"Main Event at Round {round}!";

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, msg, true);

        _resume = ResumeContext.Main;
        _resumeTilePawn = null;

        if (e == null)
        {
            // No specific event — just continue the flow
            ResumeAfterEvent();
            return;
        }

        if (e.mode == EventMode.Simple)
        {
            waitingForAcks = true;
            playersReady = 0;
            int totalPlayers = GameManager.Instance != null ? GameManager.Instance.Players.Count : 0;
            requiredReady = Mathf.Max(1, totalPlayers);

            ApplyEventToAll(e);

            if (e.enableBankOddFine)
                EnableBankOddFine(e.bankCompanyName, e.oddFineAmount, e.oddFineDurationRounds);
        }
        else
        {
            switch (e.mode)
            {
                case EventMode.ForcedRollAgainstOwner:
                    DeferUntilAllReady(DeferredMode.ForcedRollAgainstOwner, e);
                    break;

                case EventMode.Competition:
                    DeferUntilAllReady(DeferredMode.Competition, e);
                    break;

                case EventMode.TargetSelect:
                    DeferUntilAllReady(DeferredMode.TargetSelect, e);
                    break;

                case EventMode.ProposalBlock:
                    DeferUntilAllReady(DeferredMode.ProposalBlock, e);
                    break;

                case EventMode.ForcedRollTier:
                    // Show main popup first (already shown), then wait for ALL Ready, THEN open tier UI.
                    waitingForAcks = true;
                    playersReady = 0;
                    requiredReady = Mathf.Max(1, GameManager.Instance != null ? GameManager.Instance.Players.Count : 0);
                    _pendingTierEvent = e; // handled in CmdPlayerReady
                    _deferredMode = DeferredMode.None;
                    _deferredEvent = null;
                    break;

                default:
                    DeferUntilAllReady(DeferredMode.ApplyToAllThenResume, e);
                    break;
            }
        }
    }

    private void DeferUntilAllReady(DeferredMode mode, GameEventSO e)
    {
        waitingForAcks = true;
        playersReady = 0;
        requiredReady = Mathf.Max(1, GameManager.Instance != null ? GameManager.Instance.Players.Count : 0);
        _deferredMode = mode;
        _deferredEvent = e;
        _pendingTierEvent = null;
    }

    private enum DeferredMode { None, ForcedRollAgainstOwner, Competition, TargetSelect, ProposalBlock, ApplyToAllThenResume }

    private DeferredMode _deferredMode = DeferredMode.None;
    private GameEventSO _deferredEvent = null;

    /* ================= POPUPS & ACK ================= */

    [TargetRpc]
    private void TargetShowMainEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.MaineventShow(message, pauseAll);
    }

    [TargetRpc]
    private void TargetShowSideEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        if (EventUI.Instance != null)
        {
            // EventUI's Ready button should call: TurnManager.Instance.CmdTileActionReady()
            EventUI.Instance.SideeventShow(message, pauseAll);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerReady(NetworkConnection conn = null)
    {
        if (!waitingForAcks) return;

        playersReady++;
        Debug.Log($"[EventManager] Ack received {playersReady}/{requiredReady}");

        if (playersReady >= requiredReady)
        {
            waitingForAcks = false;

            // Handle ForcedRollTier (deferred until all Ready)
            if (_pendingTierEvent != null)
            {
                var e = _pendingTierEvent;
                _pendingTierEvent = null;
                StartForcedRollTier(e);
                return;
            }

            // Handle other deferred special modes
            if (_deferredMode != DeferredMode.None)
            {
                var e = _deferredEvent;
                var mode = _deferredMode;
                _deferredMode = DeferredMode.None;
                _deferredEvent = null;

                switch (mode)
                {
                    case DeferredMode.ForcedRollAgainstOwner:
                        StartForcedRollAgainstOwner(e);
                        return;
                    case DeferredMode.Competition:
                        StartCompetition(e);
                        return;
                    case DeferredMode.TargetSelect:
                        StartTargetSelect(e);
                        return;
                    case DeferredMode.ProposalBlock:
                        EnableProposalBlockForRounds(Mathf.Max(1, e.blockProposalRounds));
                        ResumeAfterEvent();
                        return;
                    case DeferredMode.ApplyToAllThenResume:
                        ApplyEventToAll(e);
                        ResumeAfterEvent();
                        return;
                }
            }

            // Default: continue flow
            Debug.Log("[EventManager] All acks received → ResumeAfterEvent()");
            ResumeAfterEvent();
        }
    }

    /* ================= RESUME FLOW ================= */

    [Server]
    private void ResumeAfterEvent()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.IsGameEnded)
            return;

        Debug.Log($"[EventManager] ResumeAfterEvent: {_resume}");
        switch (_resume)
        {
            case ResumeContext.Tile:
                if (_resumeTilePawn != null)
                    TurnManager.Instance.ServerOnTileActionComplete(_resumeTilePawn);
                break;
            case ResumeContext.Main:
                TurnManager.Instance.ServerStartTurnAfterMainEvent();
                break;
        }
        _resume = ResumeContext.None;
        _resumeTilePawn = null;
    }

    /* ================= SIMPLE EFFECTS ================= */

    [Server]
    private void ApplyEventToAll(GameEventSO e)
    {
        if (e == null || GameManager.Instance == null) return;
        foreach (var pawn in GameManager.Instance.Players)
            ApplyEventToPawn(e, pawn);
    }

    [Server]
    private void ApplyEventToPawn(GameEventSO e, PlayerPawn pawn)
    {
        if (e == null || pawn == null) return;

        bool extraRollGranted = false;

        foreach (var effect in e.effects)
        {
            int totalMoney = effect.moneyDelta;
            if (effect.randomMoneyMax > effect.randomMoneyMin)
                totalMoney += Random.Range(effect.randomMoneyMin, effect.randomMoneyMax + 1);
            if (totalMoney != 0)
                pawn.AddMoney(totalMoney);

            if (effect.skipTurn)
                TurnManager.Instance.MarkSkipTurn(pawn, Mathf.Max(1, effect.duration));

            if (effect.grantExtraRoll)
                extraRollGranted = true;

            switch (effect.targetType)
            {
                case TargetType.Factory:
                    if (!string.IsNullOrEmpty(effect.targetName) &&
                        pawn.factoryPortfolio.TryGetValue(effect.targetName, out var facRec))
                    {
                        if (!Mathf.Approximately(effect.multiplier, 1f))
                        {
                            facRec.multiplier *= effect.multiplier;
                            facRec.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                            pawn.factoryPortfolio[effect.targetName] = facRec;
                        }
                    }
                    break;

                case TargetType.Ownership:
                    if (!string.IsNullOrEmpty(effect.targetName) &&
                        pawn.factoryPortfolio.TryGetValue(effect.targetName, out var ownRec))
                    {
                        ownRec.sharePercent += effect.ownershipDelta;
                        ownRec.sharePercent = Mathf.Clamp(ownRec.sharePercent, 0, 100);
                        pawn.factoryPortfolio[effect.targetName] = ownRec;
                    }
                    break;

                case TargetType.Global:
                    if (!Mathf.Approximately(effect.multiplier, 1f) || effect.duration > 0)
                    {
                        var keys = new List<string>(pawn.factoryPortfolio.Keys);
                        foreach (var key in keys)
                        {
                            var g = pawn.factoryPortfolio[key];
                            g.multiplier *= effect.multiplier;
                            g.multiplierExpiresAt = TurnManager.Instance.roundCount.Value + effect.duration;
                            pawn.factoryPortfolio[key] = g;
                        }
                    }
                    break;
            }
        }

        if (extraRollGranted)
        {
            TurnManager.Instance.QueueExtraRoll(pawn, 1);
            Debug.Log($"[Event] {pawn.playerName.Value} gains an extra roll!");
        }
    }

    /* ================= SPECIAL MODES ================= */

    [Server]
    private void StartForcedRollAgainstOwner(GameEventSO e)
    {
        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        string comp = e.requiredCompanyName;
        if (string.IsNullOrEmpty(comp)) { ResumeAfterEvent(); return; }

        PlayerPawn owner = null;
        foreach (var p in GameManager.Instance.Players)
        {
            if (p.factoryPortfolio.TryGetValue(comp, out var rec) && rec.sharePercent > 0)
            {
                owner = p;
                break;
            }
        }

        if (owner == null)
        {
            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(c, $"No one owns {comp}; event skipped.", false);
            ResumeAfterEvent();
            return;
        }

        List<PlayerPawn> targets = new();
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null) continue;
            if (p == owner) continue;
            targets.Add(p);
        }

        if (targets.Count == 0)
        {
            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(c, $"No opponents to challenge {owner.playerName.Value}; event skipped.", false);
            ResumeAfterEvent();
            return;
        }

        int payEach = Mathf.Max(0, e.payOnOdd);
        System.Text.StringBuilder sb = new();
        sb.AppendLine($"{e.eventName} (vs owner of {comp})");
        foreach (var t in targets)
        {
            int roll = Random.Range(1, 7);
            bool odd = (roll % 2 == 1);

            if (odd && payEach > 0)
            {
                int before = t.money.Value;
                if (t.TrySpendMoney(payEach))
                {
                    owner.AddMoney(payEach);
                    sb.AppendLine($"{t.playerName.Value} roll={roll} (odd). Pays ${payEach} to {owner.playerName.Value}.");
                }
                else
                {
                    int taken = before;
                    if (taken > 0)
                    {
                        t.TrySpendMoney(taken);
                        owner.AddMoney(taken);
                        sb.AppendLine($"{t.playerName.Value} roll={roll} (odd). Could only pay ${taken} to {owner.playerName.Value}.");
                    }
                    else
                    {
                        sb.AppendLine($"{t.playerName.Value} roll={roll} (odd). Cannot pay (insufficient funds).");
                    }
                }
            }
            else
            {
                sb.AppendLine($"{t.playerName.Value} roll={roll} (even). No payment.");
            }
        }

        string summary = sb.ToString();
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(c, summary, false);

        ResumeAfterEvent();
    }

    private GameEventSO _compEvent;
    private readonly HashSet<int> _compParticipants = new();
    private readonly Dictionary<int, int> _compRolls = new();
    private int _compPot;

    [Server]
    private void StartCompetition(GameEventSO e)
    {
        _compEvent = e;
        _compParticipants.Clear();
        _compRolls.Clear();
        _cidToPawn.Clear();
        _compPot = 0;

        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        foreach (var p in GameManager.Instance.Players)
        {
            if (p?.Owner == null) continue;
            _compParticipants.Add(p.Owner.ClientId);
            _cidToPawn[p.Owner.ClientId] = p;

            if (e.entryFee > 0)
            {
                if (!p.TrySpendMoney(e.entryFee))
                {
                    int have = p.money.Value;
                    if (have > 0) p.TrySpendMoney(have);
                    _compPot += have;
                }
                else
                {
                    _compPot += e.entryFee;
                }
            }
        }

        if (_compParticipants.Count == 0)
        {
            _compEvent = null;
            ResumeAfterEvent();
            return;
        }

        StartCoroutine(CoCompetitionTimeout(20f));

        string title = $"{e.eventName}\nEntry Pot = ${_compPot}\nRoll a d6. Highest wins!";
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            if (_compParticipants.Contains(conn.ClientId))
                TargetShowCompetition(conn, title);
    }

    [TargetRpc]
    private void TargetShowCompetition(NetworkConnection conn, string title)
    {
        if (CompetitionUI.Instance != null)
            CompetitionUI.Instance.Show(title);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdSubmitCompetitionRoll(int roll, NetworkConnection conn = null)
    {
        if (conn == null) return;

        bool compActive = _compFixedPayoutMode || (_compEvent != null && _compEvent.mode == EventMode.Competition);
        if (!compActive) return;

        int cid = conn.ClientId;
        if (!_compParticipants.Contains(cid)) return;
        if (_compRolls.ContainsKey(cid)) return;

        int r = Mathf.Clamp(roll, 1, 6);
        _compRolls[cid] = r;

        BroadcastCompetitionStatus(_compRolls.Count, _compParticipants.Count);

        if (_compRolls.Count >= _compParticipants.Count)
            ResolveCompetition();
    }

    [Server]
    private void BroadcastCompetitionStatus(int have, int total)
    {
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetCompetitionStatus(c, $"Competition: {have}/{total} rolls submitted.");
    }

    [TargetRpc]
    private void TargetCompetitionStatus(NetworkConnection conn, string status)
    {
        if (CompetitionUI.Instance != null)
            CompetitionUI.Instance.UpdateStatus(status);
    }

    [Server]
    private void ResolveCompetition()
    {
        if (_compFixedPayoutMode)
        {
            int maxRoll = 0;
            foreach (var r in _compRolls.Values) if (r > maxRoll) maxRoll = r;

            List<PlayerPawn> winners = new();
            foreach (var kv in _compRolls)
                if (kv.Value == maxRoll && _cidToPawn.TryGetValue(kv.Key, out var pw) && pw != null)
                    winners.Add(pw);

            foreach (var cid in _compParticipants)
            {
                if (!_cidToPawn.TryGetValue(cid, out var pw) || pw == null) continue;
                if (winners.Contains(pw)) pw.AddMoney(_compWinnerPayout);
                else pw.AddMoney(_compOtherPayout);
            }

            string summary = $"Media Attention Roll\nMax Roll={maxRoll}\nWinners={winners.Count}\nWinner gets ${_compWinnerPayout}M, others ${_compOtherPayout}M.";
            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(c, summary, false);

            CloseCompetitionUI();

            _compFixedPayoutMode = false;
            _compWinnerPayout = 0;
            _compOtherPayout = 0;

            ResumeAfterEvent();
            return;
        }

        if (_compEvent == null)
        {
            CloseCompetitionUI();
            ResumeAfterEvent();
            return;
        }

        int max = 0;
        foreach (var r in _compRolls.Values) if (r > max) max = r;

        List<PlayerPawn> winners2 = new();
        foreach (var kv in _compRolls)
            if (kv.Value == max && _cidToPawn.TryGetValue(kv.Key, out var pw) && pw != null)
                winners2.Add(pw);

        int each = (_compEvent.tieSplitPot && winners2.Count > 0)
            ? Mathf.FloorToInt(_compPot / winners2.Count)
            : _compPot;

        if (_compEvent.tieSplitPot)
        {
            foreach (var w in winners2) w.AddMoney(each);
        }
        else if (winners2.Count > 0)
        {
            winners2[0].AddMoney(each);
        }

        string summary2 = $"{_compEvent.eventName}\nResult: Max Roll={max}, Winners={winners2.Count}, Pot=${_compPot}M.";
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(c, summary2, false);

        CloseCompetitionUI();
        _compEvent = null;
        ResumeAfterEvent();
    }

    [Server]
    private void CloseCompetitionUI()
    {
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetCloseCompetition(c);
    }

    [TargetRpc]
    private void TargetCloseCompetition(NetworkConnection conn)
    {
        if (CompetitionUI.Instance != null)
            CompetitionUI.Instance.Hide();
    }

    private GameEventSO _tsEvent;
    private PlayerPawn _tsChooser;

    [Server]
    private void StartTargetSelect(GameEventSO e)
    {
        _tsEvent = e;
        _tsChooser = TurnManager.Instance?.GetCurrentPawn();

        List<PlayerPawn> targets = GetEligibleTargetsForTS(e, _tsChooser);
        if (targets.Count == 0)
        {
            _tsEvent = null;
            ResumeAfterEvent();
            return;
        }

        if (_tsChooser?.Owner != null)
        {
            TargetShowTargetSelect(_tsChooser.Owner, SerializeNames(targets));
        }
        else
        {
            var t = targets[Random.Range(0, targets.Count)];
            ApplyTargetSelectTo(t);
        }
    }

    [TargetRpc]
    private void TargetShowTargetSelect(NetworkConnection conn, string serializedNames)
    {
        if (TargetSelectUI.Instance != null)
        {
            TargetSelectUI.Instance.Show(serializedNames);
        }
        else
        {
            var names = serializedNames.Split('|');
            int idx = Random.Range(0, names.Length);
            CmdSubmitTargetSelect(names[idx]);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdSubmitTargetSelect(string targetPlayerName, NetworkConnection conn = null)
    {
        bool allow =
            (_tsEvent != null && _tsEvent.mode == EventMode.TargetSelect)
            || _tsCyberAttackMode;

        if (!allow) return;

        var chooser = _tsChooser;
        if (chooser == null)
        {
            _tsEvent = null;
            _tsCyberAttackMode = false;
            _tsRansomRate = 0f;
            ResumeAfterEvent();
            return;
        }

        if (conn == null || chooser.Owner != conn)
            return; // only the chooser can submit

        var target = GameManager.Instance.Players.Find(p => p.playerName.Value == targetPlayerName);
        if (target == null)
        {
            _tsEvent = null;
            _tsCyberAttackMode = false;
            _tsRansomRate = 0f;
            ResumeAfterEvent();
            return;
        }

        ApplyTargetSelectTo(target);
    }

    [Server]
    private void ApplyTargetSelectTo(PlayerPawn target)
    {
        if (_tsCyberAttackMode)
        {
            var chooser = _tsChooser;
            int ransom = Mathf.FloorToInt(target.money.Value * _tsRansomRate);
            ransom = Mathf.Max(0, ransom);

            int before = target.money.Value;
            if (ransom > 0)
            {
                int taken = Mathf.Min(ransom, before);
                if (taken > 0)
                {
                    target.TrySpendMoney(taken);
                    chooser?.AddMoney(taken);
                }
            }

            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowSideEvent(c, $"Cyber Attack! {target.playerName.Value} pays ${ransom}M to {chooser.playerName.Value}. (Paid ${Mathf.Min(ransom, before)}M)", false);

            _tsCyberAttackMode = false;
            _tsRansomRate = 0f;

            ResumeAfterEvent();
            return;
        }

        ApplyEventToPawn(_tsEvent, target);
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowSideEvent(c, $"{_tsEvent.eventName}: Target → {target.playerName.Value}", false);

        _tsEvent = null;
        ResumeAfterEvent();
    }

    private List<PlayerPawn> GetEligibleTargetsForTS(GameEventSO e, PlayerPawn chooser)
    {
        var list = new List<PlayerPawn>();
        if (GameManager.Instance == null) return list;

        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null) continue;

            if (e.restrictToOpponents && p == chooser)
                continue;

            if (e.requireCompanyOwner)
            {
                string targetCompany = null;
                foreach (var eff in e.effects)
                {
                    if (eff.targetType == TargetType.Factory && !string.IsNullOrEmpty(eff.targetName))
                    {
                        targetCompany = eff.targetName;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(targetCompany))
                {
                    if (!p.factoryPortfolio.ContainsKey(targetCompany) ||
                        p.factoryPortfolio[targetCompany].sharePercent <= 0)
                        continue;
                }
            }

            list.Add(p);
        }
        return list;
    }

    private string SerializeNames(List<PlayerPawn> pawns)
    {
        var arr = new List<string>();
        foreach (var p in pawns)
            arr.Add(p.playerName.Value);
        return string.Join("|", arr);
    }

    [Server]
    private void StartMediaAttentionAllRoll(int winPayout, int otherPayout)
    {
        _compEvent = null;
        _compParticipants.Clear();
        _compRolls.Clear();
        _cidToPawn.Clear();
        _compPot = 0;

        _compFixedPayoutMode = true;
        _compWinnerPayout = winPayout;
        _compOtherPayout = otherPayout;

        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        foreach (var p in GameManager.Instance.Players)
        {
            if (p?.Owner == null) continue;
            _compParticipants.Add(p.Owner.ClientId);
            _cidToPawn[p.Owner.ClientId] = p;
        }

        if (_compParticipants.Count == 0)
        {
            _compFixedPayoutMode = false;
            ResumeAfterEvent();
            return;
        }

        StartCoroutine(CoCompetitionTimeout(20f));

        string title = $"Your Business Gains Media Attention!\nRoll a d6. Highest gets ${winPayout}M; others get ${otherPayout}M.";
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            if (_compParticipants.Contains(conn.ClientId))
                TargetShowCompetition(conn, title);
    }

    private bool _tsCyberAttackMode = false;
    private float _tsRansomRate = 0f; // e.g., 0.10f

    [Server]
    private void StartCyberAttackTargetSelect(PlayerPawn chooser, float ransomRate)
    {
        _tsEvent = null;
        _tsChooser = chooser;
        _tsCyberAttackMode = true;
        _tsRansomRate = ransomRate;

        List<PlayerPawn> targets = new();
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null || p == chooser) continue;
            targets.Add(p);
        }

        if (targets.Count == 0)
        {
            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowSideEvent(c, "Cyber Attack: No valid targets.", false);
            _tsCyberAttackMode = false;
            _tsRansomRate = 0f;
            ResumeAfterEvent();
            return;
        }

        var serialized = SerializeNames(targets);
        if (chooser?.Owner != null)
            TargetShowTargetSelect(chooser.Owner, serialized);
        else
        {
            var t = targets[Random.Range(0, targets.Count)];
            ApplyTargetSelectTo(t);
        }
    }

    private IEnumerator CoBenefactorDonationTile(PlayerPawn receiver, int amountEach)
    {
        yield return null;
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null || p == receiver) continue;
            int pay = Mathf.Min(amountEach, p.money.Value);
            if (pay > 0)
            {
                p.TrySpendMoney(pay);
                receiver.AddMoney(pay);
            }
        }

        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowSideEvent(receiver.Owner, $"Benefactor Donation: Others paid you up to ${amountEach}M each.", false);

        ResumeAfterEvent();
    }

    [Server]
    private void StartBenefactorDonationMain(PlayerPawn receiver, int amountEach)
    {
        if (receiver == null)
        {
            ResumeAfterEvent();
            return;
        }

        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null || p == receiver) continue;
            int pay = Mathf.Min(amountEach, p.money.Value);
            if (pay > 0)
            {
                p.TrySpendMoney(pay);
                receiver.AddMoney(pay);
            }
        }

        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(c, $"Benefactor Donation: Everyone paid ${amountEach}M to {receiver.playerName.Value}.", false);

        ResumeAfterEvent();
    }

    [Server]
    private void EnableBankOddFine(string companyName, int amount, int duration)
    {
        _bankCompanyName = string.IsNullOrWhiteSpace(companyName) ? "Bank" : companyName;
        _bankOddFineAmount = Mathf.Max(0, amount);
        _bankOddFineActive = true;

        _bankOddFineExpiresAtRound = (duration > 0)
            ? TurnManager.Instance.roundCount.Value + duration
            : -1;

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, $"Odd Roll Fine ACTIVE: {_bankCompanyName} collects ${_bankOddFineAmount}M on odd rolls.", false);
    }

    [Server]
    private void TickBankOddFineExpiration()
    {
        if (!_bankOddFineActive) return;
        if (_bankOddFineExpiresAtRound < 0) return;

        if (TurnManager.Instance.roundCount.Value >= _bankOddFineExpiresAtRound)
        {
            _bankOddFineActive = false;
            foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(conn, $"Odd Roll Fine EXPIRED.", false);
        }
    }

    // ======== MATCH END HOOK ========
    [Server]
    public void OnMatchEnded()
    {
        waitingForAcks = false;
        playersReady = 0;
        requiredReady = 0;

        _resume = ResumeContext.None;
        _resumeTilePawn = null;

        _compEvent = null;
        _compParticipants.Clear();
        _compRolls.Clear();
        _compPot = 0;
        _compFixedPayoutMode = false;
        _compWinnerPayout = 0;
        _compOtherPayout = 0;

        _tsEvent = null;
        _tsChooser = null;
        _tsCyberAttackMode = false;
        _tsRansomRate = 0f;

        _bankOddFineActive = false;
        _bankOddFineExpiresAtRound = -1;

        // Reset selected timeline so a new one will be chosen next match.
        _currentTimelineIndex = -1;

        // ForcedRollTier cleanup
        _tierActive = false;
        _tierAwaitingCloses = false;
        _tierClosed.Clear();
        _pendingTierEvent = null;

        RpcCloseEventUIs();
        RpcSetTimelineName("—");

    }

    [ObserversRpc(BufferLast = true)]
    private void RpcCloseEventUIs()
    {
        if (EventUI.Instance != null)
            EventUI.Instance.gameObject.SetActive(false);

        if (TargetSelectUI.Instance != null)
            TargetSelectUI.Instance.gameObject.SetActive(false);

        if (CompetitionUI.Instance != null)
            CompetitionUI.Instance.gameObject.SetActive(false);

        ForcedRollTierUI.Instance?.Hide();
    }

    private IEnumerator CoCompetitionTimeout(float seconds)
    {
        float t = seconds;
        while (t > 0f
               && (_compFixedPayoutMode || (_compEvent != null && _compEvent.mode == EventMode.Competition))
               && _compRolls.Count < _compParticipants.Count)
        {
            t -= Time.deltaTime;
            yield return null;
        }

        if (_compRolls.Count < _compParticipants.Count
            && (_compFixedPayoutMode || (_compEvent != null && _compEvent.mode == EventMode.Competition)))
        {
            foreach (var cid in _compParticipants)
                if (!_compRolls.ContainsKey(cid))
                    _compRolls[cid] = UnityEngine.Random.Range(1, 7);

            ResolveCompetition();
        }
    }

    // --- Proposal block state ---
    private int _proposalBlockExpiresAtRound = -1;

    public bool IsProposalBlockedNow()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return false;
        return (_proposalBlockExpiresAtRound >= 0 &&
                tm.roundCount.Value <= _proposalBlockExpiresAtRound);
    }

    [Server]
    private void EnableProposalBlockForRounds(int rounds)
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;
        int cur = Mathf.Max(1, tm.roundCount.Value);
        int newExpiry = cur + Mathf.Max(1, rounds) - 1; // inclusive
        _proposalBlockExpiresAtRound = Mathf.Max(_proposalBlockExpiresAtRound, newExpiry);

        foreach (var c in FishNet.InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(c, $"Proposals disabled for {rounds} round(s).", false);
    }

    /* ================= FORCED ROLL TIER ================= */

    [Server]
    private void StartForcedRollTier(GameEventSO e)
    {
        _tierActive = true;
        _tierAwaitingCloses = false;
        _tierClosed.Clear();
        _tierParticipants.Clear();
        _tierRolls.Clear();

        _tierLowMax = Mathf.Clamp(e.lowMax, 1, 5);
        _tierHighMin = Mathf.Clamp(e.highMin, 2, 6);
        _tierPay = Mathf.Max(0, e.lowPayAmount);
        _tierGain = Mathf.Max(0, e.highGainAmount);

        var list = new List<PlayerPawn>();
        if (e.affectAllPlayers || TurnManager.Instance?.GetCurrentPawn() == null)
            list.AddRange(GameManager.Instance.Players);
        else
            list.Add(TurnManager.Instance.GetCurrentPawn());

        var ids = new List<int>();
        var names = new List<string>();
        foreach (var p in list)
        {
            if (p?.Owner == null) continue;
            ids.Add(p.Owner.ClientId);
            names.Add(p.playerName.Value);
            _tierParticipants.Add(p.Owner.ClientId);
        }

        string header = $"{e.eventName}\n1–{_tierLowMax}: pay ${_tierPay} • {_tierHighMin}–6: +${_tierGain} • else: no change";

        foreach (var kv in InstanceFinder.ServerManager.Clients)
            TargetShowForcedRollTier(kv.Value, header, ids.ToArray(), names.ToArray(), kv.Key);
    }

    [TargetRpc]
    private void TargetShowForcedRollTier(NetworkConnection conn, string header, int[] cids, string[] names, int localCid)
    {
        var map = new Dictionary<int, string>();
        for (int i = 0; i < cids.Length; i++)
            map[cids[i]] = (i < names.Length ? names[i] : ("P" + cids[i]));

        ForcedRollTierUI.Instance?.Show(header, new List<int>(cids), map, localCid);
    }

    [TargetRpc]
    private void TargetTierRolling(NetworkConnection conn, int cid)
    {
        ForcedRollTierUI.Instance?.SetRolling(cid);
    }

    [TargetRpc]
    private void TargetTierRolled(NetworkConnection conn, int cid, int roll, string outcome)
    {
        ForcedRollTierUI.Instance?.SetRolled(cid, roll, outcome);
    }

    [ObserversRpc]
    private void RpcCloseForcedRollTier()
    {
        ForcedRollTierUI.Instance?.Hide();
    }

    [TargetRpc]
    private void TargetTierEnableClose(NetworkConnection conn)
    {
        ForcedRollTierUI.Instance?.EnableCloseForLocal();
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestTierRoll(NetworkConnection conn = null)
    {
        if (!_tierActive || conn == null) return;

        int cid = conn.ClientId;
        if (!_tierParticipants.Contains(cid)) return;
        if (_tierRolls.ContainsKey(cid)) return;

        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetTierRolling(c, cid);

        int roll = Random.Range(1, 7);
        _tierRolls[cid] = roll;

        var pawn = conn.FirstObject?.GetComponent<PlayerPawn>();
        string outcome;

        if (roll <= _tierLowMax)
        {
            int pay = Mathf.Min(_tierPay, pawn.money.Value);
            if (pay > 0) pawn.TrySpendMoney(pay);
            outcome = $"-${pay}";
        }
        else if (roll >= _tierHighMin)
        {
            pawn.AddMoney(_tierGain);
            outcome = $"+${_tierGain}";
        }
        else
        {
            outcome = "no change";
        }

        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetTierRolled(c, cid, roll, outcome);

        if (_tierRolls.Count >= _tierParticipants.Count)
            ResolveForcedRollTier();
    }

    [Server]
    private void ResolveForcedRollTier()
    {
        _tierActive = false;

        // Keep UI open; wait for EVERY participant to press Close.
        _tierAwaitingCloses = true;
        _tierClosed.Clear();

        // Tell clients they can enable Close now.
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetTierEnableClose(c);
    }

    // Local Close button calls this
    [ServerRpc(RequireOwnership = false)]
    public void CmdTierClientClosed(NetworkConnection conn = null)
    {
        if (conn == null || !_tierAwaitingCloses) return;

        int cid = conn.ClientId;
        if (!_tierParticipants.Contains(cid)) return; // only participants count
        if (_tierClosed.Contains(cid)) return;

        _tierClosed.Add(cid);
        BroadcastTierCloseStatus(_tierClosed.Count, _tierParticipants.Count);

        if (_tierClosed.Count >= _tierParticipants.Count)
        {
            // Everyone closed → now close UI and resume
            RpcCloseForcedRollTier();
            _tierAwaitingCloses = false;
            ResumeAfterEvent();
        }
    }

    [Server]
    private void BroadcastTierCloseStatus(int have, int total)
    {
        // Optional status; reuse Competition status UI line
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetCompetitionStatus(c, $"Tier: {have}/{total} players closed.");
    }

    /* ================= BANK ODD FINE HOOK ================= */

    [Server]
    public void OnServerPlayerRolled(PlayerPawn roller, int roll)
    {
        if (!_bankOddFineActive || roller == null) return;
        if ((roll % 2) == 0) return;

        if (MarketManager.Instance.TryGetMajorityOwner(_bankCompanyName, out var bankOwner))
        {
            if (bankOwner != null && bankOwner != roller)
            {
                int pay = Mathf.Min(_bankOddFineAmount, roller.money.Value);
                if (pay > 0)
                {
                    roller.TrySpendMoney(pay);
                    bankOwner.AddMoney(pay);
                }

                foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
                    TargetShowMainEvent(conn,
                        $"Odd Roll Fine: {roller.playerName.Value} pays ${pay}M to {bankOwner.playerName.Value}.",
                        false);
            }
        }
    }

    private string _timelineName = "—";

    [ObserversRpc(BufferLast = true)]
    private void RpcSetTimelineName(string tl)
    {
        _timelineName = string.IsNullOrWhiteSpace(tl) ? "—" : tl;
        TimelineUI.Instance?.SetTimeline(_timelineName);
    }

    [Server]
    public void ServerSelectTimelineAndAnnounce()
    {
        SelectTimelineIfNeeded();
        var tl = GetCurrentTimeline();
        string name = tl?.timelineName ?? "Classic";
        RpcSetTimelineName(name);
    }
}
