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
    [Tooltip("Label that shows the exact price the server will charge.")]
    public TMP_Text forcedBuyPriceText;

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

        // keep the price text in sync with cash (surcharge is % of cash)
        if (currentPawn != null)
        {
            currentPawn.money.OnChange -= OnLocalMoneyChanged;
            currentPawn.money.OnChange += OnLocalMoneyChanged;
        }
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
            if (c.owner == null || c.owner == currentPawn) continue;      // must belong to someone else
            if (c.GetOwnership(currentPawn) >= 100) continue;             // you already have 100%
            if (c.GetOwnership(c.owner) <= 0) continue;                   // owner must still have some
            if (MarketManager.Instance.HasSubmittedThisTurn(currentPawn, c.companyName))
                continue;                                                 // already proposed to this one

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

    public void CloseAndNotifyServer()
    {
        Hide(); // also unhooks money change
        if (MarketManager.Instance != null)
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

        if (currentPawn != null)
            currentPawn.money.OnChange -= OnLocalMoneyChanged;

        // Tell server only if we're the current pawn and still in Proposal phase
        var local = FindLocalOwnedPawn();
        if (local != null && TurnManager.Instance.IsCurrentPawn(local) &&
            TurnManager.Instance.InProposalPhaseFor(local))
        {
            if (MarketManager.Instance != null)
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
        hintTitle.text = "การเสนอซื้อหุ้นทำงานอย่างไร";
        hintBody.text =
            "• เลือกบริษัทที่คุณยังไม่ได้เป็นเจ้าของหุ้น.\n" +
            "• กรอก % และราคาที่คุณจะจ่าย.\n" +
            "• หากคุณถือหุ้นมากกว่า 60% คุณจะกลายเป็น เจ้าของโรงงานแทน" +
            "• กาให้ข้อเสนอที่ปฎิเสธไม่ได้คือการซื้อ % โดยทันทีที่ในราคาที่สูงกว่า.";
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
            {
                var (minPct, maxPct) = GetForcedBuyBounds();
                forcedBuyPercentInput.text = minPct.ToString();
            }
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
        if (forcedBuyButton) forcedBuyButton.interactable = (comp != null);

        if (comp == null)
        {
            if (forcedBuyPriceText) forcedBuyPriceText.text = "ราคา: —";
            return;
        }

        int pct;
        if (!forcedBuyPercentInput || !int.TryParse(forcedBuyPercentInput.text, out pct))
            pct = 10;

        // Clamp between min & max (e.g. 20–40)
        var (minPct, maxPct) = GetForcedBuyBounds();
        pct = Mathf.Clamp(pct, minPct, maxPct);

        // Make sure the field shows the clamped value (avoid infinite loop)
        if (forcedBuyPercentInput != null)
        {
            string newText = pct.ToString();
            if (forcedBuyPercentInput.text != newText)
                forcedBuyPercentInput.text = newText;
        }

        // Exact preview (includes surcharge)
        int transferablePct, livePriceUsed;
        int finalPrice = MarketManager.Instance.ClientPreviewForcedBuyPrice(
            comp, currentPawn, pct, out transferablePct, out livePriceUsed
        );

        if (transferablePct <= 0)
        {
            if (forcedBuyPriceText) forcedBuyPriceText.text = "ราคา: — (ไม่มี % ที่โอนย้ายได้)";
            if (forcedBuyButton) forcedBuyButton.interactable = false;
            return;
        }

        float prem = Mathf.Max(1f, MarketManager.Instance.forcedBuyPriceMult.Value);
        float coreF = livePriceUsed * (transferablePct / 100f) * prem;
        int core = Mathf.Max(1, Mathf.RoundToInt(coreF));

        float surchargeRate = Mathf.Max(0f, MarketManager.Instance.forcedBuySurchargeOfCash.Value);
        int buyerCash = Mathf.Max(0, currentPawn != null ? currentPawn.money.Value : 0);
        int surcharge = Mathf.RoundToInt(buyerCash * surchargeRate);

        bool allowsDebt = MarketManager.Instance.forcedBuyAllowsDebt.Value;
        bool enoughCash = allowsDebt || buyerCash >= finalPrice;

        if (forcedBuyPriceText)
            forcedBuyPriceText.text = $"ราคา: ${finalPrice}M";

        if (forcedBuyButton) forcedBuyButton.interactable = enoughCash;
    }


   private void OnClickForcedBuy()
    {
        var comp = GetVisibleCompany();
        if (comp == null) return;

        int pct = 10;
        if (forcedBuyPercentInput && !int.TryParse(forcedBuyPercentInput.text, out pct))
            pct = 10;

        var (minPct, maxPct) = GetForcedBuyBounds();
        pct = Mathf.Clamp(pct, minPct, maxPct);

        int transferablePct, _;
        MarketManager.Instance.ClientPreviewForcedBuyPrice(comp, currentPawn, pct, out transferablePct, out _);
        if (transferablePct <= 0) return;

        MarketManager.Instance.CmdForceBuy(comp.companyName, transferablePct);

        if (forcedBuyButton)
        {
            forcedBuyButton.interactable = false;
            StartCoroutine(ReenableForcedBuySoon());
        }

        CloseAndNotifyServer();
    }


    private System.Collections.IEnumerator ReenableForcedBuySoon()
    {
        yield return new WaitForSecondsRealtime(0.25f);
        if (forcedBuyButton != null) forcedBuyButton.interactable = true;
    }

    /* ======== Local cash change hook for live surcharge preview ======== */

    private void OnLocalMoneyChanged(int oldVal, int newVal, bool asServer)
    {
        if (panel != null && panel.activeInHierarchy)
            UpdateForcedBuyUI();
    }

    private (int minPct, int maxPct) GetForcedBuyBounds()
    {
        int minPct = 1;
        int maxPct = 100;

        if (MarketManager.Instance != null)
        {
            minPct = Mathf.Clamp(MarketManager.Instance.forcedBuyMinPercent, 1, 100);
            maxPct = Mathf.Clamp(MarketManager.Instance.forcedBuyPercentCapPerTurn, minPct, 100);
        }

        return (minPct, maxPct);
    }

}
