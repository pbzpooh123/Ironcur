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

   
    public void ClosePanel()
    {
        panel.SetActive(false);
    }
}