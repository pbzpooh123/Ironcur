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

            int baseCost = (tile != null && tile.companyCost > 0)
                ? tile.companyCost
                : (costFallback > 0 ? costFallback : 0);

            if (baseCost > 0)
            {
                float mult = 1f;
                if (EventManager.Instance != null && tile != null)
                    mult = EventManager.Instance.GetActiveSectorPriceMult(tile.sector);

                _effectiveCost = Mathf.Max(1,
                    Mathf.RoundToInt(Mathf.Max(1, baseCost) * Mathf.Max(0f, mult)));
                _currentTile = tile;
                break;
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_effectiveCost < 1) _effectiveCost = 1;

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
            helpTitleText.text = $"การลงทุนใน {name}";

        if (helpBodyText != null)
        {
            helpBodyText.text =
                $"• ราคาในตอนนี้: ${price}M " +
                $"• ประเภทบริษัท: {sector}" +
                $"• เมื่อซื้อ คุณจะเป็นเจ้าของ 100% ของบริษัทนี้ และช่องบนบอร์ดจะเปลี่ยนเป็นสีของคุณ" +
                $"• ทุก ๆ รอบ บริษัทจะจ่ายเงินปันผลให้ตามมูลค่าบริษัทและสัดส่วนหุ้นที่คุณถืออยู่" +
                $"ถ้าราคาขึ้นในอนาคต การถือบริษัทนี้อาจทำให้ได้เงินมากขึ้น ";
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
