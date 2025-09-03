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
    
   
    public bool isMyTurn = false;

    private void Update()
    {
        // ✅ Only let the owner client check input
        if (!IsOwner) return;

        if (isMyTurn && Input.GetKeyDown(KeyCode.Space))
        {
            CmdRollDiceAndMove();
        }
    }

    // 🖥️ Client → Server: request to roll
    [ServerRpc]
    private void CmdRollDiceAndMove()
    {
        int roll = Random.Range(1, 7);
        int targetTile = (currentTile + roll) % GameManager.Instance.TileCount;
        currentTile = targetTile;

        RpcMoveToTile(targetTile);
        Debug.Log($"{playerName.Value} rolled {roll} and moved to tile {targetTile}");
    }

    // 🌐 Server → All clients: play movement
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
}