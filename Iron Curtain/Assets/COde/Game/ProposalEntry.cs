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
        ownerNameText.text = $"Owner: {(company.owner != null ? company.owner.playerName.Value : "—")}";

        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(OnSubmitClicked);
    }


    private void OnSubmitClicked()
    {
        if (!int.TryParse(percentInput.text, out int percent))
        {
            Debug.LogWarning("Invalid percent");
            return;
        }
        if (!int.TryParse(priceInput.text, out int price))
        {
            Debug.LogWarning("Invalid price");
            return;
        }

        MarketManager.Instance.CmdSubmitProposal(proposer, companyName, percent, price);
        Debug.Log($"[UI] Proposal sent for {percent}% of {companyName} @ ${price}");
        
        percentInput.text = "";
        priceInput.text = "";
        gameObject.SetActive(false);
    }

    
}