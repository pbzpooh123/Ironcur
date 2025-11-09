using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;

[System.Serializable]
public class Proposal
{
    public PlayerPawn proposer;   // who made the offer
    public int percent;           // % of shares they want
    public int price;             // how much they offer 
}

[System.Serializable]
public class CompanyRecord
{
    public string companyName;
    public int baseCost;
    public int currentPrice;
    public PlayerPawn owner;
    public string ownerName;

    public float volatilityPct = 5f;
    public float trendPct = 0f;
    public int minPrice = 50;
    public int maxPrice = 99999;

    public float payoutMult = 1f;
    public int payoutMultExpiresAtRound = 0;

    public string sector;

    public Dictionary<PlayerPawn, int> ownershipPercents = new();
    public List<Proposal> proposals = new();

    private readonly HashSet<int> _proposalHintShown = new();
    private readonly HashSet<int> _reviewHintShown = new();

    public CompanyRecord(string name, int cost, PlayerPawn creator, string sectorTag = null)
    {
        companyName  = name;
        baseCost     = cost;
        currentPrice = cost;
        owner        = creator;
        ownerName    = (creator != null) ? creator.playerName.Value : null;
        sector       = sectorTag;
        if (creator != null) ownershipPercents[creator] = 100;
    }

    public int GetOwnership(PlayerPawn pawn)
        => ownershipPercents.TryGetValue(pawn, out int val) ? val : 0;

    public void SetOwnership(PlayerPawn pawn, int newPercent)
        => ownershipPercents[pawn] = Mathf.Clamp(newPercent, 0, 100);

    public PlayerPawn GetMajorityOwner()
    {
        foreach (var kv in ownershipPercents)
            if (kv.Value > 50) return kv.Key;
        return owner;
    }
}

public class MarketManager : NetworkBehaviour
{
    public static MarketManager Instance;

    public Dictionary<string, CompanyRecord> companies = new();

    private readonly Dictionary<PlayerPawn, HashSet<string>> _submittedThisTurn = new();
    private readonly HashSet<int> _proposalHintShown = new();
    private readonly HashSet<int> _reviewHintShown = new();

    [Header("Proposal Policy")]
    [Tooltip("If true, allow accept even when proposer lacks money by triggering bailouts automatically.")]
    public bool AllowDebtOnAccept = true;

    [Tooltip("Cap how many automatic bailouts could be applied for a single accept.")]
    public int MaxAutoBailoutsPerAccept = 10;

    [Header("Market Settings")]
    [Range(0f, 1f)] public float dividendYield = 0.10f;
    private float _globalTrendPct = 0f;
    private float _globalTrendPctPerRound = 0f;

    [Header("Forced Buy (Proposal Phase) - Replicated")]
    public readonly SyncVar<float> forcedBuyPriceMult = new(3f); // e.g. 3x premium
    public readonly SyncVar<bool>  forcedBuyAllowsDebt = new(true);
    public readonly SyncVar<int> forcedBuyMaxBailouts = new(10);
    public readonly SyncVar<float> forcedBuySurchargeOfCash = new(0.20f);

    [Tooltip("Max total % a player can force-buy across ALL companies in one turn.")]
    [Range(1, 100)] public int forcedBuyPercentCapPerTurn = 40;

    [Tooltip("Minimum % per forced-buy action.")]
    [Range(1, 100)] public int forcedBuyMinPercent = 20;

    private readonly Dictionary<PlayerPawn, int> _forcedPctUsedThisTurn = new();

    private void Awake()
    {
        Instance = this;
    }

    /* ================= Turn Hooks ================= */

    [Server]
    public void BeginTurnFor(PlayerPawn pawn)
    {
        if (pawn == null) return;
        _submittedThisTurn[pawn] = new HashSet<string>();
        _forcedPctUsedThisTurn[pawn] = 0;
    }

    [Server]
    public void OnRoundAdvanced(int newRound)
    {
        ServerAdvanceMarketOneStep();
    }

    [Server]
    public bool HasProposalsForOwner(PlayerPawn owner)
    {
        if (owner == null) return false;
        foreach (var c in companies.Values)
        {
            if (c == null) continue;
            if (c.owner == owner && c.proposals != null && c.proposals.Count > 0)
                return true;
        }
        return false;
    }

    /* ================= Buy Company ================= */

    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyCompany(int tileIndex, NetworkConnection conn = null)
    {
        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        var tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        float priceMult = EventManager.Instance ? EventManager.Instance.GetActiveSectorPriceMult(tile.sector) : 1f;
        int effectiveCost = Mathf.RoundToInt(tile.companyCost * priceMult);
        if (!pawn.TrySpendMoney(effectiveCost)) return;

        string key = tile.companyName;
        if (!companies.ContainsKey(key))
        {
            var record = new CompanyRecord(key, effectiveCost, pawn, tile.sector);
            record.currentPrice = effectiveCost;
            companies[key] = record;
            tile.owner = pawn;

            EnsurePortfolioEntry(pawn, key, 100);
            RpcSyncOwnership(key, pawn.playerName.Value, 100);

            // Carry sector aura
            if (EventManager.Instance != null)
            {
                var (active, payoutMult, expiresAt) = EventManager.Instance.GetActiveSectorPayoutAura(tile.sector);
                if (active && !Mathf.Approximately(payoutMult, 1f))
                {
                    record.payoutMult = payoutMult;
                    record.payoutMultExpiresAtRound = expiresAt;
                }
            }

            RpcAddCompany(key, record.baseCost, pawn.playerName.Value, tile.sector,
                          record.currentPrice, record.payoutMult, record.payoutMultExpiresAtRound);
            RpcUpdateTileOwner(key, pawn.playerName.Value, pawn.colorIndex.Value);

            pawn.ServerBroadcastPortfolio();
            RpcRefreshLocalPortfolioUI();
        }
        else
        {
            var rec = companies[key];
            rec.owner = pawn;
            rec.ownerName = pawn.playerName.Value;
            rec.sector = tile.sector;
            rec.ownershipPercents.Clear();
            rec.ownershipPercents[pawn] = 100;
            tile.owner = pawn;

            EnsurePortfolioEntry(pawn, key, 100);
            RpcSyncOwnership(key, pawn.playerName.Value, 100);
            RpcAddCompany(key, rec.baseCost, pawn.playerName.Value, tile.sector,
                          rec.currentPrice, rec.payoutMult, rec.payoutMultExpiresAtRound);
            RpcUpdateTileOwner(key, pawn.playerName.Value, pawn.colorIndex.Value);

            pawn.ServerBroadcastPortfolio();
            RpcRefreshLocalPortfolioUI();
        }
    }

