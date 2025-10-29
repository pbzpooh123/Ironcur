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
    public Button portfolioButton;             // assign in prefab

    // Identity for this panel’s player (serialized so you can see it in Inspector during play)
    [SerializeField, HideInInspector] private int _ownerCid = -1;
    [SerializeField, HideInInspector] private string _ownerName = "";

    public int OwnerCid => _ownerCid;
    public string OwnerName => _ownerName;

    [Header("Turn Highlight")]
    public Image highlightImage;
    public Color activeColor = new Color(1f, 0.9f, 0.3f, 0.75f);
    public Color idleColor   = new Color(1f, 1f, 1f, 0.15f);

    public TMP_Text bailoutText;

    public void SetInfo(string name, int money = 0)
    {
        _ownerName = name;
        if (nameText) nameText.text = name;
        UpdateMoney(money);
        UpdateBailoutMarks(0);
    }

    public void SetOwnerCid(int cid)
    {
        _ownerCid = cid;
        GameHUD.Instance?.NotifyPanelCidChanged(this, cid); // keep HUD map fresh
    }

    public void UpdateMoney(int money)
    {
        if (profitText) profitText.text = $"Money: {money:0} M";
    }

    public void SetTurnOrder(int orderIndex) // 1-based
    {
        if (turnOrderText) turnOrderText.text = $"Turn #{orderIndex}";
    }

    public void UpdateBailoutMarks(int marks)
    {
        if (bailoutText != null)
            bailoutText.text = $"Bailouts: {marks}";
    }

    /// <summary>Called by GameHUD to visually mark current turn.</summary>
    public void SetTurnActive(bool isActive)
    {
        Debug.Log($"[PlayerInfoPanel] SetTurnActive({isActive}) for {_ownerName}");
        if (!highlightImage) return;
        highlightImage.enabled = true;
        highlightImage.color = isActive ? activeColor : idleColor;
    }
}
