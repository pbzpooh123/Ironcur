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
        Refresh();

        // Disable Roll + EndTurn locally while the review is open.
        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetEndTurnInteractable(false);
            ui.SetRollInteractable(false);   // requires TurnUI update below
        }
    }

    public void Refresh()
    {
        if (panel == null || !panel.activeSelf) return;

        foreach (Transform child in listParent)
            Destroy(child.gameObject);

        if (MarketManager.Instance == null || currentPawn == null)
            return;

        // Show only proposals for companies owned by this pawn
        foreach (var company in MarketManager.Instance.companies.Values)
        {
            if (company == null) continue;
            if (company.owner != currentPawn) continue;
            if (company.proposals == null || company.proposals.Count == 0) continue;

            for (int i = 0; i < company.proposals.Count; i++)
            {
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
