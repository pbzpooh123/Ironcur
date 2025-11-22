[System.Serializable]
public struct ShareRecord
{
    public int count;                  
    public int roundBought;
    public float multiplier;           // stacking payout multiplier
    public int multiplierExpiresAt;    // round when multiplier resets
    public float sharePercent;           // 0-100, for company ownership
}