using UnityEngine;
using TMPro;

public class PortfolioRow : MonoBehaviour
{
    public TMP_Text companyText;
    public TMP_Text percentText;
    public TMP_Text multiplierText;
    public TMP_Text estPayoutText;  // add this in your prefab if you want to show it

    public void Bind(string company, int percent, float multiplier, int currentPrice)
    {
        companyText.text   = company;
        percentText.text   = percent + "%";
        multiplierText.text = (Mathf.Approximately(multiplier, 1f) ? "x1.0" : $"x{multiplier:0.##}");

        float yieldPct = 0.10f;
        if (MarketManager.Instance != null)
            yieldPct = Mathf.Clamp01(MarketManager.Instance.dividendYield);

        int baseIncome  = Mathf.RoundToInt(currentPrice * yieldPct);
        float ownRatio  = Mathf.Clamp01(percent / 100f);
        int estPayout   = Mathf.RoundToInt(baseIncome * ownRatio * multiplier);

        if (estPayoutText != null)
            estPayoutText.text = $"ประมาณค่า. +${estPayout}";
    }
}
