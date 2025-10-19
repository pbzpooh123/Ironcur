using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LeaderboardSceneController : MonoBehaviour
{
    [Header("UGS")]
    [Tooltip("How many total entries to fetch from UGS. Keep this reasonable (e.g., 100).")]
    public int fetchCount = 100;

    [Header("Layout (no scroll)")]
    public Transform leftColumn;     // assign LeftColumn
    public Transform rightColumn;    // assign RightColumn
    public GameObject rowPrefab;     // prefab with LeaderboardRow

    [Header("Paging")]
    public int rowsPerColumn = 5;    // 5 per side
    public TMP_Text pageText;        // optional "Page X / Y" label

    private const int MinFetch = 10;
    private List<UGSLeaderboard.Entry> _cache = new();
    private int _pageIndex = 0;      // 0-based
    private int PageSize => Mathf.Max(1, rowsPerColumn) * 2; // two columns

    private async void OnEnable()
    {
        await RefreshFromServer();
    }

    public async void OnRefreshButton()
    {
        await RefreshFromServer();
    }

    public void OnBackButton()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void OnPrevPage()
    {
        if (_cache.Count == 0) return;
        _pageIndex = Mathf.Max(0, _pageIndex - 1);
        RenderPage();
    }

    public void OnNextPage()
    {
        if (_cache.Count == 0) return;
        _pageIndex = Mathf.Min(GetMaxPageIndex(), _pageIndex + 1);
        RenderPage();
    }

    private async Task RefreshFromServer()
    {
        if (UGSLeaderboard.Instance == null)
        {
            Debug.LogWarning("UGSLeaderboard missing in scene.");
            return;
        }

        // fetch (at least one page)
        int toFetch = Mathf.Max(fetchCount, MinFetch);
        try
        {
            _cache = await UGSLeaderboard.Instance.GetTopAsync(toFetch);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GetTop failed: {e.Message}");
            _cache = new List<UGSLeaderboard.Entry>();
        }

        _pageIndex = 0;
        RenderPage();
    }

    private void RenderPage()
    {
        // Clear old
        ClearChildren(leftColumn);
        ClearChildren(rightColumn);

        if (_cache == null || _cache.Count == 0)
        {
            UpdatePageLabel(0, 0);
            return;
        }

        int start = _pageIndex * PageSize;
        int endExclusive = Mathf.Min(start + PageSize, _cache.Count);

        // Fill left first, then right
        int half = Mathf.Min(rowsPerColumn, endExclusive - start);
        int leftCount = Mathf.Min(rowsPerColumn, endExclusive - start);
        int rightStart = start + leftCount;
        int rightCount = Mathf.Min(rowsPerColumn, endExclusive - rightStart);

        // Left column
        for (int i = 0; i < leftCount; i++)
            SpawnRow(leftColumn, _cache[start + i]);

        // Right column
        for (int i = 0; i < rightCount; i++)
            SpawnRow(rightColumn, _cache[rightStart + i]);

        UpdatePageLabel(_pageIndex + 1, GetMaxPageIndex() + 1);
    }

    private void SpawnRow(Transform parent, UGSLeaderboard.Entry e)
    {
        var go = Instantiate(rowPrefab, parent);
        if (!go.activeSelf) go.SetActive(true);
        var row = go.GetComponent<LeaderboardRow>();
        if (row != null) row.Bind(e.Rank, e.Name, e.Score);
    }

    private void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    private int GetMaxPageIndex()
    {
        if (_cache == null || _cache.Count == 0) return 0;
        return Mathf.Max(0, (_cache.Count - 1) / PageSize);
    }

    private void UpdatePageLabel(int current, int total)
    {
        if (pageText != null)
            pageText.text = (total <= 0) ? "Page 0 / 0" : $"Page {current} / {total}";
    }
}
