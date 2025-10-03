using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerInfoPanel : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text nameText;
    public TMP_Text profitText;

    [Header("Optional")]
    public TMP_Text turnOrderText; 
    
    [Header("Company Ownership UI")]
    public Transform ownershipListParent;       // container for entries
    public GameObject ownershipEntryPrefab; 
    private readonly Dictionary<string, TMP_Text> ownershipEntries = new();

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
    
    public void UpdateCompanyOwnership(string companyName, int percent)
    {
        if (!ownershipEntries.TryGetValue(companyName, out TMP_Text txt) || txt == null)
        {
            var entryObj = Instantiate(ownershipEntryPrefab, ownershipListParent);
            txt = entryObj.GetComponentInChildren<TMP_Text>();
            ownershipEntries[companyName] = txt;
        }

        if (txt != null)
        {
            txt.text = $"{companyName}: {percent}%";
            txt.gameObject.SetActive(percent > 0); // hide if 0%
        }
        else
        {
            Debug.LogError($"[PlayerInfoPanel] Prefab {ownershipEntryPrefab.name} has no TMP_Text!");
        }
    }
    
    public TMP_Text bailoutText;

    public void UpdateBailoutMarks(int marks)
    {
        if (bailoutText != null)
            bailoutText.text = $"Bailouts: {marks}";
    }


}