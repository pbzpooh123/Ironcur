using UnityEngine;
using TMPro;

public class PlayerInfoPanel : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text nameText;
    public TMP_Text profitText;

    [Header("Optional")]
    public TMP_Text turnOrderText; // <-- add this in your prefab (optional)

    public void SetInfo(string name, int money = 0)
    {
        if (nameText)     nameText.text = name;
        UpdateMoney(money);
    }

    public void UpdateMoney(int money)
    {
        if (profitText) profitText.text = $"Money: {money:0}";
    }

    public void SetTurnOrder(int orderIndex) // 1-based
    {
        if (turnOrderText) turnOrderText.text = $"Turn #{orderIndex}";
    }
}