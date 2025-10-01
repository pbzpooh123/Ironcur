using FishNet;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InvestmentUI : MonoBehaviour
{
    public static InvestmentUI Instance;

    [Header("UI References")]
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text costText;
    public Button buyButton;
    public Button skipButton;

    private PlayerPawn currentPawn;
    private int currentTileIndex;

    private void Awake() => Instance = this;

    /// <summary>
    /// Show company purchase option when landing on a factory tile.
    /// </summary>
    public void ShowOptions(PlayerPawn pawn, int tileIndex, string companyName, int cost, bool isCompany)
    {
        currentPawn = pawn;
        currentTileIndex = tileIndex;

        titleText.text = $"Found {companyName}?";
        costText.text = $"Founding Cost: ${cost}";
        panel.SetActive(true);

        buyButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();

        buyButton.onClick.AddListener(OnBuyCompanyClicked);
        skipButton.onClick.AddListener(OnSkipClicked);

        // Disable End Turn while popup is active
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();
    }

    public void OnBuyCompanyClicked()
    {
        MarketManager.Instance.CmdBuyCompany(currentPawn, currentTileIndex);
        CloseAndContinue();
    }

    private void OnSkipClicked()
    {
        CloseAndContinue();
    }

    private void CloseAndContinue()
    {
        panel.SetActive(false);

        if (currentPawn != null)
        {
            // After tile action → show ProposalUI for this pawn
            MarketManager.Instance.CmdRequestProposalUI();
        }
    }

}
