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
            closeButton.onClick.AddListener(CloseAndResume);
        }
    }
    
    public void Show(PlayerPawn pawn)
    {
        currentPawn = pawn;
        panel.SetActive(true);
        Refresh();
        
        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();
    }
    
    public void Refresh()
    {
        if (panel == null || !panel.activeSelf) return;

        // Clear old entries
        foreach (Transform child in listParent)
            Destroy(child.gameObject);

        if (MarketManager.Instance == null || currentPawn == null)
            return;
        
        foreach (var kv in MarketManager.Instance.companies)
        {
            var company = kv.Value;
            if (company == null) continue;
            if (company.owner == currentPawn) continue;
            
            if (company.GetOwnership(currentPawn) >= 100) continue;

            var entry = Instantiate(proposalEntryPrefab, listParent);
            var ui = entry.GetComponent<ProposalEntry>();
            if (ui != null)
                ui.Setup(company, currentPawn);
        }
    }

    private void CloseAndResume()
    {
        panel.SetActive(false);

        // Re-enable EndTurn
        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.SetEndTurnInteractable(true);
    }

    public void Hide()
    {
        CloseAndResume();
    }
}
