using UnityEngine;

public enum TileType { Normal, Event, Investment }

public class TileData : MonoBehaviour
{
    public TileType tileType;
    public string description;

    // Investment-specific data
    public PlayerPawn owner;        // who owns the company
    public int sharesOwned = 0;     // how many total shares are sold
    public int maxShares = 4;       // total shares allowed
    public int companyCost = 100;   // cost to buy company
    public int shareCost = 25;      // cost to buy one share
}
