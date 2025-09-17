using UnityEngine;

public enum TileType { Normal, Event, Investment }

public class TileData : MonoBehaviour
{
    public TileType tileType;
    public string description;

    // Investment-specific data
    public PlayerPawn owner;        
    public int sharesOwned = 0;     
    public int maxShares = 4;       
    public int companyCost = 100;   
    public int shareCost = 25;      
    public int baseFactoryIncome = 50;
    public int shareIncome = 20;     

}
