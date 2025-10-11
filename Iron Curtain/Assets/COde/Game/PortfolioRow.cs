using UnityEngine;
using TMPro;

public class PortfolioRow : MonoBehaviour
{
    public TMP_Text companyText;    
    public TMP_Text percentText;     
    public TMP_Text multiplierText;  
    public TMP_Text incomeText;     
    
    public void Bind(string companyName, int percent, float multiplier, int baseCost)
    {
        if (companyText)    companyText.text    = companyName;
        if (percentText)    percentText.text    = percent + "%";

        // show 2 decimals only when needed
        if (multiplierText) multiplierText.text = "x" + (Mathf.Approximately(multiplier, 1f) 
            ? "1" 
            : multiplier.ToString("0.##"));

        // base payout is 10% of base cost
        int baseIncome        = Mathf.RoundToInt(baseCost * 0.1f);
        float ownershipRatio  = Mathf.Clamp01(percent / 100f);
        int estimatedPayout   = Mathf.RoundToInt(baseIncome * ownershipRatio * multiplier);

        if (incomeText) incomeText.text = $"+${estimatedPayout}";
    }
}