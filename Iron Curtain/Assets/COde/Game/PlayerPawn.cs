using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;

public class PlayerPawn : NetworkBehaviour
{
    public float moveSpeed = 4f;
    private int currentTile = 0;

    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<int>    lastRoll   = new();
    public readonly SyncVar<int>    money      = new();
    public readonly SyncVar<int>    bailoutMarks = new();

    public Dictionary<string, ShareRecord> factoryPortfolio = new();

    public bool isMyTurn = false;
    public PlayerInfoPanel infoPanel;

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (Owner != null)
            GiveOwnership(Owner);

        if (money.Value == 0)
            money.Value = 1000;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        money.OnChange += OnMoneyChanged;
        StartCoroutine(AutoBindInfoPanel());
    }

    private IEnumerator AutoBindInfoPanel()
    {
        float t = 2f;
        while (t > 0f && infoPanel == null)
        {
            if (GameHUD.Instance != null && !string.IsNullOrEmpty(playerName.Value))
            {
                var maybe = GameHUD.Instance.FindPanelByName(playerName.Value);
                if (maybe != null)
                {
                    infoPanel = maybe;
                    infoPanel.SetInfo(playerName.Value, money.Value);
                    break;
                }
            }
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public override void OnStopClient()
    {
        money.OnChange -= OnMoneyChanged;
        base.OnStopClient();
    }

    private void OnMoneyChanged(int oldValue, int newValue, bool asServer)
    {
        infoPanel?.UpdateMoney(newValue);
    }

    [Server]
    public void AddMoney(int amount)
    {
        money.Value += amount;
        CheckBailout();
    }

    [Server]
    public bool TrySpendMoney(int amount)
    {
        if (money.Value < amount) return false;
        money.Value -= amount;
        return true;
    }

    [Server]
    private void CheckBailout()
    {
        if (money.Value < 0)
        {
            bailoutMarks.Value += 1;
            money.Value = 100;
            TargetNotifyBailout(Owner, bailoutMarks.Value, money.Value);
        }
    }

    [TargetRpc]
    private void TargetNotifyBailout(NetworkConnection conn, int marks, int currentMoney)
    {
        Debug.Log($"Bailout! You now have ${currentMoney} and {marks} bailout mark(s).");
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
        if (!TurnManager.Instance.CanRoll(this)) return;

        int roll = Random.Range(1, 7);
        lastRoll.Value = roll;
        RpcMoveSteps(roll);
    }

    [ServerRpc]
    public void CmdEndTurn()
    {
        if (!TurnManager.Instance.CanEndTurn(this)) return;
        TurnManager.Instance.EndTurn();
    }

    [TargetRpc]
    public void TargetStartTurn(NetworkConnection conn)
    {
        isMyTurn = true;
        var ui = GameObject.FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.BindPawn(this);
            ui.SetRollInteractable(false);
            ui.SetEndTurnInteractable(false);
        }
    }

    [TargetRpc]
    public void TargetEnableEndTurn(NetworkConnection conn, bool enable)
    {
        var ui = GameObject.FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.SetEndTurnInteractable(enable);
    }

    [TargetRpc]
    public void TargetEnableRoll(NetworkConnection conn, bool enable)
    {
        var ui = GameObject.FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.SetRollInteractable(enable);
    }

    /* ---------- Movement ---------- */
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
            currentTile = nextTile;
            yield return new WaitForSeconds(0.1f);
        }

        if (IsServer)
            HandleTileLogic();
    }

    [Server]
    private void HandleTileLogic()
    {
        var data = GameManager.Instance.boardTiles[currentTile].GetComponent<TileData>();
        if (data == null)
        {
            TurnManager.Instance.ServerOnTileActionComplete(this);
            return;
        }

        if (data.tileType == TileType.Event)
        {
            EventManager.Instance.TriggerTileEvent(this);
            return; // EventManager will call ServerOnTileActionComplete when done
        }

        if (data.tileType == TileType.Investment && data.owner == null)
        {
            TargetShowInvestmentUI(Owner, currentTile, data.companyName, data.companyCost, true);
            return; // InvestmentUI will call CmdTileActionComplete after Buy/Skip
        }

        // Normal tile
        TurnManager.Instance.ServerOnTileActionComplete(this);
    }

    [TargetRpc]
    private void TargetShowInvestmentUI(NetworkConnection conn, int tileIndex, string companyName, int cost, bool isCompany)
    {
        InvestmentUI.Instance.ShowOptions(this, tileIndex, companyName, cost, isCompany);
    }

    [TargetRpc]
    public void TargetSetTurnOrder(NetworkConnection conn, int turnIndex)
    {
        if (infoPanel != null)
        {
            infoPanel.SetTurnOrder(turnIndex + 1);
        }
        else
        {
            StartCoroutine(WaitAndSetTurnOrder(turnIndex));
        }
    }

    private IEnumerator WaitAndSetTurnOrder(int turnIndex)
    {
        float t = 2f;
        while (t > 0f && infoPanel == null)
        {
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (infoPanel != null)
            infoPanel.SetTurnOrder(turnIndex + 1);
    }

    [ObserversRpc(BufferLast = true)]
    public void RpcTeleportTo(Vector3 pos)
    {
        transform.position = pos;
    }

    /* ---------- Ownership Helpers ---------- */
    public bool HasCompanies()
    {
        foreach (var kvp in factoryPortfolio)
        {
            if (kvp.Value.sharePercent > 0)
                return true;
        }
        return false;
    }

    public bool HasMajorityCompany()
    {
        foreach (var kvp in factoryPortfolio)
        {
            if (kvp.Value.sharePercent > 60)
                return true;
        }
        return false;
    }

    public List<string> GetOwnedCompanies()
    {
        var owned = new List<string>();
        foreach (var kvp in factoryPortfolio)
        {
            if (kvp.Value.sharePercent > 0)
                owned.Add(kvp.Key);
        }
        return owned;
    }
    
    [ServerRpc]
    public void CmdTileActionComplete()
    {
        if (!TurnManager.Instance.IsCurrentPawn(this)) return;
        TurnManager.Instance.ServerOnTileActionComplete(this);
    }

}
