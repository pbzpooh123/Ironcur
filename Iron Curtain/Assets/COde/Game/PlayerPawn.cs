using UnityEngine;
using FishNet.Object;
using System.Collections;
using FishNet.Object.Synchronizing;

public class PlayerPawn : NetworkBehaviour
{
    public float moveSpeed = 4f;
    private int currentTile = 0;
    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<string> business = new();
    public readonly SyncVar<string> country = new();
    [SerializeField] private string _playerName;
    [SerializeField] private string _business;
    [SerializeField] private string _country;
    
    [ServerRpc]
    public void RollDiceAndMove()
    {
        int roll = Random.Range(1, 7);
        int targetTile = (currentTile + roll) % GameManager.Instance.TileCount;
        currentTile = targetTile;

        RpcMoveToTile(targetTile);
    }

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