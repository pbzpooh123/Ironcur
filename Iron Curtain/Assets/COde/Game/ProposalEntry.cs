using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProposalEntry : MonoBehaviour
{
    public TMP_Text companyNameText;
    public TMP_Text ownerNameText;
    public TMP_InputField percentInput;
    public TMP_InputField priceInput;
    public Button submitButton;

    private string companyName;
    private PlayerPawn proposer;

    public void Setup(CompanyRecord company, PlayerPawn pawn)
    {
        companyName = company.companyName;
        proposer = pawn;

        companyNameText.text = company.companyName;
        ownerNameText.text = $"Owner: {company.owner.playerName.Value}";

        bool isSelfOwned = (company.owner == pawn);
        submitButton.interactable = !isSelfOwned;

        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(OnSubmitClicked);
    }


    private void OnSubmitClicked()
    {
        if (!int.TryParse(percentInput.text, out int percent))
        {
            Debug.LogWarning("[ProposalEntry] Invalid percent input");
            return;
        }
        if (!int.TryParse(priceInput.text, out int price))
        {
            Debug.LogWarning("[ProposalEntry] Invalid price input");
            return;
        }

        percent = Mathf.Clamp(percent, 1, 100);

        MarketManager.Instance.CmdSubmitProposal(proposer, companyName, percent, price);
        Debug.Log($"[UI] Proposal sent for {percent}% of {companyName} @ ${price}");
        
        percentInput.text = "";
        priceInput.text = "";
        gameObject.SetActive(false);
    }

    
}