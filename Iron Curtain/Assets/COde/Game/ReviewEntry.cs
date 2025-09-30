using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ReviewEntry : MonoBehaviour
{
    public TMP_Text companyNameText;
    public TMP_Text proposerNameText;
    public TMP_Text percentText;
    public TMP_Text priceText;
    public Button acceptButton;
    public Button rejectButton;

    private string companyName;
    private int proposalIndex;

    public void Setup(string compName, int index, Proposal proposal)
    {
        companyName = compName;
        proposalIndex = index;

        if (companyNameText != null)
            companyNameText.text = compName;
        if (proposerNameText != null)
            proposerNameText.text = proposal.proposer.playerName.Value;
        if (percentText != null)
            percentText.text = $"{proposal.percent}%";
        if (priceText != null)
            priceText.text = $"${proposal.price}";

        // Hook up buttons
        acceptButton.onClick.RemoveAllListeners();
        rejectButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(OnAccept);
        rejectButton.onClick.AddListener(OnReject);
    }

    private void OnAccept()
    {
        MarketManager.Instance.CmdResolveProposal(companyName, proposalIndex, true);
        Destroy(gameObject);
    }

    private void OnReject()
    {
        MarketManager.Instance.CmdResolveProposal(companyName, proposalIndex, false);
        Destroy(gameObject);
    }
}