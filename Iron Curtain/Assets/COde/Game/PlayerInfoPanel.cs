using UnityEngine;
using TMPro;

public class PlayerInfoPanel : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text businessText;
    public TMP_Text countryText;
    public TMP_Text profitText;

    public void SetInfo(string name, string business, string country, float profit = 0f)
    {
        nameText.text = name;
        businessText.text = business;
        countryText.text = country;
        profitText.text = $"Profit: {profit:0}";
    }

    public void UpdateProfit(float profit)
    {
        profitText.text = $"Profit: {profit:0}";
    }
}