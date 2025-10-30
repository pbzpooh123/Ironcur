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

    [Header("Forced Buy Bar")]
    [Tooltip("TMP_InputField for entering the percent to force-buy.")]
    public TMP_InputField forcedBuyPercentInput;
    [Tooltip("Button to execute forced buy for the visible company.")]
    public Button forcedBuyButton;
    [Tooltip("Optional label to show remaining per-turn forced-buy budget, if you expose it via a getter.")]
    public TMP_Text forcedBuyPriceText;
    [Range(1.0f, 3.0f)] public float forcedBuyPremium = 2.5f;

    private PlayerPawn currentPawn;

    // Companies we can target this turn
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

        if (forcedBuyButton != null)
        {
            forcedBuyButton.onClick.RemoveAllListeners();
            forcedBuyButton.onClick.AddListener(OnClickForcedBuy);
        }

        if (forcedBuyPercentInput != null)
        {
            forcedBuyPercentInput.onValueChanged.RemoveAllListeners();
            forcedBuyPercentInput.onValueChanged.AddListener(_ => UpdateForcedBuyUI());
        }
    }

    public void Show(PlayerPawn pawn)
    {
        currentPawn = pawn;
        panel.SetActive(true);
        Refresh();

        // Lock roll/end buttons during proposal
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
            if (c.owner == null || c.owner == currentPawn) continue;                // must belong to someone else
            if (c.GetOwnership(currentPawn) >= 100) continue;                       // you already have 100%
            if (c.GetOwnership(c.owner) <= 0) continue;                             // owner must still have some
            if (MarketManager.Instance.HasSubmittedThisTurn(currentPawn, c.companyName))
                continue;                                                           // you already proposed to this one

            _itemsCache.Add(c);
        }

        _pageIndex = Mathf.Clamp(_pageIndex, 0, GetMaxPageIndex());
        RenderPageOnly();
        UpdateForcedBuyBarState();
        UpdateForcedBuyUI(); 
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

        int pageSize = Mathf.Max(1, rowsPerPage);
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
        UpdateForcedBuyBarState();
        UpdateForcedBuyUI(); 
    }

    private void OnNextPage()
    {
        if (_itemsCache.Count == 0) return;
        _pageIndex = Mathf.Min(GetMaxPageIndex(), _pageIndex + 1);
        RenderPageOnly();
        UpdateForcedBuyBarState();
        UpdateForcedBuyUI(); 
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

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);

        // Tell server only if we're the current pawn and still in Proposal phase
        var local = FindLocalOwnedPawn();
        if (local != null && TurnManager.Instance.IsCurrentPawn(local) &&
            TurnManager.Instance.InProposalPhaseFor(local))
        {
            MarketManager.Instance.CmdNotifyProposalClosed();
        }
    }

    private PlayerPawn FindLocalOwnedPawn()
    {
        foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
            if (p != null && p.IsOwner) return p;
        return null;
    }

    public void ShowFirstTimeHint()
    {
        if (hintPanel == null) return;
        hintTitle.text = "How proposals work";
        hintBody.text =
            "• Pick a company you don’t own.\n" +
            "• Enter % and price you’ll pay.\n" +
            "• You can’t exceed 100% total.\n" +
            "• Owner can’t sell more than they own.\n" +
            "• If accepted, shares move and cash transfers.\n" +
            "• Or use Forced Buy to instantly acquire % at a Absurd cost.";
        hintPanel.SetActive(true);
    }

    public void OnHintGotIt() => hintPanel?.SetActive(false);

    /* ================== Forced Buy bar ================== */

    private void UpdateForcedBuyBarState()
    {
        bool hasTarget = (_itemsCache.Count > 0);
        if (forcedBuyButton != null) forcedBuyButton.interactable = hasTarget;
        if (forcedBuyPercentInput != null)
        {
            if (string.IsNullOrWhiteSpace(forcedBuyPercentInput.text))
                forcedBuyPercentInput.text = "10"; // sensible default
        }
    }

    private void OnClickForcedBuy()
    {
        UpdateForcedBuyUI(); 

        var comp = GetVisibleCompany();
        if (comp == null) return;

        int pct = 10;
        if (forcedBuyPercentInput && !int.TryParse(forcedBuyPercentInput.text, out pct))
            pct = 10;
        pct = Mathf.Clamp(pct, 1, 100);

        MarketManager.Instance.CmdForceBuy(comp.companyName, pct);

        if (forcedBuyButton)
        {
            forcedBuyButton.interactable = false;
            StartCoroutine(ReenableForcedBuySoon());
        }
    }


    private CompanyRecord GetVisibleCompany()
    {
        if (_itemsCache.Count == 0) return null;
        int idx = Mathf.Clamp(_pageIndex, 0, _itemsCache.Count - 1);
        return _itemsCache[idx];
    }

    private void UpdateForcedBuyUI()
    {
        var comp = GetVisibleCompany();
        bool hasTarget = comp != null;

        if (forcedBuyButton) forcedBuyButton.interactable = hasTarget;

        if (!hasTarget)
        {
            if (forcedBuyPriceText) forcedBuyPriceText.text = "Price: —";
            return;
        }

        // parse desired %
        int pct = 10;
        if (forcedBuyPercentInput && !int.TryParse(forcedBuyPercentInput.text, out pct))
            pct = 10;
        pct = Mathf.Clamp(pct, 1, 100);

        // transferable clamp (seller has %, buyer has room)
        var owner = comp.owner;
        int sellerAvail = (owner != null) ? comp.GetOwnership(owner) : 0;
        int buyerHas    = comp.GetOwnership(currentPawn);
        int buyerRoom   = Mathf.Max(0, 100 - buyerHas);
        int transferable = Mathf.Min(pct, sellerAvail, buyerRoom);

        // price = currentPrice × (%/100) × premium
        int currentPrice = Mathf.Max(1, comp.currentPrice);
        float baseF = currentPrice * (transferable / 100f);
        float finalF = baseF * Mathf.Max(1f, forcedBuyPremium);
        int finalPrice = Mathf.Max(1, Mathf.RoundToInt(finalF));

        if (forcedBuyPriceText)
        {
            if (transferable <= 0)
                forcedBuyPriceText.text = "Price: — (no transferable %)";
            else
                forcedBuyPriceText.text =
                    $"Price: ${finalPrice}M  ({currentPrice} × {transferable}% × {forcedBuyPremium:0.##})";
        }

        if (forcedBuyButton) forcedBuyButton.interactable = (transferable > 0);
    }

    private System.Collections.IEnumerator ReenableForcedBuySoon()
    {
        yield return new WaitForSecondsRealtime(0.25f);
        if (forcedBuyButton != null) forcedBuyButton.interactable = true;
    }
}
