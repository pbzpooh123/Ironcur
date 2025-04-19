using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Scened;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public int currentRound = 1;
    public int maxRounds = 10;
    public float turnDuration = 60f;

    private float timer;
    private bool turnActive;
    private bool _hasInitialized = false;

    private List<NetworkLobbyPlayer> players = new List<NetworkLobbyPlayer>();

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Wait until the scene is fully loaded and all players are spawned
        InstanceFinder.SceneManager.OnLoadEnd += OnGameSceneLoaded;
    }

    private void OnGameSceneLoaded(SceneLoadEndEventArgs args)
    {
        if (!IsServer || _hasInitialized) return;

        _hasInitialized = true;
        InstanceFinder.SceneManager.OnLoadEnd -= OnGameSceneLoaded;

        players.Clear();

        foreach (var conn in InstanceFinder.ServerManager.Clients)
        {
            if (conn.Value?.FirstObject != null &&
                conn.Value.FirstObject.TryGetComponent(out NetworkLobbyPlayer player))
            {
                players.Add(player);
            }
        }

        Debug.Log($"✅ Players loaded into game scene: {players.Count}");

        StartRound();
    }

    private void Update()
    {
        if (!IsServer || !turnActive) return;

        timer -= Time.deltaTime;
        RpcUpdateTimer(Mathf.Ceil(timer));

        if (timer <= 0f)
        {
            EndRound();
        }
    }

    void StartRound()
    {
        Debug.Log($"🟢 Starting Round {currentRound}");
        timer = turnDuration;
        turnActive = true;
    }

    void EndRound()
    {
        Debug.Log($"🛑 Ending Round {currentRound}");

        turnActive = false;

        // Apply random profit gain
        foreach (var p in players)
        {
            float gain = Random.Range(1000f, 5000f);
            p.profit.Value += gain;
            p.TargetUpdateProfit(p.Owner, p.profit.Value);
        }

        currentRound++;

        if (currentRound > maxRounds)
        {
            EndGame();
        }
        else
        {
            StartRound();
        }
    }

    void EndGame()
    {
        Debug.Log("🏁 Game over!");

        NetworkLobbyPlayer winner = null;
        float bestProfit = float.MinValue;

        foreach (var p in players)
        {
            if (p.profit.Value > bestProfit)
            {
                bestProfit = p.profit.Value;
                winner = p;
            }
        }

        foreach (var p in players)
        {
            p.TargetShowEndScreen(p.Owner, winner.playerName.Value);
        }
    }

    [ObserversRpc]
    void RpcUpdateTimer(float timeLeft)
    {
        GameUI.Instance?.UpdateCountdown(timeLeft);
    }
}
