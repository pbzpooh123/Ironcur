using UnityEngine;

public enum TileType { Normal, Event, Investment }

public class TileData : MonoBehaviour
{
    public TileType tileType;
    public string companyName;

    // Investment-specific data
    public PlayerPawn owner;        
    public int companyCost = 100;   
    public int baseFactoryIncome = 50;
      

}
