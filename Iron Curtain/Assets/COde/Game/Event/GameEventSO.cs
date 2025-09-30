using UnityEngine;
using System.Collections.Generic;

public enum EventType { Tile, Main }
public enum TargetType
{
    None,
    Factory,    // covers factories + their stock entry 
    Ownership,  // directly manipulates % shares or control
    Global      // affects all players equally
}


[System.Serializable]
public class EventEffect
{
    public TargetType targetType;       // Factory / Ownership / Global
    public string targetName;           // "Steel & Iron", "Banking", etc.
    public int moneyDelta;              // instant money bonus/penalty
    public float multiplier = 1f;       // revenue multiplier
    public int duration = 0;            // how many rounds the effect lasts
    public bool skipTurn;               // player must skip their turn
    public int randomMoneyMin;          // optional: random payout lower bound
    public int randomMoneyMax;          // optional: random payout upper bound
    public int ownershipDelta;          // % of shares gained/lost (only for Ownership type)
}

[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Game/Event", order = 1)]
public class GameEventSO : ScriptableObject
{
    [Header("Basic Info")]
    public string eventName;
    [TextArea(3, 5)]
    public string description;
    public EventType type;

    [Header("Effects")]
    public List<EventEffect> effects = new List<EventEffect>();
}