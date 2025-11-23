using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StatsUI : MonoBehaviour
{
    public static StatsUI Instance;

    [Header("Root")]
    public GameObject panel;

    [Header("Basic")]
    public TMP_Text playerNameText;
    public TMP_Text cashText;
    public TMP_Text portfolioValueText;
    public TMP_Text totalWealthText;

    [Header("Ownership")]
    public TMP_Text companyCountText;      // จำนวนบริษัทที่ถือ
    public TMP_Text majorityCountText;     // จำนวนที่ถือ > 60%

    [Header("Flow Stats")]
    public TMP_Text taxPaidText;           // ภาษีที่จ่ายสะสม
    public TMP_Text bonusGotText;          // โบนัสที่ได้สะสม
    public TMP_Text bailoutsText;          // จำนวน bailout
    public TMP_Text jailVisitsText;        // เข้า jail กี่ครั้ง

    [Header("Market Stats")]
    public TMP_Text proposalsSentText;     // proposal ที่ส่ง
    public TMP_Text proposalsAcceptedText; // proposal ที่ถูกยอมรับ
    public TMP_Text takeoversWonText;      // ยึดกิจการสำเร็จกี่ครั้ง

    [Header("Controls")]
    public Button closeButton;

    private PlayerPawn _current;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    public void Show(PlayerPawn pawn)
    {
        _current = pawn;
        Refresh();
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        _current = null;
    }

    public void Refresh()
    {
        if (_current == null) return;
        var p = _current;

        // -------- Basic --------
        if (playerNameText != null)
            playerNameText.text = $"สถิติของ {p.playerName.Value}";

        if (cashText != null)
            cashText.text = $"เงินสด: ${p.money.Value}M";

        int portfolioValue = ClientComputePortfolioValue(p);
        if (portfolioValueText != null)
            portfolioValueText.text = $"มูลค่าพอร์ต: ${portfolioValue}M";

        if (totalWealthText != null)
            totalWealthText.text = $"ทรัพย์สินรวม: ${p.money.Value + portfolioValue}M";

        // -------- Ownership --------
        int companyCount = 0;
        int majorityCount = 0;
        if (MarketManager.Instance != null)
        {
            foreach (var kv in MarketManager.Instance.companies)
            {
                var c = kv.Value;
                if (c == null) continue;

                if (c.ownershipPercents.TryGetValue(p, out float pct) && pct > 0f)
                {
                    companyCount++;
                    if (pct > 60f) majorityCount++;
                }
            }
        }

        if (companyCountText != null)
            companyCountText.text = $"จำนวนบริษัทที่มีหุ้น: {companyCount}";

        if (majorityCountText != null)
            majorityCountText.text = $"จำนวนบริษัทที่คุณเป็นเจ้าของ: {majorityCount}";

        // -------- Flow Stats (ตรงนี้ชื่อ field แล้วแต่คุณ) --------
        // ปรับชื่อให้ตรงกับ PlayerPawn ของคุณ เช่น statTaxPaid, statBonus, statBailouts ฯลฯ
        if (taxPaidText != null)
            taxPaidText.text = $"ภาษีที่จ่ายสะสม: ${p.statTaxPaid}M";      // แก้ชื่อ field ให้ตรง

        if (bonusGotText != null)
            bonusGotText.text = $"โบนัสที่ได้รับสะสม: ${p.statBonusReceived}M"; // แก้ชื่อ field ให้ตรง

        if (bailoutsText != null)
            bailoutsText.text = $"จำนวนใบแจ้งนี้: {p.statBailouts}";         // แก้ชื่อ field ให้ตรง

        if (jailVisitsText != null)
            jailVisitsText.text = $"จำนวนครั้งเข้าคุก: {p.statJailVisits}"; // แก้ชื่อ field ให้ตรง

        // -------- Market Stats --------
        if (proposalsSentText != null)
            proposalsSentText.text = $"ข้อเสนอที่ส่ง: {p.statProposalsSent}";

        if (proposalsAcceptedText != null)
            proposalsAcceptedText.text = $"ข้อเสนอที่ถูกยอมรับ: {p.statProposalsAccepted}";

        if (takeoversWonText != null)
            takeoversWonText.text = $"ยึดกิจการสำเร็จ: {p.statTakeoversWon}";
    }

    private int ClientComputePortfolioValue(PlayerPawn pawn)
    {
        if (pawn == null || MarketManager.Instance == null)
            return 0;

        int total = 0;
        // ใช้ factoryPortfolio ฝั่ง client เอง ไม่เรียกเมธอด [Server]
        foreach (var kv in pawn.factoryPortfolio)
        {
            string companyName = kv.Key;
            var rec = kv.Value;

            if (!MarketManager.Instance.companies.TryGetValue(companyName, out var comp) || comp == null)
                continue;

            int price = (comp.currentPrice > 0) ? comp.currentPrice : Mathf.Max(1, comp.baseCost);
            float pct = rec.sharePercent; // float (0–100)
            total += Mathf.RoundToInt(price * (pct / 100f));
        }

        return total;
    }
}
