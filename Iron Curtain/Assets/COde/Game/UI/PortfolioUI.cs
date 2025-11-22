using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using FishNet;                   // ← NEW
using FishNet.Connection;       // ← NEW

public class PortfolioUI : MonoBehaviour
{
    public static PortfolioUI Instance;

    [Header("Panel & Layout")]
    public GameObject panel;            // root panel
    public Transform rowsParent;        // single column container
    public GameObject rowPrefab;        // PortfolioRow prefab

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text summaryText;
    public TMP_Text cashText;

    [Header("Paging")]
    [Tooltip("How many rows to show per page")]
    public int rowsPerPage = 3;
    public TMP_Text pageText;           // optional "Page X / Y" text

    private PlayerPawn _current;

    // cache for paging
    private List<(string company, float percent, float multiplier, int currentPrice)> _itemsCache
        = new List<(string, float, float, int)>();
    private int _pageIndex = 0; // 0-based

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_current != null)
            _current.OnClientPortfolioChanged -= RefreshFromSnapshot;
    }

    // === NEW: explicit entrypoint for a chosen player (from InfoPanel button) ===
    public void ShowForPawn(PlayerPawn pawn, string displayName)
    {
        if (pawn == null) return;

        // unsubscribe old
        if (_current != null && _current != pawn)
            _current.OnClientPortfolioChanged -= RefreshFromSnapshot;

        _current = pawn;

        // subscribe so UI auto-refreshes on pushes
        _current.OnClientPortfolioChanged -= RefreshFromSnapshot;
        _current.OnClientPortfolioChanged += RefreshFromSnapshot;

        // request a fresh snapshot to THIS viewer (local client)
        var viewerConn = InstanceFinder.ClientManager != null ? InstanceFinder.ClientManager.Connection : null;
        if (viewerConn != null)
            _current.CmdRequestPortfolioForViewer(viewerConn);

        // header
        if (titleText) titleText.text = $"{displayName}'s Portfolio";

        // build from current snapshot immediately
        RefreshFromSnapshot();

        if (panel != null) panel.SetActive(true);
    }

    // (kept for compatibility if something else still calls Show)
    public void Show(PlayerPawn pawn) => ShowForPawn(pawn, pawn != null ? pawn.playerName.Value : "—");

    public void Hide()
    {
        if (_current != null)
            _current.OnClientPortfolioChanged -= RefreshFromSnapshot;

        if (panel != null)
            panel.SetActive(false);

        _current = null;

        // optional: clear list
        if (rowsParent != null)
        {
            for (int i = rowsParent.childCount - 1; i >= 0; i--)
                Destroy(rowsParent.GetChild(i).gameObject);
        }
        _itemsCache.Clear();
        UpdatePageLabel(0, 0);
    }

    // === Paging controls ===
    public void OnPrevPage()
    {
        if (_itemsCache.Count == 0) return;
        _pageIndex = Mathf.Max(0, _pageIndex - 1);
        RenderPageOnly();
    }

    public void OnNextPage()
    {
        if (_itemsCache.Count == 0) return;
        _pageIndex = Mathf.Min(GetMaxPageIndex(), _pageIndex + 1);
        RenderPageOnly();
    }

    public void OnRefreshClicked()
    {
        // Re-request to be safe (optional)
        var viewerConn = InstanceFinder.ClientManager != null ? InstanceFinder.ClientManager.Connection : null;
        if (_current != null && viewerConn != null)
            _current.CmdRequestPortfolioForViewer(viewerConn);

        RefreshFromSnapshot(); 
    }

    public void RefreshFromSnapshot()
    {
        if (_current == null) return;

        var snap = _current.GetClientPortfolioSnapshot();

        _itemsCache.Clear();

        foreach (var it in snap)
        {
            int currentPrice = 0;
            float aura = 1f;

            if (MarketManager.Instance != null &&
                MarketManager.Instance.companies.TryGetValue(it.company, out var comp) &&
                comp != null)
            {
                currentPrice = comp.currentPrice;  
                aura = Mathf.Max(0f, comp.payoutMult); 
            }

            float personal = Mathf.Max(0.01f, it.multiplier);
            float effective = personal * Mathf.Max(0.01f, aura);

            _itemsCache.Add((it.company, it.percent, effective, currentPrice));
        }

        _itemsCache.Sort((a, b) =>
        {
            int pc = b.percent.CompareTo(a.percent);
            return pc != 0 ? pc : string.Compare(a.company, b.company, System.StringComparison.Ordinal);
        });

        float yieldPct = 0.10f;
        if (MarketManager.Instance != null)
            yieldPct = Mathf.Clamp01(MarketManager.Instance.dividendYield);

        float totalPercent = 0f;
        int totalEstPayout = 0;

        foreach (var it in _itemsCache)
        {
            int baseIncome = Mathf.RoundToInt(it.currentPrice * yieldPct);
            float ownRatio = Mathf.Clamp01(it.percent / 100f);
            int est = Mathf.RoundToInt(baseIncome * ownRatio * it.multiplier); 

            totalPercent += it.percent; 
            totalEstPayout += est;
        }

        if (summaryText)
            summaryText.text =
                $"{_itemsCache.Count} บริษัท • จำนวนหุ้นทั้งหมด = {totalPercent:0.0}% • ประมาณการจ่าย = ${totalEstPayout}";

        if (cashText)
            cashText.text = $"เงิน: ${_current.money.Value}";

        _pageIndex = 0;
        RenderPageOnly();
    }

    private void RenderPageOnly()
    {
        for (int i = rowsParent.childCount - 1; i >= 0; i--)
            Destroy(rowsParent.GetChild(i).gameObject);

        if (_itemsCache.Count == 0)
        {
            UpdatePageLabel(0, 0);
            return;
        }

        int pageSize = Mathf.Max(1, rowsPerPage);
        int start = _pageIndex * pageSize;
        int endExclusive = Mathf.Min(start + pageSize, _itemsCache.Count);

        for (int i = start; i < endExclusive; i++)
        {
            var it = _itemsCache[i];
            var go = Instantiate(rowPrefab, rowsParent);
            if (!go.activeSelf) go.SetActive(true);

            var row = go.GetComponent<PortfolioRow>();
            row?.Bind(it.company, it.percent, it.multiplier, it.currentPrice);
        }

        UpdatePageLabel(_pageIndex + 1, GetMaxPageIndex() + 1);
    }

    private int GetMaxPageIndex()
    {
        int pageSize = Mathf.Max(1, rowsPerPage);
        return (_itemsCache.Count == 0) ? 0 : Mathf.Max(0, (_itemsCache.Count - 1) / pageSize);
    }

    private void UpdatePageLabel(int current, int total)
    {
        if (pageText != null)
            pageText.text = (total <= 0) ? "Page 0 / 0" : $"Page {current} / {total}";
    }
}
