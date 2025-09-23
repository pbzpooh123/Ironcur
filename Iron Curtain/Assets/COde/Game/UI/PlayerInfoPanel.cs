using UnityEngine;
using TMPro;

public class PlayerInfoPanel : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text businessText;
    public TMP_Text countryText;
    public TMP_Text profitText;

    public void SetInfo(string name, string business, string country, int profit = 0)
    {
        nameText.text = name;
        businessText.text = business;
        countryText.text = country;
        profitText.text = $"Money: {profit:0}";
    }

    public void UpdateProfit(int profit)
    {
        profitText.text = $"Money: {profit:0}";
    }

}