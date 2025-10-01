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

    // Ownership distribution: Player → %
    public Dictionary<PlayerPawn, int> ownershipPercents = new Dictionary<PlayerPawn, int>();

    // Pending proposals this round
    public List<Proposal> proposals = new List<Proposal>();

    public CompanyRecord(string name, int cost, PlayerPawn creator)
    {
        companyName = name;
        baseCost = cost;
        owner = creator;

        // Initially creator owns 100%
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
        {
            if (kv.Value > 60)
                return kv.Key;
        }
        return owner; // fallback to current
    }
}


public class MarketManager : NetworkBehaviour
{
    public static MarketManager Instance;

    public List<StockData> stocks = new List<StockData>();
    public Dictionary<string, CompanyRecord> companies = new Dictionary<string, CompanyRecord>();


    private void Awake()
    {
        Instance = this;

    }

    // === Investment Tiles ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyCompany(PlayerPawn pawn, int tileIndex)
    {
        var tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (pawn == null || tile == null) return;
        if (!pawn.TrySpendMoney(tile.companyCost)) return;

        string key = tile.companyName;
        if (!companies.ContainsKey(key))
        {
            var record = new CompanyRecord(key, tile.companyCost, pawn);
            companies[key] = record;
            tile.owner = pawn;
            RpcSyncOwnership(key, pawn.playerName.Value, 100);
            Debug.Log($"[Market] {pawn.playerName.Value} founded company {key}");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void CmdSubmitProposal(PlayerPawn proposer, string companyName, int percent, int price)
    {
        if (!companies.TryGetValue(companyName, out var company)) return;
        if (proposer == null || company.owner == proposer) return; // can't propose to self

        Proposal p = new Proposal
        {
            proposer = proposer,
            percent = percent,
            price = price
        };
        company.proposals.Add(p);

        Debug.Log($"[Market] {proposer.playerName.Value} proposed {percent}% of {companyName} for ${price}");
    }


    [Server]
    public void ResolveProposal(string companyName, Proposal proposal, bool accepted)
    {
        if (!companies.TryGetValue(companyName, out var company)) return;
        if (!company.proposals.Contains(proposal)) return;

        if (accepted)
        {
            // Transfer money
            if (!proposal.proposer.TrySpendMoney(proposal.price)) return;
            company.owner.AddMoney(proposal.price);

            // Adjust ownership
            int oldOwnerShare = company.GetOwnership(company.owner);
            int transferPercent = Mathf.Min(proposal.percent, oldOwnerShare);

            company.SetOwnership(company.owner, oldOwnerShare - transferPercent);
            int newShare = company.GetOwnership(proposal.proposer) + transferPercent;
            company.SetOwnership(proposal.proposer, newShare);
            RpcSyncOwnership(companyName, proposal.proposer.playerName.Value, newShare);
            RpcSyncOwnership(companyName, company.owner.playerName.Value, company.GetOwnership(company.owner));

            // Check majority
            var majority = company.GetMajorityOwner();
            company.owner = majority;

            Debug.Log($"[Market] Proposal accepted: {proposal.proposer.playerName.Value} now owns {newShare}% of {companyName}");
        }
        else
        {
            Debug.Log($"[Market] Proposal rejected for {companyName}");
        }

        company.proposals.Remove(proposal);
        
        // === Sync ownership into PlayerPawn.factoryPortfolio ===
        foreach (var kv in company.ownershipPercents)
        {
            var p = kv.Key;
            int percent = kv.Value;
            if (p == null) continue;

            if (!p.factoryPortfolio.ContainsKey(company.companyName))
            {
                p.factoryPortfolio[company.companyName] = new ShareRecord
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
                var rec = p.factoryPortfolio[company.companyName];
                rec.sharePercent = percent;
                p.factoryPortfolio[company.companyName] = rec;
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void CmdResolveProposal(string companyName, int proposalIndex, bool accepted)
    {
        if (!companies.TryGetValue(companyName, out var company)) return;
        if (proposalIndex < 0 || proposalIndex >= company.proposals.Count) return;

        var proposal = company.proposals[proposalIndex];
        ResolveProposal(companyName, proposal, accepted);
    }
    

    // === Payout Logic ===
    [Server]
    public void ProcessPayouts()
    {
        int currentRound = TurnManager.Instance.roundCount.Value;

        foreach (var companyKvp in companies) // iterate all registered companies
        {
            CompanyRecord company = companyKvp.Value;
            if (company == null) continue;

            // Base income for this company this round
            int baseIncome = Mathf.RoundToInt(company.baseCost * 0.1f);

            // Distribute based on ownership %
            foreach (var kv in company.ownershipPercents)
            {
                PlayerPawn pawn = kv.Key;
                int percent = kv.Value;

                if (pawn == null || percent <= 0) continue;

                // Ownership share
                float ownershipRatio = percent / 100f;
                int payout = Mathf.RoundToInt(baseIncome * ownershipRatio);

                // Apply multipliers (check expiry)
                ShareRecord rec;
                if (pawn.factoryPortfolio.TryGetValue(company.companyName, out rec))
                {
                    if (rec.multiplierExpiresAt > 0 && currentRound >= rec.multiplierExpiresAt)
                    {
                        rec.multiplier = 1f;
                        rec.multiplierExpiresAt = 0;
                        pawn.factoryPortfolio[company.companyName] = rec;
                    }

                    payout = Mathf.RoundToInt(payout * rec.multiplier);
                }

                // Pay the player
                if (payout != 0)
                {
                    pawn.AddMoney(payout);
                    Debug.Log($"[Company Payout] {pawn.playerName.Value} received ${payout} " +
                              $"from {company.companyName} ({percent}% ownership, x{rec.multiplier})");
                }
            }
        }
    }


    [TargetRpc]
    public void TargetShowProposalUI(NetworkConnection conn, PlayerPawn pawn)
    {
        if (ProposalUI.Instance != null)
            ProposalUI.Instance.Show(pawn);
        else
            Debug.LogWarning("[MarketManager] ProposalUI.Instance not found!");
    }

    [TargetRpc]
    public void TargetShowReviewUI(NetworkConnection conn, PlayerPawn pawn)
    {
        if (ReviewUI.Instance != null)
            ReviewUI.Instance.Show(pawn);
        else
            Debug.LogWarning("[MarketManager] ReviewUI.Instance not found!");
    }
    
    [ObserversRpc]
    private void RpcSyncOwnership(string companyName, string playerName, int newPercent)
    {
        // Find the pawn for this player
        var pawn = GameManager.Instance.Players.Find(p => p.playerName.Value == playerName);
        if (pawn == null) return;

        // Update local portfolio
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

        // Optional: update HUD panel
        if (pawn.infoPanel != null)
            pawn.infoPanel.UpdateCompanyOwnership(companyName, newPercent);

        Debug.Log($"[ClientSync] {pawn.playerName.Value} now has {newPercent}% of {companyName}");
    }

}
