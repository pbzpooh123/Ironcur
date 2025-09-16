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
    public readonly SyncVar<int> money = new();
    public Dictionary<string, int> portfolio = new Dictionary<string, int>();
    
    public bool isMyTurn = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        money.Value = 500; // starting cash
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

        switch (data.tileType)
        {
            case TileType.Event:
                EventManager.Instance.TriggerTileEvent(this, data.description);
                break;

            case TileType.Investment:
                if (data.owner == null)
                {
                    TargetShowInvestmentUI(Owner, currentTile, data.description, data.companyCost, true);
                }
                else if (data.owner != this && data.sharesOwned < data.maxShares)
                {
                    int sharePrice = Mathf.RoundToInt(data.companyCost * 0.5f);
                    TargetShowInvestmentUI(Owner, currentTile, $"{data.owner.playerName.Value}'s company", sharePrice, false);
                }
                break;
        }
        
        if (IsServer)
        {
            TargetOpenStockUI(Owner);
        }


        // after resolving tile, enable EndTurn button
        TargetEnableEndTurn(Owner, true);
    }

    // === UI RPCs ===
    [TargetRpc]
    private void TargetShowInvestmentUI(NetworkConnection conn, int tileIndex, string companyName, int cost, bool isCompany)
    {
        InvestmentUI.Instance.ShowOptions(tileIndex, companyName, cost, isCompany);
    }

    [TargetRpc]
    private void TargetEnableEndTurn(NetworkConnection conn, bool enable)
    {
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.SetEndTurnInteractable(enable);
    }
    
    [TargetRpc]
    private void TargetOpenStockUI(NetworkConnection conn)
    {
        StockMarketUI.Instance.Show(this);
    }

}
