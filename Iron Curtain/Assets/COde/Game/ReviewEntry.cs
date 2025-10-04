using UnityEngine;
using TMPro;
using UnityEngine.UI;
using FishNet;

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

        if (companyNameText) companyNameText.text = compName;
        if (proposerNameText) proposerNameText.text = proposal.proposer != null ? proposal.proposer.playerName.Value : "???";
        if (percentText) percentText.text = $"{proposal.percent}%";
        if (priceText) priceText.text = $"${proposal.price}";

        acceptButton.onClick.RemoveAllListeners();
        rejectButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(() =>
        {
            MarketManager.Instance.CmdResolveProposal(InstanceFinder.ClientManager.Connection, companyName, proposalIndex, true);
        });
        rejectButton.onClick.AddListener(() =>
        {
            MarketManager.Instance.CmdResolveProposal(InstanceFinder.ClientManager.Connection, companyName, proposalIndex, false);
        });
    }
}