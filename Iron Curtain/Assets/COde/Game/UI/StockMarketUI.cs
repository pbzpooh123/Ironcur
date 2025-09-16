using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StockMarketUI : MonoBehaviour
{
    public static StockMarketUI Instance;

    [Header("UI References")]
    public GameObject wrapper;             // Parent "StockMarketWindow"
    public GameObject panel;               // Inner panel with LayoutGroup
    public Transform optionContainer;      // Content container for stock options
    public GameObject optionPrefab;        // Prefab with TMP_Text + Buy Button
    public Button closeButton;             // The Close Button (sibling to panel)

    private PlayerPawn myPawn;

    private void Awake()
    {
        Instance = this;
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void Show(PlayerPawn pawn)
    {
        myPawn = pawn;

        wrapper.SetActive(true); // Enable the whole window (panel + button)

        RefreshOptions();
    }

    private void RefreshOptions()
    {
        var stockList = MarketManager.Instance.stocks;

        // Clear old children
        foreach (Transform child in optionContainer)
            Destroy(child.gameObject);

        // Add stock entries
        foreach (var stock in stockList)
        {
            int price = Mathf.RoundToInt(stock.basePrice * stock.priceMultiplier);
            int owned = myPawn.portfolio.ContainsKey(stock.stockName) ? myPawn.portfolio[stock.stockName] : 0;

            AddOption(stock.stockName, price, owned);
        }
    }

    private void AddOption(string stockName, int price, int owned)
    {
        GameObject go = Instantiate(optionPrefab, optionContainer);
        TMP_Text txt = go.GetComponentInChildren<TMP_Text>();
        txt.text = $"{stockName} (Owned: {owned}) - ${price}";

        Button btn = go.GetComponentInChildren<Button>();
        btn.onClick.AddListener(() => OnBuyStock(stockName, price));
    }

    private void OnBuyStock(string stockName, int price)
    {
        if (myPawn != null)
        {
            MarketManager.Instance.CmdBuyStock(myPawn.Owner, stockName, price);
            RefreshOptions(); 
        }
    }

    public void Close()
    {
        wrapper.SetActive(false);

        // After closing, allow End Turn
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.SetEndTurnInteractable(true);
    }
}
