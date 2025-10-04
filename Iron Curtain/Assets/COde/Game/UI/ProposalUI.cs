using UnityEngine;
using UnityEngine.UI;

public class ProposalUI : MonoBehaviour
{
    public static ProposalUI Instance;

    [Header("Refs")]
    public GameObject panel;
    public Transform listParent;
    public GameObject proposalEntryPrefab;
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

        foreach (var kv in MarketManager.Instance.companies)
        {
            var company = kv.Value;
            if (company == null) continue;
            if (company.owner == currentPawn) continue; // cannot propose to self
            if (company.GetOwnership(currentPawn) >= 100) continue; // already max

            var entry = Instantiate(proposalEntryPrefab, listParent);
            var ui = entry.GetComponent<ProposalEntry>();
            if (ui != null)
                ui.Setup(company, currentPawn);
        }
    }

    private void CloseAndNotifyServer()
    {
        panel.SetActive(false);

        // Server-authoritative: notify MarketManager so it can advance phase
        MarketManager.Instance.CmdNotifyProposalClosed();
    }

    public void Hide()
    {
        CloseAndNotifyServer();
    }
}