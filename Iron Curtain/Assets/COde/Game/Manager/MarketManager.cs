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

    // Pending proposals this turn
    public List<Proposal> proposals = new List<Proposal>();

    public CompanyRecord(string name, int cost, PlayerPawn creator)
    {
        companyName = name;
        baseCost = cost;
        owner = creator;

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
        {
            if (kv.Value > 60)
                return kv.Key;
        }
        return owner; // fallback
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
        // Reset the "submitted this turn" set for this pawn
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
    public void CmdBuyCompany(PlayerPawn pawn, int tileIndex)
    {
        // Strict validation: must be that pawn's turn and tile must be unowned
        if (!TurnManager.Instance.IsCurrentPawn(pawn)) return;

        var tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (pawn == null || tile == null) return;
        if (tile.owner != null) return; // already owned

        if (!pawn.TrySpendMoney(tile.companyCost)) return;

        string key = tile.companyName;
        if (!companies.ContainsKey(key))
        {
            var record = new CompanyRecord(key, tile.companyCost, pawn);
            companies[key] = record;
            tile.owner = pawn;

            // Spawn to all clients
            RpcAddCompany(key, tile.companyCost, pawn.playerName.Value);
            Debug.Log($"[Market] {pawn.playerName.Value} founded company {key}");
        }

        // after a tile purchase, TurnManager will switch to Proposal phase via PlayerPawn/HandleTileLogic
    }

    [ObserversRpc]
    private void RpcAddCompany(string companyName, int baseCost, string ownerName)
    {
        var ownerPawn = GameManager.Instance.Players.Find(p => p.playerName.Value == ownerName);
        if (!companies.ContainsKey(companyName))
        {
            var record = new CompanyRecord(companyName, baseCost, ownerPawn);
            companies[companyName] = record;
        }

        // Refresh proposal UI if needed
        if (ProposalUI.Instance != null && ProposalUI.Instance.panel.activeSelf)
            ProposalUI.Instance.Refresh();
    }

    /* ================= Proposal Flow ================= */

    /// <summary>
    /// Server decides whether to open Proposal UI for the current pawn.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestProposalUI(NetworkConnection conn = null)
    {
        if (conn == null) return;

        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;
        if (!TurnManager.Instance.InProposalPhaseFor(pawn)) return; // only during proposal phase & current pawn

        TargetShowProposalUI(conn, pawn.playerName.Value);
    }

    [TargetRpc]
    private void TargetShowProposalUI(NetworkConnection conn, string pawnName)
    {
        var pawn = GameManager.Instance.Players.Find(p => p.playerName.Value == pawnName);
        if (pawn != null && ProposalUI.Instance != null)
        {
            ProposalUI.Instance.Show(pawn);
        }
    }

    /// <summary>
    /// Current owner at start of owner’s turn reviews proposals only during Review phase.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdRequestReviewUI(NetworkConnection conn = null)
    {
        if (conn == null) return;

        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;
        if (!TurnManager.Instance.InReviewPhaseFor(pawn)) return;
        if (!HasProposalsForOwner(pawn)) 
        {
            // If none, proceed to roll
            TurnManager.Instance.OnOwnerFinishedReview();
            return;
        }

        TargetShowReviewUI(conn, pawn.playerName.Value);
    }

    [TargetRpc]
    private void TargetShowReviewUI(NetworkConnection conn, string pawnName)
    {
        var pawn = GameManager.Instance.Players.Find(p => p.playerName.Value == pawnName);
        if (pawn != null && ReviewUI.Instance != null)
        {
            ReviewUI.Instance.Show(pawn);
        }
    }

    /// <summary>
    /// Called from ReviewUI close → move to Rolling.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdNotifyReviewClosed(NetworkConnection conn = null)
    {
        if (conn == null) return;
        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;
        if (!TurnManager.Instance.InReviewPhaseFor(pawn)) return;

        TurnManager.Instance.OnOwnerFinishedReview();
    }

    /// <summary>
    /// Called from ProposalUI close → move to EndReady.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdNotifyProposalClosed(NetworkConnection conn = null)
    {
        if (conn == null) return;
        var pawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (pawn == null) return;
        if (!TurnManager.Instance.InProposalPhaseFor(pawn)) return;

        TurnManager.Instance.OnPlayerFinishedProposal();
    }

    /// <summary>
    /// The current pawn may submit exactly one proposal per company this turn.
    /// Enforced only while TurnPhase == Proposal and they are the current pawn.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdSubmitProposal(NetworkConnection conn, string companyName, int percent, int price)
    {
        if (conn == null) return;

        var proposer = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (proposer == null) return;
        if (!TurnManager.Instance.InProposalPhaseFor(proposer)) return;

        if (!companies.TryGetValue(companyName, out var company)) return;
        if (company.owner == proposer) return; // cannot propose to self

        // Bound checks
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

        // Also cap by owner's available percent
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
        Debug.Log($"[Market] {proposer.playerName.Value} proposed {percent}% of {companyName} for ${price}");
    }

    /* ================= Accept/Reject Proposal ================= */

    [ServerRpc(RequireOwnership = false)]
    public void CmdResolveProposal(NetworkConnection conn, string companyName, int proposalIndex, bool accepted)
    {
        // Only the current pawn (owner) during Review phase may resolve
        if (conn == null) return;
        var ownerPawn = GameManager.Instance.Players.Find(p => p.Owner == conn);
        if (ownerPawn == null) return;
        if (!TurnManager.Instance.InReviewPhaseFor(ownerPawn)) return;

        if (!companies.TryGetValue(companyName, out var company)) return;
        if (company.owner != ownerPawn) return;
        if (proposalIndex < 0 || proposalIndex >= company.proposals.Count) return;

        var proposal = company.proposals[proposalIndex];
        ResolveProposal(companyName, proposal, accepted);
        company.proposals.RemoveAt(proposalIndex);

        // If no more proposals remain, UI can refresh later; when UI closes, TurnManager advances
        ReviewUI.Instance?.Refresh();
    }

    [Server]
    private void ResolveProposal(string companyName, Proposal proposal, bool accepted)
    {
        if (!companies.TryGetValue(companyName, out var company)) return;

        if (accepted)
        {
            // Transfer funds (proposer must afford)
            if (!proposal.proposer.TrySpendMoney(proposal.price))
            {
                Debug.LogWarning($"[Market] Proposer cannot afford ${proposal.price}.");
                return;
            }
            company.owner.AddMoney(proposal.price);

            // Transfer ownership %
            int fromOwner = company.GetOwnership(company.owner);
            int transfer = Mathf.Min(proposal.percent, fromOwner);

            company.SetOwnership(company.owner, fromOwner - transfer);
            int newShare = company.GetOwnership(proposal.proposer) + transfer;
            company.SetOwnership(proposal.proposer, newShare);

            // Check majority takeover
            var prevOwner = company.owner;
            var majority = company.GetMajorityOwner();
            if (majority != prevOwner)
            {
                company.owner = majority;
                RpcUpdateTileOwner(companyName, majority.playerName.Value);
            }

            // Sync to client HUD portfolios
            RpcSyncOwnership(companyName, proposal.proposer.playerName.Value, newShare);
            RpcSyncOwnership(companyName, company.owner.playerName.Value, company.GetOwnership(company.owner));

            Debug.Log($"[Market] Proposal accepted: {proposal.proposer.playerName.Value} now owns {newShare}% of {companyName}");
        }
        else
        {
            // Just rejected
            Debug.Log($"[Market] Proposal rejected for {companyName}");
        }
    }

    [ObserversRpc]
    private void RpcSyncOwnership(string companyName, string playerName, int newPercent)
    {
        // Update local portfolio + HUD
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
    }

    [ObserversRpc]
    private void RpcUpdateTileOwner(string companyName, string newOwnerName)
    {
        var tile = FindTileByCompanyName(companyName);
        var pawn = GameManager.Instance.Players.Find(p => p.playerName.Value == newOwnerName);
        if (tile != null)
            tile.owner = pawn;
    }

    private TileData FindTileByCompanyName(string companyName)
    {
        foreach (var go in GameManager.Instance.boardTiles)
        {
            var td = go.GetComponent<TileData>();
            if (td != null && td.companyName == companyName)
                return td;
        }
        return null;
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
                {
                    pawn.AddMoney(payout);
                }
            }
        }
    }
}
