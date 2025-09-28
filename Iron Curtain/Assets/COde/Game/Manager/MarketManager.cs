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
        
        // Deduct money and set ownership
        pawn.TrySpendMoney(tile.companyCost);
        
        tile.owner = pawn;

        // Register full ownership in factoryPortfolio
        string key = tile.description;
        if (!pawn.factoryPortfolio.ContainsKey(key))
        {
            pawn.factoryPortfolio[key] = new ShareRecord 
            { 
                count = tile.maxShares, 
                roundBought = TurnManager.Instance.roundCount.Value 
            };
        }
        else
        {
            pawn.factoryPortfolio[key].count += tile.maxShares;
            pawn.factoryPortfolio[key].roundBought = TurnManager.Instance.roundCount.Value;
        }

        Debug.Log($"{pawn.playerName.Value} bought company {tile.description} with {tile.maxShares} shares!");
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
        
        // Buyer pays
        buyer.TrySpendMoney(sharePrice);
        

        // Owner earns
        tile.owner.AddMoney(sharePrice);
        

        // Increase global share count
        tile.sharesOwned++;

        string key = tile.description;

        // === Transfer logic ===
        // Buyer gains 1 share
        if (!buyer.factoryPortfolio.ContainsKey(key))
            buyer.factoryPortfolio[key] = new ShareRecord { count = 0, roundBought = TurnManager.Instance.roundCount.Value };

        buyer.factoryPortfolio[key].count++;
        buyer.factoryPortfolio[key].roundBought = TurnManager.Instance.roundCount.Value;

        // Owner loses 1 share
        if (tile.owner.factoryPortfolio.ContainsKey(key))
        {
            tile.owner.factoryPortfolio[key].count = Mathf.Max(0, tile.owner.factoryPortfolio[key].count - 1);
        }

        Debug.Log($"{buyer.playerName.Value} bought 1 share in {tile.owner.playerName.Value}'s factory! " +
                  $"{tile.owner.playerName.Value} received ${sharePrice} instantly.");
    }



    [ServerRpc(RequireOwnership = false)]
    public void CmdBuyStock(NetworkConnection conn, string stockName, int price)
    {
        if (conn == null || conn.FirstObject == null) return;

        PlayerPawn pawn = conn.FirstObject.GetComponent<PlayerPawn>();
        if (pawn == null) return;
        
        pawn.TrySpendMoney(price);
       

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
        // NEW (null-guard + snapshot)
if (GameManager.Instance == null) return;

int currentRound = TurnManager.Instance.roundCount.Value;

foreach (var pawn in GameManager.Instance.GetPlayersSnapshot())
{
    // === Factories (payout every round) ===
    foreach (var kvp in pawn.factoryPortfolio)
    {
        var record = kvp.Value;

        // reset expired multipliers
        if (record.multiplierExpiresAt > 0 && currentRound >= record.multiplierExpiresAt)
        {
            record.multiplier = 1f;
            record.multiplierExpiresAt = 0;
            // If ShareRecord is a struct, write back:
            pawn.factoryPortfolio[kvp.Key] = record;
        }

        // find tile
        TileData tile = null;
        foreach (var go in GameManager.Instance.boardTiles)
        {
            var td = go.GetComponent<TileData>();
            if (td != null && td.description == kvp.Key)
            {
                tile = td;
                break;
            }
        }
        if (tile == null) continue;

        int income = Mathf.RoundToInt(tile.companyCost * 0.1f * record.count * record.multiplier);
        pawn.AddMoney(income);
        Debug.Log($"{pawn.playerName.Value} earned ${income} from factory {kvp.Key} (x{record.multiplier})");

        // ensure write-back if ShareRecord is a struct
        pawn.factoryPortfolio[kvp.Key] = record;
    }

    // === Stocks (every 4 rounds, delayed by 2 rounds) ===
    foreach (var kvp in pawn.stockPortfolio)
    {
        var record = kvp.Value;
        int age = currentRound - record.roundBought;
        if (age < 2) continue;              // not matured yet
        if (currentRound % 4 != 0) continue; // only every 4th round

        // reset expired multipliers
        if (record.multiplierExpiresAt > 0 && currentRound >= record.multiplierExpiresAt)
        {
            record.multiplier = 1f;
            record.multiplierExpiresAt = 0;
        }

        // find stock definition
        StockData stock = stocks.Find(s => s.stockName == kvp.Key);
        if (stock == null) continue;

        int income = Mathf.RoundToInt(stock.basePrice * 0.1f * stock.priceMultiplier * record.multiplier) * record.count;
        pawn.AddMoney(income);
        Debug.Log($"{pawn.playerName.Value} received ${income} from stock {kvp.Key} (x{record.multiplier})");

        // ensure write-back if ShareRecord is a struct
        pawn.stockPortfolio[kvp.Key] = record;
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
