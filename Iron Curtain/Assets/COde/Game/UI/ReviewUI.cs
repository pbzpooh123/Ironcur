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
        Debug.Log("[ReviewUI] Refresh start");
        if (panel == null || !panel.activeSelf) return;

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

        var myName = currentPawn.playerName.Value;
        int shown = 0;
        foreach (var company in MarketManager.Instance.companies.Values)
        {
            if (company == null) continue;

            // ---- NEW: tolerate null owner; fallback to ownerName match ----
            bool isOwner =
                (company.owner != null && company.owner == currentPawn) ||
                (!string.IsNullOrEmpty(company.ownerName) && company.ownerName == myName);

            if (!isOwner) continue;
            if (company.proposals == null || company.proposals.Count == 0) continue;

            for (int i = 0; i < company.proposals.Count; i++)
            {
                var p = company.proposals[i];
                var go = Instantiate(reviewEntryPrefab, listParent);
                var entry = go.GetComponent<ReviewEntry>();
                if (entry != null)
                    entry.Setup(company.companyName, i, p);
                shown++;
            }
        }

        Debug.Log($"[ReviewUI] Entries shown = {shown}");
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
