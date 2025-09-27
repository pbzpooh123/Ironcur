using UnityEngine;

[CreateAssetMenu(menuName = "Game/Investment Option")]
public class InvestmentOption : ScriptableObject
{
    public string investmentName;
    [TextArea] public string description;
    public float cost;
    public float successChance; // 0 to 1
    public float reward;
    public float failurePenalty;

    public bool requiresEventTrigger;
}