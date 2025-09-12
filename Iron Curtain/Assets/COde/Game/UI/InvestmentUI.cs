using FishNet;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InvestmentUI : MonoBehaviour
{
    public static InvestmentUI Instance;

    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text costText;
    public Button buyButton;
    public Button skipButton;

    private int currentTileIndex;
    private bool buyingCompany;

    private void Awake() => Instance = this;

    public void ShowOptions(int tileIndex, string companyName, int cost, bool isCompany)
    {
        currentTileIndex = tileIndex;
        buyingCompany = isCompany;

        titleText.text = isCompany ? $"Buy {companyName}?" : $"Invest in {companyName}?";
        costText.text = $"Cost: ${cost}";

        panel.SetActive(true);

        buyButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();

        buyButton.onClick.AddListener(OnBuyCompanyClicked);
        skipButton.onClick.AddListener(OnSkipClicked);
    }
    
    public void OnBuyCompanyClicked()
    {
        if (InstanceFinder.ClientManager.Connection != null)
        {
            MarketManager.Instance.CmdBuyCompany(InstanceFinder.ClientManager.Connection, currentTileIndex);
            panel.SetActive(false);
        }
    }

    private void OnSkipClicked()
    {
        panel.SetActive(false);
    }
}