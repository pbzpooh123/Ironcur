using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using System.Threading.Tasks;

public class PlayerPawn : NetworkBehaviour
{
    public float moveSpeed = 4f;
    private int currentTile = 0;

    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<int> lastRoll = new();
    public readonly SyncVar<int> money = new();
    public readonly SyncVar<int> bailoutMarks = new();

    public Dictionary<string, ShareRecord> factoryPortfolio = new();

    public bool isMyTurn = false;
    public PlayerInfoPanel infoPanel;

    public readonly SyncVar<int> jailTurnsLeft = new();
    private SpriteRenderer _sr;
    public readonly SyncVar<int> colorIndex = new();

    private void Awake()
    {
        // Find a SpriteRenderer on this GO or children
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
    }
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (Owner != null)
            GiveOwnership(Owner);

        if (money.Value == 0)
            money.Value = 1000;

        if (Owner?.FirstObject != null &&
        Owner.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobby))
        {
            colorIndex.Value = PlayerColors.Clamp(lobby.colorIndex.Value);
        }
        if (colorIndex.Value == 0)
        {
            colorIndex.Value = -1;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyColor(colorIndex.Value);
        colorIndex.OnChange += OnColorChanged;
        money.OnChange += OnMoneyChanged;
        StartCoroutine(AutoBindInfoPanel());
    }

    private IEnumerator AutoBindInfoPanel()
    {
        float t = 2f;
        while (t > 0f && infoPanel == null)
        {
            if (GameHUD.Instance != null && !string.IsNullOrEmpty(playerName.Value))
            {
                var maybe = GameHUD.Instance.FindPanelByName(playerName.Value);
                if (maybe != null)
                {
                    infoPanel = maybe;
                    infoPanel.SetInfo(playerName.Value, money.Value);
                    break;
                }
            }
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
    }
     private void OnColorChanged(int oldVal, int newVal, bool asServer)
    {
        ApplyColor(newVal);
    }

    private void ApplyColor(int idx)
    {
        if (_sr == null)
        {
            // try lazily once more
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr == null) return;
        }
        _sr.color = PlayerColors.Get(idx);
    }

    public void ApplyColorIndex(int idx)
    {
        
        if (IsServerInitialized)
            colorIndex.Value = PlayerColors.Clamp(idx);
        else
            CmdSetColorIndex(idx);
    
        ApplyColor(idx);
    }

    [ServerRpc]
    public void CmdSetColorIndex(int idx)
    {
        colorIndex.Value = PlayerColors.Clamp(idx);
    }

    public override void OnStopClient()
    {
        money.OnChange -= OnMoneyChanged;
        base.OnStopClient();
    }

    private void OnMoneyChanged(int oldValue, int newValue, bool asServer)
    {
        infoPanel?.UpdateMoney(newValue);
        Debug.Log($"[Money] {playerName.Value}: ${oldValue} -> ${newValue}");
        int delta = newValue - oldValue;
        if (delta != 0 && infoPanel != null)
        {
            var deltaCtrl = infoPanel.GetComponentInChildren<MoneyDeltaController>(true);
            if (deltaCtrl != null)
            {
                deltaCtrl.ShowDelta(delta);
            }

        }
    }

    /* ---------- Money ---------- */
    [Server] public void AddMoney(int amount) => money.Value += amount;

    [Server]
    public bool TrySpendMoney(int amount)
    {
        if (money.Value < amount) return false;
        money.Value -= amount;
        return true;
    }

    [Server]
    private void CheckBailout()
    {
        if (money.Value < 0)
        {
            bailoutMarks.Value += 1;
            money.Value = 100;
            TargetNotifyBailout(Owner, bailoutMarks.Value, money.Value);
        }
    }

    [TargetRpc]
    private void TargetNotifyBailout(NetworkConnection conn, int marks, int currentMoney)
    {
        Debug.Log($"Bailout! You now have ${currentMoney} and {marks} bailout mark(s).");
    }

    /* ---------- Turn UI ---------- */
    public void OnRollDiceButton()
    {
        if (!IsOwner || !isMyTurn) return;
        CmdRollDiceAndMove();
    }

    public void OnEndTurnButton()
    {
        if (!IsOwner || !isMyTurn) return;
        CmdEndTurn();
    }

    [ServerRpc]
    public void CmdRollDiceAndMove()
    {
        if (!TurnManager.Instance.CanRoll(this)) return;

        int roll = Random.Range(2, 13);
        lastRoll.Value = roll;

        // Rule hooks (e.g., Odd Fine)
        EventManager.Instance?.OnServerPlayerRolled(this, roll);

        RpcMoveSteps(roll);
    }

    [ServerRpc]
    public void CmdEndTurn()
    {
        if (!TurnManager.Instance.CanEndTurn(this))
        {
            Debug.LogWarning("[Turn] EndTurn blocked; not in EndReady phase.");
            return;
        }
        TurnManager.Instance.EndTurn();
    }

    [TargetRpc]
    public void TargetStartTurn(NetworkConnection conn)
    {
        isMyTurn = true;
        var ui = GameObject.FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.BindPawn(this);
            ui.SetRollInteractable(false);
            ui.SetEndTurnInteractable(false);
        }
    }

    [TargetRpc]
    public void TargetEndTurn(NetworkConnection conn)
    {
        isMyTurn = false;
        var ui = GameObject.FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetRollInteractable(false);
            ui.SetEndTurnInteractable(false);
        }
    }

    [TargetRpc]
    public void TargetEnableEndTurn(NetworkConnection conn, bool enable)
    {
        var ui = GameObject.FindObjectOfType<TurnUI>();
        if (ui != null) ui.SetEndTurnInteractable(enable);
    }

    [TargetRpc]
    public void TargetEnableRoll(NetworkConnection conn, bool enable)
    {
        var ui = GameObject.FindObjectOfType<TurnUI>();
        if (ui != null) ui.SetRollInteractable(enable);
    }

    /* ---------- Movement ---------- */
    [ObserversRpc]
    private void RpcMoveSteps(int steps)
    {
        StopAllCoroutines();
        StartCoroutine(MoveStepByStep(steps));
    }

    private IEnumerator MoveStepByStep(int steps)
    {
        int tileCount = GameManager.Instance.TileCount;
        for (int i = 1; i <= steps; i++)
        {
            int nextTile = (currentTile + 1) % tileCount;
            Vector3 targetPos = GameManager.Instance.GetTilePosition(nextTile);

            while (Vector3.Distance(transform.position, targetPos) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetPos;
            currentTile = nextTile;
            yield return new WaitForSeconds(0.1f);
        }

        if (IsServerInitialized)
            HandleTileLogic();
    }

    [Server]
    private void HandleTileLogic()
    {
        var data = GameManager.Instance.GetTileData(currentTile);
        if (data == null)
        {
            TurnManager.Instance.ServerOnTileActionComplete(this);
            return;
        }

        if (data.tileType == TileType.Event)
        {
            // Tile Event uses EventManager (which brackets & acks internally)
            EventManager.Instance.TriggerTileEvent(this);
            return;
        }

        if (data.tileType == TileType.Investment && data.owner == null)
        {
            // Investment UI already gates completion via its own Ready → Cmd
            TargetShowInvestmentUI(Owner, currentTile, data.companyName, data.companyCost, true);
            return;
        }

        switch (data.tileType)
        {
            case TileType.Tax:
                {
                    // <<< CHANGED: BRACKET + WAIT FOR READY >>>
                    TurnManager.Instance.ServerBeginTileAction(this);

                    int percent = Mathf.Clamp(data.taxPercent, 0, 100);
                    int percentPart = Mathf.FloorToInt(money.Value * (percent / 100f));
                    int totalOwed = Mathf.Max(0, data.taxFlat + percentPart);

                    int paid = PayWithOptionalBailouts(totalOwed, allowBailout: true, maxBailouts: 5);

                    // Show panel and wait; Ready will call CmdTileActionComplete()
                    TargetShowTilePopupAndWait(Owner, $"TAX: Owed ${totalOwed}M. Paid ${paid}M.");
                    return;
                }

            case TileType.Bonus:
                {
                    // <<< CHANGED: BRACKET + WAIT FOR READY >>>
                    TurnManager.Instance.ServerBeginTileAction(this);

                    int bonus = Mathf.Max(0, data.bonusAmount);
                    if (bonus > 0) AddMoney(bonus);

                    TargetShowTilePopupAndWait(Owner, $"BONUS: You received ${bonus}M.");
                    return;
                }

            case TileType.Jail:
                {
                    // <<< CHANGED: BRACKET + WAIT FOR READY >>>
                    TurnManager.Instance.ServerBeginTileAction(this);

                    ServerSetJail(2); // e.g., 2 jailed turns

                    TargetShowTilePopupAndWait(Owner, $"You are jailed for {jailTurnsLeft.Value} turn(s).");
                    return;
                }
        }

        // Normal tile (no UI to wait on)
        TurnManager.Instance.ServerOnTileActionComplete(this);
    }

    /* ---------- Tile popups ---------- */

    // Investment popup (already gated elsewhere)
    [TargetRpc]
    private void TargetShowInvestmentUI(NetworkConnection conn, int tileIndex, string companyName, int cost, bool isCompany)
    {
        InvestmentUI.Instance.ShowOptions(this, tileIndex, companyName, cost, isCompany);
    }

    // <<< NEW: show side popup that MUST be acknowledged; Ready -> CmdTileActionComplete() >>>
    [TargetRpc]
    private void TargetShowTilePopupAndWait(NetworkConnection conn, string msg)
    {
        if (EventUI.Instance != null)
        {
            EventUI.Instance.SideeventShow(msg, true);

            // You add this one-liner Ready callback on the UI side:
            // EventUI has a Ready button that invokes this callback.
            EventUI.Instance.SetSideeventReadyCallback(() =>
            {
                // Client → Server: mark tile complete
                CmdTileActionComplete();
            });
        }
        else
        {
            // Fallback: if UI missing, complete immediately to avoid deadlocks
            CmdTileActionComplete();
        }
    }

    // Legacy “toast” that does NOT wait (kept for non-gated messages)
    [TargetRpc]
    private void TargetShowToast(NetworkConnection conn, string msg)
    {
        if (EventUI.Instance != null)
            EventUI.Instance.SideeventShow(msg, true);
        else
            Debug.Log($"[Toast] {msg}");
    }

    /* ---------- Turn order visuals ---------- */

    [TargetRpc]
    public void TargetSetTurnOrder(NetworkConnection conn, int turnIndex)
    {
        if (infoPanel != null)
        {
            infoPanel.SetTurnOrder(turnIndex + 1);
        }
        else
        {
            StartCoroutine(WaitAndSetTurnOrder(turnIndex));
        }
    }

    private IEnumerator WaitAndSetTurnOrder(int turnIndex)
    {
        float t = 2f;
        while (t > 0f && infoPanel == null)
        {
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (infoPanel != null)
            infoPanel.SetTurnOrder(turnIndex + 1);
    }

    [ObserversRpc(BufferLast = true)]
    public void RpcTeleportTo(Vector3 pos)
    {
        transform.position = pos;
    }

    /* ---------- Ownership helpers ---------- */

    public bool HasCompanies()
    {
        foreach (var kvp in factoryPortfolio)
            if (kvp.Value.sharePercent > 0)
                return true;
        return false;
    }

    public bool HasMajorityCompany()
    {
        foreach (var kvp in factoryPortfolio)
            if (kvp.Value.sharePercent > 60)
                return true;
        return false;
    }

    public List<string> GetOwnedCompanies()
    {
        var owned = new List<string>();
        foreach (var kvp in factoryPortfolio)
            if (kvp.Value.sharePercent > 0)
                owned.Add(kvp.Key);
        return owned;
    }

    /* ---------- Tile action completion ---------- */

    // Existing API that your UI should call when the player presses Ready
    [ServerRpc]
    public void CmdTileActionComplete()
    {
        if (!TurnManager.Instance.IsCurrentPawn(this)) return;
        TurnManager.Instance.ServerOnTileActionComplete(this);
    }

    /* ---------- Jail ---------- */
    [Server]
    public void ServerSetJail(int turns)
    {
        jailTurnsLeft.Value = Mathf.Max(1, turns);
        // (Message shown by the wait-popup path above for Jail tiles)
    }

    [Server]
    public void ServerReleaseFromJail()
    {
        jailTurnsLeft.Value = 0;
        TargetShowToast(Owner, "You are released from jail.");
    }

    /* ---------- Bailout helper ---------- */
    [Server]
    public void ForceBailoutOnce()
    {
        bailoutMarks.Value += 1;
        money.Value += 100; // +$100 bailout
        TargetNotifyBailout(Owner, bailoutMarks.Value, money.Value);
    }

    [Server]
    private int PayWithOptionalBailouts(int amount, bool allowBailout = true, int maxBailouts = 10)
    {
        if (amount <= 0) return 0;

        if (!allowBailout)
        {
            int paid = Mathf.Min(amount, money.Value);
            if (paid > 0) TrySpendMoney(paid);
            return paid;
        }

        int guard = 0;
        while (money.Value < amount && guard < maxBailouts)
        {
            ForceBailoutOnce();
            guard++;
        }

        int finalPay = Mathf.Min(amount, money.Value);
        if (finalPay > 0) TrySpendMoney(finalPay);
        return finalPay;
    }

    [TargetRpc]
    public void TargetSubmitToLeaderboard(NetworkConnection conn, long score, string displayName)
    {
        _ = SubmitMyScoreAsync(score, displayName);
    }

    private async Task SubmitMyScoreAsync(long score, string displayName)
    {
        if (UGSLeaderboard.Instance == null)
        {
            Debug.LogWarning("UGSLeaderboard singleton missing.");
            return;
        }
        try
        {
            await UGSLeaderboard.Instance.SubmitMyScoreAsync(score, displayName);
            Debug.Log($"[UGS] Submitted score {score} for {displayName}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UGS] Submit failed: {ex}");
        }
    }

    [System.Serializable]
    public struct PortfolioItemDTO
    {
        public string company;
        public int percent;
        public float multiplier;
    }

    // Client-side cached snapshot we render from (NOT authoritative)
    private readonly List<PortfolioItemDTO> _clientPortfolio = new();
    public System.Action OnClientPortfolioChanged;

    [TargetRpc]
    private void TargetReceivePortfolio(NetworkConnection conn, string[] names, int[] percents, float[] multipliers)
    {
        _clientPortfolio.Clear();
        for (int i = 0; i < names.Length && i < percents.Length && i < multipliers.Length; i++)
        {
            _clientPortfolio.Add(new PortfolioItemDTO
            {
                company = names[i],
                percent = percents[i],
                multiplier = multipliers[i]
            });
        }
        OnClientPortfolioChanged?.Invoke();
    }

    // Server → ALL observers (use when portfolio changes on server)
    [ObserversRpc(BufferLast = true)]
    private void RpcReceivePortfolioBroadcast(string[] names, int[] percents, float[] multipliers)
    {
        _clientPortfolio.Clear();
        for (int i = 0; i < names.Length && i < percents.Length && i < multipliers.Length; i++)
        {
            _clientPortfolio.Add(new PortfolioItemDTO
            {
                company = names[i],
                percent = percents[i],
                multiplier = multipliers[i]
            });
        }
        OnClientPortfolioChanged?.Invoke();
    }

    // Client asks server to send a fresh snapshot just to this viewer.
    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestPortfolioForViewer(NetworkConnection conn = null)
    {
        if (conn == null) return;

        var names = new List<string>();
        var perc = new List<int>();
        var mult = new List<float>();

        foreach (var kv in factoryPortfolio)
        {
            var rec = kv.Value;
            if (rec.sharePercent <= 0) continue;
            names.Add(kv.Key);
            perc.Add(rec.sharePercent);
            mult.Add(rec.multiplier);
        }

        TargetReceivePortfolio(conn, names.ToArray(), perc.ToArray(), mult.ToArray());
    }

    // Call this on SERVER whenever this pawn’s portfolio changes
    [Server]
    public void ServerBroadcastPortfolio()
    {
        var names = new List<string>();
        var perc = new List<int>();
        var mult = new List<float>();

        foreach (var kv in factoryPortfolio)
        {
            var rec = kv.Value;
            if (rec.sharePercent <= 0) continue;
            names.Add(kv.Key);
            perc.Add(rec.sharePercent);
            mult.Add(rec.multiplier);
        }

        RpcReceivePortfolioBroadcast(names.ToArray(), perc.ToArray(), mult.ToArray());
    }

    // Public read-only snapshot for UI code (client-side)
    public List<PortfolioItemDTO> GetClientPortfolioSnapshot()
    {
        return new List<PortfolioItemDTO>(_clientPortfolio);
    }


}
