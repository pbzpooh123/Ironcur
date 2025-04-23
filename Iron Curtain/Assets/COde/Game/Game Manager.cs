using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Scened;
using System.Linq; // Needed for Count()


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
    private List<PlayerInvestment> pendingInvestments = new List<PlayerInvestment>();
    
    [Header("Investments")]
    public List<InvestmentOption> AvailableInvestments; // drag & drop in Inspector
    public List<DynamicInvestment> CurrentInvestments = new(); // runtime data
    private List<PlayerInvestment> investmentHistory = new(); // all previous investments

    public bool warActive; // example event toggle

    
    



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
        
        foreach (var inv in pendingInvestments)
        {
            float roll = Random.Range(0f, 1f);
            bool success = roll <= inv.investment.successChance;

            if (success)
            {
                inv.player.profit.Value += inv.investment.reward;
                Debug.Log($"{inv.player.playerName.Value} succeeded in {inv.investment.investmentName}, gained ${inv.investment.reward}");
            }
            else
            {
                inv.player.profit.Value -= inv.investment.failurePenalty;
                Debug.Log($"{inv.player.playerName.Value} failed in {inv.investment.investmentName}, lost ${inv.investment.failurePenalty}");
            }

            inv.player.TargetUpdateProfit(inv.player.Owner, inv.player.profit.Value);
        }

        pendingInvestments.Clear();


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
    
    public struct PlayerInvestment
    {
        public NetworkLobbyPlayer player;
        public InvestmentOption investment;
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
    
    public void ReceiveInvestment(NetworkLobbyPlayer player, string investmentName)
    {
        var option = AvailableInvestments.Find(i => i.investmentName == investmentName);
        if (option == null)
        {
            Debug.LogError("Invalid investment: " + investmentName);
            return;
        }

        // Remove cost
        player.profit.Value -= option.cost;

        // Store for round end
        pendingInvestments.Add(new PlayerInvestment
        {
            player = player,
            investment = option
        });

        Debug.Log($"{player.playerName.Value} invested in {investmentName}");
    }
    
    public void GenerateDynamicInvestments()
    {
        CurrentInvestments.Clear();

        foreach (var baseOption in AvailableInvestments)
        {
            float cost = baseOption.cost;
            float chance = baseOption.successChance;

            int timesBought = investmentHistory.Count(i => i.investment.investmentName == baseOption.investmentName);

            // Example: demand mechanic
            if (timesBought >= 2)
            {
                cost += 200f;
                chance -= 0.05f;
            }
            else if (timesBought == 0)
            {
                cost -= 100f;
                chance += 0.05f;
            }

            // Example: war boosts weapons
            if (baseOption.investmentName == "Weapons" && warActive)
            {
                cost += 100f;
                chance += 0.1f;
            }

            // Add other event effects here...

            CurrentInvestments.Add(new DynamicInvestment
            {
                template = baseOption,
                currentCost = Mathf.Max(0, cost),
                currentSuccessChance = Mathf.Clamp01(chance)
            });
        }
    }



    [ObserversRpc]
    void RpcUpdateTimer(float timeLeft)
    {
        GameUI.Instance?.UpdateCountdown(timeLeft);
    }
}
