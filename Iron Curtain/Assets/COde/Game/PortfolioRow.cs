using System.Globalization;
using UnityEngine;
using TMPro;

public class PortfolioRow : MonoBehaviour
{
    public TMP_Text companyText;
    public TMP_Text percentText;
    public TMP_Text multiplierText;
    public TMP_Text incomeText;

    static readonly CultureInfo _ci = CultureInfo.InvariantCulture;

    public void Bind(string companyName, int percent, float multiplier, int baseCost)
    {
        // sanitize inputs
        percent   = Mathf.Clamp(percent, 0, 100);
        multiplier = float.IsNaN(multiplier) || float.IsInfinity(multiplier) ? 1f : Mathf.Max(0f, multiplier);

        if (companyText) companyText.text = companyName;

        if (percentText) percentText.text = $"{percent}%";

        if (multiplierText)
        {
            // x1 for exactly 1, otherwise x<2 dp>
            multiplierText.text = Mathf.Approximately(multiplier, 1f)
                ? "x1"
                : $"x{multiplier.ToString("0.##", _ci)}";
        }

        // Base payout is 10% of base cost
        string payoutStr;
        if (baseCost > 0)
        {
            int baseIncome       = Mathf.RoundToInt(baseCost * 0.1f);
            float ownershipRatio = percent / 100f; // percent is clamped
            int estimatedPayout  = Mathf.RoundToInt(baseIncome * ownershipRatio * multiplier);

            payoutStr = $"+${FormatMoney(estimatedPayout)}";
        }
        else
        {
            // unknown company cost on this client (still syncing) – show a placeholder
            payoutStr = "~$?";
        }

        if (incomeText) incomeText.text = payoutStr;
    }

    private static string FormatMoney(int amount)
    {
        // 12,345 style separators without culture surprises in different locales
        return amount.ToString("#,0", _ci);
    }
}