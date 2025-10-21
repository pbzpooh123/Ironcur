using UnityEngine;
using System.Collections.Generic;

public enum EventType
{
    Tile,
    Main
}

public enum TargetType
{
    Factory,    // affects a specific company in the player's factoryPortfolio (via targetName)
    Ownership,  // changes sharePercent (via ownershipDelta)
    Global      // affects all companies owned by that player (multiplier/duration)
}

/* Which special behavior (if any) this event uses */
public enum EventMode
{
    Simple,              
    ForcedRollAgainstOwner, 
    Competition,         
    TargetSelect,           
    ProposalBlock,       
    ForcedRollTier  
    
}

[System.Serializable]
public class EventEffect
{
    [Header("Targeting")]
    public TargetType targetType = TargetType.Global;
    public string targetName;         // For Factory  effects

    [Header("Money")]
    public int moneyDelta = 0;       
    public int randomMoneyMin = 0;    // Additional random money range (min ≤ max)
    public int randomMoneyMax = 0;

    [Header("Multiplier")]
    public float multiplier = 1f;     // Multiplier to apply (for Factory or Global)
    public int duration = 0;          // How many rounds the multiplier lasts 

    [Header("Turn Control")]
    public bool skipTurn = false;     // If true, that pawn skips N turns (N = max(1, duration))

    [Header("Ownership (only if targetType = Ownership)")]
    public int ownershipDelta = 0;    // Changes sharePercent for targetName 

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
    public string history;
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
    
    [Header("Rule #6: Bank Odd Fine")]
    public bool enableBankOddFine = false;
    public string bankCompanyName = "Bank";
    public int oddFineAmount = 500;
    public int oddFineDurationRounds = 3; // 0 = no expiration

    [Header("ProposalBlock")]
    public int blockProposalRounds = 1; // 1 = this round only

    [Header("ForcedRollTier")]
    public int lowMax = 2;             
    public int lowPayAmount = 200;
    public int highMin = 5;           
    public int highGainAmount = 200;
    public bool affectAllPlayers = true; 

    [Header("Recession (optional)")]
    public bool triggerRecession = false;
    [Min(1)] public int recessionRounds = 3;

}
