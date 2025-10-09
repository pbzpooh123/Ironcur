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

    // Resume context to differentiate Tile vs Main
    private enum ResumeContext { None, Tile, Main }
    private ResumeContext _resume = ResumeContext.None;
    private PlayerPawn _resumeTilePawn = null;

    [Header("Event Databases (Optional)")]
    public List<GameEventSO> tileEvents = new();
    public List<GameEventSO> mainEvents = new();
    
    
    private bool _compFixedPayoutMode = false;
    private int _compWinnerPayout = 0;
    private int _compOtherPayout = 0;
    
    // Bank Odd Fine
    private bool _bankOddFineActive = false;
    private string _bankCompanyName = "Bank";
    private int _bankOddFineAmount = 500;
    private int _bankOddFineExpiresAtRound = -1;

    private void Awake() => Instance = this;

    public List<TimelineSO> timelines = new(); // assign in inspector
    private int _currentTimelineIndex = -1;

    #region ================= Timeline =================
    [Server]
    private GameEventSO PickMainEventFromCurrentTimelineOrFallback()
    {
        if (timelines != null && timelines.Count > 0)
        {
            if (_currentTimelineIndex < 0 || _currentTimelineIndex >= timelines.Count)
            {
                _currentTimelineIndex = Random.Range(0, timelines.Count);
                var name = timelines[_currentTimelineIndex].timelineName;
                foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
                    TargetShowMainEvent(conn, $"Timeline selected: {name}", false);
            }

            var tl = timelines[_currentTimelineIndex];
            if (tl != null && tl.mainEvents != null && tl.mainEvents.Count > 0)
                return tl.mainEvents[Random.Range(0, tl.mainEvents.Count)];
        }

        // fallback to your existing list
        if (mainEvents != null && mainEvents.Count > 0)
            return mainEvents[Random.Range(0, mainEvents.Count)];

        return null;
    }

    #endregion
    
    #region ================= TILE EVENTS =================
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (tileEvents == null || tileEvents.Count == 0 || pawn == null)
        {
            TurnManager.Instance.ServerOnTileActionComplete(pawn);
            return;
        }

        var e = tileEvents[Random.Range(0, tileEvents.Count)];

        // === Special named tile events ===
        if (e.eventName == "A Benefactor Donates Money to You")
        {
            _resume = ResumeContext.Tile;
            _resumeTilePawn = pawn;

            // Show a popup to the pawn (optional)
            TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);

            // Apply: everyone except pawn pays $100M to pawn
            StartCoroutine(CoBenefactorDonationTile(pawn, 100)); // 100M
            return;
        }
        else if (e.eventName == "Accident at Your Factory Causes Production Halt")
        {
            _resume = ResumeContext.Tile;
            _resumeTilePawn = pawn;

            TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);

            // Interpretation: bank pays the landing player a random 100–1000M.
            // If you prefer the landing player pays the bank, swap the signs.
            int amt = Random.Range(100, 1001);
            pawn.AddMoney(amt);
            ResumeAfterEvent();
            return;
        }

        // === Default tile behaviour ===
        _resume = ResumeContext.Tile;
        _resumeTilePawn = pawn;

        waitingForAcks = true;
        playersReady = 0;
        requiredReady = 1;
        TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);

        // Apply effects only to the pawn (your existing semantics)
        ApplyEventToPawn(e, pawn);
    }

    #endregion

    #region ================= MAIN EVENTS =================
    [Server]
