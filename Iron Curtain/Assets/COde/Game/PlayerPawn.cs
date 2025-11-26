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

        // --- Name from lobby if possible ---
        if (Owner?.FirstObject != null &&
            Owner.FirstObject.TryGetComponent(out NetworkLobbyPlayer lobby))
        {
            playerName.Value = string.IsNullOrWhiteSpace(lobby.playerName.Value)
                ? $"P{Owner.ClientId}"
                : lobby.playerName.Value.Trim();
        }
        else
        {
            playerName.Value = $"P{Owner?.ClientId ?? -1}";
        }

        // --- Slot / character index from ColorLockManager (authoritative) ---
        int slotFromLock = -1;
        if (ColorLockManager.Instance != null &&
            ColorLockManager.Instance.TryGetSlotForCid(Owner.ClientId, out slotFromLock))
        {
            colorIndex.Value = PlayerColors.ClampOrUnset(slotFromLock);
        }
        else
        {
            // Fallback: PlayerPrefs or default 0 if for some reason the lock isn't ready
            int pref = PlayerPrefs.GetInt("ColorIndex", 0);
            colorIndex.Value = PlayerColors.ClampOrUnset(pref);
        }

        Debug.Log($"[PlayerPawn] OnStartServer cid={Owner.ClientId} slotFromLock={slotFromLock} finalColorIndex={colorIndex.Value}");
    }


   public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyColor(colorIndex.Value);
        colorIndex.OnChange += OnColorChanged;
        money.OnChange += OnMoneyChanged;
        bailoutMarks.OnChange += OnBailoutMarksChanged;

        if (IsOwner && TurnUI.Instance != null)
            TurnUI.Instance.BindPawn(this);
        StartCoroutine(AutoBindInfoPanel());
        Debug.Log($"create name tag for pawn {this}");
         if (PawnNameTagManager.Instance != null)
            PawnNameTagManager.Instance.CreateForPawn(this);
    }

    private void OnMoneyChanged(int oldValue, int newValue, bool asServer)
    {
        if (infoPanel == null)
        {
            // lazy bind if HUD appeared late
            TryEnsureInfoPanel();
            if (infoPanel == null)
            {
                Debug.LogWarning("[PlayerPawn] Money changed, but infoPanel still null. Waiting for HUD...");
                return;
            }
        }

        infoPanel.UpdateMoney(newValue);
        Debug.Log($"[Money] {playerName.Value}: ${oldValue} -> ${newValue}");
        int delta = newValue - oldValue;

        if (delta != 0)
        {
            var deltaCtrl = infoPanel.GetComponentInChildren<MoneyDeltaController>(true);
            if (deltaCtrl != null) deltaCtrl.ShowDelta(delta);
        }
    }

    private IEnumerator AutoBindInfoPanel()
    {
        float t = 5f;
        while (t > 0f && infoPanel == null)
        {
            TryEnsureInfoPanel();
            if (infoPanel != null)
            {
                infoPanel.SetInfo(
                    string.IsNullOrWhiteSpace(playerName.Value) ? $"P{Owner?.ClientId ?? -1}" : playerName.Value,
                    money.Value);

                infoPanel.UpdateBailoutMarks(bailoutMarks.Value);
                if (Owner != null) infoPanel.SetOwnerCid(Owner.ClientId);

                yield break;
            }
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
    }


    private void TryEnsureInfoPanel()
    {
        if (GameHUD.Instance == null) return;

        if (infoPanel == null && !string.IsNullOrWhiteSpace(playerName.Value))
            infoPanel = GameHUD.Instance.FindPanelByName(playerName.Value);

        if (infoPanel == null && Owner != null && Owner.IsActive)
        {
            var byCid = GameHUD.Instance.FindPanelByCid(Owner.ClientId);
            if (byCid != null)
                infoPanel = byCid;
        }

        if (infoPanel != null && Owner != null)
            infoPanel.SetOwnerCid(Owner.ClientId);
    }
     private void OnColorChanged(int oldVal, int newVal, bool asServer)
    {
        ApplyColor(newVal);
    }

    public void ApplyColor(int idx)
    {
        if (_sr == null)
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr == null)
            {
                Debug.LogWarning("[PlayerPawn] No SpriteRenderer found for ApplyColor on " + gameObject.name);
                return;
            }
        }

        // 1) Set sprite from CharacterLibrary
        var lib = CharacterLibrary.Instance;
        if (lib != null)
        {
            var sprite = lib.GetSprite(idx);
            if (sprite != null)
            {
                _sr.sprite = sprite;
            }
        }

        // 2) Optional tint – you can change this to Color.white if you don't want per-player color tint anymore
        _sr.color = PlayerColors.GetOr(Color.white, idx);
    }

    public void ApplyColorIndex(int idx)
    {
        ApplyColor(idx); // immediate visual for local UX

        if (IsServerInitialized)
            colorIndex.Value = PlayerColors.ClampOrUnset(idx);
        else if (IsOwner)
            CmdSetColorIndex(idx);   // only owner will call
        // non-owners do nothing here
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void CmdSetColorIndex(int idx, FishNet.Connection.NetworkConnection conn = null)
    {
        if (conn != Owner) return; // reject non-owners
        colorIndex.Value = PlayerColors.ClampOrUnset(idx);
    }

    [ServerRpc]
    public void CmdSetColorIndex(int idx)
    {
        colorIndex.Value = PlayerColors.ClampOrUnset(idx);
    }

    public override void OnStopClient()
    {
        money.OnChange -= OnMoneyChanged;
        bailoutMarks.OnChange -= OnBailoutMarksChanged;
        if (PawnNameTagManager.Instance != null)
            PawnNameTagManager.Instance.RemoveForPawn(this);
        base.OnStopClient();
    }

    private void OnBailoutMarksChanged(int oldValue, int newValue, bool asServer)
    {
        if (infoPanel == null)
        {
            TryEnsureInfoPanel();
            if (infoPanel == null)
            {
                Debug.LogWarning("[PlayerPawn] BailoutMarks changed, but infoPanel is null.");
                return;
            }
        }

        infoPanel.UpdateBailoutMarks(newValue);
    }



    /* ---------- Money ---------- */
    [Server]
    public void AddMoney(int amount)
    {
        money.Value += amount;

        if (amount > 0)
        {
            statTotalIncome += amount;
        }
        else if (amount < 0)
        {
            int lost = -amount;
            statTotalSpending += lost;
        }
    }

    [Server]
    public bool TrySpendMoney(int amount)
    {
        if (amount <= 0) return true;
        if (money.Value < amount) return false;

        money.Value -= amount;
        statTotalSpending += amount;
        return true;
    }


    [TargetRpc]
    private void TargetNotifyBailout(NetworkConnection conn, int marks, int currentMoney)
    {
        infoPanel?.UpdateBailoutMarks(marks);
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

        int d1 = Random.Range(1, 7); 
        int d2 = Random.Range(1, 7);
        int total = d1 + d2;
        lastRoll.Value = total;
        statTotalRollSum += d1 + d2;
        if (d1 == 6) statSixRolled++;
        if (d2 == 6) statSixRolled++;
        EventManager.Instance?.OnServerPlayerRolled(this, total);

        TargetShowDiceAndMove(Owner, d1, d2, total);
    }


    [TargetRpc]
    public void TargetShowDiceAndMove(NetworkConnection conn, int d1, int d2, int totalSteps)
    {
        if (DiceUI.Instance != null)
        {
            DiceUI.Instance.ShowDiceRollingWithCallback(d1, d2, () =>
            {
                CmdStartMovement(totalSteps); 
            });
        }
        else
        {
            CmdStartMovement(totalSteps);
        }
    }

    [ServerRpc]
    private void CmdStartMovement(int steps)
    {
        if (!IsServerInitialized) return;
        TurnManager.Instance?.ServerEnterMovingPhase();
        StopAllCoroutines();
        StartCoroutine(ServerMoveStepByStep(steps));
       
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
        StartCoroutine(CoResyncButtons());
    }

    private IEnumerator CoResyncButtons()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        if (IsOwner)
            CmdRequestButtonResync();
    }

    [ServerRpc]
    private void CmdRequestButtonResync(FishNet.Connection.NetworkConnection caller = null)
    {
        if (caller != Owner) return;
        if (TurnManager.Instance.IsCurrentPawn(this))
        {
            if (TurnManager.Instance.IsPhase(TurnPhase.Rolling))
                TargetEnableRoll(Owner, true);
            else
                TargetEnableRoll(Owner, false);

            TargetEnableEndTurn(Owner, TurnManager.Instance.IsPhase(TurnPhase.EndReady));
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
    [Server] // ← IMPORTANT
    private IEnumerator ServerMoveStepByStep(int steps)
    {
        int tileCount = GameManager.Instance.TileCount;

        for (int i = 1; i <= steps; i++)
        {
            
            int nextTile = (currentTile + 1) % tileCount;
            Vector3 targetPos = GameManager.Instance.GetTilePosition(nextTile);
            TileHighlighter.Instance?.FlashPassAt(targetPos, 1f);

            // move the authoritative transform (server only)
            while (Vector3.Distance(transform.position, targetPos) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
                yield return null; // server ticks; NetworkTransform replicates to clients
            }

            transform.position = targetPos;
            currentTile = nextTile;

            yield return new WaitForSeconds(0.1f);
        }
        TileHighlighter.Instance?.FlashLandAt(GameManager.Instance.GetTilePosition(currentTile), 1f);

        // now that server is at the final tile, process tile logic
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
            EventManager.Instance.TriggerTileEvent(this);
            return;
        }

        if (data.tileType == TileType.Investment && data.owner == null)
        {
            TurnManager.Instance.ServerBeginTileAction(this);
            TurnManager.Instance.ServerEnterInvestmentPhase();
            int price = MarketManager.Instance.ComputeEffectivePrice(data);
            TargetShowInvestmentUI(Owner, currentTile, data.companyName, price, true);
            return;
        }

        if (data.tileType == TileType.Investment && data.owner != null && data.owner != this)
        {
            // A) STEAL ON LANDING
            if (data.enableStealOnLanding)
            {
                TurnManager.Instance.ServerBeginTileAction(this);

                var ownerPawn = data.owner;
                int ownerMoney = Mathf.Max(0, ownerPawn.money.Value);
                int pct = Mathf.Clamp(data.stealPercentOfOwnerMoney, 0, 100);
                int stealAmt = Mathf.RoundToInt(ownerMoney * (pct / 100f));
                if (data.stealFlatMin > 0) stealAmt = Mathf.Max(stealAmt, data.stealFlatMin);
                if (data.stealFlatMax > 0) stealAmt = Mathf.Min(stealAmt, data.stealFlatMax);
                stealAmt = Mathf.Max(0, stealAmt);

                if (stealAmt > 0)
                {
                    ownerPawn.money.Value -= stealAmt;
                    this.money.Value += stealAmt;
                    Notifier.Instance?.ToastAll(
                        $"{playerName.Value} stole ${stealAmt}M from {ownerPawn.playerName.Value} at {data.companyName}!",
                        ToastKind.Warning);
                }

                TargetShowTilePopupAndWait(Owner,
                    $"You raided {data.companyName} and took ${stealAmt}M from {ownerPawn.playerName.Value}.");
                return;
            }
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
                    if (paid > 0)
                    statTaxPaid += paid;

    
                    TargetShowTilePopupAndWait(Owner, $"ภาษี: จ่าย ${paid}M.");
                    return;
                }

            case TileType.Bonus:
                {
                    TurnManager.Instance.ServerBeginTileAction(this);

                    int bonus = Mathf.Max(0, data.bonusAmount);
                    if (bonus > 0)
                    {
                        AddMoney(bonus);
                        statBonusReceived += bonus;  
                    }

                    TargetShowTilePopupAndWait(Owner, $"โบนัส: คุณได้รับ ${bonus}M.");
                    return;
                }


            case TileType.Jail:
                {
              
                    TurnManager.Instance.ServerBeginTileAction(this);

                    ServerSetJail(2);

                    TargetShowTilePopupAndWait(Owner, $"คุณถูกจองจำเป็นเวลา {jailTurnsLeft.Value} รอบ");
                    return;
                }
        }

        TurnManager.Instance.ServerOnTileActionComplete(this);
    }

    /* ---------- Tile popups ---------- */

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
            EventUI.Instance.SetSideeventReadyCallback(() =>
            {
                CmdTileActionComplete();
            });
        }
        else
        {
            CmdTileActionComplete();
        }
    }
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
         StartCoroutine(WaitAndSetTurnOrder(turnIndex));
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
        statJailVisits += 1; 
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
        statBailouts += 1;          // ★ NEW

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
        public float percent;
        public float multiplier;
    }

    // Client-side cached snapshot we render from (NOT authoritative)
    private readonly List<PortfolioItemDTO> _clientPortfolio = new();
    public System.Action OnClientPortfolioChanged;

    [TargetRpc]
    private void TargetReceivePortfolio(NetworkConnection conn, string[] names, float[] percents, float[] multipliers)
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
    private void RpcReceivePortfolioBroadcast(string[] names, float[] percents, float[] multipliers)
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
        var perc = new List<float>();
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
        var perc = new List<float>();
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

    [ObserversRpc]
    private void RpcShowDice(int d1, int d2)
    {
        if (DiceUI.Instance != null)
        {
            DiceUI.Instance.ShowDiceRollingWithCallback(d1, d2, () =>
            {
                Debug.Log("Dice animation finished on client.");
            });
        }
    }

    // ================= END-GAME STATS =================
    [Header("End Game Stats")]
public int statTotalIncome;       
public int statTotalSpending;     
public int statTaxPaid;          
public int statEventLoss;      
public int statBonusReceived;  
public int statBailouts;       
public int statJailVisits;     
    
public int statProposalsSent;   
public int statProposalsAccepted;  
public int statTakeoversWon;     

public int statTotalRollSum;     
public int statSixRolled;        
    
   
       


}