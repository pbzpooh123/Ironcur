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

    public void ShowOptions(PlayerPawn pawn, int tileIndex, string companyName, int costMaybeBase, bool isCompany)
    {
        currentPawn = pawn;
        currentTileIndex = tileIndex;

        // UI placeholder while we fetch/compute
        if (titleText) titleText.text = $"Found {companyName}?";
        if (costText)  costText.text  = "Founding Cost: —";
        if (buyButton) buyButton.interactable = false;

        panel.SetActive(true);

        buyButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyCompanyClicked);
        skipButton.onClick.AddListener(OnSkipClicked);

        // live money gate once price is ready
        pawn.money.OnChange -= OnMoneyChanged;
        pawn.money.OnChange += OnMoneyChanged;

        // disable EndTurn while popup is up
        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();

        // Start (re)computing price safely
        if (_priceCo != null) StopCoroutine(_priceCo);
        _priceCo = StartCoroutine(CoComputePrice(costMaybeBase));
    }

    private IEnumerator CoComputePrice(int costFallback)
    {
        _effectiveCost = -1;

        // Wait a few frames for tile & EventManager to be ready (scene startup race)
        var timeout = 0.5f; // seconds
        TileData tile = null;

        while (timeout > 0f)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.boardTiles != null &&
                currentTileIndex >= 0 &&
                currentTileIndex < GameManager.Instance.boardTiles.Length)
            {
                var go = GameManager.Instance.boardTiles[currentTileIndex];
                if (go) tile = go.GetComponent<TileData>();
            }

            // We need at least some base cost (>0). Use tile first, then fallback arg.
            int baseCost = (tile != null && tile.companyCost > 0) ? tile.companyCost
                                                                  : (costFallback > 0 ? costFallback : 0);

            if (baseCost > 0) // good to compute now
            {
                float mult = 1f;
                if (EventManager.Instance != null && tile != null)
                    mult = EventManager.Instance.GetActiveSectorPriceMult(tile.sector);

                // Mirror server formula and clamp
                _effectiveCost = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseCost) * Mathf.Max(0f, mult)));
                break;
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Final guard: never show 0 even if authoring was wrong
        if (_effectiveCost < 1) _effectiveCost = 1;

        // Update UI
        if (costText)
            costText.text = $"Founding Cost: ${_effectiveCost}M";

        if (buyButton && currentPawn != null)
            buyButton.interactable = (currentPawn.money.Value >= _effectiveCost);

        _priceCo = null;
    }

    private void OnMoneyChanged(int oldVal, int newVal, bool asServer)
    {
        if (!panel || !panel.activeInHierarchy || buyButton == null) return;
        if (_effectiveCost < 1) { buyButton.interactable = false; return; } // wait for price
        buyButton.interactable = newVal >= _effectiveCost;
    }

    private void OnBuyCompanyClicked()
    {
        if (currentPawn == null) return;
        buyButton.interactable = false;
        // Server recomputes authoritatively (already correct on your side)
        MarketManager.Instance.CmdBuyCompany(currentTileIndex);
        CloseAndContinue();
    }

    private void OnSkipClicked() => CloseAndContinue();

    public void CloseAndContinue()
    {
        if (_priceCo != null) { StopCoroutine(_priceCo); _priceCo = null; }
        if (currentPawn != null) currentPawn.money.OnChange -= OnMoneyChanged;

        if (panel) panel.SetActive(false);

        if (currentPawn != null)
        {
            currentPawn.CmdTileActionComplete();
            MarketManager.Instance.CmdRequestProposalUI();
        }
    }
}
