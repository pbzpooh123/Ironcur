using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ReviewUI : MonoBehaviour
{
    public static ReviewUI Instance;

    [Header("Refs")]
    public GameObject panel;
    public Transform listParent;
    public GameObject reviewEntryPrefab;
    public Button closeButton;

    private PlayerPawn currentPawn;

    private void Awake()
    {
        Instance = this;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseAndNotifyServer);
        }
    }

    public void Show(PlayerPawn pawn)
    {
        currentPawn = pawn;
        panel.SetActive(true);
        StartCoroutine(DelayedRefresh());

        // Disable Roll + EndTurn locally while the review is open.
        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetEndTurnInteractable(false);
            ui.SetRollInteractable(false);   // requires TurnUI update below
        }
    }
    
    private IEnumerator DelayedRefresh()
    {
        float timeout = 3f;
        while (timeout > 0f)
        {
            timeout -= Time.deltaTime;
            // Wait until we actually have proposals for this pawn
            bool found = false;
            if (MarketManager.Instance != null && currentPawn != null)
            {
                foreach (var c in MarketManager.Instance.companies.Values)
                {
                    if (c != null && c.owner == currentPawn && c.proposals != null && c.proposals.Count > 0)
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
        foreach (var c in MarketManager.Instance.companies.Values)
        {
            if (c.owner == null && !string.IsNullOrEmpty(c.ownerName))
            {
                var tryOwner = GameManager.Instance.Players.Find(p => p.playerName.Value == c.ownerName);
                if (tryOwner != null)
                {
                    c.owner = tryOwner;
                    Debug.Log($"[ReviewUI] Late rebind: {c.companyName} → {tryOwner.playerName.Value}");
                }
            }
        }
        if (panel == null || !panel.activeSelf)
            return;
        if (MarketManager.Instance == null)
        {
            Debug.LogWarning("[ReviewUI] MarketManager not ready yet.");
            return;
        }
        if (currentPawn == null)
        {
            Debug.LogWarning("[ReviewUI] currentPawn is null; skipping refresh.");
            return;
        }

        foreach (Transform child in listParent)
            Destroy(child.gameObject);
        
        Debug.Log($"[ReviewUI] Checking {MarketManager.Instance.companies.Count} companies for {currentPawn.playerName.Value}");

        foreach (var kv in MarketManager.Instance.companies)
        {
            var c = kv.Value;
            if (c == null)
            {
                Debug.Log($"[ReviewUI] {kv.Key} is null");
                continue;
            }

            Debug.Log($"[ReviewUI] {kv.Key} owner={c.owner?.playerName.Value ?? "null"} proposals={(c.proposals?.Count ?? 0)}");
        }

        foreach (var company in MarketManager.Instance.companies.Values)
        {
            if (company == null) continue;
            if (company.owner == null) continue; // new null check
            if (company.owner != currentPawn) continue;
            if (company.proposals == null || company.proposals.Count == 0) continue;

            for (int i = 0; i < company.proposals.Count; i++)
            {
                Debug.Log($"[ReviewUI] Adding review entry for {company.companyName}");
                var p = company.proposals[i];
                var go = Instantiate(reviewEntryPrefab, listParent);
                var entry = go.GetComponent<ReviewEntry>();
                if (entry != null)
                    entry.Setup(company.companyName, i, p);
            }
        }
    }


    private void CloseAndNotifyServer()
    {
        panel.SetActive(false);

        // Tell server we finished review → TurnManager will phase → Rolling.
        MarketManager.Instance?.CmdNotifyReviewClosed();

        // Do NOT re-enable roll/endTurn here. The server will send the correct UI state.
    }

    public void Hide() => CloseAndNotifyServer();
}
