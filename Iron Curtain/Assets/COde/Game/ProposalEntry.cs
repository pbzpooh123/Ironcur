using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProposalEntry : MonoBehaviour
{
    public TMP_Text companyNameText;
    public TMP_InputField percentInput;
    public TMP_InputField priceInput;
    public Button submitButton;
    public TMP_Text warningText; // optional for feedback

    public string CompanyKey { get; private set; }

    private CompanyRecord _company;
    private PlayerPawn _pawn;
    private bool _submittedThisEntry = false;

    public void Setup(CompanyRecord company, PlayerPawn pawn)
    {
        _company = company;
        _pawn = pawn;
        CompanyKey = company.companyName;

        companyNameText.text = company.companyName;

        percentInput.onValueChanged.AddListener(_ => Validate());
        priceInput.onValueChanged.AddListener(_ => Validate());

        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(OnSubmitClicked);

        _submittedThisEntry = false;
        if (warningText != null) warningText.text = "";

        Validate();
    }

    private void Validate()
    {
        if (_submittedThisEntry) { submitButton.interactable = false; return; }
        if (_company == null || _pawn == null) { submitButton.interactable = false; return; }

        if (!int.TryParse(percentInput.text, out int pct)) { submitButton.interactable = false; return; }
        if (!int.TryParse(priceInput.text, out int price))  { submitButton.interactable = false; return; }

        if (pct < 1 || pct > 40) { submitButton.interactable = false; return; }
        if (price < 1) { submitButton.interactable = false; return; }

        // Cannot propose on your own company
        if (!string.IsNullOrEmpty(_company.ownerName) &&
            _company.ownerName == _pawn.playerName.Value)
        {
            submitButton.interactable = false; 
            if (warningText != null) warningText.text = "Can't propose to your own company.";
            return;
        }

        // Must be able to afford at SUBMIT time
        if (_pawn.money.Value < price)
        {
            submitButton.interactable = false;
            if (warningText != null) warningText.text = "Not enough money to submit.";
            return;
        }

        submitButton.interactable = true;
        if (warningText != null) warningText.text = "";
    }

    private void OnSubmitClicked()
    {
        if (_company == null || _pawn == null) return;
        if (!int.TryParse(percentInput.text, out int pct)) return;
        if (!int.TryParse(priceInput.text, out int price)) return;

        // Prevent double press locally.
        submitButton.interactable = false;
        _submittedThisEntry = true;

        // ✅ New signature: (string companyName, int percent, int price)
        MarketManager.Instance.CmdSubmitProposal(_company.companyName, pct, price);
    }
    
    public void OnServerRejected(string reason)
    {
        _submittedThisEntry = false;
        if (warningText != null) warningText.text = reason;
        Validate();
    }
}
