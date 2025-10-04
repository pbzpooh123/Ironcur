using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet;

public class ProposalEntry : MonoBehaviour
{
    public TMP_Text companyNameText;
    public TMP_Text ownerNameText;
    public TMP_InputField percentInput;
    public TMP_InputField priceInput;
    public Button submitButton;

    private string companyName;

    public void Setup(CompanyRecord company, PlayerPawn pawn)
    {
        companyName = company.companyName;

        if (companyNameText) companyNameText.text = company.companyName;
        if (ownerNameText) ownerNameText.text = $"Owner: {(company.owner != null ? company.owner.playerName.Value : "N/A")}";

        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(OnSubmit);
    }

    private void OnSubmit()
    {
        int percent = 0;
        int price = 0;

        int.TryParse(percentInput?.text, out percent);
        int.TryParse(priceInput?.text, out price);

        // Call server with current connection
        MarketManager.Instance.CmdSubmitProposal(InstanceFinder.ClientManager.Connection, companyName, percent, price);
    }
}