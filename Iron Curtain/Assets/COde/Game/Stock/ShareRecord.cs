[System.Serializable]
public class ShareRecord
{
    public int count;
    public int roundBought;
    public float multiplier = 1f;       // optional
    public int multiplierExpiresAt = 0; // optional end round
}