    [ObserversRpc]
    private void RpcAddCompany(string companyName, int baseCost, string ownerName, string sector,
                               int currentPrice, float payoutMult, int payoutMultExpiresAtRound)
    {
        var ownerPawn = FindPawnByName(ownerName);

        if (!companies.TryGetValue(companyName, out var rec))
        {
            rec = new CompanyRecord(companyName, baseCost, ownerPawn, sector);
            companies[companyName] = rec;
        }

        rec.ownerName  = ownerName;
        rec.owner      = ownerPawn;
        rec.sector     = sector;
        rec.baseCost   = baseCost;
        rec.currentPrice = currentPrice;
        rec.payoutMult = payoutMult;
        rec.payoutMultExpiresAtRound = payoutMultExpiresAtRound;

        if (ownerPawn != null)
        {
            rec.ownershipPercents.Clear();
            rec.ownershipPercents[ownerPawn] = 100;
            EnsurePortfolioEntry(ownerPawn, companyName, 100);
        }
        else
        {
            StartCoroutine(RebindOwnerLater(companyName, ownerName));
        }

        ProposalUI.Instance?.Refresh();
        ReviewUI.Instance?.Refresh();
        RpcRefreshLocalPortfolioUI();

        if (ownerPawn != null)
            Notifier.Instance?.ToastAll($"{ownerPawn.playerName.Value} ได้ซื้อ {companyName} แล้ว!", ToastKind.Info);
    }

    [ObserversRpc]
    private void RpcSyncCompanyPayoutAura(string companyName, float payoutMult, int expiresAtRound)
    {
        if (companies.TryGetValue(companyName, out var c))
        {
            c.payoutMult = Mathf.Max(0f, payoutMult);
            c.payoutMultExpiresAtRound = expiresAtRound;
        }
        RpcRefreshLocalPortfolioUI();
    }

    private PlayerPawn FindPawnByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var gm = GameManager.Instance;
        if (gm != null && gm.Players != null)
        {
            var p = gm.Players.Find(pp => pp != null && pp.playerName.Value == name);
            if (p != null) return p;
        }

        foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
            if (p != null && p.playerName.Value == name) return p;

