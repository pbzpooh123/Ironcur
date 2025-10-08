using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReviewEntry : MonoBehaviour
{
    [Header("UI Refs (assign in prefab if possible)")]
    public TMP_Text infoText;
    public Button acceptButton;
    public Button rejectButton;

    private string _companyName;
    private int _index;
    private Proposal _proposal;

    private void Awake()
    {
        // Auto-wire if not assigned (helps during iteration).
        if (infoText == null)
            infoText = GetComponentInChildren<TMP_Text>();
        if (acceptButton == null || rejectButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.name.ToLower().Contains("accept") && acceptButton == null)
                    acceptButton = b;
                else if (b.name.ToLower().Contains("reject") && rejectButton == null)
                    rejectButton = b;
            }
        }

        if (infoText == null)
            Debug.LogError($"[ReviewEntry] infoText is not assigned on {name}.");
        if (acceptButton == null)
            Debug.LogError($"[ReviewEntry] acceptButton is not assigned on {name}.");
        if (rejectButton == null)
            Debug.LogError($"[ReviewEntry] rejectButton is not assigned on {name}.");
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

        // ---- Defensive checks on UI ----
        if (infoText == null || acceptButton == null || rejectButton == null)
        {
            Debug.LogError("[ReviewEntry] Missing UI refs; cannot setup.");
            gameObject.SetActive(false);
            return;
        }

        string proposerName = (_proposal.proposer != null) ? _proposal.proposer.playerName.Value : "(unknown)";
        infoText.text = $"{proposerName} offers ${_proposal.price} for {_proposal.percent}%";

        // Determine whether Accept can be clicked
        bool allowDebt = (MarketManager.Instance != null) && MarketManager.Instance.AllowDebtOnAccept;

        bool proposerKnown = (_proposal.proposer != null);
        bool proposerCanAfford = proposerKnown && (_proposal.proposer.money.Value >= _proposal.price);

        // In Debt mode we allow accept even if proposer can't currently afford (server will bail them out).
        bool canAccept = allowDebt || proposerCanAfford;

        acceptButton.interactable = canAccept;
        rejectButton.interactable = true;

        // Clear previous listeners
        acceptButton.onClick.RemoveAllListeners();
        rejectButton.onClick.RemoveAllListeners();

        acceptButton.onClick.AddListener(() =>
        {
            acceptButton.interactable = false;
            rejectButton.interactable = false;
            MarketManager.Instance.CmdResolveProposal(_companyName, _index, true);  // no conn param
        });

        rejectButton.onClick.AddListener(() =>
        {
            acceptButton.interactable = false;
            rejectButton.interactable = false;
            MarketManager.Instance.CmdResolveProposal(_companyName, _index, false); // no conn param
        });
    }
}
