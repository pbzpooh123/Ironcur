using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using FishNet;

public class InvestmentUI : MonoBehaviour
{
    public GameObject panel;
    public GameObject buttonPrefab;
    public Transform buttonContainer;
    public TMP_Text playerProfitText;

    private NetworkLobbyPlayer localPlayer;

    void Start()
    {
        panel.SetActive(false);
    }

    public void OpenInvestmentPanel(NetworkLobbyPlayer player)
    {
        localPlayer = player;
        playerProfitText.text = $"Profit: ${player.profit.Value:F0}";
        panel.SetActive(true);

        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        List<DynamicInvestment> investments = GameManager.Instance.CurrentInvestments;

        foreach (var dyn in investments)
        {
            GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);
            TMP_Text text = buttonObj.GetComponentInChildren<TMP_Text>();

            string display = $"{dyn.template.investmentName}\n" +
                             $"Cost: ${dyn.currentCost:F0}\n" +
                             $"Success: {(dyn.currentSuccessChance * 100):F0}%\n" +
                             $"{dyn.template.description}";

            text.text = display;

            Button btn = buttonObj.GetComponent<Button>();
            btn.interactable = localPlayer.profit.Value >= dyn.currentCost;

            btn.onClick.AddListener(() =>
            {
                localPlayer.CmdMakeInvestment(dyn.template.investmentName); // still uses name
                panel.SetActive(false);
            });
        }
    }

    public void ClosePanel()
    {
        panel.SetActive(false);
    }
}