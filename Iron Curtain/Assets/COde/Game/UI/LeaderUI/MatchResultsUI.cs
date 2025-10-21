using System.Collections;
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

    public void Show(string[] names, int[] startMoney, int[] bailouts, int[] finalScores)
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
        StartCoroutine(CoBuildAndAnimate(names, startMoney, bailouts, finalScores));
    }

    private IEnumerator CoBuildAndAnimate(string[] names, int[] startMoney, int[] bailouts, int[] finalScores)
    {
        yield return null;

        // clear old rows
        for (int i = rowsParent.childCount - 1; i >= 0; i--)
            Destroy(rowsParent.GetChild(i).gameObject);

        // compute a shared max (avoid 0)
        int globalMax = 0;
        for (int i = 0; i < finalScores.Length; i++)
            if (finalScores[i] > globalMax) globalMax = finalScores[i];
        if (globalMax <= 0) globalMax = 1;

        // build rows
        for (int i = 0; i < names.Length; i++)
        {
            var go = Instantiate(rowPrefab, rowsParent);
            if (!go.activeSelf) go.SetActive(true);

            var row = go.GetComponent<ResultsEntryUI>();
            if (row != null)
                row.Bind(names[i], startMoney[i], bailouts[i], finalScores[i], i, globalMax);

            yield return null;
        }

        // let all tweens start
        yield return new WaitForSeconds(0.25f);

        if (readyButton != null) readyButton.interactable = true;
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
