using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReviewEntry : MonoBehaviour
{
    [Header("UI Refs (assign in prefab)")]
    public TMP_Text proposerText;   // e.g., "Alice"
    public TMP_Text percentText;    // e.g., "25%"
    public TMP_Text priceText;      // e.g., "$1,500"
    public Button acceptButton;
    public Button rejectButton;

    [Header("Legacy fallback (optional)")]
    public TMP_Text infoText;       // only used if one of the 3 texts is missing

    private string _companyName;
    private int _index;
    private Proposal _proposal;

    private void Awake()
    {
        // Auto-wire if fields are empty (optional convenience)
        if (proposerText == null || percentText == null || priceText == null)
        {
            var labels = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in labels)
            {
                var n = t.gameObject.name.ToLower();
                if (proposerText == null && (n.Contains("proposer") || n.Contains("buyer") || n.Contains("from")))
                    proposerText = t;
                else if (percentText == null && (n.Contains("percent") || n.Contains("pct") || n.Contains("share")))
                    percentText = t;
                else if (priceText == null && (n.Contains("price") || n.Contains("offer") || n.Contains("amount")))
                    priceText = t;
            }
        }

        if (acceptButton == null || rejectButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                var n = b.name.ToLower();
                if (acceptButton == null && n.Contains("accept")) acceptButton = b;
                else if (rejectButton == null && n.Contains("reject")) rejectButton = b;
            }
        }

        // Soft warnings (we still fallback to infoText if needed)
        if ((proposerText == null || percentText == null || priceText == null) && infoText == null)
            Debug.LogWarning($"[ReviewEntry] Missing some TMP fields and no fallback infoText on {name}.");
        if (acceptButton == null) Debug.LogError($"[ReviewEntry] acceptButton is not assigned on {name}.");
        if (rejectButton == null) Debug.LogError($"[ReviewEntry] rejectButton is not assigned on {name}.");
    }

    public void Setup(string companyName, int index, Proposal p)
    {
        _companyName = companyName;
        _index = index;
        _proposal = p;

        if (_proposal == null)
        {
            Debug.LogError("[ReviewEntry] Setup called with null Proposal");
            gameObject.SetActive(false);
            return;
        }

        // Determine whether Accept can be clicked
        bool allowDebt = (MarketManager.Instance != null) && MarketManager.Instance.AllowDebtOnAccept;
        bool proposerKnown = (_proposal.proposer != null);
        string proposerName = proposerKnown ? _proposal.proposer.playerName.Value : "Unknown";
        bool proposerCanAfford = proposerKnown && (_proposal.proposer.money.Value >= _proposal.price);
        bool canAccept = allowDebt || proposerCanAfford;

        // Fill fields (or fallback)
        if (proposerText != null && percentText != null && priceText != null)
        {
            proposerText.text = proposerName;
            percentText.text  = $"{Mathf.Clamp(_proposal.percent, 0, 100)}%";
            priceText.text    = FormatMoney(_proposal.price);
        }
        else if (infoText != null)
        {
            infoText.text = $"{proposerName} offers {FormatMoney(_proposal.price)} for {_proposal.percent}%";
        }

        // Buttons
        if (acceptButton != null)
        {
            acceptButton.interactable = canAccept;
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(() =>
            {
                acceptButton.interactable = false;
                if (rejectButton != null) rejectButton.interactable = false;
                MarketManager.Instance.CmdResolveProposal(_companyName, _index, true);
            });
        }

        if (rejectButton != null)
        {
            rejectButton.interactable = true;
            rejectButton.onClick.RemoveAllListeners();
            rejectButton.onClick.AddListener(() =>
            {
                rejectButton.interactable = false;
                if (acceptButton != null) acceptButton.interactable = false;
                MarketManager.Instance.CmdResolveProposal(_companyName, _index, false);
            });
        }
    }

    private static string FormatMoney(int amount)
    {
        // $1,500M style (match the rest of your UI if needed)
        return "$" + amount.ToString("N0");
    }
}
