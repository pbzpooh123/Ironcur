using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PortfolioUI : MonoBehaviour
{
    public static PortfolioUI Instance;

    [Header("Panel & Layout")]
    public GameObject panel;          
    public Transform rowsParent;        
    public GameObject rowPrefab;       
    
    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text summaryText;
    public TMP_Text cashText; 

    private PlayerPawn _current;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    public void Show(PlayerPawn pawn)
    {
        _current = pawn;
        Refresh();
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Refresh()
{
    if (_current == null || _current.factoryPortfolio == null) return;

    // Clear old rows
    for (int i = rowsParent.childCount - 1; i >= 0; i--)
        Destroy(rowsParent.GetChild(i).gameObject);

    // Gather items we can display (must have >0% and a known company record to get baseCost)
    var items = new List<(string company, int percent, float multiplier, int baseCost)>();

    foreach (var kv in _current.factoryPortfolio)
    {
        string companyName = kv.Key;
        var rec = kv.Value;
        if (rec.sharePercent <= 0) continue;

        // look up base cost from MarketManager
        int baseCost = 0;
        if (MarketManager.Instance != null &&
            MarketManager.Instance.companies.TryGetValue(companyName, out var comp) &&
            comp != null)
        {
            baseCost = comp.baseCost;
        }

        items.Add((companyName, rec.sharePercent, Mathf.Max(0.01f, rec.multiplier), baseCost));
    }

    // Sort by percent desc, then name
    items.Sort((a, b) =>
    {
        int pc = b.percent.CompareTo(a.percent);
        return pc != 0 ? pc : string.Compare(a.company, b.company, System.StringComparison.Ordinal);
    });

    // Build rows + totals
    int totalPercent = 0;
    int totalEstPayout = 0;
    

    foreach (var it in items)
    {
        var go = Instantiate(rowPrefab, rowsParent);
        var row = go.GetComponent<PortfolioRow>();
        if (row != null)
            row.Bind(it.company, it.percent, it.multiplier, it.baseCost);

        // recompute estimated payout to accumulate
        int baseIncome      = Mathf.RoundToInt(it.baseCost * 0.1f);
        float ownRatio      = Mathf.Clamp01(it.percent / 100f);
        int est             = Mathf.RoundToInt(baseIncome * ownRatio * it.multiplier);

        totalPercent += it.percent;
        totalEstPayout += est;
    }

    if (titleText)
        titleText.text = $"{_current.playerName.Value}'s Portfolio";

    if (summaryText)
        summaryText.text = $"{items.Count} companies • Total % = {totalPercent} • Est. payout = ${totalEstPayout}";

    if (cashText)
        cashText.text = $"Cash: ${_current.money.Value}";
}
    
    public void OnCloseClicked() => Hide();
    public void OnRefreshClicked() => Refresh();
}
