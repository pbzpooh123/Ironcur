using UnityEngine;
using FishNet.Object;
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
    private void CmdRollDiceAndMove()
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
    private void CmdEndTurn()
    {
        isMyTurn = false;
        TurnManager.Instance.EndTurn();
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
    public void TargetStartTurn(FishNet.Connection.NetworkConnection conn)
    {
        isMyTurn = true;
        UIManager.Instance.EnableTurnUI(true, false); 
        Debug.Log($"{playerName.Value}'s turn started!");
    }

    [TargetRpc]
    private void TargetEnableEndTurn(FishNet.Connection.NetworkConnection conn, bool enable)
    {
        if (enable)
            UIManager.Instance.EnableTurnUI(false, true); 
    }
}
