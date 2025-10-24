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
    private bool waitingForAcks = false;

    private enum ResumeContext { None, Tile, Main }
    private ResumeContext _resume = ResumeContext.None;
    private PlayerPawn _resumeTilePawn = null;

    [Header("Event Databases")]
    public List<GameEventSO> tileEvents = new();
    public List<GameEventSO> mainEvents = new();

    private bool _compFixedPayoutMode = false;
    private int _compWinnerPayout = 0;
    private int _compOtherPayout = 0;

    private bool _bankOddFineActive = false;
    private string _bankCompanyName = "Bank";
    private int _bankOddFineAmount = 500;
    private int _bankOddFineExpiresAtRound = -1;

    private void Awake() => Instance = this;

    [Header("Timelines (Main Events only)")]
    public List<TimelineSO> timelines = new();
    private int _currentTimelineIndex = -1;
    private readonly Dictionary<int, PlayerPawn> _cidToPawn = new();

    private bool _tierActive = false;
    private int _tierLowMax, _tierHighMin, _tierPay, _tierGain;
    private readonly HashSet<int> _tierParticipants = new();
    private readonly Dictionary<int, int> _tierRolls = new();

    private GameEventSO _pendingTierEvent = null;
    private bool _tierAwaitingCloses = false;
    private readonly HashSet<int> _tierClosed = new();

    private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();


    // ================= UTILS =================

    [Server]
    private int CountConnectedOwnedPlayers()
    {
        int n = 0;
        if (GameManager.Instance != null)
        {
            foreach (var p in GameManager.Instance.Players)
                if (p?.Owner != null)
                    n++;
        }
        return Mathf.Max(1, n);
    }

    /* ================= TIMELINE HELPERS ================= */
    
    private List<GameEventSO> _mainEventDeck = new();
    private int _deckTimelineIndex = -1;

    [Server]
    private void BuildEventDeck()
    {
        var tl = GetCurrentTimeline();
        List<GameEventSO> src = null;

        if (tl != null && tl.mainEvents != null && tl.mainEvents.Count > 0)
        {
            src = tl.mainEvents;
            _deckTimelineIndex = _currentTimelineIndex;
        }
        else
        {
            src = mainEvents; // fallback
            _deckTimelineIndex = -2; // special tag for fallback
        }

        _mainEventDeck.Clear();
        if (src != null)
            _mainEventDeck.AddRange(src);

        // shuffle
        for (int i = 0; i < _mainEventDeck.Count; i++)
        {
            int j = Random.Range(i, _mainEventDeck.Count);
            var tmp = _mainEventDeck[i];
            _mainEventDeck[i] = _mainEventDeck[j];
            _mainEventDeck[j] = tmp;
        }
    }

    [Server]
    private GameEventSO DrawMainEventNoRepeat()
    {
        // If deck not built or timeline changed, rebuild.
        if (_mainEventDeck.Count == 0 ||
            (_deckTimelineIndex != _currentTimelineIndex && _deckTimelineIndex >= 0))
            BuildEventDeck();

        if (_mainEventDeck.Count == 0) return null;

        var e = _mainEventDeck[0];
        _mainEventDeck.RemoveAt(0);
        return e;
    }

    [Server]
    private void SelectTimelineIfNeeded()
    {
        if (timelines == null || timelines.Count == 0) return;
        if (_currentTimelineIndex >= 0 && _currentTimelineIndex < timelines.Count) return;

        _currentTimelineIndex = Random.Range(0, timelines.Count);
        var name = timelines[_currentTimelineIndex]?.timelineName ?? "Unknown Era";
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, "Timeline Selected", name, false);
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
        return DrawMainEventNoRepeat();
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

        var chooser = TurnManager.Instance?.GetCurrentPawn();

        if (e.eventName == "Fundraising for New Business Development")
        {
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

        // Default side popup flow
        TurnManager.Instance.ServerBeginTileAction(pawn);
        _resume = ResumeContext.Tile;
        _resumeTilePawn = pawn;

        TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);
        ApplyEventToPawn(e, pawn);
    }

    private PlayerPawn _tileFlowPawn;
    private int _tileStepOpenCount = 0;

    [Server] private void BeginTileFlow(PlayerPawn pawn)
    {
        _tileFlowPawn = pawn;
        _tileStepOpenCount = 0;
        TurnManager.Instance.ServerBeginTileAction(pawn);
    }

    [Server] private void ShowStep(PlayerPawn pawn, string msg)
    {
        _tileStepOpenCount++;
        if (pawn?.Owner != null)
            TargetShowSideEventStep(pawn.Owner, msg);
    }

    [TargetRpc]
    private void TargetShowSideEventStep(FishNet.Connection.NetworkConnection conn, string message)
    {
        if (EventUI.Instance != null)
        {
            EventUI.Instance.SideeventShow(message, pauseAll: true);
            EventUI.Instance.SetSideeventReadyCallback(() =>
            {
                EventManager.Instance.CmdTileStepClosed();
            });
        }
    }

    [TargetRpc]
    private void TargetShowSideEventFinal(FishNet.Connection.NetworkConnection conn, string message)
    {
        if (EventUI.Instance != null)
        {
            EventUI.Instance.SideeventShow(message, pauseAll: true);
            EventUI.Instance.SetSideeventReadyCallback(() =>
            {
                TurnManager.Instance.CmdTileActionReady();
            });
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdTileStepClosed(FishNet.Connection.NetworkConnection conn = null)
    {
        if (_tileFlowPawn == null || _tileFlowPawn.Owner != conn) return;
        _tileStepOpenCount = Mathf.Max(0, _tileStepOpenCount - 1);
    }

    /* ================= MAIN EVENTS (TIMELINE-BOUND) ================= */

    [Server]
    public void TriggerMainEvent(int round)
    {
        ServerPruneSectorSurges();
        TickBankOddFineExpiration();
        SelectTimelineIfNeeded();

        GameEventSO e = PickMainEventFromTimeline();
        RpcUpdateNextMainEventUI(PeekNextMainEventName());

        string title = (e != null) ? e.eventName : $"Main Event — Round {round}";
        string body  = (e != null) ? e.description : "—";

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, title, body, true);

        _resume = ResumeContext.Main;
        _resumeTilePawn = null;

        if (e == null)
        {
            ResumeAfterEvent();
            return;
        }

        // Apply global parts (sector surges etc.) without spawning a new popup
        ApplyGlobalEventParts(e);

        // Always gate the event by “Ready” when a main popup was shown
        waitingForAcks = true;
        playersReady   = 0;
        requiredReady  = CountConnectedOwnedPlayers();

        switch (e.mode)
        {
            case EventMode.Simple:
                ApplyEventToAll(e);
                if (e.triggerRecession)
                    StartCoroutine(CoRecession(Mathf.Max(1, e.recessionRounds)));
                // CmdPlayerReady() will call ResumeAfterEvent() when everyone clicks Ready
                break;

            case EventMode.ForcedRollAgainstOwner:
                DeferUntilAllReady(DeferredMode.ForcedRollAgainstOwner, e);
                break;

            case EventMode.TargetSelect:
                DeferUntilAllReady(DeferredMode.TargetSelect, e);
                break;

            case EventMode.ProposalBlock:
                DeferUntilAllReady(DeferredMode.ProposalBlock, e);
                break;

            case EventMode.ForcedRollTier:
                // After everyone clicks Ready, we’ll open the Tier UI
                _pendingTierEvent = e;
                _deferredMode = DeferredMode.None;
                _deferredEvent = null;
                break;

            default:
                DeferUntilAllReady(DeferredMode.ApplyToAllThenResume, e);
                break;
        }
    }

    private void DeferUntilAllReady(DeferredMode mode, GameEventSO e)
    {
        waitingForAcks = true;
        playersReady = 0;
        // FIX: count only owned/connected players
        requiredReady = CountConnectedOwnedPlayers();
        _deferredMode = mode;
        _deferredEvent = e;
        _pendingTierEvent = null;
    }

    private enum DeferredMode { None, ForcedRollAgainstOwner, Competition, TargetSelect, ProposalBlock, ApplyToAllThenResume }

    private DeferredMode _deferredMode = DeferredMode.None;
    private GameEventSO _deferredEvent = null;

    /* ================= POPUPS & ACK ================= */

    [TargetRpc]
    private void TargetShowMainEvent(NetworkConnection conn, string title, string body, bool pauseAll)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.MaineventShow(title, body, pauseAll);
    }

    [TargetRpc]
    private void TargetShowSideEvent(NetworkConnection conn, string message, bool pauseAll)
    {
        if (EventUI.Instance != null)
        {
            EventUI.Instance.SideeventShow(message, pauseAll);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerReady(NetworkConnection conn = null)
    {
        if (!waitingForAcks) return;

        // helpful log
        Debug.Log($"[EventManager] CmdPlayerReady from cid={conn?.ClientId}  -> {playersReady + 1}/{requiredReady}");

        playersReady++;

        if (playersReady >= requiredReady)
        {
            waitingForAcks = false;

            if (_pendingTierEvent != null)
            {
                var e = _pendingTierEvent;
                _pendingTierEvent = null;
                StartForcedRollTier(e);
                return;
            }

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
            
            ResumeAfterEvent();
        }
    }

    /* ================= RESUME FLOW ================= */

    [Server]
    private void ResumeAfterEvent()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.IsGameEnded)
            return;

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
                totalMoney += Random.Range(effect.randomMoneyMin, effect.randomMoneyMax + 1);
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
                TargetShowMainEvent(c, $"No one owns {comp}; event skipped.", "", false);
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
                TargetShowMainEvent(c, $"No opponents to challenge {owner.playerName.Value}; event skipped.", "", false);
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
            TargetShowMainEvent(c, summary, "", false);

        ResumeAfterEvent();
    }

    // ===================== MEDIA ATTENTION (grid UI) =====================

    private GameEventSO _compEvent;
    private readonly HashSet<int> _compParticipants = new();
    private readonly Dictionary<int, int> _compRolls = new();
    private int _compPot;
    private bool _compAwaitingCloses = false;
    private readonly HashSet<int> _compClosed = new();

    private bool _mediaModeActive = false;
    private int _mediaWinnerPayout = 0;
    private int _mediaOtherPayout = 0;
    private readonly HashSet<int> _mediaParticipants = new();
    private readonly Dictionary<int, int> _mediaRolls = new();
    private bool _mediaAwaitingCloses = false;
    private readonly HashSet<int> _mediaClosed = new();

    [ObserversRpc]
    private void RpcSetRollMode(ForcedRollTierUI.RollMode mode)
    {
        ForcedRollTierUI.CurrentRollMode = mode;
    }

    [TargetRpc]
    private void TargetGridRolling(FishNet.Connection.NetworkConnection conn, int cid)
    {
        ForcedRollTierUI.Instance?.SetRolling(cid);
    }

    [TargetRpc]
    private void TargetGridRolled(FishNet.Connection.NetworkConnection conn, int cid, int roll, string outcome)
    {
        ForcedRollTierUI.Instance?.SetRolled(cid, roll, outcome);
    }

    [TargetRpc]
    private void TargetSetFooterAndEnableClose(FishNet.Connection.NetworkConnection conn, string footer)
    {
        ForcedRollTierUI.Instance?.SetFooter(footer);
        ForcedRollTierUI.Instance?.EnableCloseForLocal();
    }

    [ObserversRpc]
    private void RpcUpdateMediaCloseStatus(int have, int total)
    {
        ForcedRollTierUI.Instance?.UpdateCloseStatus(have, total);
    }

    [ObserversRpc]
    private void RpcCloseGridUI()
    {
        ForcedRollTierUI.Instance?.Hide();
    }

    [Server]
    private void StartMediaAttentionAllRoll(int winPayout, int otherPayout)
    {
        _mediaModeActive = true;
        _mediaWinnerPayout = winPayout;
        _mediaOtherPayout = otherPayout;

        _mediaParticipants.Clear();
        _mediaRolls.Clear();
        _cidToPawn.Clear();
        _mediaAwaitingCloses = false;
        _mediaClosed.Clear();

        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        foreach (var p in GameManager.Instance.Players)
        {
            if (p?.Owner == null) continue;
            _mediaParticipants.Add(p.Owner.ClientId);
            _cidToPawn[p.Owner.ClientId] = p;
        }

        if (_mediaParticipants.Count == 0)
        {
            _mediaModeActive = false;
            ResumeAfterEvent();
            return;
        }

        var ids = new List<int>(_mediaParticipants);
        var names = new Dictionary<int, string>();
        foreach (var cid in ids)
            names[cid] = _cidToPawn.TryGetValue(cid, out var pp) && pp != null ? pp.playerName.Value : ("P" + cid);

        RpcSetRollMode(ForcedRollTierUI.RollMode.Media);

        string header = $"Roll a d6. Highest gets ${_mediaWinnerPayout}M; others get ${_mediaOtherPayout}M.";
        foreach (var kv in FishNet.InstanceFinder.ServerManager.Clients)
            TargetShowForcedRollTier(kv.Value, header, ids.ToArray(), ToNames(ids, names), kv.Key);

        StartCoroutine(CoMediaTimeout(20f));
    }

    private string[] ToNames(List<int> ids, Dictionary<int, string> map)
    {
        var arr = new string[ids.Count];
        for (int i = 0; i < ids.Count; i++) arr[i] = map.TryGetValue(ids[i], out var n) ? n : ("P" + ids[i]);
        return arr;
    }

    [TargetRpc]
    private void TargetShowForcedRollTier(FishNet.Connection.NetworkConnection conn, string header, int[] cids, string[] names, int localCid)
    {
        var map = new Dictionary<int, string>();
        for (int i = 0; i < cids.Length; i++)
            map[cids[i]] = (i < names.Length ? names[i] : ("P" + cids[i]));

        ForcedRollTierUI.Instance?.Show(header, new List<int>(cids), map, localCid);
    }

    private IEnumerator CoMediaTimeout(float seconds)
    {
        float t = seconds;
        while (t > 0f && _mediaModeActive && _mediaRolls.Count < _mediaParticipants.Count)
        {
            t -= Time.deltaTime;
            yield return null;
        }

        if (_mediaModeActive && _mediaRolls.Count < _mediaParticipants.Count)
        {
            foreach (var cid in _mediaParticipants)
                if (!_mediaRolls.ContainsKey(cid))
                    _mediaRolls[cid] = UnityEngine.Random.Range(1, 7);

            ResolveMediaAttention();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestMediaRoll(FishNet.Connection.NetworkConnection conn = null)
    {
        if (!_mediaModeActive || conn == null) return;

        int cid = conn.ClientId;
        if (!_mediaParticipants.Contains(cid)) return;
        if (_mediaRolls.ContainsKey(cid)) return;

        foreach (var c in FishNet.InstanceFinder.ServerManager.Clients.Values)
            TargetGridRolling(c, cid);

        int roll = UnityEngine.Random.Range(1, 7);
        _mediaRolls[cid] = roll;

        string outcome = $"roll {roll}";
        foreach (var c in FishNet.InstanceFinder.ServerManager.Clients.Values)
            TargetGridRolled(c, cid, roll, outcome);

        if (_mediaRolls.Count >= _mediaParticipants.Count)
            ResolveMediaAttention();
    }

    [Server]
    private void ResolveMediaAttention()
    {
        if (!_mediaModeActive) return;

        int maxRoll = 0;
        foreach (var r in _mediaRolls.Values) if (r > maxRoll) maxRoll = r;

        var winners = new List<PlayerPawn>();
        foreach (var kv in _mediaRolls)
            if (kv.Value == maxRoll && _cidToPawn.TryGetValue(kv.Key, out var pw) && pw != null)
                winners.Add(pw);

        foreach (var cid in _mediaParticipants)
        {
            if (!_cidToPawn.TryGetValue(cid, out var pw) || pw == null) continue;
            if (winners.Contains(pw)) pw.AddMoney(_mediaWinnerPayout);
            else pw.AddMoney(_mediaOtherPayout);
        }

        foreach (var winPawn in winners)
        {
            MarketManager.Instance.ServerBoostAllCompaniesOwnedBy(
                winPawn,
                priceDeltaPercent: +20f,
                payoutMultiplier: 1.25f,
                durationRounds: 2
            );
        }


        string footer = $"Max {maxRoll}. Winners: {winners.Count}. Winner +${_mediaWinnerPayout}M, others +${_mediaOtherPayout}M.";
        foreach (var c in FishNet.InstanceFinder.ServerManager.Clients.Values)
            TargetSetFooterAndEnableClose(c, footer);

        _mediaAwaitingCloses = true;
        _mediaClosed.Clear();
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdMediaClientClosed(FishNet.Connection.NetworkConnection conn = null)
    {
        if (conn == null || !_mediaAwaitingCloses) return;
        int cid = conn.ClientId;
        if (!_mediaParticipants.Contains(cid)) return;
        if (_mediaClosed.Contains(cid)) return;

        _mediaClosed.Add(cid);
        RpcUpdateMediaCloseStatus(_mediaClosed.Count, _mediaParticipants.Count);

        if (_mediaClosed.Count >= _mediaParticipants.Count)
        {
            _mediaAwaitingCloses = false;
            _mediaModeActive = false;
            RpcCloseGridUI();
            ResumeAfterEvent();
        }
    }

    /* ================= TARGET SELECT ================= */

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
            return;

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

                    MarketManager.Instance.ServerNerfAllCompaniesOwnedBy(
                    target,
                    priceDeltaPercent: -35f,
                    payoutMultiplier: 0.55f,
                    durationRounds: 2
                    );
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

    private bool _tsCyberAttackMode = false;
    private float _tsRansomRate = 0f;

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
            TargetShowMainEvent(c, $"Benefactor Donation: Everyone paid ${amountEach}M to {receiver.playerName.Value}.", "", false);

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
            TargetShowMainEvent(conn, $"Odd Roll Fine ACTIVE: {_bankCompanyName} collects ${_bankOddFineAmount}M on odd rolls.", "", false);
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
                TargetShowMainEvent(conn, $"Odd Roll Fine EXPIRED.", "", false);
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

        _currentTimelineIndex = -1;

        _tierActive = false;
        _tierAwaitingCloses = false;
        _tierClosed.Clear();
        _pendingTierEvent = null;
        _mainEventDeck.Clear();
        _deckTimelineIndex = -1;

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

        ForcedRollTierUI.Instance?.Hide();
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
            TargetShowMainEvent(c, $"Proposals disabled for {rounds} round(s).", "", false);
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

        // FIX: pass isMedia = false (do NOT force Media mode for Tier)
        foreach (var kv in InstanceFinder.ServerManager.Clients)
            TargetShowForcedRollTier(kv.Value, header, ids.ToArray(), names.ToArray(), kv.Key, false);
    }

    // Overload that lets us force the roll mode per-client when showing Tier vs Media
    [TargetRpc]
    private void TargetShowForcedRollTier(NetworkConnection conn, string header, int[] cids, string[] names, int localCid, bool isMedia)
    {
        ForcedRollTierUI.CurrentRollMode = isMedia
            ? ForcedRollTierUI.RollMode.Media
            : ForcedRollTierUI.RollMode.Tier;

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

    [Server]
    private void ResolveForcedRollTier()
    {
        _tierActive = false;

        _tierAwaitingCloses = true;
        _tierClosed.Clear();

        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetTierEnableClose(c);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdTierClientClosed(NetworkConnection conn = null)
    {
        if (conn == null || !_tierAwaitingCloses) return;

        int cid = conn.ClientId;
        if (!_tierParticipants.Contains(cid)) return;
        if (_tierClosed.Contains(cid)) return;

        _tierClosed.Add(cid);
        BroadcastTierCloseStatus(_tierClosed.Count, _tierParticipants.Count);

        if (_tierClosed.Count >= _tierParticipants.Count)
        {
            RpcCloseForcedRollTier();
            _tierAwaitingCloses = false;
            ResumeAfterEvent();
        }
    }

    [Server]
    private void BroadcastTierCloseStatus(int have, int total)
    {
        RpcUpdateTierCloseStatus(have, total);
    }

    [ObserversRpc]
    private void RpcUpdateTierCloseStatus(int have, int total)
    {
        ForcedRollTierUI.Instance?.UpdateCloseStatus(have, total);
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

        int roll = UnityEngine.Random.Range(1, 7);
        _tierRolls[cid] = roll;

        var pawn = conn.FirstObject?.GetComponent<PlayerPawn>();
        string outcome;

        if (pawn != null)
        {
            if (roll <= _tierLowMax)
            {
                int pay = Mathf.Min(_tierPay, pawn.money.Value);
                if (pay > 0) pawn.TrySpendMoney(pay);
                MarketManager.Instance.ServerNerfAllCompaniesOwnedBy(
                    pawn,
                    priceDeltaPercent: -20,     // negative is allowed (e.g., -15%)
                    payoutMultiplier:  0.5f,      // < 1f means nerf payouts
                    durationRounds:    2
                );
                outcome = $"-${pay}";
            }
            else if (roll >= _tierHighMin)
            {
                pawn.AddMoney(_tierGain);
                MarketManager.Instance.ServerBoostAllCompaniesOwnedBy(
                    pawn,
                    priceDeltaPercent: 20,     // e.g., +20%
                    payoutMultiplier:  2f,      // > 1f means boost payouts
                    durationRounds:    2
                );
                outcome = $"+${_tierGain}";
            }
            else
            {
                outcome = "no change";
            }
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
                        $"Odd Roll Fine: {roller.playerName.Value} pays ${pay}M to {bankOwner.playerName.Value}.",  "",
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

        // NEW: seed “next main event” name
        RpcUpdateNextMainEventUI(PeekNextMainEventName());
    }


    [Server]
    private IEnumerator CoRecession(int rounds)
    {
        MarketManager.Instance.ServerSetGlobalTrend(-2f);
        MarketManager.Instance.ServerBoostAllPayouts(0.8f, rounds);
        int end = TurnManager.Instance.roundCount.Value + rounds;
        while (TurnManager.Instance.roundCount.Value < end) yield return null;
        MarketManager.Instance.ServerSetGlobalTrend(0f);
    }

    [Server]
    public string PeekNextMainEventName()
    {
        // Ensure the deck exists and matches current timeline
        if (_mainEventDeck.Count == 0 ||
            (_deckTimelineIndex != _currentTimelineIndex && _deckTimelineIndex >= 0))
            BuildEventDeck();

        if (_mainEventDeck.Count == 0) return "—";
        var next = _mainEventDeck[0];
        return (next != null) ? next.eventName : "—";
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcUpdateNextMainEventUI(string nextName)
    {
        // If you have a dedicated label:
        TurnUI.Instance.SetNextMainEventName(nextName);
    }
    
    private struct SectorSurge {
        public float priceMult;      // affects buy cost & immediate price bump
        public float payoutMult;     // company-level payout multiplier
        public int   expiresAtRound; // absolute round number
    }
    private readonly Dictionary<string, SectorSurge> _sectorSurges = new();

    private int CurRound() => TurnManager.Instance?.roundCount.Value ?? 1;

    [Server]
    private void PruneSectorSurges()
    {
        int now = CurRound();
        var rm = new List<string>();
        foreach (var kv in _sectorSurges)
            if (now >= kv.Value.expiresAtRound) rm.Add(kv.Key);
        foreach (var s in rm) _sectorSurges.Remove(s);
    }

    [Server]
    public float GetActiveSectorPriceMult(string sector)
    {
        sector = Norm(sector);
        if (!string.IsNullOrEmpty(sector) && _sectorSurges.TryGetValue(sector, out var s))
            if (CurRound() < s.expiresAtRound) return Mathf.Max(0.01f, s.priceMult);
        return 1f;
    }

    [Server]
    public (bool active, float payoutMult, int expiresAt) GetActiveSectorPayoutAura(string sector)
    {
        sector = Norm(sector);
        if (!string.IsNullOrEmpty(sector) && _sectorSurges.TryGetValue(sector, out var s))
            if (CurRound() < s.expiresAtRound) return (true, s.payoutMult, s.expiresAtRound);
        return (false, 1f, 0);
    }

    [Server]
    public void ActivateSectorSurge(string sector, float priceMult, float payoutMult, int durationRounds)
    {
        sector = Norm(sector);
        if (string.IsNullOrEmpty(sector)) return;

        if (_sectorSurges.TryGetValue(sector, out var cur))
        {
            cur.priceMult      *= Mathf.Max(0.01f, priceMult);
            cur.payoutMult     *= Mathf.Max(0f,    payoutMult);
            cur.expiresAtRound  = Mathf.Max(cur.expiresAtRound, CurRound() + Mathf.Max(1, durationRounds));
            _sectorSurges[sector] = cur;
        }
        else
        {
            _sectorSurges[sector] = new SectorSurge {
                priceMult      = Mathf.Max(0.01f, priceMult),
                payoutMult     = Mathf.Max(0f,    payoutMult),
                expiresAtRound = CurRound() + Mathf.Max(1, durationRounds)
            };
        }

        // Immediate effects to existing companies (no extra popup)
        float deltaPct = (_sectorSurges[sector].priceMult - 1f) * 100f;
        foreach (var kv in MarketManager.Instance.companies)
        {
            var c = kv.Value;
            if (c == null) continue;
            if (Norm(c.sector) != sector) continue;

            if (Mathf.Abs(deltaPct) > 0.001f)
                MarketManager.Instance.ServerBumpCompanyPrice(c.companyName, deltaPct);

            if (!Mathf.Approximately(_sectorSurges[sector].payoutMult, 1f))
                MarketManager.Instance.ServerBoostCompanyPayouts(
                    c.companyName,
                    _sectorSurges[sector].payoutMult,
                    _sectorSurges[sector].expiresAtRound - CurRound());
        }
    }

    [Server]
    public void ServerPruneSectorSurges()
    {
        PruneSectorSurges(); // your existing private method
    }
    
    [Server]
    private void ApplyGlobalEventParts(GameEventSO e)
    {
        if (e?.sectorImpacts == null) return;
        foreach (var s in e.sectorImpacts)
        {
            if (string.IsNullOrWhiteSpace(s.sector)) continue;
            ActivateSectorSurge(Norm(s.sector), s.priceMult, s.payoutMult, s.durationRounds);
        }
    }
}
