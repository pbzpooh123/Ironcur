using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;

[System.Serializable]
public class BusinessData
{
    public string businessType;
    public int baseIncome;
    public float incomeMultiplier = 1f;   // 1 = normal, >1 = plus, <1 = minus
    public int ownedShares = 0;
}


public class PlayerPawn : NetworkBehaviour
{
    public float moveSpeed = 4f;
    private int currentTile = 0;

    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<string> business   = new();
    public readonly SyncVar<string> country    = new();
    public readonly SyncVar<int>    lastRoll   = new();
    
    public Dictionary<string, ShareRecord> stockPortfolio   = new();
    public Dictionary<string, ShareRecord> factoryPortfolio = new();

    public bool isMyTurn = false;
    public readonly SyncVar<int> money = new SyncVar<int>(); 

    public PlayerInfoPanel infoPanel;  // assigned when spawning the panel

    public override void OnStartClient()
    {
        base.OnStartClient();
        money.OnChange += OnMoneyChanged;
        
    }
    private void OnMoneyChanged(int oldValue, int newValue, bool asServer)
    {
        Debug.Log($"{playerName.Value} money changed {oldValue} -> {newValue}");
        
        if (infoPanel != null)
            infoPanel.UpdateProfit(newValue);
    }

   
    [Server]
    public void AddMoney(int amount)
    {
        money.Value += amount;
    }

    [Server]
    public bool TrySpendMoney(int amount)
    {
        if (money.Value < amount) return false;
        money.Value -= amount;
        return true;
    }
    
    
    // === Called from UI Button ===
    public void OnRollDiceButton()
    {
        if (!IsOwner || !isMyTurn) return;
        CmdRollDiceAndMove();
    }

    public void OnEndTurnButton()
    {
        if (!IsOwner || !isMyTurn) return;
        CmdEndTurn();
    }

    // --- Server side ---
    [ServerRpc]
    public void CmdRollDiceAndMove()
    {
        int roll = Random.Range(1, 7);
        lastRoll.Value = roll;

        // broadcast movement steps to all
        RpcMoveSteps(roll);
        Debug.Log($"{playerName.Value} rolled {roll}");

        // client will get EndTurn UI enabled AFTER finishing movement
    }

    [ServerRpc]
    public void CmdEndTurn()
    {
        if (IsServer)
            TurnManager.Instance.EndTurn();
    }

    [TargetRpc]
    public void TargetStartTurn(NetworkConnection conn)
    {
        Debug.Log($"{playerName.Value} it’s your turn!");
        isMyTurn = true;

        // Enable UI
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.BindPawn(this);
    }

    // --- Movement ---
    [ObserversRpc]
    private void RpcMoveSteps(int steps)
    {
        StopAllCoroutines();
        StartCoroutine(MoveStepByStep(steps));
    }

    private IEnumerator MoveStepByStep(int steps)
    {
        int tileCount = GameManager.Instance.TileCount;

        for (int i = 1; i <= steps; i++)
        {
            int nextTile = (currentTile + 1) % tileCount;
            Vector3 targetPos = GameManager.Instance.GetTilePosition(nextTile);

            while (Vector3.Distance(transform.position, targetPos) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetPos;
            currentTile = nextTile; // update step

            yield return new WaitForSeconds(0.1f);
        }

        // === Trigger tile logic after finishing movement ===
        if (IsServer)
            HandleTileLogic();
    }

    private void HandleTileLogic()
    {
        TileData data = GameManager.Instance.boardTiles[currentTile].GetComponent<TileData>();
        if (data == null) return;

        // === 1. Event Tile ===
        if (data.tileType == TileType.Event)
        {
            EventManager.Instance.TriggerTileEvent(this);
            return; 
        }

        // === 2. Investment Tile ===
        if (data.tileType == TileType.Investment)
        {
            if (data.owner == null)
            {
                InvestmentUI.Instance.ShowOptions(this, currentTile, data.description, data.companyCost, true);
            }
            else if (data.owner != this && data.sharesOwned < data.maxShares)
            {
                int sharePrice = Mathf.RoundToInt(data.companyCost * 0.5f);
                InvestmentUI.Instance.ShowOptions(this, currentTile, $"{data.owner.playerName.Value}'s company", sharePrice, false);
            }
        }
    }

    
    [TargetRpc]
    public void TargetEnableEndTurn(NetworkConnection conn, bool enable)
    {
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.SetEndTurnInteractable(enable);
    }
    
    [TargetRpc]
    public void TargetOpenStockUI(NetworkConnection conn)
    {
        StockMarketUI.Instance.Show(this);
    }

    [Server]
    private void ResumeAfterEvent()
    {
        // After event ends → let stock market UI open
        TargetOpenStockUI(Owner);
        TargetEnableEndTurn(Owner, true);
    }
    
    [TargetRpc]
    public void TargetSetTurnOrder(NetworkConnection conn, int turnIndex)
    {
        var panels = FindObjectsOfType<PlayerInfoPanel>();
        foreach (var panel in panels)
        {
            if (panel.nameText.text == playerName.Value) 
            {
                panel.turnOrderText.text = $"Turn #{turnIndex + 1}";
            }
        }
    }

}
