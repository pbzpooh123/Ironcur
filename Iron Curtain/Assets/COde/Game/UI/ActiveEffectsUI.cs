using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActiveEffectsUI : MonoBehaviour
{
    public static ActiveEffectsUI Instance;

    [Header("UI Refs")]
    public GameObject panel;          // root panel
    public Transform listParent;      // parent for rows
    public GameObject rowPrefab;      // prefab with a TMP_Text
    public Button closeButton;

    [Header("Paging")]
    [Tooltip("Max number of rows per page.")]
    public int rowsPerPage = 6;
    public TMP_Text pageText;
    public Button prevButton;
    public Button nextButton;

    [Header("Empty state")]
    public string noEffectsText = "ตอนนี้ยังไม่มีเอฟเฟกต์พิเศษที่กำลังทำงานอยู่";

    private PlayerPawn _viewer;

    // cache of current lines so we can page them
    private List<string> _currentLines = new();
    private int _pageIndex = 0;   // 0-based

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
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

    public void ShowForPawn(PlayerPawn pawn)
    {
        _viewer = pawn;
        _pageIndex = 0; // reset page whenever we open
        if (panel) panel.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }

    public void Refresh()
    {
        if (panel == null || !panel.activeInHierarchy) return;

        _currentLines.Clear();

        if (EventManager.Instance != null)
        {
            // Ask EventManager for a human-readable summary
            var lines = EventManager.Instance.BuildEffectsSummary(_viewer);
            if (lines != null) _currentLines.AddRange(lines);
        }

        if (_currentLines.Count == 0)
        {
            _currentLines.Add(noEffectsText);
        }

        // Clamp page index
        int maxPage = GetMaxPageIndex();
        _pageIndex = Mathf.Clamp(_pageIndex, 0, maxPage);

        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        // clear old rows
        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        if (_currentLines.Count == 0)
        {
            AddRow(noEffectsText);
            UpdatePager(0, 0);
            SetPagerVisible(false);
            return;
        }

        int pageSize = Mathf.Max(1, rowsPerPage);
        int start = _pageIndex * pageSize;
        int endExclusive = Mathf.Min(start + pageSize, _currentLines.Count);

        for (int i = start; i < endExclusive; i++)
            AddRow(_currentLines[i]);

        int totalPages = GetMaxPageIndex() + 1;
        int currentPage = _pageIndex + 1;

        UpdatePager(currentPage, totalPages);
        SetPagerVisible(_currentLines.Count > rowsPerPage);
    }

    private void AddRow(string text)
    {
        var go = Instantiate(rowPrefab, listParent);
        var label = go.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = text;
    }

    private int GetMaxPageIndex()
    {
        if (_currentLines.Count == 0) return 0;
        int pageSize = Mathf.Max(1, rowsPerPage);
        return Mathf.Max(0, (_currentLines.Count - 1) / pageSize);
    }

    private void OnPrevPage()
    {
        if (_currentLines.Count == 0) return;
        _pageIndex = Mathf.Max(0, _pageIndex - 1);
        RenderCurrentPage();
    }

    private void OnNextPage()
    {
        if (_currentLines.Count == 0) return;
        _pageIndex = Mathf.Min(GetMaxPageIndex(), _pageIndex + 1);
        RenderCurrentPage();
    }

    private void UpdatePager(int current, int total)
    {
        if (pageText != null)
        {
            if (total <= 0) pageText.text = "Page 0 / 0";
            else pageText.text = $"Page {current} / {total}";
        }

        if (prevButton != null)
            prevButton.interactable = (_currentLines.Count > rowsPerPage && _pageIndex > 0);

        if (nextButton != null)
            nextButton.interactable = (_currentLines.Count > rowsPerPage && _pageIndex < GetMaxPageIndex());
    }

    private void SetPagerVisible(bool visible)
    {
        if (pageText != null) pageText.gameObject.SetActive(visible);
        if (prevButton != null) prevButton.gameObject.SetActive(visible);
        if (nextButton != null) nextButton.gameObject.SetActive(visible);
    }
}
