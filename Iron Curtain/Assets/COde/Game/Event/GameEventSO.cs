using UnityEngine;
using System.Collections.Generic;

public enum EventType
{
    Tile,
    Main
}

/* Matches your latest EventManager: only these three are used */
public enum TargetType
{
    Factory,    // affects a specific company in the player's factoryPortfolio (via targetName)
    Ownership,  // changes sharePercent (via ownershipDelta)
    Global      // affects all companies owned by that player (multiplier/duration)
}

/* Which special behavior (if any) this event uses */
public enum EventMode
{
    Simple,                 // Just apply effects (money, multipliers, ownership, etc.)
    ForcedRollAgainstOwner, // Find owner of requiredCompanyName, others roll d6; odd → payOnOdd to owner
    Competition,            // All players pay entryFee into pot; roll d6; highest wins; tieSplitPot option
    TargetSelect            // Current pawn picks a target player; effects apply to the selected target only
}

[System.Serializable]
public class EventEffect
{
    [Header("Targeting")]
    public TargetType targetType = TargetType.Global;
    public string targetName;         // For Factory or Ownership effects (e.g., "Steel & Iron")

    [Header("Money")]
    public int moneyDelta = 0;        // Immediate money change (+/-). Applied to the selected pawn.
    public int randomMoneyMin = 0;    // Additional random money range (min ≤ max)
    public int randomMoneyMax = 0;

    [Header("Multiplier")]
    public float multiplier = 1f;     // Multiplier to apply (for Factory or Global)
    public int duration = 0;          // How many rounds the multiplier lasts (round-based expiry)

    [Header("Turn Control")]
    public bool skipTurn = false;     // If true, that pawn skips N turns (N = max(1, duration))

    [Header("Ownership (only if targetType = Ownership)")]
    public int ownershipDelta = 0;    // Changes sharePercent for targetName (clamped 0..100)
    
    [Header("Extra Turn / Movement")]
    public bool grantExtraRoll = false;
}

[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Game/Event", order = 1)]
public class GameEventSO : ScriptableObject
{
    [Header("Basic Info")]
    public string eventName;
    [TextArea(3, 5)]
    public string description;
    public EventType type = EventType.Main;

    [Header("Mode")]
    public EventMode mode = EventMode.Simple;

    [Header("Mode: ForcedRollAgainstOwner")]
    public string requiredCompanyName;    // The company name whose owner is the 'defender' of the event
    public int payOnOdd = 0;              // How much each odd-roller pays to the owner

    [Header("Mode: Competition")]
    public int entryFee = 0;              // Each participant pays this into the pot
    public bool tieSplitPot = true;       // If multiple winners, split pot equally (floor)

    [Header("Mode: TargetSelect")]
    public bool restrictToOpponents = true;   // If true, cannot target yourself
    public bool requireCompanyOwner = false;  // If true, only players who own the company in effects (Factory effect targetName) can be targeted

    [Header("Effects")]
    public List<EventEffect> effects = new List<EventEffect>();
}
