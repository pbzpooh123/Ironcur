using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using FishNet.Object.Synchronizing;

public class PlayerPawn : NetworkBehaviour
{
    public float moveSpeed = 4f;
    private int currentTile = 0;

    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<string> business   = new();
    public readonly SyncVar<string> country    = new();
    public readonly SyncVar<int>    lastRoll   = new();

    public bool isMyTurn = false;

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

        int targetTile = (currentTile + roll) % GameManager.Instance.TileCount;
        currentTile = targetTile;

        RpcMoveToTile(targetTile);
        Debug.Log($"{playerName.Value} rolled {roll} and moved to tile {targetTile}");

        // ✅ After movement finishes → UI lets them press End Turn
        TargetEnableEndTurn(Owner, true);
    }

    [ServerRpc]
    public void CmdEndTurn()
    {
        if (IsServer)
        {
            TurnManager.Instance.EndTurn();
        }
    }

// Called by TurnManager when it’s your turn
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

    // --- Client side ---
    [ObserversRpc]
    private void RpcMoveToTile(int tileIndex)
    {
        StopAllCoroutines();
        StartCoroutine(SmoothMove(tileIndex));
    }

    private IEnumerator SmoothMove(int tileIndex)
    {
        Vector3 target = GameManager.Instance.GetTilePosition(tileIndex);

        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }
    

    [TargetRpc]
    private void TargetEnableEndTurn(FishNet.Connection.NetworkConnection conn, bool enable)
    {
        if (enable)
            UIManager.Instance.EnableTurnUI(false, true); 
    }
}
