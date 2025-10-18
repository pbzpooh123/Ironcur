using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using FishNet.Managing.Scened;

public class MatchResultsUI : MonoBehaviour
{
    public static MatchResultsUI Instance;

    [Header("Root/Panel")]
    public GameObject root;            // your results Canvas or Panel (set in Inspector)
    public Transform rowsParent;       // container for rows
    public GameObject rowPrefab;       // prefab with ResultsEntryUI

    private void Awake()
    {
        Instance = this;
        if (root != null) root.SetActive(false);   // hide on boot
    }

    // Call this from RPC on clients
    public void Show(string[] names, int[] startMoney, int[] bailouts, int[] finalScores)
    {
        // 1) Make sure we are ACTIVE before any coroutine
        if (root != null && !root.activeSelf) root.SetActive(true);
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(CoBuildAndAnimate(names, startMoney, bailouts, finalScores));
    }

    private IEnumerator CoBuildAndAnimate(string[] names, int[] startMoney, int[] bailouts, int[] finalScores)
    {
        // optional: let layout enable
        yield return null;

        // clear old
        for (int i = rowsParent.childCount - 1; i >= 0; i--)
            Destroy(rowsParent.GetChild(i).gameObject);

        int n = names.Length;
        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(rowPrefab, rowsParent);

            // 2) Ensure each row is ACTIVE before we bind/animate
            if (!go.activeSelf) go.SetActive(true);

            var row = go.GetComponent<ResultsEntryUI>();
            if (row != null)
            {
                // Bind data; row will animate bar/score internally
                row.Bind(names[i], startMoney[i], bailouts[i], finalScores[i], i);
            }

            // small stagger if you like
            yield return null;
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    [Header("Scenes")]
    [SerializeField] private string leaderboardSceneName = "Leaderboard"; 

 
    public void GoToLeaderboardScene()
    {
        if (string.IsNullOrWhiteSpace(leaderboardSceneName))
        {
            Debug.LogError("[MatchResultsUI] Leaderboard scene name is empty.");
            return;
        }
        SceneLoadData loadData = new SceneLoadData(leaderboardSceneName)
        {
            ReplaceScenes = ReplaceOption.All
        };
    }
}
