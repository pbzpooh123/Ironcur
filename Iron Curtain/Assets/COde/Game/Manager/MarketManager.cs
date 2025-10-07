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

    // Track proposals made this TURN by the current pawn → prevent duplicate proposals for same company
    private readonly Dictionary<PlayerPawn, HashSet<string>> _submittedThisTurn = new();

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
    Debug.Log($"[CmdBuyCompany] Called by conn={conn?.ClientId}, tileIndex={tileIndex}");

    if (!IsServer)
    {
        Debug.LogWarning("[CmdBuyCompany] Not running on server.");
        return;
    }

    // Who sent this?
    var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
    if (pawn == null)
    {
        Debug.LogWarning("[CmdBuyCompany] Could not resolve pawn from connection.");
        return;
    }

    // Optional but recommended: only allow current pawn
    if (!TurnManager.Instance.IsCurrentPawn(pawn))
    {
        Debug.LogWarning($"[CmdBuyCompany] {pawn.playerName.Value} is not the current pawn.");
        return;
    }

    // Validate tile index
    if (GameManager.Instance.boardTiles == null ||
        tileIndex < 0 || tileIndex >= GameManager.Instance.boardTiles.Length)
    {
        Debug.LogWarning($"[CmdBuyCompany] Invalid tileIndex {tileIndex} or boardTiles missing on server.");
        return;
    }

    var tile = GameManager.Instance.GetTileData(tileIndex);
    if (tile == null)
    {
        Debug.LogWarning($"[CmdBuyCompany] Invalid tileIndex={tileIndex} or TileData missing.");
        return;
    }
    
    if (tile.owner != null)
    {
        Debug.LogWarning($"[CmdBuyCompany] Tile {tile.companyName} already owned by {tile.owner.playerName.Value}.");
        return;
    }

    // Spend money
    if (!pawn.TrySpendMoney(tile.companyCost))
    {
        Debug.LogWarning($"[CmdBuyCompany] {pawn.playerName.Value} cannot afford {tile.companyCost}.");
        return;
    }

    string key = tile.companyName;
    if (!companies.ContainsKey(key))
    {
        var record = new CompanyRecord(key, tile.companyCost, pawn);
        record.ownerName = pawn.playerName.Value; // keep string for clients to rebind
        companies[key] = record;
        tile.owner = pawn;

        Debug.Log($"[Market] {pawn.playerName.Value} founded company {key}");

        // Send to all clients
        RpcAddCompany(key, tile.companyCost, record.ownerName);
    }
    else
    {
        Debug.LogWarning($"[CmdBuyCompany] Company {key} already exists in server dictionary.");
    }
    
}


    [ObserversRpc]
    private void RpcAddCompany(string companyName, int baseCost, string ownerName)
    {
        var ownerPawn = GameManager.Instance.Players.Find(p => p.playerName.Value == ownerName);

        if (!companies.ContainsKey(companyName))
        {
            var record = new CompanyRecord(companyName, baseCost, ownerPawn);
            record.ownerName = ownerName; 
            
            if (ownerPawn != null)
            {
                record.owner = ownerPawn;
                record.ownershipPercents[ownerPawn] = 100;
            }

            companies[companyName] = record;

            
            if (ownerPawn == null && !string.IsNullOrEmpty(ownerName))
                StartCoroutine(RebindOwnerLater(companyName, ownerName));
        }
        
        if (ProposalUI.Instance != null && ProposalUI.Instance.panel.activeSelf)
            ProposalUI.Instance.Refresh();
    }

    private IEnumerator RebindOwnerLater(string companyName, string ownerName)
    {
        PlayerPawn found = null;
        // keep waiting until GameManager.Players is populated
        while (found == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.Players.Count > 0)
                found = GameManager.Instance.Players.Find(p => p.playerName.Value == ownerName);
            yield return new WaitForSeconds(0.2f);
        }

        if (companies.TryGetValue(companyName, out var rec))
        {
            rec.owner = found;
            rec.ownerName = ownerName;

            if (!rec.ownershipPercents.ContainsKey(found))
                rec.ownershipPercents[found] = 100;

            Debug.Log($"[MarketManager] Rebound owner {ownerName} for {companyName}");
            ReviewUI.Instance?.Refresh();
        }
    }

    /* ================= Proposal Flow (server-driven UI) ================= */

    /// <summary>
    /// Server-only entry point to open Proposal UI for the current pawn.
    /// </summary>
    [Server]
    public void ShowProposalForPawn(PlayerPawn pawn)
    {
        if (pawn == null || pawn.Owner == null)
        {
            Debug.LogWarning("[MarketManager] ShowProposalForPawn: pawn or owner null.");
            return;
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
        if (conn == null)
            return;

        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null)
            return;

        // Use server-only entry to show on that client.
        ShowProposalForPawn(pawn);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void CmdNotifyProposalClosed(NetworkConnection conn = null)
    {
        if (conn == null) return;
        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;
        TurnManager.Instance.OnPlayerFinishedProposal();
        Debug.Log($"[Market] ProposalUI closed by {pawn.playerName.Value}. EndTurn enabled.");
    }

    /// <summary>
    /// Server-only entry to open Review UI for the current pawn (if they have proposals).
    /// </summary>
    [Server]
    public void ShowReviewForPawn(PlayerPawn owner)
    {
        if (owner == null || owner.Owner == null)
        {
            Debug.LogWarning("[MarketManager] ShowReviewForPawn: owner or conn is null");
            return;
        }
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
    public void CmdNotifyReviewClosed(NetworkConnection conn = null)
    {
        if (conn == null) return;
        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;
        
        TurnManager.Instance.OnOwnerFinishedReview();
        Debug.Log($"[Market] ReviewUI closed by {pawn.playerName.Value}. EndTurn enabled.");
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
    public void CmdSubmitProposal(NetworkConnection conn, string companyName, int percent, int price)
    {
        if (conn == null) return;

        var proposer = GameManager.Instance.Players.Find(p => p.Owner == conn);
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
    public void CmdResolveProposal(NetworkConnection conn, string companyName, int proposalIndex, bool accepted)
    {
        // Only owner should resolve (ideally during Review phase)
        if (conn == null) return;
        var ownerPawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (ownerPawn == null) return;

        if (!companies.TryGetValue(companyName, out var company)) return;
        if (company.owner != ownerPawn) return;
        if (proposalIndex < 0 || proposalIndex >= company.proposals.Count) return;

        var proposal = company.proposals[proposalIndex];

        // Keep prev owner ref to sync HUD if majority changes
        var prevOwner = company.owner;

        ResolveProposal(companyName, proposal, accepted);
        company.proposals.RemoveAt(proposalIndex);
        SyncProposalsToClients(companyName);

        // Refresh owner’s Review UI on client
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
            // Transfer funds
            if (!proposal.proposer.TrySpendMoney(proposal.price))
            {
                Debug.LogWarning($"[Market] Proposer cannot afford ${proposal.price}.");
                return;
            }
            prevOwner.AddMoney(proposal.price);

            // Transfer ownership %
            int fromOwner = company.GetOwnership(prevOwner);
            int transfer = Mathf.Min(proposal.percent, fromOwner);

            company.SetOwnership(prevOwner, fromOwner - transfer);
            int newShare = company.GetOwnership(proposal.proposer) + transfer;
            company.SetOwnership(proposal.proposer, newShare);

            // Check majority takeover
            var majority = company.GetMajorityOwner();
            if (majority != prevOwner)
            {
                company.owner = majority;
                RpcUpdateTileOwner(companyName, majority.playerName.Value);
            }
            
            if (company.GetOwnership(prevOwner) <= 0)
            {
                TransferOwnershipFull(companyName, proposal.proposer);
            }
            
            // Sync to all clients: proposer, prevOwner, and (if changed) new owner (though prevOwner covers most cases)
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
}
