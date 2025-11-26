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

    [Header("Help / Info")]
    public Button helpButton;         // ? button
    public GameObject helpPanel;      // panel to open
    public TMP_Text helpTitleText;    // title inside help panel
    public TMP_Text helpBodyText;     // body text inside help panel
    public Button helpCloseButton;    // close button on help panel

    private PlayerPawn currentPawn;
    private int currentTileIndex;
    private string _currentCompanyName;
    private TileData _currentTile;

    private int _effectiveCost = -1;      // -1 = not ready yet
    private Coroutine _priceCo;

    private void Awake()
    {
        Instance = this;

        // make sure help panel starts hidden
        if (helpPanel != null)
            helpPanel.SetActive(false);

        if (helpCloseButton != null)
            helpCloseButton.onClick.AddListener(CloseHelpPanel);
    }

    public void ShowOptions(PlayerPawn pawn, int tileIndex, string companyName, int costMaybeBase, bool isCompany)
    {
        currentPawn = pawn;
        currentTileIndex = tileIndex;
        _currentCompanyName = companyName;
        _currentTile = null;

        if (titleText) titleText.text = $"ซื้อ {companyName} นี้ไหม?";
        if (costText)  costText.text  = "ราคา: —";
        if (buyButton) buyButton.interactable = false;

        panel.SetActive(true);

        buyButton.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyCompanyClicked);
        skipButton.onClick.AddListener(OnSkipClicked);

        // hook help / ? button
        if (helpButton)
        {
            helpButton.onClick.RemoveAllListeners();
            helpButton.onClick.AddListener(OnHelpClicked);
        }

        // live money gate once price is ready
        pawn.money.OnChange -= OnMoneyChanged;
        pawn.money.OnChange += OnMoneyChanged;

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();

        if (_priceCo != null) StopCoroutine(_priceCo);
        _priceCo = StartCoroutine(CoComputePrice(costMaybeBase));
    }

    private IEnumerator CoComputePrice(int costFallback)
    {
        _effectiveCost = -1;
        _currentTile = null;

        // Optional: still find the tile, but only for help text / sector info
        var timeout = 0.5f;
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

            if (tile != null)
                break;

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // >>> KEY CHANGE: trust the server-sent price <<<
        _effectiveCost = Mathf.Max(1, costFallback);
        _currentTile = tile;

        if (costText)
            costText.text = $"ราคา: ${_effectiveCost}M";

        if (buyButton && currentPawn != null)
            buyButton.interactable = (currentPawn.money.Value >= _effectiveCost);

        _priceCo = null;
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

    // --- ? button: open help panel ---
    private void OnHelpClicked()
    {
        // Try to get the latest tile + price info
        TileData tile = _currentTile;

        if (tile == null &&
            GameManager.Instance != null &&
            GameManager.Instance.boardTiles != null &&
            currentTileIndex >= 0 &&
            currentTileIndex < GameManager.Instance.boardTiles.Length)
        {
            var go = GameManager.Instance.boardTiles[currentTileIndex];
            if (go) tile = go.GetComponent<TileData>();
        }

        int price = _effectiveCost;
        if (price < 1 && tile != null && MarketManager.Instance != null)
        {
            price = MarketManager.Instance.ComputeEffectivePrice(tile);
        }

        string name = string.IsNullOrEmpty(_currentCompanyName) ? "บริษัท" : _currentCompanyName;
        string sector = (tile != null && !string.IsNullOrEmpty(tile.sector)) ? tile.sector : "—";

        // Fill help panel text
        if (helpTitleText != null)
            helpTitleText.text = $"การลงทุนในบริษัท";

        if (helpBodyText != null)
        {
            helpBodyText.text =
                $"ประเภทบริษัท: {sector}";
        }

        // Show help panel (or fallback to sideevent if you forgot to wire it)
        if (helpPanel != null)
        {
            helpPanel.SetActive(true);
        }
        else
        {
            // fallback so it still works even if panel not assigned
            EventUI.Instance?.SideeventShow(helpBodyText != null ? helpBodyText.text : "ข้อมูลการลงทุน", true);
        }
    }

    private void CloseHelpPanel()
    {
        if (helpPanel != null)
            helpPanel.SetActive(false);
    }

    public void CloseAndContinue()
    {
        if (_priceCo != null) { StopCoroutine(_priceCo); _priceCo = null; }
        if (currentPawn != null) currentPawn.money.OnChange -= OnMoneyChanged;

        if (panel) panel.SetActive(false);
        CloseHelpPanel(); // just in case help is open

        if (currentPawn != null)
        {
            currentPawn.CmdTileActionComplete();
            MarketManager.Instance.CmdRequestProposalUI();
        }
    }
}
