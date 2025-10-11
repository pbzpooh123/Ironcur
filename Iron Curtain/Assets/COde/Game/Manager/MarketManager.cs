using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

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
    public PlayerPawn owner;

    // NEW: helps filter on clients before owner pawn rebinds
    public string ownerName;     

    public Dictionary<PlayerPawn, int> ownershipPercents = new Dictionary<PlayerPawn, int>();
    public List<Proposal> proposals = new List<Proposal>();

    public CompanyRecord(string name, int cost, PlayerPawn creator)
    {
        companyName = name;
        baseCost = cost;
        owner = creator;
        ownerName = (creator != null) ? creator.playerName.Value : null;

        if (creator != null)
            ownershipPercents[creator] = 100;
    }

    public int GetOwnership(PlayerPawn pawn)
    {
        return ownershipPercents.TryGetValue(pawn, out int val) ? val : 0;
    }

    public void SetOwnership(PlayerPawn pawn, int newPercent)
    {
        ownershipPercents[pawn] = Mathf.Clamp(newPercent, 0, 100);
    }

    public PlayerPawn GetMajorityOwner()
    {
        foreach (var kv in ownershipPercents)
            if (kv.Value > 60) return kv.Key;
        return owner;
    }
}

public class MarketManager : NetworkBehaviour
{
    public static MarketManager Instance;

    public Dictionary<string, CompanyRecord> companies = new Dictionary<string, CompanyRecord>();
    
    private readonly Dictionary<PlayerPawn, HashSet<string>> _submittedThisTurn = new();
    [Header("Proposal Policy")]
    [Tooltip("If true, allow accept even when proposer lacks money by triggering bailouts automatically.")]
    public bool AllowDebtOnAccept = true;

    [Tooltip("Cap how many automatic bailouts could be applied for a single accept.")]
    public int MaxAutoBailoutsPerAccept = 10;

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
    }

    [Server]
    public void OnRoundAdvanced(int newRound)
    {
        // No-op for now; could clear cross-round data here if needed.
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
    if (conn == null)
    {
        Debug.LogWarning("[Market] CmdBuyCompany called with null conn.");
        return;
    }

    // Resolve the caller's pawn on the server.
    var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
    if (pawn == null)
    {
        Debug.LogWarning("[Market] CmdBuyCompany: Could not resolve pawn for caller.");
        return;
    }

    Debug.Log($"[Market] CmdBuyCompany received from {pawn.playerName.Value}, tile={tileIndex}");

    
    var tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
    if (tile == null)
    {
        Debug.LogWarning("[Market] CmdBuyCompany: TileData null.");
        return;
    }
    if (tile.owner != null)
    {
        Debug.LogWarning("[Market] CmdBuyCompany: tile already owned.");
        return;
    }

    // Cost check.
    if (!pawn.TrySpendMoney(tile.companyCost))
    {
        Debug.LogWarning($"[Market] CmdBuyCompany: {pawn.playerName.Value} cannot afford ${tile.companyCost}.");
        return;
    }

    string key = tile.companyName;
    if (!companies.ContainsKey(key))
    {
        var record = new CompanyRecord(key, tile.companyCost, pawn);
        companies[key] = record;
        tile.owner = pawn;

        // Sync to everyone.
        RpcAddCompany(key, tile.companyCost, pawn.playerName.Value);
        Debug.Log($"[Market] {pawn.playerName.Value} founded company {key}");
    }
    else
    {
        // In case you landed on an existing company with no owner (edge case)
        var rec = companies[key];
        rec.owner = pawn;
        rec.ownerName = pawn.playerName.Value;
        rec.ownershipPercents.Clear();
        rec.ownershipPercents[pawn] = 100;
        tile.owner = pawn;
        RpcAddCompany(key, rec.baseCost, pawn.playerName.Value);
        Debug.Log($"[Market] {pawn.playerName.Value} took ownership of existing company {key}");
    }

    // Continue your tile flow on the server (optional; your InvestmentUI already notifies).
    TurnManager.Instance.ServerOnTileActionComplete(pawn);
}


    [ObserversRpc]
