using UnityEngine;
using TMPro;

public class PlayerInfoPanel : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text profitText;
    public TMP_Text turnOrderText;

    public void SetInfo(string name, int profit = 0,int turnOrder = -1)
    {
        nameText.text = name;
        profitText.text = $"Money: {profit:0}";
        if (turnOrder >= 0)
            turnOrderText.text = $"Turn #{turnOrder + 1}";
    }

    public void UpdateProfit(int profit)
    {
        profitText.text = $"Money: {profit:0}";
    }

}