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

        // subscribe once so UI auto-refreshes on pushes
        _current.OnClientPortfolioChanged -= Refresh;
        _current.OnClientPortfolioChanged += Refresh;

        // ask server for a fresh snapshot for THIS viewer
        if (_current.IsServerInitialized)
        {
            // if you’re host, you already have state; still fine to request
            _current.CmdRequestPortfolioForViewer(_current.Owner);
        }
        else
        {
            _current.CmdRequestPortfolioForViewer(); // viewer’s connection is passed by FishNet
        }

        Refresh();
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (_current != null) _current.OnClientPortfolioChanged -= Refresh;
        if (panel != null) panel.SetActive(false);
    }

    public void Refresh()
    {
        if (_current == null) return;

        // Clear old rows
        for (int i = rowsParent.childCount - 1; i >= 0; i--)
            Destroy(rowsParent.GetChild(i).gameObject);

        // Use the client snapshot (kept up-to-date by RPCs)
        var items = _current.GetClientPortfolioSnapshot();

        // Sort by percent desc, then name
        items.Sort((a, b) =>
        {
            int pc = b.percent.CompareTo(a.percent);
            return pc != 0 ? pc : string.Compare(a.company, b.company, System.StringComparison.Ordinal);
        });

        int totalPercent = 0;
        int totalEstPayout = 0;

        foreach (var it in items)
        {
            var go = Instantiate(rowPrefab, rowsParent);
            var row = go.GetComponent<PortfolioRow>();
            int baseCost = 0;

            if (MarketManager.Instance != null &&
                MarketManager.Instance.companies.TryGetValue(it.company, out var comp) &&
                comp != null)
            {
                baseCost = comp.baseCost;
            }

            row?.Bind(it.company, it.percent, Mathf.Max(0.01f, it.multiplier), baseCost);

            int baseIncome = Mathf.RoundToInt(baseCost * 0.1f);
            float ownRatio = Mathf.Clamp01(it.percent / 100f);
            int est = Mathf.RoundToInt(baseIncome * ownRatio * it.multiplier);

            totalPercent += it.percent;
            totalEstPayout += est;
        }

        if (titleText) titleText.text = $"{_current.playerName.Value}'s Portfolio";
        if (summaryText) summaryText.text = $"{items.Count} companies • Total % = {totalPercent} • Est. payout = ${totalEstPayout}";
        if (cashText) cashText.text = $"Cash: ${_current.money.Value}";
    }


    public void OnCloseClicked() => Hide();
    public void OnRefreshClicked() => Refresh();
}
