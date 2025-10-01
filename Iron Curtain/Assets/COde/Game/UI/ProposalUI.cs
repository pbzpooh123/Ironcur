using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProposalUI : MonoBehaviour
{
    public static ProposalUI Instance;
    public GameObject panel;
    public Transform listParent;
    public GameObject proposalEntryPrefab;

    private PlayerPawn currentPawn;

    public Button closeButton; // assign in inspector

    private void Awake()
    {
        Instance = this;
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    public void Show(PlayerPawn pawn)
    {
        currentPawn = pawn;
        panel.SetActive(true);

        foreach (Transform child in listParent)
            Destroy(child.gameObject);
        

        foreach (var kv in MarketManager.Instance.companies)
        {
            var company = kv.Value;
            if (company.owner == pawn) continue;
            if (company.GetOwnership(pawn) >= 100) continue;
            
            var entry = Instantiate(proposalEntryPrefab, listParent);
            var ui = entry.GetComponent<ProposalEntry>();
            
            ui.Setup(company, pawn);
            
        }
        
        var uii = FindObjectOfType<TurnUI>();
        if (uii != null) uii.ForceDisableEndTurn();
        
    }

    public void Hide()
    {
        panel.SetActive(false);
        if (currentPawn != null)
            currentPawn.TargetEnableEndTurn(currentPawn.Owner, true);
    }
}

