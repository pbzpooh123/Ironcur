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

    // === Investment Tiles ===
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

        PlayerPawn buyer = conn.FirstObject.GetComponent<PlayerPawn>();
        if (buyer == null) return;

        TileData tile = GameManager.Instance.boardTiles[tileIndex].GetComponent<TileData>();
        if (tile == null || tile.tileType != TileType.Investment) return;
        if (tile.owner == null || tile.owner == buyer) return; // must belong to another
        if (tile.sharesOwned >= tile.maxShares) return;

        int sharePrice = Mathf.RoundToInt(tile.companyCost * 0.5f);
        if (buyer.money.Value < sharePrice) return;
        
        buyer.money.Value -= sharePrice;
        
        tile.owner.money.Value += sharePrice;
        
        tile.sharesOwned++;

        // Record in buyer's portfolio
        string key = tile.description;
        if (!buyer.factoryPortfolio.ContainsKey(key))
            buyer.factoryPortfolio[key] = new ShareRecord { count = 0, roundBought = TurnManager.Instance.roundCount.Value };

        buyer.factoryPortfolio[key].count++;
        buyer.factoryPortfolio[key].roundBought = TurnManager.Instance.roundCount.Value;

        Debug.Log($"{buyer.playerName.Value} bought 1 share in {tile.owner.playerName.Value}'s factory! " +
                  $"{tile.owner.playerName.Value} received ${sharePrice} instantly.");
    }


    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyStock(NetworkConnection conn, string stockName, int price)
    {
        if (conn == null || conn.FirstObject == null) return;

        PlayerPawn pawn = conn.FirstObject.GetComponent<PlayerPawn>();
        if (pawn == null) return;

        if (pawn.money.Value < price) return;

        pawn.money.Value -= price;

        if (!pawn.stockPortfolio.ContainsKey(stockName))
            pawn.stockPortfolio[stockName] = new ShareRecord { count = 0, roundBought = TurnManager.Instance.roundCount.Value };

        pawn.stockPortfolio[stockName].count++;
        pawn.stockPortfolio[stockName].roundBought = TurnManager.Instance.roundCount.Value;

        TargetConfirmBuy(conn, stockName);
        Debug.Log($"{pawn.playerName.Value} bought 1 share of {stockName}. Now owns {pawn.stockPortfolio[stockName].count} shares.");
    }

    // === Payout Logic ===
    [Server]
    public void ProcessPayouts()
    {
        foreach (var pawn in GameManager.Instance.Players)
        {
            // === Factory payout (every round) ===
            foreach (var kvp in pawn.factoryPortfolio)
            {
                ShareRecord record = kvp.Value;

                // manual lookup because boardTiles is GameObjects
                TileData tile = null;
                foreach (var go in GameManager.Instance.boardTiles)
                {
                    TileData td = go.GetComponent<TileData>();
                    if (td != null && td.description == kvp.Key)
                    {
                        tile = td;
                        break;
                    }
                }

                if (tile == null) continue;

                int income = Mathf.RoundToInt(tile.companyCost * 0.1f) * record.count;
                pawn.money.Value += income;

                Debug.Log($"{pawn.playerName.Value} earned ${income} from factory {kvp.Key}");
            }

            // === Stock payout (every 4 rounds, delayed by 2 rounds) ===
            foreach (var kvp in pawn.stockPortfolio)
            {
                ShareRecord record = kvp.Value;
                int age = TurnManager.Instance.roundCount.Value - record.roundBought;
                if (age < 2) continue;
                if (TurnManager.Instance.roundCount.Value % 4 != 0) continue;

                StockData stock = stocks.Find(s => s.stockName == kvp.Key);
                if (stock == null) continue;

                int income = Mathf.RoundToInt(stock.basePrice * 0.1f * stock.priceMultiplier) * record.count;
                pawn.money.Value += income;

                Debug.Log($"{pawn.playerName.Value} received ${income} from stock {kvp.Key}");
            }
        }
    }
    
    [TargetRpc]
    public void TargetConfirmBuy(NetworkConnection conn, string stockName)
    {
        if (StockMarketUI.Instance != null)
            StockMarketUI.Instance.RefreshOptions();
    }
}
