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

    // NEW: cache the surge-adjusted cost we actually require
    private int _effectiveCost = 0;

    private void Awake() => Instance = this;

    public void ShowOptions(PlayerPawn pawn, int tileIndex, string companyName, int cost /*may be base*/, bool isCompany)
    {
        currentPawn = pawn;
        currentTileIndex = tileIndex;

        // compute surge-adjusted cost to match the server
        var tile = GameManager.Instance.boardTiles[currentTileIndex].GetComponent<TileData>();
        float mult = 1f;
        if (EventManager.Instance != null && tile != null)
            mult = EventManager.Instance.GetActiveSectorPriceMult(tile.sector);

        // prefer tile.companyCost (authoritative base), then apply surge
        int baseCost = tile != null ? tile.companyCost : cost;
        _effectiveCost = Mathf.RoundToInt(baseCost * mult);

        // UI text
        titleText.text = $"Found {companyName}?";
        if (Mathf.Approximately(mult, 1f))
            costText.text = $"Founding Cost: ${_effectiveCost}";
        else
            costText.text = $"Founding Cost: ${_effectiveCost}  (x{mult:0.##} sector surge)";

        panel.SetActive(true);

        // wire buttons
        buyButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyCompanyClicked);
        skipButton.onClick.AddListener(OnSkipClicked);

        // initial state uses EFFECTIVE cost
        buyButton.interactable = pawn.money.Value >= _effectiveCost;

        // avoid duplicate subscriptions if ShowOptions is called again
        pawn.money.OnChange -= OnMoneyChanged;
        pawn.money.OnChange += OnMoneyChanged;

        // disable End Turn while popup active
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();
    }

    private void OnMoneyChanged(int oldVal, int newVal, bool asServer)
    {
        if (!panel || !panel.activeInHierarchy) return;
        if (currentPawn == null) return;

        // just use the cached effective cost (matches what server will charge)
        buyButton.interactable = newVal >= _effectiveCost;
    }

    private void CloseAndContinue()
    {
        // tidy up listener
        if (currentPawn != null)
            currentPawn.money.OnChange -= OnMoneyChanged;

        if (panel) panel.SetActive(false);

        if (currentPawn != null)
        {
            currentPawn.CmdTileActionComplete();
            MarketManager.Instance.CmdRequestProposalUI();
        }
    }

    public void OnBuyCompanyClicked()
    {
        if (currentPawn == null) return;

        Debug.Log($"[InvestmentUI] Buy clicked by {currentPawn.playerName.Value}, tile={currentTileIndex}");
        buyButton.interactable = false; // prevent double clicks

        // server re-computes the same surge-adjusted cost; this just triggers it
        MarketManager.Instance.CmdBuyCompany(currentTileIndex);

        CloseAndContinue();
    }

    private void OnSkipClicked() => CloseAndContinue();
}
