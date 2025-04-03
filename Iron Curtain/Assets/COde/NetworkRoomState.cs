using Mirror;
using UnityEngine;

public class NetworkRoomState : NetworkBehaviour
{
    public static NetworkRoomState instance;

    [SyncVar(hook = nameof(OnRoomCodeChanged))]
    public string roomCode;

    private void Awake()
    {
        instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        roomCode = GenerateRoomCode();
    }

    private void OnRoomCodeChanged(string oldCode, string newCode)
    {
        Debug.Log($"Room code updated: {newCode}");
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] code = new char[6];
        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[Random.Range(0, chars.Length)];
        }
        return new string(code);
    }
}