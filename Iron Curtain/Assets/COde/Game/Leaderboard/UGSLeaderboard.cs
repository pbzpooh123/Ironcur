using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;

public class UGSLeaderboard : MonoBehaviour
{
    public static UGSLeaderboard Instance { get; private set; }

    [Header("UGS Leaderboard")]
    [Tooltip("Leaderboard ID from Unity Dashboard")]
    public string leaderboardId = "main_leaderboard";

    private bool _ready;

    private async void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        await EnsureReadyAsync();
    }

    private async Task EnsureReadyAsync()
    {
        if (_ready) return;

        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        _ready = true;
        Debug.Log("[UGSLeaderboard] Ready.");
    }

    /// <summary>
    /// Submit this client user's score. We set the player name through Authentication.
    /// </summary>
    public async Task SubmitMyScoreAsync(long score, string displayName)
    {
        await EnsureReadyAsync();

        // Set (or update) the player's display name
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            try
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(displayName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGSLeaderboard] Could not set player name: {e.Message}");
            }
        }

        // Most recent SDKs take (id, double score[, options]).
        // We won't pass metadata to avoid signature mismatches across versions.
        await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, (double)score);

        Debug.Log($"[UGSLeaderboard] Submitted score {score} for '{displayName}'.");
    }

    public class Entry
    {
        public int Rank;      // 1-based rank for UI
        public string Name;   // PlayerName (falls back to PlayerId)
        public long Score;
    }

    public async Task<List<Entry>> GetTopAsync(int limit = 25)
    {
        await EnsureReadyAsync();

        var page = await LeaderboardsService.Instance.GetScoresAsync(
            leaderboardId,
            new GetScoresOptions { Limit = limit }
        );

        var list = new List<Entry>(page.Results.Count);
        foreach (var r in page.Results)
        {
            // Use PlayerName if available; otherwise fall back to PlayerId.
            string name = !string.IsNullOrWhiteSpace(r.PlayerName) ? r.PlayerName : r.PlayerId;

            list.Add(new Entry
            {
                Rank = r.Rank + 1,          // SDK rank is 0-based
                Name = name,
                Score = (long)r.Score       // r.Score is double
            });
        }

        return list;
    }
}
