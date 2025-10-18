using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LeaderboardSceneController : MonoBehaviour
{
    [Header("UI")]
    public Transform contentParent;     // Vertical list container
    public GameObject rowPrefab;        // Prefab with LeaderboardRow
    public int topCount = 25;

    private async void OnEnable()
    {
        await RefreshAsync();
    }

    public async void OnRefreshButton()
    {
        await RefreshAsync();
    }

    public void OnBackButton()
    {
        // Load your main menu/lobby scene name
        SceneManager.LoadScene("MainMenu");
    }

    private async Task RefreshAsync()
    {
        if (UGSLeaderboard.Instance == null)
        {
            Debug.LogWarning("UGSLeaderboard missing in scene. Add it to this scene as well.");
            return;
        }

        // Clear old
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);

        List<UGSLeaderboard.Entry> top;
        try
        {
            top = await UGSLeaderboard.Instance.GetTopAsync(topCount);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GetTop failed: {e.Message}");
            return;
        }

        foreach (var e in top)
        {
            var go = Instantiate(rowPrefab, contentParent);
            var row = go.GetComponent<LeaderboardRow>();
            if (row != null) row.Bind(e.Rank, e.Name, e.Score);
        }
    }
}