        return null;
    }

    private IEnumerator RebindOwnerLater(string companyName, string ownerName)
    {
        Debug.Log($"[RebindOwnerLater] Waiting for owner {ownerName} for {companyName}");
        float timeout = 5f;
        PlayerPawn found = null;

        while (timeout > 0f && (found = FindPawnByName(ownerName)) == null)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (found == null)
        {
            Debug.LogWarning($"[RebindOwnerLater] Failed to rebind owner for {companyName}");
            yield break;
        }

        if (companies.TryGetValue(companyName, out var rec))
        {
            rec.owner = found;
            rec.ownerName = ownerName;

            if (rec.ownershipPercents.Count == 0 || !rec.ownershipPercents.ContainsKey(found))
            {
                rec.ownershipPercents.Clear();
                rec.ownershipPercents[found] = 100;
                EnsurePortfolioEntry(found, companyName, 100);
            }

            Debug.Log($"[RebindOwnerLater] Rebound owner {ownerName} for {companyName}");

            if (ReviewUI.Instance != null && ReviewUI.Instance.panel.activeSelf)
                ReviewUI.Instance.Refresh();
            if (ProposalUI.Instance != null && ProposalUI.Instance.panel.activeSelf)
                ProposalUI.Instance.Refresh();
        }
    }

    /* ================= Proposal Flow (server-driven UI) ================= */

    [Server]
    public void ShowProposalForPawn(PlayerPawn pawn)
    {
        if (pawn == null || pawn.Owner == null) return;
        if (pawn.jailTurnsLeft.Value > 0) return;

        if (!TurnManager.Instance.IsCurrentPawn(pawn) ||
            !TurnManager.Instance.InProposalPhaseFor(pawn))
        {
            Debug.Log($"[MarketManager] Blocked ShowProposalForPawn for {pawn.playerName.Value} (not in Proposal phase).");
            return;
        }

        if (!_proposalHintShown.Contains(pawn.Owner.ClientId))
        {
            _proposalHintShown.Add(pawn.Owner.ClientId);
            TargetShowProposalHint(pawn.Owner);
        }

        TargetShowProposalUI(pawn.Owner);
    }

    [TargetRpc]
    private void TargetShowProposalUI(NetworkConnection conn)
    {
        if (ProposalUI.Instance == null)
        {
            Debug.LogWarning("[MarketManager] ProposalUI.Instance is NULL on client.");
            return;
        }

        PlayerPawn local = FindLocalOwnedPawn();
        if (local == null)
        {
            Debug.LogWarning("[MarketManager] TargetShowProposalUI: No local-owned pawn found.");
            return;
        }

        ProposalUI.Instance.Show(local);
        Debug.Log("[MarketManager] ProposalUI opened.");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestProposalUI(NetworkConnection conn = null)
    {
        if (conn == null) return;

        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;

        if (!TurnManager.Instance.IsCurrentPawn(pawn) ||
            !TurnManager.Instance.InProposalPhaseFor(pawn))
        {
            Debug.Log($"[MarketManager] Client blocked from opening ProposalUI (not in Proposal phase).");
            return;
        }

        ShowProposalForPawn(pawn);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdNotifyProposalClosed(NetworkConnection caller = null)
    {
        if (caller == null) return;
        var pawn = GameManager.Instance.Players.Find(p => p.Owner == caller);
        if (pawn == null) return;

        TurnManager.Instance.OnPlayerFinishedProposal();
        Debug.Log($"[Market] ProposalUI closed by {pawn.playerName.Value}. EndReady enabled.");
    }

    [Server]
    public void ShowReviewForPawn(PlayerPawn owner)
    {
        if (owner == null || owner.Owner == null) return;
        if (owner.jailTurnsLeft.Value > 0) return;

        if (!_reviewHintShown.Contains(owner.Owner.ClientId))
        {
            _reviewHintShown.Add(owner.Owner.ClientId);
            TargetShowReviewHint(owner.Owner);
        }
        TargetShowReviewUI(owner.Owner);
    }

    [TargetRpc]
    private void TargetShowReviewUI(NetworkConnection conn)
    {
        Debug.Log("[MarketManager] TargetShowReviewUI reached client.");
        StartCoroutine(OpenReviewUILocal());
    }

    private IEnumerator OpenReviewUILocal()
    {
        float t = 3f;
        while (t > 0f && (ReviewUI.Instance == null || FindLocalOwnedPawn() == null))
        {
            t -= Time.deltaTime;
            yield return null;
        }

        var local = FindLocalOwnedPawn();
        if (ReviewUI.Instance == null || local == null)
        {
            Debug.LogWarning("[MarketManager] ReviewUI not found or local pawn missing after wait.");
            yield break;
        }

        ReviewUI.Instance.Show(local);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdNotifyReviewClosed(NetworkConnection caller = null)
    {
        if (caller == null) return;
        var pawn = GameManager.Instance.Players.Find(p => p.Owner == caller);
        if (pawn == null) return;

        TurnManager.Instance.OnOwnerFinishedReview();
        Debug.Log($"[Market] ReviewUI closed by {pawn.playerName.Value}. Proceed to Rolling.");
    }

    private PlayerPawn FindLocalOwnedPawn()
    {
        var pawns = GameObject.FindObjectsOfType<PlayerPawn>();
        foreach (var p in pawns) if (p.IsOwner) return p;
        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdSubmitProposal(string companyName, int percent, int price, NetworkConnection caller = null)
    {
        if (caller == null) return;

        var proposer = GameManager.Instance.Players.Find(p => p.Owner == caller);
        if (proposer == null) return;

        if (!TurnManager.Instance.IsCurrentPawn(proposer) ||
            !TurnManager.Instance.InProposalPhaseFor(proposer))
            return;

        if (EventManager.Instance != null && EventManager.Instance.IsProposalBlockedNow())
            return;

        if (!companies.TryGetValue(companyName, out var company)) return;
        if (company.GetOwnership(proposer) > 50) return;

        price = Mathf.Max(1, price);

        if (!_submittedThisTurn.TryGetValue(proposer, out var set))
            _submittedThisTurn[proposer] = set = new HashSet<string>();
        if (set.Contains(companyName)) return;

        int sellerAvail = company.GetOwnership(company.owner);
        if (sellerAvail <= 0) return;

        int buyerHas = company.GetOwnership(proposer);
        int buyerRoom = Mathf.Max(0, 100 - buyerHas);

        int maxTransfer = Mathf.Min(percent, sellerAvail, buyerRoom);
        if (maxTransfer <= 0) return;

        company.proposals.Add(new Proposal { proposer = proposer, percent = maxTransfer, price = price });
        set.Add(companyName);
        SyncProposalsToClients(companyName);
        Debug.Log($"[Market] {proposer.playerName.Value} proposed {maxTransfer}% of {companyName} for ${price}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdResolveProposal(string companyName, int proposalIndex, bool accepted, NetworkConnection caller = null)
    {
        if (caller == null) return;

        var ownerPawn = GameManager.Instance.Players.Find(p => p.Owner == caller);
        if (ownerPawn == null) return;

        if (!companies.TryGetValue(companyName, out var company)) return;
        if (company.owner != ownerPawn) return;
        if (proposalIndex < 0 || proposalIndex >= company.proposals.Count) return;

        var proposal = company.proposals[proposalIndex];

        ResolveProposal(companyName, proposal, accepted);

        company.proposals.RemoveAt(proposalIndex);
        SyncProposalsToClients(companyName);

        if (ownerPawn.Owner != null)
            TargetRefreshReviewUI(ownerPawn.Owner);
    }

    [Server]
    private void ResolveProposal(string companyName, Proposal proposal, bool accepted)
    {
        if (!companies.TryGetValue(companyName, out var company)) return;
        var prevOwner = company.owner;

        if (!accepted)
        {
            Debug.Log($"[Market] Proposal rejected for {companyName}");
            return;
        }

        int sellerAvail = company.GetOwnership(prevOwner);
        int buyerHas = company.GetOwnership(proposal.proposer);
        int buyerRoom = Mathf.Max(0, 100 - buyerHas);

        int transfer = Mathf.Min(proposal.percent, sellerAvail, buyerRoom);
        if (transfer <= 0)
        {
            Debug.LogWarning($"[Market] Accept aborted: no transferable % (sellerAvail={sellerAvail}, buyerRoom={buyerRoom}).");
            return;
        }

        bool paid;
        if (!AllowDebtOnAccept)
        {
            if (proposal.proposer.money.Value < proposal.price)
            {
                Debug.LogWarning($"[Market] Accept failed: proposer {proposal.proposer.playerName.Value} lacks funds (${proposal.price}).");
                return;
            }
            paid = proposal.proposer.TrySpendMoney(proposal.price);
        }
        else
        {
            paid = TryPayWithBailouts(proposal.proposer, proposal.price);
        }

        if (!paid)
        {
            Debug.LogWarning($"[Market] Accept failed: proposer could not pay ${proposal.price} even after bailouts.");
            return;
        }

        prevOwner.AddMoney(proposal.price);

        company.SetOwnership(prevOwner, sellerAvail - transfer);
        int newBuyerShare = buyerHas + transfer;
        company.SetOwnership(proposal.proposer, newBuyerShare);

        var majority = company.GetMajorityOwner();
        if (majority != prevOwner)
        {
            company.owner = majority;
            company.ownerName = majority.playerName.Value;

            RpcSyncMajorityOwner(companyName, company.ownerName);
            RpcUpdateTileOwner(companyName, company.ownerName, majority.colorIndex.Value);
            Notifier.Instance?.ToastAll($"{majority.playerName.Value} เข้าควบคุม {companyName}!", ToastKind.Success);
        }

        proposal.proposer.ServerBroadcastPortfolio();
        prevOwner.ServerBroadcastPortfolio();
        if (company.owner != prevOwner)
            company.owner.ServerBroadcastPortfolio();

        RpcSyncOwnership(companyName, proposal.proposer.playerName.Value, company.GetOwnership(proposal.proposer));
        RpcSyncOwnership(companyName, prevOwner.playerName.Value, company.GetOwnership(prevOwner));
        if (company.owner != prevOwner)
            RpcSyncOwnership(companyName, company.owner.playerName.Value, company.GetOwnership(company.owner));

        Debug.Log($"[Market] Proposal accepted: {proposal.proposer.playerName.Value} +{transfer}% ({newBuyerShare}% total) of {companyName}");
    }

    [Server]
    private bool TryPayWithBailouts(PlayerPawn p, int amount, int maxBailouts = 10)
    {
        int attempts = 0;
        while (p.money.Value < amount && attempts < maxBailouts)
        {
            p.ForceBailoutOnce();
            attempts++;
        }
        return p.TrySpendMoney(amount);
    }

    [TargetRpc]
    private void TargetRefreshReviewUI(NetworkConnection conn)
    {
        if (ReviewUI.Instance != null && ReviewUI.Instance.panel.activeSelf)
            ReviewUI.Instance.Refresh();
    }

    [ObserversRpc]
    private void RpcSyncOwnership(string companyName, string playerName, int newPercent)
    {
        var pawn = GameManager.Instance.Players.Find(p => p.playerName.Value == playerName);
        if (pawn == null) return;
        if (!pawn.factoryPortfolio.ContainsKey(companyName))
        {
            pawn.factoryPortfolio[companyName] = new ShareRecord
            {
                count = 0,
                roundBought = TurnManager.Instance.roundCount.Value,
                multiplier = 1f,
                multiplierExpiresAt = 0,
                sharePercent = newPercent
            };
        }
        else
        {
            var rec = pawn.factoryPortfolio[companyName];
            rec.sharePercent = newPercent;
            pawn.factoryPortfolio[companyName] = rec;
        }

        if (companies.TryGetValue(companyName, out var comp) && comp != null)
        {
            if (newPercent > 0) comp.SetOwnership(pawn, newPercent);
            else comp.ownershipPercents.Remove(pawn);
        }

        RpcRefreshLocalPortfolioUI();
        Debug.Log($"[ClientSync] {playerName} now has {newPercent}% of {companyName}");
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcUpdateTileOwner(string companyName, string newOwnerName, int colorIndex)
    {
        StartCoroutine(CoApplyTileOwnerVisual(companyName, newOwnerName, colorIndex));
    }

    private IEnumerator CoApplyTileOwnerVisual(string companyName, string newOwnerName, int colorIndex)
    {
        float t = 2f;
        TileData tile = null;
        while (t > 0f && (tile = FindTileByCompanyName(companyName)) == null)
        {
            t -= Time.deltaTime;
            yield return null;
        }
        if (tile == null)
        {
            Debug.LogWarning($"[RpcUpdateTileOwner] Tile '{companyName}' not found on client.");
            yield break;
        }

        var pawn = GameManager.Instance?.Players.Find(p => p.playerName.Value == newOwnerName);
        tile.owner = pawn;

        bool hasOwner = !string.IsNullOrEmpty(newOwnerName);

        if (tile.visuals != null)
        {
            if (!hasOwner) tile.visuals.ShowUnclaimed();
            else tile.visuals.ShowOwnedByColor(colorIndex);
        }
        else
        {
            Debug.LogWarning($"[RpcUpdateTileOwner] No visuals for '{companyName}' on client.");
        }
    }

    /* ================= Payouts ================= */

    [Server]
    public void ProcessPayouts()
    {
        int currentRound = TurnManager.Instance.roundCount.Value;

        var totals = new Dictionary<PlayerPawn, int>();

        foreach (var company in companies.Values)
        {
            if (company == null) continue;

            if (company.payoutMultExpiresAtRound > 0 &&
                currentRound >= company.payoutMultExpiresAtRound)
            {
                company.payoutMult = 1f;
                company.payoutMultExpiresAtRound = 0;
                RpcSyncCompanyPayoutAura(company.companyName, 1f, 0);
            }

            int baseIncome = Mathf.RoundToInt(company.currentPrice * dividendYield);

            foreach (var kv in company.ownershipPercents)
            {
                PlayerPawn pawn = kv.Key;
                int percent = kv.Value;
                if (pawn == null || percent <= 0) continue;

                float payoutF = baseIncome * (percent / 100f);

                if (pawn.factoryPortfolio.TryGetValue(company.companyName, out var rec))
                {
                    if (rec.multiplierExpiresAt > 0 && currentRound >= rec.multiplierExpiresAt)
                    {
                        rec.multiplier = 1f;
                        rec.multiplierExpiresAt = 0;
                        pawn.factoryPortfolio[company.companyName] = rec;
                        pawn.ServerBroadcastPortfolio();
                    }
                    payoutF *= rec.multiplier;
                }

                payoutF *= company.payoutMult;

                int finalPayout = Mathf.RoundToInt(payoutF);
                if (finalPayout == 0) continue;

                if (!totals.ContainsKey(pawn)) totals[pawn] = 0;
                totals[pawn] += finalPayout;
            }
        }

        foreach (var kv in totals)
            kv.Key.AddMoney(kv.Value);
    }

    private TileData FindTileByCompanyName(string companyName)
    {
        if (GameManager.Instance == null || GameManager.Instance.boardTiles == null)
            return null;

        foreach (var go in GameManager.Instance.boardTiles)
        {
            if (go == null) continue;
            var td = go.GetComponent<TileData>();
            if (td != null && td.companyName == companyName)
                return td;
        }
        return null;
    }

    [ObserversRpc]
    private void RpcSyncProposals(string companyName, string[] proposerNames, int[] percents, int[] prices)
    {
        if (!companies.TryGetValue(companyName, out var company)) return;

        company.proposals.Clear();
        for (int i = 0; i < proposerNames.Length; i++)
        {
            var proposerPawn = GameManager.Instance.Players.Find(p => p.playerName.Value == proposerNames[i]);

            company.proposals.Add(new Proposal
            {
                proposer = proposerPawn,
                percent = percents[i],
                price = prices[i]
            });

            if (proposerPawn == null)
                StartCoroutine(RebindProposalProposerLater(companyName, i, proposerNames[i]));
        }

        if (ReviewUI.Instance != null && ReviewUI.Instance.gameObject.activeInHierarchy)
            StartCoroutine(WaitAndRefreshReviewUI());
    }

    private IEnumerator WaitAndRefreshReviewUI()
    {
        yield return null;
        ReviewUI.Instance.Refresh();
    }

    private IEnumerator RebindProposalProposerLater(string companyName, int index, string proposerName)
    {
        PlayerPawn found = null;
        while (found == null)
        {
            found = GameManager.Instance.Players.Find(p => p.playerName.Value == proposerName);
            yield return null;
        }
        if (companies.TryGetValue(companyName, out var comp))
        {
            if (index >= 0 && index < comp.proposals.Count)
                comp.proposals[index].proposer = found;

            ReviewUI.Instance?.Refresh();
        }
    }

    [Server]
    private void SyncProposalsToClients(string companyName)
    {
        if (!companies.TryGetValue(companyName, out var c)) return;

        int n = c.proposals.Count;
        var names = new string[n];
        var perc = new int[n];
        var price = new int[n];

        for (int i = 0; i < n; i++)
        {
            names[i] = c.proposals[i].proposer != null ? c.proposals[i].proposer.playerName.Value : "";
            perc[i] = c.proposals[i].percent;
            price[i] = c.proposals[i].price;
        }

        RpcSyncProposals(companyName, names, perc, price);
    }

    [Server]
    public bool ServerHasAnyCompany(PlayerPawn pawn)
    {
        if (pawn == null) return false;
        foreach (var c in companies.Values)
        {
            if (c == null) continue;
            if (c.GetOwnership(pawn) > 0)
                return true;
        }
        return false;
    }

    [Server]
    private void TransferOwnershipFull(string companyName, PlayerPawn newOwner)
    {
        if (!companies.TryGetValue(companyName, out var c)) return;
        var oldOwner = c.owner;
        if (oldOwner == newOwner) return;

        c.owner = newOwner;
        c.ownerName = newOwner.playerName.Value;

        c.ownershipPercents.Clear();
        c.ownershipPercents[newOwner] = 100;

        oldOwner?.factoryPortfolio.Remove(companyName);

        if (!newOwner.factoryPortfolio.ContainsKey(companyName))
            newOwner.factoryPortfolio[companyName] = new ShareRecord
            {
                sharePercent = 100,
                multiplier = 1f,
                multiplierExpiresAt = 0,
                roundBought = TurnManager.Instance.roundCount.Value
            };

        RpcSyncMajorityOwner(companyName, c.ownerName);
        RpcSyncOwnership(companyName, newOwner.playerName.Value, 100);
        RpcUpdateTileOwner(companyName, c.ownerName, newOwner.colorIndex.Value);
    }

    [Server]
    public bool HasSubmittedThisTurn(PlayerPawn pawn, string companyName)
    {
        if (pawn == null) return false;
        return _submittedThisTurn.TryGetValue(pawn, out var set) && set.Contains(companyName);
    }

    [Server]
    public bool TryGetMajorityOwner(string companyName, out PlayerPawn majorityOwner)
    {
        majorityOwner = null;
        if (!companies.TryGetValue(companyName, out var comp) || comp == null) return false;

        int best = 0;
        PlayerPawn bestPawn = null;
        foreach (var kv in comp.ownershipPercents)
        {
            if (kv.Key == null) continue;
            if (kv.Value > best)
            {
                best = kv.Value;
                bestPawn = kv.Key;
            }
        }

        if (bestPawn != null && best > 50)
        {
            majorityOwner = bestPawn;
            return true;
        }
        return false;
    }

    // ======== MATCH END HOOK ========
    [Server]
    public void OnMatchEnded()
    {
        _submittedThisTurn.Clear();
        _proposalHintShown.Clear();
        _reviewHintShown.Clear();
        _globalTrendPctPerRound = 0f;
        RpcCloseMarketUI();
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcCloseMarketUI()
    {
        if (ProposalUI.Instance != null)
        {
            if (ProposalUI.Instance.panel != null)
                ProposalUI.Instance.Hide();
        }

        if (ReviewUI.Instance != null)
        {
            if (ReviewUI.Instance.panel != null)
                ReviewUI.Instance.Hide();
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcRefreshLocalPortfolioUI()
    {
        if (PortfolioUI.Instance == null) return;

        PlayerPawn local = null;
        foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
            if (p != null && p.IsOwner)
            {
                local = p;
                break;
            }

        if (local == null) return;
        if (PortfolioUI.Instance.panel != null && PortfolioUI.Instance.panel.activeInHierarchy)
            PortfolioUI.Instance.RefreshFromSnapshot();
    }

    private void EnsurePortfolioEntry(PlayerPawn pawn, string company, int percent)
    {
        if (pawn == null) return;

        if (!pawn.factoryPortfolio.ContainsKey(company))
        {
            pawn.factoryPortfolio[company] = new ShareRecord
            {
                count = 0,
                roundBought = TurnManager.Instance.roundCount.Value,
                multiplier = 1f,
                multiplierExpiresAt = 0,
                sharePercent = percent
            };
        }
        else
        {
            var rec = pawn.factoryPortfolio[company];
            rec.sharePercent = percent;
            pawn.factoryPortfolio[company] = rec;
        }
    }

    [Server]
    private void ServerAdvanceMarketOneStep()
    {
        foreach (var kv in companies)
        {
            var c = kv.Value;
            if (c == null) continue;

            if (c.payoutMultExpiresAtRound > 0 &&
                TurnManager.Instance.roundCount.Value >= c.payoutMultExpiresAtRound)
            {
                c.payoutMult = 1f;
                c.payoutMultExpiresAtRound = 0;
            }

            float r = Random.Range(-c.volatilityPct, c.volatilityPct);
            float stepPct = c.trendPct + _globalTrendPct + r;

            int newPrice = Mathf.RoundToInt(c.currentPrice * (1f + (stepPct / 100f)));
            newPrice = Mathf.Clamp(newPrice, c.minPrice, c.maxPrice);

            if (newPrice != c.currentPrice)
            {
                c.currentPrice = newPrice;
                RpcSyncMarketPrice(c.companyName, newPrice);
            }
        }
    }

    [ObserversRpc]
    private void RpcSyncMarketPrice(string companyName, int newPrice)
    {
        if (companies.TryGetValue(companyName, out var c))
            c.currentPrice = newPrice;

        if (PortfolioUI.Instance != null && PortfolioUI.Instance.panel != null &&
            PortfolioUI.Instance.panel.activeInHierarchy)
            PortfolioUI.Instance.RefreshFromSnapshot();
    }

    /* ---------- PUBLIC EVENT HOOKS ---------- */

    [Server]
    public void ServerBumpCompanyPrice(string company, float deltaPercent)
    {
        if (!companies.TryGetValue(company, out var c) || c == null) return;
        int np = Mathf.RoundToInt(c.currentPrice * (1f + deltaPercent / 100f));
        c.currentPrice = Mathf.Clamp(np, c.minPrice, c.maxPrice);
        RpcSyncMarketPrice(company, c.currentPrice);
    }

    [Server]
    public void ServerBumpAllPrices(float deltaPercent)
    {
        foreach (var kv in companies)
            ServerBumpCompanyPrice(kv.Key, deltaPercent);
    }

    [Server]
    public void ServerSetCompanyVolatility(string company, float newVolPct)
    {
        if (!companies.TryGetValue(company, out var c) || c == null) return;
        c.volatilityPct = Mathf.Max(0f, newVolPct);
    }

    [Server]
    public void ServerSetGlobalTrend(float percentBias)
    {
        _globalTrendPct = percentBias;
    }

    [Server]
    public void ServerBoostCompanyPayouts(string company, float multiplier, int durationRounds)
    {
        if (!companies.TryGetValue(company, out var c) || c == null) return;
        c.payoutMult = Mathf.Max(0f, multiplier);
        c.payoutMultExpiresAtRound = TurnManager.Instance.roundCount.Value + Mathf.Max(1, durationRounds);

        RpcSyncCompanyPayoutAura(company, c.payoutMult, c.payoutMultExpiresAtRound);
    }

    [Server]
    public void ServerBoostAllCompaniesOwnedBy(PlayerPawn pawn, float priceDeltaPercent, float payoutMultiplier, int durationRounds)
    {
        if (pawn == null) return;
        foreach (var kv in companies)
        {
            var c = kv.Value;
            if (c == null) continue;

            if (c.ownershipPercents.TryGetValue(pawn, out int pct) && pct > 0)
            {
                if (Mathf.Abs(priceDeltaPercent) > 0.001f)
                    ServerBumpCompanyPrice(c.companyName, priceDeltaPercent);

                if (payoutMultiplier != 1f && durationRounds > 0)
                    ServerBoostCompanyPayouts(c.companyName, payoutMultiplier, durationRounds);
            }
        }
    }

    [Server]
    public void ServerNerfAllCompaniesOwnedBy(PlayerPawn pawn, float priceDeltaPercent, float payoutMultiplier, int durationRounds)
    {
        if (pawn == null) return;

        foreach (var kv in companies)
        {
            var c = kv.Value;
            if (c == null) continue;

            if (c.ownershipPercents.TryGetValue(pawn, out int pct) && pct > 0)
            {
                if (Mathf.Abs(priceDeltaPercent) > 0.001f)
                    ServerBumpCompanyPrice(c.companyName, priceDeltaPercent);

                if (!Mathf.Approximately(payoutMultiplier, 1f) && durationRounds > 0)
                    ServerBoostCompanyPayouts(c.companyName, payoutMultiplier, durationRounds);
            }
        }
    }

    [Server]
    public void ServerBoostAllPayouts(float multiplier, int durationRounds)
    {
        if (GameManager.Instance == null) return;
        int now = TurnManager.Instance.roundCount.Value;

        foreach (var comp in companies.Values)
        {
            if (comp == null) continue;

            foreach (var kv in comp.ownershipPercents)
            {
                var pawn = kv.Key;
                if (pawn == null || kv.Value <= 0) continue;

                if (!pawn.factoryPortfolio.TryGetValue(comp.companyName, out var rec))
                {
                    rec = new ShareRecord
                    {
                        count = 0,
                        roundBought = now,
                        multiplier = 1f,
                        multiplierExpiresAt = 0,
                        sharePercent = kv.Value
                    };
                }

                rec.multiplier *= multiplier;
                rec.multiplierExpiresAt = Mathf.Max(rec.multiplierExpiresAt, now + durationRounds);

                pawn.factoryPortfolio[comp.companyName] = rec;
                pawn.ServerBroadcastPortfolio();
            }
        }
    }

    [Server]
    public void ApplyGlobalTrendThisRound()
    {
        if (Mathf.Approximately(_globalTrendPctPerRound, 0f)) return;

        float k = 1f + (_globalTrendPctPerRound / 100f);
        foreach (var comp in companies.Values)
        {
            if (comp == null) continue;
            comp.baseCost = Mathf.Max(1, Mathf.RoundToInt(comp.baseCost * k));
        }
    }

    [TargetRpc]
    private void TargetShowProposalHint(NetworkConnection conn)
    {
        if (ProposalUI.Instance != null)
        {
            ProposalUI.Instance.ShowFirstTimeHint();
            return;
        }
        EventUI.Instance?.SideeventShow(
            "Proposal tip:\n• Choose a company you don’t own.\n• Enter % you want and the price you’ll pay.\n• You can’t exceed 100% total and owner can’t sell more than they have.",
            true
        );
    }

    [TargetRpc]
    private void TargetShowReviewHint(NetworkConnection conn)
    {
        if (ReviewUI.Instance != null)
        {
            ReviewUI.Instance.ShowFirstTimeHint();
            return;
        }
        EventUI.Instance?.SideeventShow(
            "Review tip:\n• Review offers made to your companies.\n• Accept to transfer shares for cash; Reject to keep them.\n• Majority (>50%) can change tile owner color.",
            true
        );
    }

    [Server]
    public int ServerCountCompaniesOwnedBy(PlayerPawn pawn, string sectorFilter = null, bool onlyMajority = false)
    {
        if (pawn == null) return 0;
        int count = 0;

        foreach (var kv in companies)
        {
            var c = kv.Value;
            if (c == null) continue;

            if (!string.IsNullOrWhiteSpace(sectorFilter) &&
                !string.Equals(c.sector, sectorFilter, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (onlyMajority)
            {
                if (c.ownershipPercents.TryGetValue(pawn, out int pct) && pct > 50)
                    count++;
            }
            else
            {
                if (c.ownershipPercents.TryGetValue(pawn, out int pct) && pct > 0)
                    count++;
            }
        }

        return count;
    }

    [ObserversRpc]
    private void RpcSyncMajorityOwner(string companyName, string newOwnerName)
    {
        if (companies.TryGetValue(companyName, out var c))
        {
            c.ownerName = newOwnerName;
            c.owner = FindPawnByName(newOwnerName);
        }

        ProposalUI.Instance?.Refresh();
        ReviewUI.Instance?.Refresh();
        RpcRefreshLocalPortfolioUI();
    }

    [Server]
    public void ForceCloseReviewFor(PlayerPawn pawn)
    {
        TargetHideReview(pawn.Owner);
    }

    [Server]
    public void ForceCloseProposalFor(PlayerPawn pawn, bool autoSkip)
    {
        TargetHideProposal(pawn.Owner);
    }

    [TargetRpc]
    private void TargetHideReview(NetworkConnection conn)
    {
        if (ReviewUI.Instance != null) ReviewUI.Instance.Hide();
    }

    [TargetRpc]
    private void TargetHideProposal(NetworkConnection conn)
    {
        if (ProposalUI.Instance != null) ProposalUI.Instance.Hide();
    }

    [Server]
    public int GetLiveCompanyPrice(string companyName, int fallbackBase)
    {
        if (companies.TryGetValue(companyName, out var c) && c != null && c.currentPrice > 0)
            return c.currentPrice;
        return fallbackBase;
    }

   public int ClientPreviewForcedBuyPrice(CompanyRecord comp, PlayerPawn buyer, int requestedPct,
                                       out int transferablePct, out int livePriceUsed)
    {
        transferablePct = 0;
        livePriceUsed = 0;
        if (comp == null || buyer == null) return 0;

        var seller = comp.owner;
        if (seller == null || seller == buyer) return 0;

        int sellerAvail = comp.GetOwnership(seller);
        int buyerHas = comp.GetOwnership(buyer);
        int buyerRoom = Mathf.Max(0, 100 - buyerHas);

        int xfer = Mathf.Min(Mathf.Clamp(requestedPct, 1, 100), sellerAvail, buyerRoom);
        if (xfer <= 0) return 0;

        int live = (comp.currentPrice > 0) ? comp.currentPrice : Mathf.Max(1, comp.baseCost);
        livePriceUsed = live;

        float prem = Mathf.Max(1f, forcedBuyPriceMult.Value);
        float core = live * (xfer / 100f) * prem;

        // NEW: “hurt tax” = % of buyer's current money (pre-payment)
        float surcharge = Mathf.Max(0f, buyer.money.Value) * Mathf.Max(0f, forcedBuySurchargeOfCash.Value);

        float costF = core + surcharge;

        transferablePct = xfer;
        return Mathf.Max(1, Mathf.RoundToInt(costF));
}


    [Server]
    private int GetLivePriceOrBase(string companyName, int baseCost)
    {
        if (companies.TryGetValue(companyName, out var c) && c != null && c.currentPrice > 0)
            return c.currentPrice;
        return Mathf.Max(1, baseCost);
    }

    /* Price model:
       cost = livePrice * (percent/100f) * forcedBuyPriceMult.Value
    */
    [Server]
    private bool ServerExecuteForcedBuy(PlayerPawn buyer, string companyName, int percent, out int paid, out int transferred)
    {
        paid = 0; transferred = 0;

        if (buyer == null || string.IsNullOrWhiteSpace(companyName)) return false;
        if (!companies.TryGetValue(companyName, out var comp) || comp == null) return false;

        var seller = comp.owner;
        if (seller == null || seller == buyer) return false;

        int sellerAvail = comp.GetOwnership(seller);
        int buyerHas = comp.GetOwnership(buyer);
        int buyerRoom = Mathf.Max(0, 100 - buyerHas);
        int xfer = Mathf.Min(Mathf.Max(percent, 0), sellerAvail, buyerRoom);
        if (xfer <= 0) return false;

        int live = GetLivePriceOrBase(companyName, comp.baseCost);
        float prem = Mathf.Max(1f, forcedBuyPriceMult.Value);
        float core = live * (xfer / 100f) * prem;
        int buyerCashBefore = Mathf.Max(0, buyer.money.Value);
        float surchargeF = buyerCashBefore * Mathf.Max(0f, forcedBuySurchargeOfCash.Value);
        int cost = Mathf.Max(1, Mathf.RoundToInt(core + surchargeF));

        bool ok = forcedBuyAllowsDebt.Value
            ? TryPayWithBailouts(buyer, cost, forcedBuyMaxBailouts.Value)
            : (buyer.money.Value >= cost && buyer.TrySpendMoney(cost));

        if (!ok) return false;

        seller.AddMoney(cost);

        comp.SetOwnership(seller, sellerAvail - xfer);
        comp.SetOwnership(buyer, buyerHas + xfer);

        var majority = comp.GetMajorityOwner();
        if (majority != comp.owner)
        {
            comp.owner = majority;
            comp.ownerName = majority.playerName.Value;
            RpcSyncMajorityOwner(comp.companyName, comp.ownerName);
            RpcUpdateTileOwner(comp.companyName, comp.ownerName, majority.colorIndex.Value);
            Notifier.Instance?.ToastAll($"{majority.playerName.Value} ได้เข้าควบคุม {companyName} แล้ว!", ToastKind.Success);
        }

        buyer.ServerBroadcastPortfolio();
        seller.ServerBroadcastPortfolio();
        if (comp.owner != seller) comp.owner.ServerBroadcastPortfolio();

        RpcSyncOwnership(companyName, buyer.playerName.Value, comp.GetOwnership(buyer));
        RpcSyncOwnership(companyName, seller.playerName.Value, comp.GetOwnership(seller));
        if (comp.owner != seller)
            RpcSyncOwnership(companyName, comp.owner.playerName.Value, comp.GetOwnership(comp.owner));

        paid = cost;
        transferred = xfer;
        Notifier.Instance?.ToastAll($"{buyer.playerName.Value} ได้บังคับซื้อสำเร็จ: +{xfer}% {companyName} ในราคา ${paid}M", ToastKind.Success);
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdForceBuy(string companyName, int requestedPercent, NetworkConnection caller = null)
    {
        if (caller == null) return;

        var proposer = GameManager.Instance.Players.Find(p => p.Owner == caller);
        if (proposer == null) return;

        if (!TurnManager.Instance.IsCurrentPawn(proposer) ||
            !TurnManager.Instance.InProposalPhaseFor(proposer))
        {
            Debug.Log($"[Market] CmdForceBuy blocked: not current/Proposal.");
            return;
        }
        if (EventManager.Instance != null && EventManager.Instance.IsProposalBlockedNow())
            return;

        requestedPercent = Mathf.Clamp(requestedPercent, forcedBuyMinPercent, 100);

        if (!_forcedPctUsedThisTurn.TryGetValue(proposer, out var used))
            _forcedPctUsedThisTurn[proposer] = used = 0;

        int remainingBudget = Mathf.Max(0, forcedBuyPercentCapPerTurn - used);
        if (remainingBudget <= 0)
        {
            TargetToast(caller, $"Forced Buy cap reached ({forcedBuyPercentCapPerTurn}% this turn).");
            return;
        }

        int attemptPct = Mathf.Min(requestedPercent, remainingBudget);

        if (!companies.TryGetValue(companyName, out var comp) || comp == null || comp.owner == null || comp.owner == proposer)
        {
            TargetToast(caller, "Forced Buy failed: invalid target.");
            return;
        }

        if (ServerExecuteForcedBuy(proposer, companyName, attemptPct, out int paid, out int xfer))
        {
            _forcedPctUsedThisTurn[proposer] = used + xfer;
            TargetToast(caller, $"Forced Buy success: +{xfer}% {companyName} for ${paid}M. Used {_forcedPctUsedThisTurn[proposer]}%/{forcedBuyPercentCapPerTurn}% this turn.");
            if (_forcedPctUsedThisTurn[proposer] >= forcedBuyPercentCapPerTurn)
                TargetHideProposal(caller);
        }
        else
        {
            TargetToast(caller, $"Forced Buy failed (not enough funds or no transferable %).");
        }

        var seller = comp?.owner;
        if (seller != null && seller.Owner != null)
            TargetRefreshReviewUI(seller.Owner);
    }

    [TargetRpc]
    private void TargetToast(NetworkConnection conn, string msg)
    {
        Notifier.Instance?.ToastAll(msg, ToastKind.Info);
    }
}
