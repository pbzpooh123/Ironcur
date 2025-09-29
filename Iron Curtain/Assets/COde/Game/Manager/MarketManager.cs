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

        // Example stock setup
        stocks.Add(new StockData("Steel & Iron", 100));
        stocks.Add(new StockData("Oil & Gas", 120));
        stocks.Add(new StockData("Food & Beverage", 80));
        stocks.Add(new StockData("Electronics", 90));
        stocks.Add(new StockData("Weapons", 150));
        stocks.Add(new StockData("Real Estate", 110));
        stocks.Add(new StockData("Banking", 130));
    }

    // === Buy full company from investment tile ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyCompany(NetworkConnection conn, int tileIndex)
    {
        if (conn == null || conn.FirstObject == null) return;
        var pawn = conn.FirstObject.GetComponent<PlayerPawn>();
        if (pawn == null) return;

        var tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (tile == null || tile.tileType != TileType.Investment) return;
        if (tile.owner != null) return;
        if (pawn.money.Value < tile.companyCost) return;

        pawn.AddMoney(-tile.companyCost);
        tile.owner = pawn;

        string key = tile.description;
        if (!pawn.factoryPortfolio.ContainsKey(key))
            pawn.factoryPortfolio[key] = new ShareRecord { count = tile.maxShares, roundBought = TurnManager.Instance.roundCount.Value };
        else
            pawn.factoryPortfolio[key].count += tile.maxShares;

        Debug.Log($"{pawn.playerName.Value} bought {tile.description} company!");
    }

    // === Buy a share from another player’s company ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyShare(NetworkConnection conn, int tileIndex)
    {
        if (conn == null || conn.FirstObject == null) return;
        var buyer = conn.FirstObject.GetComponent<PlayerPawn>();
        if (buyer == null) return;

        var tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (tile == null || tile.tileType != TileType.Investment) return;
        if (tile.owner == null || tile.owner == buyer) return;
        if (tile.sharesOwned >= tile.maxShares) return;

        int sharePrice = Mathf.RoundToInt(tile.companyCost * 0.5f);
        if (buyer.money.Value < sharePrice) return;

        buyer.AddMoney(-sharePrice);
        tile.owner.AddMoney(sharePrice);

        tile.sharesOwned++;

        string key = tile.description;
        if (!buyer.factoryPortfolio.ContainsKey(key))
            buyer.factoryPortfolio[key] = new ShareRecord { count = 0, roundBought = TurnManager.Instance.roundCount.Value };

        buyer.factoryPortfolio[key].count++;
        tile.owner.factoryPortfolio[key].count--;

        Debug.Log($"{buyer.playerName.Value} bought a share in {tile.owner.playerName.Value}'s {key}.");
    }

    // === Buy a stock ===
    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyStock(NetworkConnection conn, string stockName, int price)
    {
        if (conn == null || conn.FirstObject == null) return;
        var pawn = conn.FirstObject.GetComponent<PlayerPawn>();
        if (pawn == null) return;
        if (pawn.money.Value < price) return;

        pawn.AddMoney(-price);

        if (!pawn.stockPortfolio.ContainsKey(stockName))
            pawn.stockPortfolio[stockName] = new ShareRecord { count = 0, roundBought = TurnManager.Instance.roundCount.Value };

        pawn.stockPortfolio[stockName].count++;

        TargetConfirmBuy(conn, stockName);
        Debug.Log($"{pawn.playerName.Value} bought stock {stockName}.");
    }

    // === Process payouts at end of round ===
    [Server]
    public void ProcessPayouts()
    {
        int round = TurnManager.Instance.roundCount.Value;

        foreach (var pawn in GameManager.Instance.Players)
        {
            // Factories: income each round
            foreach (var kvp in pawn.factoryPortfolio)
            {
                var tile = FindTileByName(kvp.Key);
                if (tile == null) continue;

                int income = Mathf.RoundToInt(tile.companyCost * 0.1f) * kvp.Value.count;
                pawn.AddMoney(income);
                Debug.Log($"{pawn.playerName.Value} earned {income} from {kvp.Key} factory.");
            }

            // Stocks: income every 4 rounds (after 2 rounds held)
            foreach (var kvp in pawn.stockPortfolio)
            {
                var record = kvp.Value;
                int age = round - record.roundBought;
                if (age < 2) continue;
                if (round % 4 != 0) continue;

                var stock = stocks.Find(s => s.stockName == kvp.Key);
                if (stock == null) continue;

                int income = Mathf.RoundToInt(stock.basePrice * 0.1f) * record.count;
                pawn.AddMoney(income);
                Debug.Log($"{pawn.playerName.Value} earned {income} from stock {kvp.Key}.");
            }
        }
    }

    private TileData FindTileByName(string name)
    {
        foreach (var go in GameManager.Instance.boardTiles)
        {
            var td = go.GetComponent<TileData>();
            if (td != null && td.description == name)
                return td;
        }
        return null;
    }

    [TargetRpc]
    private void TargetConfirmBuy(NetworkConnection conn, string stockName)
    {
        StockMarketUI.Instance?.RefreshOptions();
    }
}