public void TriggerMainEvent(int round)
{
    TickBankOddFineExpiration();

    GameEventSO e = PickMainEventFromCurrentTimelineOrFallback();
    string msg = (e != null) ? $"{e.eventName}\n\n{e.description}" : $"Main Event at Round {round}!";


    if (mainEvents != null && mainEvents.Count > 0)
    {
        e = mainEvents[Random.Range(0, mainEvents.Count)];
        msg = $"{e.eventName}\n\n{e.description}";
    }
    else
    {
        msg = $"Main Event at Round {round}!";
    }

    foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
        TargetShowMainEvent(conn, msg, true);

    _resume = ResumeContext.Main;
    _resumeTilePawn = null;

    // === Special named main events ===
    var chooser = TurnManager.Instance?.GetCurrentPawn(); // "you" for main events

    if (e != null && e.eventName == "Your Business Gains Media Attention!")
    {
        // All players roll; highest gets 1000, others 100
        StartMediaAttentionAllRoll(winPayout: 1000, otherPayout: 100);
        return;
    }
    else if (e != null && e.eventName == "Your Business Is Hit by a Cyber Attack!")
    {
        // chooser chooses 1 target; target pays 10% to chooser
        StartCyberAttackTargetSelect(chooser, 0.10f);
        return;
    }
    else if (e != null && e.eventName == "A Benefactor Donates Money to You")
    {
        // All players except chooser pay 100M to chooser
        StartBenefactorDonationMain(chooser, 100);
        return;
    }

    // ===== Default main-event flow (simple or special modes already supported) =====
    if (e == null || e.mode == EventMode.Simple)
    {
        waitingForAcks = true;
        playersReady = 0;
        int totalPlayers = GameManager.Instance != null ? GameManager.Instance.Players.Count : 0;
        requiredReady = Mathf.Max(1, totalPlayers);

        if (e != null)
        {
            ApplyEventToAll(e);

            // Enable Rule #6 if this main event has it
            if (e.enableBankOddFine)
                EnableBankOddFine(e.bankCompanyName, e.oddFineAmount, e.oddFineDurationRounds);
        }
    }
    else
    {
        switch (e.mode)
        {
            case EventMode.ForcedRollAgainstOwner:
                StartForcedRollAgainstOwner(e);
                break;
            case EventMode.Competition:
                StartCompetition(e);
                break;
            case EventMode.TargetSelect:
                StartTargetSelect(e);
                break;
            default:
                ApplyEventToAll(e);
                ResumeAfterEvent();
                break;
        }
    }
}

    #endregion

    #region =============== POPUPS & ACK ===============
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
            EventUI.Instance.SideeventShow(message, pauseAll);
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
            Debug.Log("[EventManager] All acks received → ResumeAfterEvent()");
            ResumeAfterEvent();
        }
    }
    #endregion
    
    #region =============== RESUME FLOW ===============
    [Server]
    private void ResumeAfterEvent()
    {
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
    #endregion

    #region ========== SIMPLE EFFECTS APPLICATOR ==========
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

    #endregion

    // ======= Special Modes (unchanged behavior, integrated with ResumeAfterEvent) =======

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
        _compPot = 0;

        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        foreach (var p in GameManager.Instance.Players)
        {
            if (p?.Owner == null) continue;
            _compParticipants.Add(p.Owner.ClientId);

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

        string title = $"{e.eventName}\nEntry Pot = ${_compPot}\nRoll a d6. Highest wins!";
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
        {
            if (_compParticipants.Contains(conn.ClientId))
                TargetShowCompetition(conn, title);
        }
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
        if (_compEvent == null || _compEvent.mode != EventMode.Competition) return;
        if (conn == null) return;

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
    // If we're in 'fixed payouts' mode (Media Attention)
    if (_compFixedPayoutMode)
    {
        int maxRoll = 0;
        foreach (var r in _compRolls.Values)
            if (r > maxRoll) maxRoll = r;

        // Who are winners?
        List<PlayerPawn> winners = new();
        foreach (var kv in _compRolls)
        {
            if (kv.Value == maxRoll)
            {
                var conn = InstanceFinder.ServerManager.Clients.TryGetValue(kv.Key, out var c) ? c : null;
                var pawn = conn?.FirstObject?.GetComponent<PlayerPawn>();
                if (pawn != null) winners.Add(pawn);
            }
        }

        // Payouts
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
        {
            var pawn = conn?.FirstObject?.GetComponent<PlayerPawn>();
            if (pawn == null) continue;

            if (winners.Contains(pawn))
                pawn.AddMoney(_compWinnerPayout);
            else
                pawn.AddMoney(_compOtherPayout);
        }

        string summary = $"Media Attention Roll\nMax Roll={maxRoll}\nWinners={winners.Count}\nWinner gets ${_compWinnerPayout}M, others ${_compOtherPayout}M.";
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(c, summary, false);

        CloseCompetitionUI();

        // Reset flags
        _compFixedPayoutMode = false;
        _compWinnerPayout = 0;
        _compOtherPayout = 0;

        ResumeAfterEvent();
        return;
    }

    // ====== Your original "pot / tie-split" competition logic ======
    if (_compEvent == null)
    {
        CloseCompetitionUI();
        ResumeAfterEvent();
        return;
    }

    int max = 0;
    foreach (var r in _compRolls.Values)
        if (r > max) max = r;

    List<PlayerPawn> winners2 = new();
    foreach (var kv in _compRolls)
    {
        if (kv.Value == max)
        {
            var conn = InstanceFinder.ServerManager.Clients.TryGetValue(kv.Key, out var c) ? c : null;
            var pawn = conn?.FirstObject?.GetComponent<PlayerPawn>();
            if (pawn != null) winners2.Add(pawn);
        }
    }

    int each = (_compEvent.tieSplitPot && winners2.Count > 0)
        ? Mathf.FloorToInt(_compPot / winners2.Count)
        : _compPot;

    if (winners2.Count > 0)
    {
        if (_compEvent.tieSplitPot)
            foreach (var w in winners2) w.AddMoney(each);
        else
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
        if (_tsEvent == null || _tsEvent.mode != EventMode.TargetSelect) return;

        var chooser = _tsChooser;
        if (chooser == null) { _tsEvent = null; ResumeAfterEvent(); return; }
        if (conn == null || chooser.Owner != conn) return;

        var target = GameManager.Instance.Players.Find(p => p.playerName.Value == targetPlayerName);
        if (target == null)
        {
            _tsEvent = null;
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
            // Target pays % ransom to chooser
            var chooser = _tsChooser;
            int ransom = Mathf.FloorToInt(target.money.Value * _tsRansomRate);
            ransom = Mathf.Max(0, ransom);

            int before = target.money.Value;
            if (ransom > 0)
            {
                // take what they have if they can't pay full (no bailout for events)
                int taken = Mathf.Min(ransom, before);
                if (taken > 0)
                {
                    target.TrySpendMoney(taken);
                    chooser?.AddMoney(taken);
                }
            }

            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(c, $"Cyber Attack! {target.playerName.Value} pays ${ransom}M to {chooser.playerName.Value}. (Paid ${Mathf.Min(ransom, before)}M)", false);

            // reset flags
            _tsCyberAttackMode = false;
            _tsRansomRate = 0f;

            ResumeAfterEvent();
            return;
        }

        // ===== Default TargetSelect (SO-based) behaviour =====
        ApplyEventToPawn(_tsEvent, target);
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(c, $"{_tsEvent.eventName}: Target → {target.playerName.Value}", false);

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
        _compEvent = null; // Not using SO values; using our own special rule.
        _compParticipants.Clear();
        _compRolls.Clear();
        _compPot = 0;

        _compFixedPayoutMode = true;
        _compWinnerPayout = winPayout;
        _compOtherPayout = otherPayout;

        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        foreach (var p in GameManager.Instance.Players)
        {
            if (p?.Owner == null) continue;
            _compParticipants.Add(p.Owner.ClientId);
        }

        Debug.Log($"[EventManager] MediaAttention: participants={_compParticipants.Count}");

        if (_compParticipants.Count == 0)
        {
            _compFixedPayoutMode = false;
            ResumeAfterEvent();
            return;
        }

        string title = $"Your Business Gains Media Attention!\nRoll a d6. Highest gets ${winPayout}M; others get ${otherPayout}M.";
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
        {
            if (_compParticipants.Contains(conn.ClientId))
                TargetShowCompetition(conn, title);
        }
    }

    private bool _tsCyberAttackMode = false;
    private float _tsRansomRate = 0f; // e.g., 0.10f

    [Server]
    private void StartCyberAttackTargetSelect(PlayerPawn chooser, float ransomRate)
    {
        _tsEvent = null;  // not using a SO effect for this mode
        _tsChooser = chooser;
        _tsCyberAttackMode = true;
        _tsRansomRate = ransomRate;

        List<PlayerPawn> targets = new();
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null || p == chooser) continue; // restrict to opponents
            targets.Add(p);
        }

        if (targets.Count == 0)
        {
            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(c, "Cyber Attack: No valid targets.", false);
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
            // fallback server auto-pick
            var t = targets[Random.Range(0, targets.Count)];
            ApplyTargetSelectTo(t);
        }
    }

    
    private IEnumerator CoBenefactorDonationTile(PlayerPawn receiver, int amountEach)
    {
        yield return null; // optional small wait to let UI draw
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null || p == receiver) continue;
            int pay = Mathf.Min(amountEach, p.money.Value); // no bailout for events
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
            TargetShowMainEvent(conn, $"🏦 Odd Roll Fine ACTIVE: {_bankCompanyName} collects ${_bankOddFineAmount}M on odd rolls.", false);
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
                TargetShowMainEvent(conn, $"🏦 Odd Roll Fine EXPIRED.", false);
        }
    }
    
    [Server]
    public void OnServerPlayerRolled(PlayerPawn roller, int roll)
    {
        if (!_bankOddFineActive || roller == null) return;
        if ((roll % 2) == 0) return; // only odd

        if (MarketManager.Instance.TryGetMajorityOwner(_bankCompanyName, out var bankOwner))
        {
            if (bankOwner != null && bankOwner != roller)
            {
                int pay = Mathf.Min(_bankOddFineAmount, roller.money.Value); // no bailout
                if (pay > 0)
                {
                    roller.TrySpendMoney(pay);
                    bankOwner.AddMoney(pay);
                }

                foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
                    TargetShowMainEvent(conn,
                        $"🏦 Odd Roll Fine: {roller.playerName.Value} pays ${pay}M to {bankOwner.playerName.Value}.",
                        false);
            }
        }
    }

}
