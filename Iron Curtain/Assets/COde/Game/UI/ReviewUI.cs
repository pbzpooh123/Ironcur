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

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.SetEndTurnInteractable(false);
        if (ui != null) ui.SetRollInteractable(false);
    }

    public void Refresh()
    {
        if (panel == null || !panel.activeSelf) return;

        foreach (Transform child in listParent)
            Destroy(child.gameObject);

        if (MarketManager.Instance == null || currentPawn == null)
            return;

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
                entry.Setup(company.companyName, i, p);
            }
        }
    }

    private void CloseAndNotifyServer()
    {
        panel.SetActive(false);
        MarketManager.Instance.CmdNotifyReviewClosed();
    }

    public void Hide()
    {
        CloseAndNotifyServer();
    }
}