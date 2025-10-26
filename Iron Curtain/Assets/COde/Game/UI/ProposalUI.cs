using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProposalUI : MonoBehaviour
{
    public static ProposalUI Instance;

    [Header("Refs")]
    public GameObject panel;
    public Transform listParent;
    public GameObject proposalEntryPrefab;
    public Button closeButton;

    [Header("Hint")]
    public GameObject hintPanel;
    public TMP_Text hintTitle;
    public TMP_Text hintBody;

    [Header("Paging")]
    [Tooltip("How many rows to show per page (set to 1 to show one row per page).")]
    public int rowsPerPage = 1;
    public TMP_Text pageText;
    public Button prevButton;
    public Button nextButton;

    private PlayerPawn currentPawn;

    // Cache: the list of companies we can propose to (CompanyRecord)
    private readonly List<CompanyRecord> _itemsCache = new();
    private int _pageIndex = 0;

    private void Awake()
    {
        Instance = this;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseAndNotifyServer);
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(OnPrevPage);
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextPage);
        }
    }

    public void Show(PlayerPawn pawn)
    {
        currentPawn = pawn;
        panel.SetActive(true);
        Refresh();

        if (TurnManager.Instance != null && TurnManager.Instance.IsServerInitialized)
            TurnManager.Instance.InProposalPhaseFor(pawn);

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.SetEndTurnInteractable(false);
        if (ui != null) ui.SetRollInteractable(false);
    }

    public void Refresh()
    {
        if (panel == null || !panel.activeSelf) return;
        if (MarketManager.Instance == null || currentPawn == null) return;

    
        _itemsCache.Clear();

        foreach (var kv in MarketManager.Instance.companies)
        {
            var c = kv.Value;
            if (c == null) continue;
            if (c.owner == null || c.owner == currentPawn) continue;              // must belong to someone else
            if (c.GetOwnership(currentPawn) >= 100) continue;                     // you already have 100%
            if (c.GetOwnership(c.owner) <= 0) continue;
            if (MarketManager.Instance.HasSubmittedThisTurn(currentPawn, c.companyName))
                continue;

            _itemsCache.Add(c);
        }

        _pageIndex = 0;
        RenderPageOnly();
    }

    private void RenderPageOnly()
    {
        // clear rows
        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        if (_itemsCache.Count == 0)
        {
            UpdatePageLabel(0, 0);
            SetPagerInteractable(false);
            return;
        }

        int pageSize = Mathf.Max(1, rowsPerPage); // set to 1 for single row
        int start = _pageIndex * pageSize;
        int endExclusive = Mathf.Min(start + pageSize, _itemsCache.Count);

        for (int i = start; i < endExclusive; i++)
        {
            var comp = _itemsCache[i];
            var entryGO = Instantiate(proposalEntryPrefab, listParent);
            var ui = entryGO.GetComponent<ProposalEntry>();
            if (ui != null)
                ui.Setup(comp, currentPawn);
        }

        UpdatePageLabel(_pageIndex + 1, GetMaxPageIndex() + 1);
        SetPagerInteractable(true);
    }

    private void OnPrevPage()
    {
        if (_itemsCache.Count == 0) return;
        _pageIndex = Mathf.Max(0, _pageIndex - 1);
        RenderPageOnly();
    }

    private void OnNextPage()
    {
        if (_itemsCache.Count == 0) return;
        _pageIndex = Mathf.Min(GetMaxPageIndex(), _pageIndex + 1);
        RenderPageOnly();
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
        if (prevButton != null) prevButton.interactable = (_itemsCache.Count > 0 && _pageIndex > 0);
        if (nextButton != null) nextButton.interactable = (_itemsCache.Count > 0 && _pageIndex < GetMaxPageIndex());
    }

    private void SetPagerInteractable(bool on)
    {
        if (prevButton != null) prevButton.gameObject.SetActive(on);
        if (nextButton != null) nextButton.gameObject.SetActive(on);
        if (pageText != null) pageText.gameObject.SetActive(on);
    }

    private void CloseAndNotifyServer()
    {
        panel.SetActive(false);
        MarketManager.Instance.CmdNotifyProposalClosed();
    }

    public ProposalEntry FindEntryForCompany(string companyName)
    {
        foreach (Transform t in listParent)
        {
            var e = t.GetComponent<ProposalEntry>();
            if (e != null && e.CompanyKey == companyName)
                return e;
        }
        return null;
    }

    public void Hide() => CloseAndNotifyServer();

    public void ShowFirstTimeHint()
    {
        if (hintPanel == null) return;
        hintTitle.text = "How proposals work";
        hintBody.text =
            "• Pick a company you don’t own.\n" +
            "• Enter % and price you’ll pay.\n" +
            "• You can’t exceed 100% total.\n" +
            "• Owner can’t sell more than they own.\n" +
            "• If accepted, shares move and cash transfers.";
        hintPanel.SetActive(true);
    }

    public void OnHintGotIt() => hintPanel?.SetActive(false);
}
