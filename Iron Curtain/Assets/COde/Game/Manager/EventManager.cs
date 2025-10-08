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

    private void Awake() => Instance = this;

    #region ================= TILE EVENTS =================
    [Server]
    public void TriggerTileEvent(PlayerPawn pawn)
    {
        if (tileEvents == null || tileEvents.Count == 0 || pawn == null)
        {
            // nothing to do; tile flow completes immediately
            TurnManager.Instance.ServerOnTileActionComplete(pawn);
            return;
        }

        var e = tileEvents[Random.Range(0, tileEvents.Count)];

        _resume = ResumeContext.Tile;
        _resumeTilePawn = pawn;

        waitingForAcks = true;
        playersReady = 0;
        requiredReady = 1; // only the triggering pawn must ack

        TargetShowSideEvent(pawn.Owner, $"{e.eventName}\n\n{e.description}", true);

        // Apply effects to the pawn only
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
            msg = $"Main Event at Round {round}!";
        }

        foreach (var conn in InstanceFinder.ServerManager.Clients.Values)
            TargetShowMainEvent(conn, msg, true);

        _resume = ResumeContext.Main;
        _resumeTilePawn = null;

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
            // Special modes manage their own flow and then call ResumeAfterEvent().
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

            // 3) EXTRA ROLL: mark the flag regardless of targetType
            if (effect.grantExtraRoll)
                extraRoll = true;

            // 4) Per-type logic
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

                default:
                    break;
            }
            if (extraRoll)
            {
                TurnManager.Instance.QueueExtraRoll(pawn, 1);
                Debug.Log($"[Event] Extra roll queued for {pawn.playerName.Value}");
            }
        }

        if (extraRoll)
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
        if (_compEvent == null)
        {
            CloseCompetitionUI();
            ResumeAfterEvent();
            return;
        }

        int maxRoll = 0;
        foreach (var r in _compRolls.Values)
            if (r > maxRoll) maxRoll = r;

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
}
