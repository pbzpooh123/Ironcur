using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProposalUI : MonoBehaviour
{
    public static ProposalUI Instance;
    public GameObject panel;
    public Transform listParent;
    public GameObject proposalEntryPrefab;

    private PlayerPawn currentPawn;

    private void Awake() => Instance = this;

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
    }
}

