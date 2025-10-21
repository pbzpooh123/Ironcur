using UnityEngine;

public enum TileType { Normal, Event, Investment ,Tax,Bonus,GoToJail, Jail}

public class TileData : MonoBehaviour
{
    public TileType tileType;
    public string companyName;

    // Investment-specific data
    public PlayerPawn owner;        
    public int companyCost = 100;   
    public int baseFactoryIncome = 50;
    
    [Header("Tax Settings (if TileType = Tax)")]
    public int taxFlat = 0;       // e.g. 200
    [Range(0,100)]
    public int taxPercent = 0;    // e.g. 10 (% of current cash)

    [Header("Bonus Settings (if TileType = Bonus)")]
    public int bonusAmount = 100; // e.g. +100

    [Header("Jail Settings")]
    public int jailSkipTurns = 2;

    [Header("Visuals")]
    public TileVisuals visuals;

    private void Awake()
    {
        if (visuals == null) visuals = GetComponent<TileVisuals>();
    }

    public void RefreshVisuals()
    {
        if (visuals == null) return;

        if (owner == null)
            visuals.ShowUnclaimed();
        else
            visuals.ShowOwnedByColor(owner.colorIndex.Value);
    }

}
