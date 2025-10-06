using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProposalEntry : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text companyNameText;
    public TMP_InputField percentInput;
    public TMP_InputField priceInput;
    public Button submitButton;

    private CompanyRecord company;
    private PlayerPawn currentPawn;

    public void Setup(CompanyRecord record, PlayerPawn pawn)
    {
        company = record;
        currentPawn = pawn;

        companyNameText.text = record.companyName;

        // Clear input and set up listeners
        percentInput.text = "";
        priceInput.text = "";

        submitButton.interactable = false; // start disabled

        percentInput.onValueChanged.AddListener(_ => ValidateInputs());
        priceInput.onValueChanged.AddListener(_ => ValidateInputs());

        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(OnSubmitClicked);
    }

    private void ValidateInputs()
    {
        bool validPercent = int.TryParse(percentInput.text, out int p) && p > 0;
        bool validPrice   = int.TryParse(priceInput.text, out int pr) && pr > 0;

        // Enable only if both valid
        submitButton.interactable = validPercent && validPrice;
    }

    private void OnSubmitClicked()
    {
        if (company == null || currentPawn == null) return;

        if (!int.TryParse(percentInput.text, out int percent)) percent = 0;
        if (!int.TryParse(priceInput.text, out int price)) price = 0;

        if (percent <= 0 || price <= 0) return;

        MarketManager.Instance.CmdSubmitProposal(currentPawn.Owner, company.companyName, percent, price);

        // Disable to prevent double click
        submitButton.interactable = false;
    }
}