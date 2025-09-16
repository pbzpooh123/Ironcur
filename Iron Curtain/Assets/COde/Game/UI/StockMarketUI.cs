using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StockMarketUI : MonoBehaviour
{
    public static StockMarketUI Instance;

    public GameObject panel;
    public Transform optionContainer;
    public GameObject optionPrefab; // prefab with TMP_Text + Buy Button

    private PlayerPawn myPawn;
    public Button closeButton;

    private void Awake()
    {
        Instance = this;
        closeButton.onClick.AddListener(Close);
    }
    
    public void Show(PlayerPawn pawn)
    {
        myPawn = pawn;
        panel.SetActive(true);

        var stockList = MarketManager.Instance.stocks;

        foreach (Transform child in optionContainer)
            Destroy(child.gameObject);

        foreach (var stock in stockList)
        {
            int price = Mathf.RoundToInt(stock.basePrice * stock.priceMultiplier);
            int owned = pawn.portfolio.ContainsKey(stock.stockName) ? pawn.portfolio[stock.stockName] : 0;
            AddOption($"{stock.stockName} (Owned: {owned})", price);
        }
    }

    private void AddOption(string name, int price)
    {
        GameObject go = Instantiate(optionPrefab, optionContainer);
        TMP_Text txt = go.GetComponentInChildren<TMP_Text>();
        txt.text = $"{name} - ${price}";

        Button btn = go.GetComponentInChildren<Button>();
        btn.onClick.AddListener(() => OnBuyStock(name, price));
    }

    private void OnBuyStock(string stockName, int price)
    {
        if (myPawn != null)
        {
            MarketManager.Instance.CmdBuyStock(myPawn.Owner, stockName, price);
        }
    }

    public void Close()
    {
        panel.SetActive(false);
        // after closing → allow End Turn
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null)
            ui.SetEndTurnInteractable(true);
    }
}