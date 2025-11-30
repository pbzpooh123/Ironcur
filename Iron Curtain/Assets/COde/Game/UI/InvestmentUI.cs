using FishNet;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class InvestmentUI : MonoBehaviour
{
    public static InvestmentUI Instance;

    [Header("Left panel")]
    public GameObject panel;
    public TMP_Text companyNameText;       // ชื่อบริษัท (ใหญ่ ๆ ด้านซ้าย)
    public TMP_Text priceText;             // "ราคา: $400M"
    public TMP_Text playerMoneyText;       // "จำนวนเงินที่คุณมี 1506 M"
    public Image companyIcon;              // ไอคอนบริษัท (ถ้ามี; ไม่บังคับ)

    [Header("Buttons")]
    public Button buyButton;               // ปุ่ม "ซื้อ"
    public Button closeButton;             // ปุ่ม X มุมขวาบน

    [Header("Right panel")]
    public TMP_Text effectHeaderText;      // ใส่เป็น "ผลกระทบที่อาจเกิดขึ้น" ใน Inspector ก็ได้
    public TMP_Text effectBodyText;        // บรรทัดผลกระทบ

    private PlayerPawn currentPawn;
    private int currentTileIndex;
    private string _currentCompanyName;
    private TileData _currentTile;

    private int _effectiveCost = -1;       // -1 = ยังไม่คำนวณเสร็จ
    private Coroutine _priceCo;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    public void ShowOptions(PlayerPawn pawn, int tileIndex, string companyName, int costMaybeBase, bool isCompany)
    {
        currentPawn       = pawn;
        currentTileIndex  = tileIndex;
        _currentCompanyName = companyName;
        _currentTile      = null;
        _effectiveCost    = -1;

        if (panel != null && !panel.activeSelf)
            panel.SetActive(true);

        // ----- Left panel text -----
        if (companyNameText != null)
            companyNameText.text = companyName;

        if (priceText != null)
            priceText.text = "ราคา: —";

        if (playerMoneyText != null)
            playerMoneyText.text = $"จำนวนเงินที่คุณมี  {pawn.money.Value} M";

        if (buyButton != null)
        {
            buyButton.interactable = false;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyCompanyClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();

        pawn.money.OnChange -= OnMoneyChanged;
        pawn.money.OnChange += OnMoneyChanged;

        if (_priceCo != null) StopCoroutine(_priceCo);
        _priceCo = StartCoroutine(CoComputePrice(costMaybeBase));

        FillEffectPreview();
    }

    private IEnumerator CoComputePrice(int costFallback)
    {
        _effectiveCost = -1;
        _currentTile = null;

        float timeout = 0.5f;
        TileData tile = null;

        while (timeout > 0f)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.boardTiles != null &&
                currentTileIndex >= 0 &&
                currentTileIndex < GameManager.Instance.boardTiles.Length)
            {
                var go = GameManager.Instance.boardTiles[currentTileIndex];
                if (go != null) tile = go.GetComponent<TileData>();
            }

            if (tile != null)
                break;

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        _currentTile = tile;
        _effectiveCost = Mathf.Max(1, costFallback); // ใช้ราคาที่ server ส่งมาเป็นหลัก

        if (priceText != null)
            priceText.text = $"ราคา: ${_effectiveCost}M";

        if (buyButton != null && currentPawn != null)
            buyButton.interactable = (currentPawn.money.Value >= _effectiveCost);

        _priceCo = null;
    }

    private void OnMoneyChanged(int oldVal, int newVal, bool asServer)
    {
        if (panel == null || !panel.activeInHierarchy) return;

        if (playerMoneyText != null)
            playerMoneyText.text = $"จำนวนเงินที่คุณมี  {newVal} M";

        if (buyButton != null)
        {
            if (_effectiveCost < 1)
                buyButton.interactable = false;
            else
                buyButton.interactable = (newVal >= _effectiveCost);
        }
    }

    private void OnBuyCompanyClicked()
    {
        if (currentPawn == null) return;
        if (buyButton != null) buyButton.interactable = false;

        // server-side buy
        MarketManager.Instance.CmdBuyCompany(currentTileIndex);

        CloseAndContinue();
    }

    private void OnCloseClicked()
    {
        CloseAndContinue();
    }

    private void FillEffectPreview()
    {
        if (effectBodyText != null)
        {
            effectBodyText.text =
                "ปี 19XX รายได้ +100% ในรอบนั้น\n\n" +
                "ปี 19XX รายได้ลดลง -200% ในรอบนั้น";
        }
    }

    public void CloseAndContinue()
    {
        if (_priceCo != null)
        {
            StopCoroutine(_priceCo);
            _priceCo = null;
        }

        if (currentPawn != null)
            currentPawn.money.OnChange -= OnMoneyChanged;

        if (panel != null)
            panel.SetActive(false);

        // แจ้ง server ว่าจบ tile action แล้ว และขอเปิด proposal UI ต่อ
        if (currentPawn != null)
        {
            currentPawn.CmdTileActionComplete();
            MarketManager.Instance.CmdRequestProposalUI();
        }
    }
}
