using UnityEngine;

public enum TileType { Normal, Event, Investment, Start }

public class TileData : MonoBehaviour
{
    public TileType tileType = TileType.Normal;

    [TextArea] 
    public string description; // Popup text or investment info
}