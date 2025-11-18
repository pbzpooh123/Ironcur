using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MatchResultsUI : MonoBehaviour
{
    public static MatchResultsUI Instance;

    [Header("Root/Panel")]
    public GameObject root;
    public Transform rowsParent;
    public GameObject rowPrefab;

    [Header("Controls")]
    public Button readyButton;

    [Header("Awards (single text)")]
    public TMP_Text awardsText;

    [Tooltip("Characters per second for award text typing.")]
    [SerializeField] private float awardCharsPerSecond = 30f;

    [Tooltip("Pause (seconds) after each line is fully shown.")]
    [SerializeField] private float awardLinePause = 0.4f;

    [Header("Scenes")]
    [SerializeField] private string leaderboardSceneName = "Leaderboard";

    void Awake()
    {
        Instance = this;
        if (root != null) root.SetActive(false);
        if (readyButton != null) readyButton.interactable = false;
        if (awardsText != null) awardsText.text = "";
    }

    public void Show(
        string[] names, int[] startMoney, int[] bailouts, int[] finalPoints,
        string portfolioKingName, string incomeKingName, string spendingKingName,
        string taxVictimName, string unluckyName, string takeoverKingName,
        string proposalsSharkName, string proposalsAcceptedKingName,
        string diceGodName
    )
    {
        if (root != null && !root.activeSelf) root.SetActive(true);
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyClicked);
            readyButton.onClick.AddListener(OnReadyClicked);
            readyButton.interactable = false;
        }

        StopAllCoroutines();
        StartCoroutine(CoBuildAndAnimate(
            names, startMoney, bailouts, finalPoints,
            portfolioKingName, incomeKingName, spendingKingName,
            taxVictimName, unluckyName, takeoverKingName,
            proposalsSharkName, proposalsAcceptedKingName,
            diceGodName
        ));
    }

    private IEnumerator CoBuildAndAnimate(
        string[] names, int[] startMoney, int[] bailouts, int[] finalPoints,
        string portfolioKingName, string incomeKingName, string spendingKingName,
        string taxVictimName, string unluckyName, string takeoverKingName,
        string proposalsSharkName, string proposalsAcceptedKingName,
        string diceGodName
    )
    {
        yield return null;

        // clear old rows
        for (int i = rowsParent.childCount - 1; i >= 0; i--)
            Destroy(rowsParent.GetChild(i).gameObject);

        // build one row per player (each row will handle its own drop anim)
        for (int i = 0; i < names.Length; i++)
        {
            var go = Instantiate(rowPrefab, rowsParent);
            if (!go.activeSelf) go.SetActive(true);

            var row = go.GetComponent<ResultsEntryUI>();
            if (row != null)
                row.Bind(names[i], startMoney[i], bailouts[i], finalPoints[i]);

            yield return null;
        }

        // Build award lines into a list
        var lines = new List<string>();
        AddAwardLine(lines, "เจ้าพ่อพอร์ตหุ้น",       portfolioKingName);
        AddAwardLine(lines, "ราชาเงินเข้า",            incomeKingName);
        AddAwardLine(lines, "จอมสุรุ่ยสุร่าย",         spendingKingName);
        AddAwardLine(lines, "เหยื่อภาษีแห่งชาติ",      taxVictimName);
        AddAwardLine(lines, "ตัวซวยประจำเกม",          unluckyName);
        AddAwardLine(lines, "นักยึดกิจการอันดับ 1",    takeoverKingName);
        AddAwardLine(lines, "ฉลามการเงิน",             proposalsSharkName);
        AddAwardLine(lines, "นักเจรจาโหด",             proposalsAcceptedKingName);
        AddAwardLine(lines, "เทพลูกเต๋า",               diceGodName);

        // Type out all award lines in a single TMP_Text
        yield return StartCoroutine(CoTypeAwards(lines));

        // allow Ready after awards are fully shown
        if (readyButton != null) readyButton.interactable = true;
    }

    private void AddAwardLine(List<string> list, string title, string winnerName)
    {
        if (string.IsNullOrWhiteSpace(winnerName) || winnerName == "—")
            return;

        // 25 points = AWARD_POINTS in TurnManager
        list.Add($"{title}: {winnerName} (+25 แต้ม)");
    }

    private IEnumerator CoTypeAwards(List<string> lines)
    {
        if (awardsText == null) yield break;

        awardsText.text = "";

        if (lines == null || lines.Count == 0)
        {
            // no awards – you can set a default message if you want
            yield break;
        }

        float charDelay = (awardCharsPerSecond > 0f)
            ? 1f / awardCharsPerSecond
            : 0.03f;

        string builtSoFar = "";

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!string.IsNullOrEmpty(builtSoFar))
                builtSoFar += "\n";

            string prefix = builtSoFar;

            // type this line character by character
            for (int i = 0; i <= line.Length; i++)
            {
                awardsText.text = prefix + line.Substring(0, i);
                yield return new WaitForSeconds(charDelay);
            }

            builtSoFar = prefix + line;
            yield return new WaitForSeconds(awardLinePause);
        }
    }

    private bool _sentReady = false;
    private void OnReadyClicked()
    {
        if (_sentReady) return;
        _sentReady = true;
        if (readyButton != null) readyButton.interactable = false;

        if (TurnManager.Instance != null)
            TurnManager.Instance.CmdFinalResultsReady();
        else
            Debug.LogWarning("TurnManager.Instance missing when pressing Ready on results screen.");
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    public void GoToLeaderboardScene()
    {
        if (string.IsNullOrWhiteSpace(leaderboardSceneName))
        {
            Debug.LogError("[MatchResultsUI] Leaderboard scene name is empty.");
            return;
        }
        SceneManager.LoadScene(leaderboardSceneName);
    }
}
