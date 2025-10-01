using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReviewUI : MonoBehaviour
{
    public static ReviewUI Instance;
    public GameObject panel;
    public Transform listParent;
    public GameObject reviewEntryPrefab;

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

        foreach (var company in MarketManager.Instance.companies.Values)
        {
            if (company.owner != pawn) continue;

            for (int i = 0; i < company.proposals.Count; i++)
            {
                var proposal = company.proposals[i];
                var entry = Instantiate(reviewEntryPrefab, listParent);
                var ui = entry.GetComponent<ReviewEntry>();
                ui.Setup(company.companyName, i, proposal);
            }
        }
        var uii = FindObjectOfType<TurnUI>();
        if (uii != null) uii.ForceDisableEndTurn();
    }

    public void Hide()
    {
        panel.SetActive(false);
        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.SetEndTurnInteractable(true);
    }
}
