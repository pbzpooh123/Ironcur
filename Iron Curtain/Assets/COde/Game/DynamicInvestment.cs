using UnityEngine;

[System.Serializable]
public class DynamicInvestment
{
    public InvestmentOption template;  // reference to the base investment
    public float currentCost;
    public float currentSuccessChance;

    public string Name => template.investmentName;
    public string Description => template.description;
}