private void RpcAddCompany(string companyName, int baseCost, string ownerName)
{
    Debug.Log($"[RpcAddCompany] company={companyName}, ownerName={ownerName}");

    if (!companies.TryGetValue(companyName, out var rec))
    {
        var ownerPawn = FindPawnByName(ownerName);
        rec = new CompanyRecord(companyName, baseCost, ownerPawn);
        rec.ownerName = ownerName;

        // If owner found now, ensure ownership map has 100% for them.
        if (ownerPawn != null)
        {
            rec.owner = ownerPawn;
            rec.ownershipPercents.Clear();
            rec.ownershipPercents[ownerPawn] = 100;
        }

        companies[companyName] = rec;

        if (ownerPawn == null)
            StartCoroutine(RebindOwnerLater(companyName, ownerName));
    }
    else
    {
        // Company already exists (edge case). Ensure ownerName and try to assign owner.
        rec.ownerName = ownerName;

        if (rec.owner == null)
        {
            var ownerPawn = FindPawnByName(ownerName);
            if (ownerPawn != null)
            {
                rec.owner = ownerPawn;
                rec.ownershipPercents.Clear();
                rec.ownershipPercents[ownerPawn] = 100;
            }
            else
            {
                StartCoroutine(RebindOwnerLater(companyName, ownerName));
            }
        }
    }

    // If any UI is open, refresh it.
    if (ProposalUI.Instance != null && ProposalUI.Instance.panel.activeSelf)
        ProposalUI.Instance.Refresh();
    if (ReviewUI.Instance != null && ReviewUI.Instance.panel.activeSelf)
        ReviewUI.Instance.Refresh();
}

