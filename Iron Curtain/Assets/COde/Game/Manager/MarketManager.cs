using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

public class MarketManager : NetworkBehaviour
{
    public static MarketManager Instance;

    public List<StockData> stocks = new List<StockData>();

    private void Awake()
    {
        Instance = this;

        // Example setup — you can tweak these
        stocks.Add(new StockData("Steel & Iron", 100));
        stocks.Add(new StockData("Oil & Gas", 120));
        stocks.Add(new StockData("Food & Beverage", 80));
        stocks.Add(new StockData("Electronics", 90));
        stocks.Add(new StockData("Weapons", 150));
        stocks.Add(new StockData("Real Estate", 110));
        stocks.Add(new StockData("Banking", 130));
    }
    
    [Server]
    public void OfferInvestment(PlayerPawn pawn, TileData tile, int tileIndex)
    {
        if (tile.owner == null)
        {
            // Offer to buy the whole company
            TargetShowInvestmentUI(pawn.Owner, tileIndex, tile.description, tile.companyCost, true);
        }
        else if (tile.owner != pawn && tile.sharesOwned < tile.maxShares)
        {
            // Offer to buy a share instead
            int sharePrice = Mathf.RoundToInt(tile.companyCost * 0.5f);
            TargetShowInvestmentUI(pawn.Owner, tileIndex, $"{tile.owner.playerName.Value}'s company", sharePrice, false);
        }
    }

    // === Tell client to show UI ===
    [TargetRpc]
    private void TargetShowInvestmentUI(NetworkConnection conn, int tileIndex, string companyName, int cost, bool isCompany)
    {
        InvestmentUI.Instance.ShowOptions(tileIndex, companyName, cost, isCompany);
    }

    // === Commands from client ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyCompany(NetworkConnection conn, int tileIndex)
    {
        if (conn == null || conn.FirstObject == null) return;

        PlayerPawn pawn = conn.FirstObject.GetComponent<PlayerPawn>();
        if (pawn == null) return;

        TileData tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (tile == null || tile.tileType != TileType.Investment) return;
        if (tile.owner != null) return; // already owned
        if (pawn.money.Value < tile.companyCost) return;

        pawn.money.Value -= tile.companyCost;
        tile.owner = pawn;
        Debug.Log($"{pawn.playerName.Value} bought company {tile.description}!");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyShare(NetworkConnection conn, int tileIndex)
    {
        if (conn == null || conn.FirstObject == null) return;

        PlayerPawn pawn = conn.FirstObject.GetComponent<PlayerPawn>();
        if (pawn == null) return;

        TileData tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (tile == null || tile.tileType != TileType.Investment) return;
        if (tile.owner == null || tile.owner == pawn) return; // must belong to another
        if (tile.sharesOwned >= tile.maxShares) return;

        int sharePrice = Mathf.RoundToInt(tile.companyCost * 0.5f);
        if (pawn.money.Value < sharePrice) return;

        pawn.money.Value -= sharePrice;
        tile.sharesOwned++;
        Debug.Log($"{pawn.playerName.Value} bought 1 share in {tile.owner.playerName.Value}'s company!");
    }

    
    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyStock(NetworkConnection conn, string stockName, int price)
    {
        PlayerPawn pawn = conn.FirstObject.GetComponent<PlayerPawn>();
        if (pawn == null) return;

        if (pawn.money.Value < price) return;

        pawn.money.Value -= price;

        if (!pawn.portfolio.ContainsKey(stockName))
            pawn.portfolio[stockName] = 0;

        pawn.portfolio[stockName]++;

        Debug.Log($"{pawn.playerName.Value} bought 1 share of {stockName}. Now owns {pawn.portfolio[stockName]} shares.");
    }

}
