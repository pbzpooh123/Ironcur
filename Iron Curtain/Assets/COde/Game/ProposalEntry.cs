using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProposalEntry : MonoBehaviour
{
    public TMP_Text companyNameText;
    public TMP_InputField percentInput;
    public TMP_InputField priceInput;
    public Button submitButton;
    public TMP_Text warningText; 

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

    private int ComputeMaxPercent()
    {
        if (_company == null || _pawn == null) return 0;

        // Seller available %
        int sellerAvail = 0;
        if (_company.owner != null)
            sellerAvail = _company.GetOwnership(_company.owner);
        else if (!string.IsNullOrEmpty(_company.ownerName))
        {
            var ownerPawn = GameManager.Instance?.Players.Find(p => p.playerName.Value == _company.ownerName);
            if (ownerPawn != null)
                sellerAvail = _company.GetOwnership(ownerPawn);
        }

        // Buyer room to 100%
        int buyerHas  = _company.GetOwnership(_pawn);
        int buyerRoom = Mathf.Max(0, 100 - buyerHas);

        return Mathf.Max(0, Mathf.Min(sellerAvail, buyerRoom));
    }

    private void Validate()
    {
        if (_submittedThisEntry || _company == null || _pawn == null)
        {
            submitButton.interactable = false;
            return;
        }

        // Basic parses
        if (!int.TryParse(percentInput.text, out int pct) ||
            !int.TryParse(priceInput.text, out int price))
        {
            submitButton.interactable = false;
            return;
        }

        int maxPct = ComputeMaxPercent();
        if (pct < 1 || pct > maxPct || maxPct <= 0)
        {
            submitButton.interactable = false;
            if (warningText != null)
            {
                if (maxPct <= 0) warningText.text = "No transferable shares available.";
                else warningText.text = $"Max you can request now is {maxPct}%.";
            }
            return;
        }

        if (price < 1)
        {
            submitButton.interactable = false;
            if (warningText != null) warningText.text = "Price must be ≥ 1.";
            return;
        }

        // Can't propose to your own company
        if (!string.IsNullOrEmpty(_company.ownerName) &&
            _company.ownerName == _pawn.playerName.Value)
        {
            submitButton.interactable = false;
            if (warningText != null) warningText.text = "Can't propose to your own company.";
            return;
        }

        // Do NOT hard-block on buyer cash; server will enforce/bailout.
        // Optional: soft warning
        if (warningText != null)
        {
            warningText.text = (_pawn.money.Value < price) ? "You don't have enough cash now; owner can still accept." : "";
        }

        submitButton.interactable = true;
    }

    private void OnSubmitClicked()
    {
        if (_company == null || _pawn == null) return;
        if (!int.TryParse(percentInput.text, out int pct)) return;
        if (!int.TryParse(priceInput.text, out int price)) return;

        int maxPct = ComputeMaxPercent();
        pct = Mathf.Clamp(pct, 1, Mathf.Max(1, maxPct));

        submitButton.interactable = false;
        _submittedThisEntry = true;

        MarketManager.Instance.CmdSubmitProposal(_company.companyName, pct, price);
    }
    
    public void OnServerRejected(string reason)
    {
        _submittedThisEntry = false;
        if (warningText != null) warningText.text = reason;
        Validate();
    }
}
