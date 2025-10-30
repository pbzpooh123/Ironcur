using UnityEngine;

public enum TileType { Normal, Event, Investment, Tax, Bonus, GoToJail, Jail }

public class TileData : MonoBehaviour
{
    public TileType tileType;
    public string companyName;
    public string sector;

    // Investment-specific data
    public PlayerPawn owner;
    public int companyCost = 100;
    public int baseFactoryIncome = 50;

    [Header("Tax Settings (if TileType = Tax)")]
    public int taxFlat = 0;
    [Range(0, 100)] public int taxPercent = 0;

    [Header("Bonus Settings (if TileType = Bonus)")]
    public int bonusAmount = 100;

    [Header("Jail Settings")]
    public int jailSkipTurns = 2;

    [Header("Visuals")]
    public TileVisuals visuals;

    [Header("On-Land Company Effects")]
    public bool enableStealOnLanding = true;
    [Range(0,100)] public int stealPercentOfOwnerMoney = 10;
    public int stealFlatMin = 0; 
    public int stealFlatMax = 0; 

    private void Awake()
    {
        if (visuals == null) visuals = GetComponent<TileVisuals>();
    }

    private void OnEnable() => RefreshVisuals();
    private void Start()    => RefreshVisuals();

    public void RefreshVisuals()
    {
        // If you don’t want prices shown on non-investment tiles, hide and return.
        if (tileType != TileType.Investment)
        {
            if (visuals != null)
            {
                visuals.SetPriceVisible(false);
                visuals.SetSurgeVisible(false);
            }
            return;
        }

        if (visuals == null) return;

        int displayPrice;

        // If company exists in market, show its true currentPrice.
        if (MarketManager.Instance != null &&
            MarketManager.Instance.companies.TryGetValue(companyName, out var comp) &&
            comp != null && comp.currentPrice > 0)
        {
            displayPrice = comp.currentPrice;
        }
        else
        {
            // Unowned: base cost * active sector PRICE multiplier (client copy of surge)
            float mult = 1f;
            if (EventManager.Instance != null &&
                EventManager.Instance.ClientIsSectorSurgeActive(sector))
            {
                mult = EventManager.Instance.ClientGetSectorPriceMult(sector);
            }

            displayPrice = Mathf.Max(1, Mathf.RoundToInt(companyCost * mult));
        }

        visuals.SetPrice($"${displayPrice}M");

        // Show surge badge only if a PRICE surge is active AND multiplier != 1
        if (EventManager.Instance != null &&
            EventManager.Instance.ClientIsSectorSurgeActive(sector))
        {
            float m = EventManager.Instance.ClientGetSectorPriceMult(sector);
            if (Mathf.Abs(m - 1f) > 0.001f)
            {
                visuals.SetSurgeVisible(true);
                visuals.SetSurgeText($"×{m:0.##}");
            }
            else
            {
                visuals.SetSurgeVisible(false);
            }
        }
        else
        {
            visuals.SetSurgeVisible(false);
        }

        // Optional: owner color/flag
        visuals.SetOwner(owner);
    }
}
