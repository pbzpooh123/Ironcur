using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReviewUI : MonoBehaviour
{
    public static ReviewUI Instance;

    [Header("Refs")]
    public GameObject panel;
    public Transform listParent;
    public GameObject reviewEntryPrefab;
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

    // Flat cache of what we can show (companyName, proposalIndex, Proposal)
    private readonly List<(string company, int index, Proposal p)> _itemsCache = new();
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
        StartCoroutine(DelayedRefresh());

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetEndTurnInteractable(false);
            ui.SetRollInteractable(false);
        }
    }

    private IEnumerator DelayedRefresh()
    {
        float timeout = 3f;
        while (timeout > 0f)
        {
            timeout -= Time.deltaTime;
            bool found = false;
            if (MarketManager.Instance != null && currentPawn != null)
            {
                foreach (var c in MarketManager.Instance.companies.Values)
                {
                    if (c != null && (c.owner == currentPawn || c.ownerName == currentPawn.playerName.Value)
                        && c.proposals != null && c.proposals.Count > 0)
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (found) break;
            yield return null;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (panel == null || !panel.activeSelf) return;
        if (MarketManager.Instance == null || currentPawn == null) return;

        // rebuild cache
        _itemsCache.Clear();

        var myName = currentPawn.playerName.Value;
        foreach (var company in MarketManager.Instance.companies.Values)
        {
            if (company == null) continue;

            bool isOwner =
                (company.owner != null && company.owner == currentPawn) ||
                (!string.IsNullOrEmpty(company.ownerName) && company.ownerName == myName);

            if (!isOwner) continue;
            if (company.proposals == null || company.proposals.Count == 0) continue;

            for (int i = 0; i < company.proposals.Count; i++)
                _itemsCache.Add((company.companyName, i, company.proposals[i]));
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

        int pageSize = Mathf.Max(1, rowsPerPage); // set to 1 to force single row
        int start = _pageIndex * pageSize;
        int endExclusive = Mathf.Min(start + pageSize, _itemsCache.Count);

        for (int i = start; i < endExclusive; i++)
        {
            var it = _itemsCache[i];
            var go = Instantiate(reviewEntryPrefab, listParent);
            var entry = go.GetComponent<ReviewEntry>();
            if (entry != null)
                entry.Setup(it.company, it.index, it.p);
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

    public void CloseAndNotifyServer()
    {
        panel.SetActive(false);
        MarketManager.Instance?.CmdNotifyReviewClosed();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);

        var local = FindLocalOwnedPawn();
        if (local != null && TurnManager.Instance.IsCurrentPawn(local) &&
            TurnManager.Instance.InReviewPhaseFor(local))
        {
            MarketManager.Instance.CmdNotifyReviewClosed();
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
        hintTitle.text = "การดูข้อเสนอทำงานอย่างไร";
        hintBody.text =
            "• ดูข้อเสนอทั้งหมดที่ทำกับบริษัทของคุณ.\n" +
            "• ยอมรับเพื่อขาย % และรับเงินสด.\n" +
            "• ปฏิเสธเพื่อเก็บหุ้นของคุณไว้.\n";
        hintPanel.SetActive(true);
    }

    public void OnHintGotIt() => hintPanel?.SetActive(false);
}
