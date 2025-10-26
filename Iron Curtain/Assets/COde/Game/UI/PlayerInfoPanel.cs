using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerInfoPanel : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text nameText;
    public TMP_Text profitText;

    [Header("Optional")]
    public TMP_Text turnOrderText; 
    private readonly Dictionary<string, TMP_Text> ownershipEntries = new();

    [Header("Actions")]
    public Button portfolioButton;             // ← assign in prefab

    // Identity for this panel’s player
    [HideInInspector] public int ownerCid = -1;
    [HideInInspector] public string ownerName = "";

    public void SetInfo(string name, int money = 0)
    {
        ownerName = name;
        if (nameText) nameText.text = name;
        UpdateMoney(money);
        UpdateBailoutMarks(0);
    }

    public void SetOwnerCid(int cid) => ownerCid = cid;

    public void UpdateMoney(int money)
    {
        if (profitText) profitText.text = $"Money: {money:0} M";
    }

    public void SetTurnOrder(int orderIndex) // 1-based
    {
        if (turnOrderText) turnOrderText.text = $"Turn #{orderIndex}";
    }

    public TMP_Text bailoutText;
    public void UpdateBailoutMarks(int marks)
    {
        if (bailoutText != null)
            bailoutText.text = $"Bailouts: {marks}";
    }
}
