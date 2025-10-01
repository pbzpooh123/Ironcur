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
    public string pendingOwnerName; // used when owner pawn is not yet resolved

    public Dictionary<PlayerPawn, int> ownershipPercents = new Dictionary<PlayerPawn, int>();
    public List<Proposal> proposals = new List<Proposal>();

    public CompanyRecord(string name, int cost)
    {
        companyName = name;
        baseCost = cost;
    }

    public void SetOwner(PlayerPawn newOwner)
    {
        owner = newOwner;
        ownershipPercents.Clear();
        if (newOwner != null)
            ownershipPercents[newOwner] = 100;
    }

    public int GetOwnership(PlayerPawn pawn)
    {
        return ownershipPercents.TryGetValue(pawn, out int val) ? val : 0;
    }

    public void SetOwnership(PlayerPawn pawn, int newPercent)
    {
        if (pawn == null) return;
        ownershipPercents[pawn] = Mathf.Clamp(newPercent, 0, 100);
    }

    public PlayerPawn GetMajorityOwner()
    {
        foreach (var kv in ownershipPercents)
            if (kv.Value > 60)
                return kv.Key;
        return owner;
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
        Debug.Log(NetworkObject.IsSpawned);
    }

    // === Investment Tiles ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyCompany(PlayerPawn pawn, int tileIndex)
    {
        var tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (pawn == null || tile == null) return;

        // Optional: prevent double-buy, or buying if already owned.
        if (tile.owner != null) return; 
        if (!pawn.TrySpendMoney(tile.companyCost)) return;
        if (string.IsNullOrWhiteSpace(tile.companyName)) return;

        string key = tile.companyName;
        if (!companies.ContainsKey(key))
        {
            var record = new CompanyRecord(key, tile.companyCost);
            record.SetOwner(pawn);
            companies[key] = record;

            tile.owner = pawn;

            Debug.Log("[Server] Calling RpcAddCompany");
            RpcAddCompany(key, tile.companyCost, pawn.playerName.Value);
            
            RpcSyncOwnership(key, pawn.playerName.Value, 100);

            // Optional: initialize server-side portfolio for multipliers support later.
            if (!pawn.factoryPortfolio.ContainsKey(key))
            {
                pawn.factoryPortfolio[key] = new ShareRecord
                {
                    count = 0,
                    roundBought = TurnManager.Instance.roundCount.Value,
                    multiplier = 1f,
                    multiplierExpiresAt = 0,
                    sharePercent = 100
                };
            }
            else
            {
                var rec = pawn.factoryPortfolio[key];
                rec.sharePercent = 100;
                pawn.factoryPortfolio[key] = rec;
            }

            Debug.Log($"[Market] {pawn.playerName.Value} founded company {key}");
        }
    }

    [ObserversRpc]
    private void RpcAddCompany(string companyName, int baseCost, string ownerName)
    {
        Debug.Log("[ClientSync] RpcAddCompany REACHED CLIENT");

        if (!companies.ContainsKey(companyName))
        {
            var record = new CompanyRecord(companyName, baseCost);
            companies[companyName] = record;

            // Try to bind owner now
            var ownerPawn = GameManager.Instance.Players.Find(p => p.playerName.Value == ownerName);
            if (ownerPawn != null)
            {
                record.SetOwner(ownerPawn);
                Debug.Log($"[ClientSync] Company {companyName} added with owner {ownerName}, total={companies.Count}");
            }
            else
            {
                // Store for later binding
                record.pendingOwnerName = ownerName;
                StartCoroutine(RebindOwnerLater(companyName, ownerName));
                Debug.Log($"[ClientSync] Company {companyName} added; owner pending {ownerName}");
            }
        }

        // Refresh Proposal UI if open
        if (ProposalUI.Instance != null && ProposalUI.Instance.panel.activeSelf)
            ProposalUI.Instance.Refresh();
    }

// Rebind coroutine unchanged, but call SetOwner once found
    private System.Collections.IEnumerator RebindOwnerLater(string companyName, string ownerName)
    {
        PlayerPawn found = null;
        while (found == null)
        {
            if (GameManager.Instance != null)
                found = GameManager.Instance.Players.Find(p => p.playerName.Value == ownerName);
            yield return null;
        }

        if (companies.TryGetValue(companyName, out var rec))
        {
            rec.SetOwner(found);
            rec.pendingOwnerName = null;
            Debug.Log($"[ClientSync] Rebound owner for {companyName} -> {ownerName}");
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
    

    // Server side: don't send pawnName anymore
    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestProposalUI(NetworkConnection conn = null)
    {
        if (conn == null)
        {
            Debug.LogWarning("[MarketManager] CmdRequestProposalUI: conn is null!");
            return;
        }

        TargetShowProposalUI(conn); // no pawnName
    }

    [TargetRpc]
    private void TargetShowProposalUI(NetworkConnection conn)
    {
        StartCoroutine(WaitAndOpenProposalUI());
    }

    private System.Collections.IEnumerator WaitAndOpenProposalUI()
    {
        float timeout = 3f;
        PlayerPawn localPawn = null;

        while (timeout > 0f)
        {
            // Find local player's pawn (robust)
            if (localPawn == null)
            {
                foreach (var p in FindObjectsOfType<PlayerPawn>())
                {
                    if (p != null && p.IsOwner)
                    {
                        localPawn = p;
                        break;
                    }
                }
            }

            // Wait until UI and local pawn exist
            if (ProposalUI.Instance != null && localPawn != null)
                break;

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (ProposalUI.Instance != null && localPawn != null)
        {
            ProposalUI.Instance.Show(localPawn);
            Debug.Log($"[MarketManager] ProposalUI opened for {localPawn.playerName.Value}");
        }
        else
        {
            Debug.LogWarning($"[MarketManager] Failed to open ProposalUI on client. " +
                             $"UI={(ProposalUI.Instance != null)}, pawn={(localPawn != null)}");
        }
    }



    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestReviewUI(NetworkConnection conn = null)
    {
        if (conn == null)
        {
            Debug.LogWarning("[MarketManager] CmdRequestReviewUI: conn is null!");
            return;
        }

        TargetShowReviewUI(conn);
    }

    [TargetRpc]
    private void TargetShowReviewUI(NetworkConnection conn)
    {
        StartCoroutine(WaitAndOpenReviewUI());
    }

    private System.Collections.IEnumerator WaitAndOpenReviewUI()
    {
        float timeout = 3f;
        PlayerPawn localPawn = null;

        while (timeout > 0f)
        {
            // Find local player's pawn
            if (localPawn == null)
            {
                foreach (var p in FindObjectsOfType<PlayerPawn>())
                {
                    if (p != null && p.IsOwner)
                    {
                        localPawn = p;
                        break;
                    }
                }
            }

            // Wait until UI and local pawn exist
            if (ReviewUI.Instance != null && localPawn != null)
                break;

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (ReviewUI.Instance != null && localPawn != null)
        {
            ReviewUI.Instance.Show(localPawn);
            Debug.Log($"[MarketManager] ReviewUI opened for {localPawn.playerName.Value}");
        }
        else
        {
            Debug.LogWarning($"[MarketManager] Failed to open ReviewUI on client. " +
                             $"UI={(ReviewUI.Instance != null)}, pawn={(localPawn != null)}");
        }
    }

    

}
