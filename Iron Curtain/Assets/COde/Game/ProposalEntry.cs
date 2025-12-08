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

    [Header("Company Icon")]
    public Image companyIconImage;

    [Header("Owner Display")]
    public TMP_Text ownerNameText;     

    public string CompanyKey { get; private set; }

    private CompanyRecord _company;
    private PlayerPawn _pawn;
    private bool _submittedThisEntry = false;

    public void Setup(CompanyRecord company, PlayerPawn pawn)
    {
        _company = company;
        _pawn = pawn;
        CompanyKey = company.companyName;

        if (companyNameText != null)
            companyNameText.text = company.companyName;

        // icon
        if (companyIconImage != null)
        {
            var icon = CompanyIconHelper.GetIconForCompany(company.companyName);
            companyIconImage.sprite  = icon;
            companyIconImage.enabled = (icon != null);
        }

        // owner text
        UpdateOwnerDisplay();

        percentInput.onValueChanged.RemoveAllListeners();
        priceInput.onValueChanged.RemoveAllListeners();
        percentInput.onValueChanged.AddListener(_ => Validate());
        priceInput.onValueChanged.AddListener(_ => Validate());

        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(OnSubmitClicked);

        _submittedThisEntry = false;
        if (warningText != null) warningText.text = "";

        Validate();
    }

    private void UpdateOwnerDisplay()
    {
        if (_company == null)
        {
            if (ownerNameText != null) ownerNameText.text = "Owner: —";
            return;
        }

        string ownerName = _company.ownerName;

        if (string.IsNullOrEmpty(ownerName) && _company.owner != null)
        {
            if (!string.IsNullOrEmpty(_company.owner.playerName.Value))
                ownerName = _company.owner.playerName.Value;
        }

        if (ownerNameText != null)
        {
            ownerNameText.text = string.IsNullOrEmpty(ownerName)
                ? "Owner: —"
                : $"{ownerName}";
        }
    }

    private float ComputeMaxPercent()
    {
        if (_company == null || _pawn == null) return 0f;

        // Seller available %
        float sellerAvail = 0f;
        if (_company.owner != null)
        {
            sellerAvail = _company.GetOwnership(_company.owner);
        }
        else if (!string.IsNullOrEmpty(_company.ownerName))
        {
            var ownerPawn = GameManager.Instance?.Players.Find(p => p.playerName.Value == _company.ownerName);
            if (ownerPawn != null)
                sellerAvail = _company.GetOwnership(ownerPawn);
        }

        // Buyer room to 100%
        float buyerHas  = _company.GetOwnership(_pawn);
        float buyerRoom = Mathf.Max(0f, 100f - buyerHas);

        float max = Mathf.Max(0f, Mathf.Min(sellerAvail, buyerRoom));

        // ถ้าอยากล็อกเป็น step 0.5 เช่น 37.5, 62.5 ให้เปิดใช้สองบรรทัดนี้
        max = Mathf.Floor(max * 2f) / 2f;

        return max;
    }

    private void Validate()
    {
        if (_submittedThisEntry || _company == null || _pawn == null)
        {
            submitButton.interactable = false;
            return;
        }

        // เปอร์เซ็นต์เป็น float (รองรับทศนิยม)
        if (!float.TryParse(percentInput.text, out float pct) ||
            !int.TryParse(priceInput.text, out int price))
        {
            submitButton.interactable = false;
            return;
        }

        float maxPct = ComputeMaxPercent();

        // ขั้นต่ำ 0.5% (จะไปปรับทีหลังก็ได้)
        if (pct < 0.5f || pct > maxPct || maxPct <= 0f)
        {
            submitButton.interactable = false;
            if (warningText != null)
            {
                if (maxPct <= 0f) warningText.text = "No transferable shares available.";
                else warningText.text = $"Max you can request now is {maxPct:0.#}%.";
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

        if (warningText != null)
        {
            warningText.text = (_pawn.money.Value < price)
                ? "You don't have enough cash now; owner can still accept."
                : "";
        }

        submitButton.interactable = true;
    }

    private void OnSubmitClicked()
    {
        if (_company == null || _pawn == null) return;
        if (!float.TryParse(percentInput.text, out float pct)) return;
        if (!int.TryParse(priceInput.text, out int price)) return;

        float maxPct = ComputeMaxPercent();
        pct = Mathf.Clamp(pct, 0.5f, Mathf.Max(0.5f, maxPct));

        submitButton.interactable = false;
        _submittedThisEntry = true;

        // ตรงนี้คุณต้องเปลี่ยนลายเซ็น RPC ให้รับ float แทน int
        MarketManager.Instance.CmdSubmitProposal(_company.companyName, pct, price);
        ProposalUI.Instance.CloseAndNotifyServer();
    }
    
    public void OnServerRejected(string reason)
    {
        _submittedThisEntry = false;
        if (warningText != null) warningText.text = reason;
        Validate();
    }
}
