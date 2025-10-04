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

    [Header("Event Databases (Optional)")]
    public List<GameEventSO> tileEvents = new();
    public List<GameEventSO> mainEvents = new();

    private void Awake() => Instance = this;

    #region ================= TILE EVENTS =================
    /// <summary>
    /// Random tile event from database. Only the pawn sees it. Wait for 1 ack.
    /// </summary>
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (tileEvents == null || tileEvents.Count == 0 || pawn == null)
            return;

        var e = tileEvents[Random.Range(0, tileEvents.Count)];

        waitingForAcks = true;
        playersReady = 0;
        requiredReady = 1; // only the triggering pawn must ack

        TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}",true);

        // Apply effects to the pawn only (simple semantics for tile events)
        ApplyEventToPawn(e, pawn);
    }
    #endregion

    #region ================= MAIN EVENTS =================
    [Server]
    public void TriggerMainEvent(int round)
    {
        GameEventSO e = null;
        string msg;

        if (mainEvents != null && mainEvents.Count > 0)
        {
            e = mainEvents[Random.Range(0, mainEvents.Count)];
            msg = $"{e.eventName}\n\n{e.description}";
        }
        else
        {
            msg = $"🌍 Main Event at Round {round}!";
        }

        // Show popup to everyone
        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, msg, true);

        // If no special mode → Simple
        if (e == null || e.mode == EventMode.Simple)
        {
            waitingForAcks = true;
            playersReady = 0;
            int totalPlayers = GameManager.Instance != null ? GameManager.Instance.Players.Count : 0;
            requiredReady = Mathf.Max(1, totalPlayers);

            if (e != null)
                ApplyEventToAll(e);
        }
        else
        {
            // Special modes do not use 'ack all' flow; they manage themselves then call ResumeAfterEvent.
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
                    // fallback: just apply to all and continue
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

    /// <summary>
    /// Called by client button on popup. Closes with ack flow.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerReady(NetworkConnection conn = null)
    {
        if (!waitingForAcks) return;

        playersReady++;
        if (playersReady >= requiredReady)
        {
            waitingForAcks = false;
            ResumeAfterEvent();
        }
    }
    #endregion

    #region =============== RESUME FLOW ===============
    /// <summary>
    /// After the popup or special mode finishes → allow current pawn to end turn (or open your own UI elsewhere).
    /// </summary>
    [Server]
    private void ResumeAfterEvent()
    {
        var pawn = TurnManager.Instance?.GetCurrentPawn();
        if (pawn != null)
        {
            // If you want to open stock UI or proposal UI here, call your MarketManager methods.
            // We only re-enable End Turn to keep your flow unchanged.
            pawn.TargetEnableEndTurn(pawn.Owner, true);
        }
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

    /// <summary>
    /// Apply the event effects to a single pawn.
    /// Supports: money, skip, Factory multiplier, Ownership delta, Global multiplier to all companies of this pawn.
    /// Uses struct write-back pattern for ShareRecord.
    /// </summary>
    [Server]
    private void ApplyEventToPawn(GameEventSO e, PlayerPawn pawn)
    {
        if (e == null || pawn == null) return;
        
        bool extraRoll = false;

        foreach (var effect in e.effects)
        {
            // 1) Instant / Random money
            int totalMoney = effect.moneyDelta;
            if (effect.randomMoneyMax > effect.randomMoneyMin)
                totalMoney += Random.Range(effect.randomMoneyMin, effect.randomMoneyMax + 1);
            if (totalMoney != 0)
                pawn.AddMoney(totalMoney);

            // 2) Skip turn
            if (effect.skipTurn)
                TurnManager.Instance.MarkSkipTurn(pawn, Mathf.Max(1, effect.duration));
            
            if (effect.grantExtraRoll)
                extraRoll = true;

            // 3) Per-type logic
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
                            pawn.factoryPortfolio[effect.targetName] = facRec; // write-back
                        }
                    }
                    break;

                case TargetType.Ownership:
                    if (!string.IsNullOrEmpty(effect.targetName) &&
                        pawn.factoryPortfolio.TryGetValue(effect.targetName, out var ownRec))
                    {
                        ownRec.sharePercent += effect.ownershipDelta;
                        ownRec.sharePercent = Mathf.Clamp(ownRec.sharePercent, 0, 100);
                        pawn.factoryPortfolio[effect.targetName] = ownRec; // write-back
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
                            pawn.factoryPortfolio[key] = g; // write-back
                        }
                    }
                    // === After all effects ===
                    if (extraRoll)
                    {
                        Debug.Log($"[Event] {pawn.playerName.Value} gains an extra roll!");
                        pawn.TargetGrantExtraRoll(pawn.Owner);
                    }
                    break;
                
                default:
                    break;
            }
        }
    }
    #endregion

    /* =======================================================================================
       MODE 1) FORCED ROLL AGAINST OWNER
       - Find company owner of e.requiredCompanyName.
       - All other players roll a d6; on odd → pay e.payOnOdd to owner.
       - Sends summary to everyone.
       ======================================================================================= */

    [Server]
    private void StartForcedRollAgainstOwner(GameEventSO e)
    {
        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        string comp = e.requiredCompanyName;
        if (string.IsNullOrEmpty(comp)) { ResumeAfterEvent(); return; }

        // Who owns this company (by sharePercent > 0)?
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
            // No owner found → nothing happens
            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(c, $"No one owns {comp}; event skipped.", false);
            ResumeAfterEvent();
            return;
        }

        List<PlayerPawn> targets = new();
        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null) continue;
            if (p == owner) continue; // don't include owner
            targets.Add(p);
        }

        if (targets.Count == 0)
        {
            foreach (var c in InstanceFinder.ServerManager.Clients.Values)
                TargetShowMainEvent(c, $"No opponents to challenge {owner.playerName.Value}; event skipped.", false);
            ResumeAfterEvent();
            return;
        }

        // Each target rolls a d6. If odd → pay e.payOnOdd to owner (if they have money; else take remaining).
        int payEach = Mathf.Max(0, e.payOnOdd);
        System.Text.StringBuilder sb = new();
        sb.AppendLine($"{e.eventName} (vs owner of {comp})");
        foreach (var t in targets)
        {
            int roll = Random.Range(1, 7);
            bool odd = (roll % 2 == 1);

            if (odd && payEach > 0)
            {
                // Try spend
                int before = t.money.Value;
                if (t.TrySpendMoney(payEach))
                {
                    owner.AddMoney(payEach);
                    sb.AppendLine($"{t.playerName.Value} roll={roll} (odd). Pays ${payEach} to {owner.playerName.Value}.");
                }
                else
                {
                    // take what they have
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

    /* =======================================================================================
       MODE 2) COMPETITION
       - Participants pay entryFee → pot.
       - They roll d6 once (via CompetitionUI, if available; else you can add a timeout/autoroll).
       - Highest roll wins the pot. If tie + tieSplitPot → split equally (floor).
       ======================================================================================= */

    private GameEventSO _compEvent;
    private readonly HashSet<int> _compParticipants = new();
    private readonly Dictionary<int, int> _compRolls = new(); // connId -> roll
    private int _compPot;

    [Server]
    private void StartCompetition(GameEventSO e)
    {
        _compEvent = e;
        _compParticipants.Clear();
        _compRolls.Clear();
        _compPot = 0;

        if (GameManager.Instance == null) { ResumeAfterEvent(); return; }

        // For now: all players participate
        foreach (var p in GameManager.Instance.Players)
        {
            if (p?.Owner == null) continue;
            _compParticipants.Add(p.Owner.ClientId);

            if (e.entryFee > 0)
            {
                if (!p.TrySpendMoney(e.entryFee))
                {
                    // take whatever they have
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

        // Ask each participant to roll (via optional CompetitionUI)
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
        // If UI missing, you can implement an autoroll fallback on a timeout if you like.
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
        if (_compEvent == null)
        {
            CloseCompetitionUI();
            ResumeAfterEvent();
            return;
        }

        // Find max roll
        int maxRoll = 0;
        foreach (var r in _compRolls.Values)
            if (r > maxRoll) maxRoll = r;

        // Find winners
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

        int each = (_compEvent.tieSplitPot && winners.Count > 0)
            ? Mathf.FloorToInt(_compPot / winners.Count)
            : _compPot;

        if (winners.Count > 0)
        {
            if (_compEvent.tieSplitPot)
                foreach (var w in winners) w.AddMoney(each);
            else
                winners[0].AddMoney(each);
        }

        // Show summary
        string summary = $"{_compEvent.eventName}\nResult: Max Roll={maxRoll}, Winners={winners.Count}, Pot=${_compPot}.";
        foreach (var c in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(c, summary, false);

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

    /* =======================================================================================
       MODE 3) TARGET SELECT
       - The current pawn chooses a target from eligible players.
       - Apply event effects only to that target.
       - If no UI → auto-pick random target on server.
       ======================================================================================= */

    private GameEventSO _tsEvent;
    private PlayerPawn _tsChooser; // the pawn who picks the target

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
        // serializedNames = "name1|name2|name3"
        if (TargetSelectUI.Instance != null)
        {
            TargetSelectUI.Instance.Show(serializedNames);
        }
        else
        {
            // If the UI is not present, auto-pick on client (or do nothing and rely on timeout).
            var names = serializedNames.Split('|');
            int idx = Random.Range(0, names.Length);
            CmdSubmitTargetSelect(names[idx]); // fallback
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdSubmitTargetSelect(string targetPlayerName, NetworkConnection conn = null)
    {
        if (_tsEvent == null || _tsEvent.mode != EventMode.TargetSelect) return;

        var chooser = _tsChooser;
        if (chooser == null) { _tsEvent = null; ResumeAfterEvent(); return; }
        if (conn == null || chooser.Owner != conn) return; // only the chooser can confirm

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
        // Apply event effects ONLY to 'target'
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

            // restrict to opponents
            if (e.restrictToOpponents && p == chooser)
                continue;

            // require owner of the factory named in effects (first Factory effect with targetName)
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
}
