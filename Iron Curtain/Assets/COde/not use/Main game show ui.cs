using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Maingameshowui : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject countryPanel;
    public GameObject stockmarketPanel;
    public GameObject realestatePanel;
    public GameObject businessPanel;
    public GameObject graphPanel;
    public GameObject researchanddevelopmentPanel;
   
    
    public void OpenCountryPanel()
    {
        CloseAllPanels();
        countryPanel.SetActive(true);
    }

    public void OpenStockmarketPanel()
    {
        CloseAllPanels();
        stockmarketPanel.SetActive(true);
    }
    
    public void OpenrealestatePanel()
    {
        CloseAllPanels();
        realestatePanel.SetActive(true);
    }
    
    public void OpenGraphPanel()
    {
        CloseAllPanels();
        graphPanel.SetActive(true);
    }

    public void OpenBusinessPanel()
    {
        CloseAllPanels();
        businessPanel.SetActive(true);
    }

    public void OpenRandDPanel()
    {
        CloseAllPanels();
        researchanddevelopmentPanel.SetActive(true);
    }

    private void CloseAllPanels()
    {
        countryPanel.SetActive(false);
        graphPanel.SetActive(false);
        businessPanel.SetActive(false);
        researchanddevelopmentPanel.SetActive(false);
        realestatePanel.SetActive(false);
        stockmarketPanel.SetActive(false);
    }

    // --- UI Button Handlers ---

    public void BacktoMain()
    {
        CloseAllPanels();
    }
}
