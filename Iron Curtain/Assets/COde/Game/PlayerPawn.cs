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
    public float incomeMultiplier = 1f;
    public int ownedShares = 0;
}

public class PlayerPawn : NetworkBehaviour
{
    public float moveSpeed = 4f;
    public int id;

    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<int>    lastRoll   = new();

    public Dictionary<string, ShareRecord> stockPortfolio   = new();
    public Dictionary<string, ShareRecord> factoryPortfolio = new();

    public bool isMyTurn = false;

    public readonly SyncVar<int> money       = new();     // OK
    public readonly SyncVar<int> currentTile = new();     // Sync the tile (server writes)

    public PlayerInfoPanel infoPanel;

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

    /* ---------- Money ---------- */

    [Server]
    public void AddMoney(int amount) => money.Value += amount;

    [Server]
    public bool TrySpendMoney(int amount)
    {
        if (money.Value < amount) return false;
        money.Value -= amount;
        return true;
    }

    /* ---------- Turn UI ---------- */

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

    [ServerRpc]
    public void CmdRollDiceAndMove()
    {
        int roll = Random.Range(1, 7);
        lastRoll.Value = roll;
        RpcMoveSteps(roll);
        Debug.Log($"{playerName.Value} rolled {roll}");
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

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.BindPawn(this);
    }

    /* ---------- Movement ---------- */

    // Visual movement runs on everyone, but only the SERVER updates SyncVar currentTile at the end.
    [ObserversRpc]
    private void RpcMoveSteps(int steps)
    {
        StopAllCoroutines();
        StartCoroutine(MoveStepByStep(steps));
    }

    private IEnumerator MoveStepByStep(int steps)
    {
        int tileCount = GameManager.Instance.TileCount;

        // Drive animation from a local variable; start from the synced tile.
        int localTile = currentTile.Value;

        for (int i = 1; i <= steps; i++)
        {
            int nextTile = (localTile + 1) % tileCount;
            Vector3 targetPos = GameManager.Instance.GetTilePosition(nextTile);

            while (Vector3.Distance(transform.position, targetPos) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetPos;
            localTile = nextTile;

            yield return new WaitForSeconds(0.1f);
        }

        // Only the SERVER commits the new tile to the SyncVar and runs tile logic.
        if (IsServer)
        {
            currentTile.Value = localTile;
            HandleTileLogic();
        }
    }

    private void HandleTileLogic()
    {
        var data = GameManager.Instance.boardTiles[currentTile.Value].GetComponent<TileData>();
        if (data == null) return;

        if (data.tileType == TileType.Event)
        {
            EventManager.Instance.TriggerTileEvent(this);
            return;
        }

        if (data.tileType == TileType.Investment)
        {
            if (data.owner == null)
            {
                InvestmentUI.Instance.ShowOptions(this, currentTile.Value, data.description, data.companyCost, true);
            }
            else if (data.owner != this && data.sharesOwned < data.maxShares)
            {
                int sharePrice = Mathf.RoundToInt(data.companyCost * 0.5f);
                InvestmentUI.Instance.ShowOptions(this, currentTile.Value, $"{data.owner.playerName.Value}'s company", sharePrice, false);
            }
        }
    }

    [TargetRpc]
    public void TargetEnableEndTurn(NetworkConnection conn, bool enable)
    {
        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.SetEndTurnInteractable(enable);
    }

    [TargetRpc]
    public void TargetOpenStockUI(NetworkConnection conn)
    {
        StockMarketUI.Instance.Show(this);
    }

    [Server]
    private void ResumeAfterEvent()
    {
        TargetOpenStockUI(Owner);
        TargetEnableEndTurn(Owner, true);
    }


    /* ---------- Spawn/Teleport ---------- */
}
