using FishNet;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    private int _effectiveCost = -1;      // -1 = not ready yet
    private Coroutine _priceCo;

    private void Awake() => Instance = this;

    public void ShowOptions(PlayerPawn pawn, int tileIndex, string companyName, int finalCostFromServer, bool isCompany)
    {
        currentPawn = pawn;
        currentTileIndex = tileIndex;

        if (titleText) titleText.text = $"ซื้อ {companyName} นี้ไหม?";

        _effectiveCost = Mathf.Max(1, finalCostFromServer);

        if (costText)
            costText.text = $"ราคา: ${_effectiveCost}M";

        panel.SetActive(true);

        buyButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyCompanyClicked);
        skipButton.onClick.AddListener(OnSkipClicked);

        pawn.money.OnChange -= OnMoneyChanged;
        pawn.money.OnChange += OnMoneyChanged;

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();

        if (buyButton)
            buyButton.interactable = (pawn.money.Value >= _effectiveCost);
    }


    private void OnMoneyChanged(int oldVal, int newVal, bool asServer)
    {
        if (!panel || !panel.activeInHierarchy || buyButton == null) return;
        if (_effectiveCost < 1) { buyButton.interactable = false; return; }
        buyButton.interactable = newVal >= _effectiveCost;
    }

    private void OnBuyCompanyClicked()
    {
        if (currentPawn == null) return;
        buyButton.interactable = false;
        MarketManager.Instance.CmdBuyCompany(currentTileIndex);
        CloseAndContinue();
    }

    private void OnSkipClicked() => CloseAndContinue();

    public void CloseAndContinue()
    {
        if (currentPawn != null) currentPawn.money.OnChange -= OnMoneyChanged;

        if (panel) panel.SetActive(false);

        if (currentPawn != null)
        {
            currentPawn.CmdTileActionComplete();
            MarketManager.Instance.CmdRequestProposalUI();
        }
    }
}
