using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MatchResultsUI : MonoBehaviour
{
    public static MatchResultsUI Instance;

    [Header("Root/Panel")]
    public GameObject root;
    public Transform rowsParent;
    public GameObject rowPrefab;

    [Header("Controls")]
    public Button readyButton;

    [Header("Scenes")]
    [SerializeField] private string leaderboardSceneName = "Leaderboard";

    void Awake()
    {
        Instance = this;
        if (root != null) root.SetActive(false);
        if (readyButton != null) readyButton.interactable = false;
    }

    // มี investorTitles / investorSummaries + ชื่อผู้ได้รางวัล 9 แบบจาก TurnManager
    public void Show(
        string[] names, int[] startMoney, int[] bailouts, int[] finalPoints,
        string[] investorTitles, string[] investorSummaries,
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
            investorTitles, investorSummaries,
            portfolioKingName, incomeKingName, spendingKingName,
            taxVictimName, unluckyName, takeoverKingName,
            proposalsSharkName, proposalsAcceptedKingName,
            diceGodName
        ));
    }

    private IEnumerator CoBuildAndAnimate(
        string[] names, int[] startMoney, int[] bailouts, int[] finalPoints,
        string[] investorTitles, string[] investorSummaries,
        string portfolioKingName, string incomeKingName, string spendingKingName,
        string taxVictimName, string unluckyName, string takeoverKingName,
        string proposalsSharkName, string proposalsAcceptedKingName,
        string diceGodName
    )
    {
        yield return null;

        // เคลียร์ row เก่า
        for (int i = rowsParent.childCount - 1; i >= 0; i--)
            Destroy(rowsParent.GetChild(i).gameObject);

        // สร้าง row ต่อผู้เล่น
        for (int i = 0; i < names.Length; i++)
        {
            var go = Instantiate(rowPrefab, rowsParent);
            if (!go.activeSelf) go.SetActive(true);

            var row = go.GetComponent<ResultsEntryUI>();
            if (row != null)
            {
                string invTitle = (investorTitles != null && i < investorTitles.Length) ? investorTitles[i] : "";
                string summary  = (investorSummaries != null && i < investorSummaries.Length) ? investorSummaries[i] : "";

                // awards list (หลายบรรทัด)
                string awards = BuildAwardsForPlayer(
                    names[i],
                    portfolioKingName, incomeKingName, spendingKingName,
                    taxVictimName, unluckyName, takeoverKingName,
                    proposalsSharkName, proposalsAcceptedKingName,
                    diceGodName
                );

                // headline = ฉายารางวัลอันแรกที่ได้ (ถ้ามี)
                string awardHeadline = BuildAwardHeadlineForPlayer(
                    names[i],
                    portfolioKingName, incomeKingName, spendingKingName,
                    taxVictimName, unluckyName, takeoverKingName,
                    proposalsSharkName, proposalsAcceptedKingName,
                    diceGodName
                );
                
                row.Bind(
                          names[i],
                          startMoney[i],
                          bailouts[i],
                          finalPoints[i],
                          invTitle,
                          summary,
                          awards
                         );

            }

            yield return null;
        }

        if (readyButton != null) readyButton.interactable = true;
    }

    // ---------- Awards helpers ----------

    private string BuildAwardsForPlayer(
        string playerName,
        string portfolioKingName, string incomeKingName, string spendingKingName,
        string taxVictimName, string unluckyName, string takeoverKingName,
        string proposalsSharkName, string proposalsAcceptedKingName,
        string diceGodName
    )
    {
        var list = new List<string>();

        bool IsMe(string winner) =>
            !string.IsNullOrWhiteSpace(winner) &&
            string.Equals(playerName?.Trim(), winner.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (IsMe(portfolioKingName))         list.Add("เจ้าพ่อพอร์ตหุ้น (+25 แต้ม)");
        if (IsMe(incomeKingName))            list.Add("ราชาเงินเข้า (+25 แต้ม)");
        if (IsMe(spendingKingName))          list.Add("จอมสุรุ่ยสุร่าย (+25 แต้ม)");
        if (IsMe(taxVictimName))             list.Add("เหยื่อภาษีแห่งชาติ (+25 แต้ม)");
        if (IsMe(unluckyName))               list.Add("คนโชคร้ายประจำเกม (+25 แต้ม)");
        if (IsMe(takeoverKingName))          list.Add("นักยึดกิจการอันดับ 1 (+25 แต้ม)");
        if (IsMe(proposalsSharkName))        list.Add("ฉลามการเงิน (+25 แต้ม)");
        if (IsMe(proposalsAcceptedKingName)) list.Add("นักเจรจาโหด (+25 แต้ม)");
        if (IsMe(diceGodName))               list.Add("เทพลูกเต๋า (+25 แต้ม)");

        return (list.Count > 0) ? string.Join("\n", list) : "";
    }

    // เอา “อันแรกที่เจอ” ไปขึ้นหัว
    private string BuildAwardHeadlineForPlayer(
        string playerName,
        string portfolioKingName, string incomeKingName, string spendingKingName,
        string taxVictimName, string unluckyName, string takeoverKingName,
        string proposalsSharkName, string proposalsAcceptedKingName,
        string diceGodName
    )
    {
        bool IsMe(string winner) =>
            !string.IsNullOrWhiteSpace(winner) &&
            string.Equals(playerName?.Trim(), winner.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (IsMe(portfolioKingName))          return "เจ้าพ่อพอร์ตหุ้น";
        if (IsMe(incomeKingName))             return "ราชาเงินเข้า";
        if (IsMe(spendingKingName))           return "จอมสุรุ่ยสุร่าย";
        if (IsMe(taxVictimName))              return "เหยื่อภาษีแห่งชาติ";
        if (IsMe(unluckyName))                return "คนโชคร้ายประจำเกม";
        if (IsMe(takeoverKingName))           return "นักยึดกิจการอันดับ 1";
        if (IsMe(proposalsSharkName))         return "ฉลามการเงิน";
        if (IsMe(proposalsAcceptedKingName))  return "นักเจรจาโหด";
        if (IsMe(diceGodName))                return "เทพลูกเต๋า";

        return "";
    }

    // ---------- Ready / Scene ----------

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

        if (readyButton != null)
            readyButton.interactable = false;

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.FadeOut(() =>
            {
                SceneManager.LoadScene(leaderboardSceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(leaderboardSceneName);
        }
    }
}
