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
    private bool buyingCompany;

    private void Awake() => Instance = this;

    public void ShowOptions(PlayerPawn pawn,int tileIndex, string companyName, int cost, bool isCompany)
    {
        currentPawn = pawn;
        currentTileIndex = tileIndex;
        buyingCompany = isCompany;

        titleText.text = isCompany ? $"Buy {companyName}?" : $"Invest in {companyName}?";
        costText.text = $"Cost: ${cost}";

        panel.SetActive(true);

        buyButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();

        if (buyingCompany)
        {
            buyButton.onClick.AddListener(OnBuyCompanyClicked);
        }
        else
        {
            buyButton.onClick.AddListener(OnBuyShareClicked);
        }

        skipButton.onClick.AddListener(OnSkipClicked);
        // Disable End Turn while popup open
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();
    }
    
    public void OnBuyCompanyClicked()
    {
        if (InstanceFinder.ClientManager.Connection != null)
        {
            MarketManager.Instance.CmdBuyCompany(
                InstanceFinder.ClientManager.Connection, 
                currentTileIndex
            );
            panel.SetActive(false);
            CloseAndContinue();

        }
    }
    
    public void OnBuyShareClicked()
    {
        if (InstanceFinder.ClientManager.Connection != null)
        {
            MarketManager.Instance.CmdBuyShare(
                InstanceFinder.ClientManager.Connection, 
                currentTileIndex
            );
            panel.SetActive(false);
            CloseAndContinue();
        }
        

    }

    private void OnSkipClicked()
    {
        panel.SetActive(false);
        CloseAndContinue();

    }
    
    private void CloseAndContinue()
    {
        panel.SetActive(false);

        if (currentPawn != null)
        {
            currentPawn.TargetOpenStockUI(currentPawn.Owner);
        }
    }
}