private PlayerPawn FindPawnByName(string name)
{
    if (string.IsNullOrEmpty(name)) return null;

    // First try GameManager list (preferred).
    var gm = GameManager.Instance;
    if (gm != null && gm.Players != null)
    {
        var p = gm.Players.Find(pp => pp != null && pp.playerName.Value == name);
        if (p != null) return p;
    }

    // Fallback: brute force the scene.
    foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
    {
        if (p != null && p.playerName.Value == name)
            return p;
    }

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
        }

        Debug.Log($"[RebindOwnerLater] Rebound owner {ownerName} for {companyName}");

        if (ReviewUI.Instance != null && ReviewUI.Instance.panel.activeSelf)
            ReviewUI.Instance.Refresh();
        if (ProposalUI.Instance != null && ProposalUI.Instance.panel.activeSelf)
            ProposalUI.Instance.Refresh();
    }
}

    /* ================= Proposal Flow (server-driven UI) ================= */
    
    /// Server-only entry point to open Proposal UI for the current pawn.
    [Server]
    public void ShowProposalForPawn(PlayerPawn pawn)
    {
        if (pawn == null || pawn.Owner == null) return;
        if (pawn.jailTurnsLeft.Value > 0) return;   // jailed → block proposal
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
        if (conn == null)
            return;

        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null)
            return;

        // Use server-only entry to show on that client.
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


    /// <summary>
    /// Server-only entry to open Review UI for the current pawn (if they have proposals).
    /// </summary>
    [Server]
    public void ShowReviewForPawn(PlayerPawn owner)
    {
        if (owner == null || owner.Owner == null) return;
        if (owner.jailTurnsLeft.Value > 0) return;  // jailed → block review
        TargetShowReviewUI(owner.Owner);
    }

    [TargetRpc]
    private void TargetShowReviewUI(NetworkConnection conn)
    {
        Debug.Log("[MarketManager] TargetShowReviewUI reached client.");

        // Wait for UI and local pawn to be ready
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
    // Utility: find the local-owned pawn on the client
    private PlayerPawn FindLocalOwnedPawn()
    {
        var pawns = GameObject.FindObjectsOfType<PlayerPawn>();
        foreach (var p in pawns) if (p.IsOwner) return p;
        return null;
    }

    /// <summary>
    /// The current pawn may submit exactly one proposal per company this turn.
    /// Only use from Proposal phase and only for the current pawn.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdSubmitProposal(string companyName, int percent, int price, NetworkConnection caller = null)
    {
        if (caller == null)
        {
            return;
        }
        var proposer = GameManager.Instance.Players.Find(p => p.Owner == caller);
        if (proposer == null) return;

        // Optional if you have phase-checks:
        // if (!TurnManager.Instance.InProposalPhaseFor(proposer)) return;

        if (!companies.TryGetValue(companyName, out var company)) return;
        if (company.owner == proposer) return; // cannot propose to self

        // Bounds
        percent = Mathf.Clamp(percent, 1, 40);
        price   = Mathf.Max(1, price);

        // Only one per company this turn
        if (!_submittedThisTurn.TryGetValue(proposer, out var set))
        {
            set = new HashSet<string>();
            _submittedThisTurn[proposer] = set;
        }
        if (set.Contains(companyName))
        {
            Debug.LogWarning($"[Market] {proposer.playerName.Value} already proposed to {companyName} this turn.");
            return;
        }

        // Cap by owner's available
        int ownerAvailable = company.GetOwnership(company.owner);
        if (ownerAvailable <= 0)
        {
            Debug.LogWarning($"[Market] No owner share left to sell in {companyName}.");
            return;
        }
        percent = Mathf.Min(percent, ownerAvailable);

        company.proposals.Add(new Proposal
        {
            proposer = proposer,
            percent = percent,
            price = price
        });

        set.Add(companyName);
        SyncProposalsToClients(companyName);
        Debug.Log($"[Market] {proposer.playerName.Value} proposed {percent}% of {companyName} for ${price}");
    }

    /* ================= Accept/Reject Proposal ================= */

    [ServerRpc(RequireOwnership = false)]
    public void CmdResolveProposal(string companyName, int proposalIndex, bool accepted, NetworkConnection caller = null)
    {
        if (caller == null) return; // who pressed Accept/Reject?

        var ownerPawn = GameManager.Instance.Players.Find(p => p.Owner == caller);
        if (ownerPawn == null) return;

        if (!companies.TryGetValue(companyName, out var company)) return;
        if (company.owner != ownerPawn) return; // only the owner can resolve their company proposals
        if (proposalIndex < 0 || proposalIndex >= company.proposals.Count) return;

        var proposal = company.proposals[proposalIndex];

        ResolveProposal(companyName, proposal, accepted);

        // Remove the processed proposal and sync to all clients
        company.proposals.RemoveAt(proposalIndex);
        SyncProposalsToClients(companyName);

        // Refresh Review UI for the owner
        if (ownerPawn.Owner != null)
            TargetRefreshReviewUI(ownerPawn.Owner);
    }

    [Server]
    private void ResolveProposal(string companyName, Proposal proposal, bool accepted)
    {
        if (!companies.TryGetValue(companyName, out var company)) return;
        var prevOwner = company.owner;

        if (accepted)
        {
            bool paid;
            if (!AllowDebtOnAccept)
            {
                // Strict accept: proposer must afford NOW
                if (proposal.proposer.money.Value < proposal.price)
                {
                    Debug.LogWarning($"[Market] Accept failed: proposer {proposal.proposer.playerName.Value} lacks funds (${proposal.price}).");
                    return; // proposal remains pending
                }
                paid = proposal.proposer.TrySpendMoney(proposal.price);
            }
            else
            {
                // Debt mode: auto-bailout until proposer can pay (capped)
                paid = TryPayWithBailouts(proposal.proposer, proposal.price);
            }

            if (!paid)
            {
                Debug.LogWarning($"[Market] Accept failed: proposer could not pay ${proposal.price} even after bailouts.");
                return;
            }

            // Owner gets paid
            prevOwner.AddMoney(proposal.price);

            // Transfer ownership
            int fromOwner = company.GetOwnership(prevOwner);
            int transfer = Mathf.Min(proposal.percent, fromOwner);

            company.SetOwnership(prevOwner, fromOwner - transfer);
            int newShare = company.GetOwnership(proposal.proposer) + transfer;
            company.SetOwnership(proposal.proposer, newShare);

            // Majority takeover
            var majority = company.GetMajorityOwner();
            if (majority != prevOwner)
            {
                company.owner = majority;
                RpcUpdateTileOwner(companyName, majority.playerName.Value);
            }

            // Sync
            RpcSyncOwnership(companyName, proposal.proposer.playerName.Value, company.GetOwnership(proposal.proposer));
            RpcSyncOwnership(companyName, prevOwner.playerName.Value, company.GetOwnership(prevOwner));
            if (company.owner != prevOwner)
                RpcSyncOwnership(companyName, company.owner.playerName.Value, company.GetOwnership(company.owner));

            Debug.Log($"[Market] Proposal accepted: {proposal.proposer.playerName.Value} now has {newShare}% of {companyName}");
        }
        else
        {
            Debug.Log($"[Market] Proposal rejected for {companyName}");
        }
    }

    [Server]
    private bool TryPayWithBailouts(PlayerPawn p, int amount)
    {
        int attempts = 0;
        while (p.money.Value < amount && attempts < MaxAutoBailoutsPerAccept)
        {
            p.ForceBailoutOnce(); // +$100 +1 mark
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

        pawn.infoPanel?.UpdateCompanyOwnership(companyName, newPercent);
        Debug.Log($"[ClientSync] {playerName} now has {newPercent}% of {companyName}");
    }

    [ObserversRpc]
    private void RpcUpdateTileOwner(string companyName, string newOwnerName)
    {
        var tile = GameManager.Instance.FindTileByCompanyName(companyName);
        var pawn = GameManager.Instance.Players.Find(p => p.playerName.Value == newOwnerName);
        if (tile != null)
            tile.owner = pawn;
    }


    /* ================= Payouts ================= */

    [Server]
    public void ProcessPayouts()
    {
        int currentRound = TurnManager.Instance.roundCount.Value;

        foreach (var companyKvp in companies)
        {
            CompanyRecord company = companyKvp.Value;
            if (company == null) continue;

            int baseIncome = Mathf.RoundToInt(company.baseCost * 0.1f);

            foreach (var kv in company.ownershipPercents)
            {
                PlayerPawn pawn = kv.Key;
                int percent = kv.Value;
                if (pawn == null || percent <= 0) continue;

                float ownershipRatio = percent / 100f;
                int payout = Mathf.RoundToInt(baseIncome * ownershipRatio);

                if (pawn.factoryPortfolio.TryGetValue(company.companyName, out var rec))
                {
                    if (rec.multiplierExpiresAt > 0 && currentRound >= rec.multiplierExpiresAt)
                    {
                        rec.multiplier = 1f;
                        rec.multiplierExpiresAt = 0;
                        pawn.factoryPortfolio[company.companyName] = rec;
                    }
                    payout = Mathf.RoundToInt(payout * rec.multiplier);
                }

                if (payout != 0)
                    pawn.AddMoney(payout);
            }
        }
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

        // If ReviewUI is open for someone, refresh
        if (ReviewUI.Instance != null && ReviewUI.Instance.gameObject.activeInHierarchy)
            StartCoroutine(WaitAndRefreshReviewUI());

    }
    
    private IEnumerator WaitAndRefreshReviewUI()
    {
        yield return null; // wait 1 frame so currentPawn gets set in Show()
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
        var perc  = new int[n];
        var price = new int[n];

        for (int i = 0; i < n; i++)
        {
            names[i] = c.proposals[i].proposer != null ? c.proposals[i].proposer.playerName.Value : "";
            perc[i]  = c.proposals[i].percent;
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
        c.ownershipPercents.Clear();
        c.ownershipPercents[newOwner] = 100;

        // Remove company from old owner's portfolio
        oldOwner?.factoryPortfolio.Remove(companyName);

        // Add to new owner's portfolio
        if (!newOwner.factoryPortfolio.ContainsKey(companyName))
            newOwner.factoryPortfolio[companyName] = new ShareRecord
            {
                sharePercent = 100,
                multiplier = 1f,
                multiplierExpiresAt = 0,
                roundBought = TurnManager.Instance.roundCount.Value
            };

        RpcSyncOwnership(companyName, newOwner.playerName.Value, 100);
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

        if (bestPawn != null && best > 60)
        {
            majorityOwner = bestPawn;
            return true;
        }
        return false;
    }

}
