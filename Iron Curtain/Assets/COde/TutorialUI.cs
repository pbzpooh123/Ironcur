using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HowToPlayUI : MonoBehaviour
{
    public static HowToPlayUI Instance;

    [Header("Main Panel")]
    public GameObject panel;              // Panel ใหญ่ของหน้าสอนเล่น

    [Header("Pages")]
    [Tooltip("ใส่ GameObject ของแต่ละหน้าเรียงตามลำดับ Page1, Page2, Page3...")]
    public List<GameObject> pages;

    [Header("Controls")]
    public Button closeButton;
    public Button prevButton;
    public Button nextButton;
    public TMP_Text pageText;            // "Page 1 / 4" แบบเดียวกับ ProposalUI

    [Header("Optional")]
    [Tooltip("ติ๊กอันนี้ถ้าไม่อยากให้โชว์อัตโนมัติในครั้งถัดไป")]
    public Toggle dontShowAgainToggle;

    private int _pageIndex = 0;

    private const string PREF_HOWTO_SEEN = "ui.howToPlaySeen";

    private void Awake()
    {
        Instance = this;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseClicked);
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

        // ปิดไว้ก่อน
        if (panel != null) panel.SetActive(false);
    }

    // เรียกจาก Lobby ถ้าจะให้โชว์อัตโนมัติครั้งแรก
    public void TryAutoShowFirstTime()
    {
        int seen = PlayerPrefs.GetInt(PREF_HOWTO_SEEN, 0);
        if (seen == 0)
            Show();
    }

    // เรียกจากปุ่ม "วิธีเล่น" ใน Lobby
    public void Show()
    {
        if (panel == null) return;
        panel.SetActive(true);

        _pageIndex = 0;
        SetPage(_pageIndex);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void OnCloseClicked()
    {
        // ถ้าติ๊ก "ไม่ต้องแสดงอีก"
        if (dontShowAgainToggle != null && dontShowAgainToggle.isOn)
        {
            PlayerPrefs.SetInt(PREF_HOWTO_SEEN, 1);
            PlayerPrefs.Save();
        }

        Hide();
    }

    private void OnPrevPage()
    {
        if (pages == null || pages.Count == 0) return;

        _pageIndex = Mathf.Max(0, _pageIndex - 1);
        SetPage(_pageIndex);
    }

    private void OnNextPage()
    {
        if (pages == null || pages.Count == 0) return;

        _pageIndex = Mathf.Min(GetMaxPageIndex(), _pageIndex + 1);
        SetPage(_pageIndex);
    }

    private int GetMaxPageIndex()
    {
        if (pages == null || pages.Count == 0) return 0;
        return Mathf.Max(0, pages.Count - 1);
    }

    private void SetPage(int index)
    {
        if (pages == null) return;

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == index);
        }

        UpdatePageLabel(index + 1, pages.Count);

        // อัปเดตสถานะปุ่ม next/prev
        if (prevButton != null) prevButton.interactable = (index > 0);
        if (nextButton != null) nextButton.interactable = (index < GetMaxPageIndex());
    }

    private void UpdatePageLabel(int current, int total)
    {
        if (pageText == null) return;

        if (total <= 0)
            pageText.text = "Page 0 / 0";
        else
            pageText.text = $"Page {current} / {total}";
    }
}
