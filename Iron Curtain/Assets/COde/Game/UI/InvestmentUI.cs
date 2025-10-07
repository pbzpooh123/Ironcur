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

        // Initial state
        buyButton.interactable = pawn.money.Value >= cost;

        // Auto-update on money change
        pawn.money.OnChange += OnMoneyChanged;

        // Disable End Turn while popup active
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();
    }

    private void OnMoneyChanged(int oldVal, int newVal, bool asServer)
    {
        var tile = GameManager.Instance.boardTiles[currentTileIndex].GetComponent<TileData>();
        if (tile != null && currentPawn != null)
            buyButton.interactable = newVal >= tile.companyCost;
    }

    private void CloseAndContinue()
    {
        panel.SetActive(false);
        if (currentPawn != null)
        {
            currentPawn.money.OnChange -= OnMoneyChanged; // clean up listener

            currentPawn.CmdTileActionComplete();
            MarketManager.Instance.CmdRequestProposalUI();
        }
    }


    public void OnBuyCompanyClicked()
    {
        Debug.Log($"[InvestmentUI] Buy clicked by {currentPawn?.playerName.Value}, tile={currentTileIndex}");
        buyButton.interactable = false;  // prevent double click

        // Call RPC with just tileIndex; server will resolve pawn via conn
        MarketManager.Instance.CmdBuyCompany(currentTileIndex);

        CloseAndContinue();
    }

    private void OnSkipClicked()
    {
        CloseAndContinue();
    }
    
}