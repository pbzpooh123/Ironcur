using UnityEngine;

[RequireComponent(typeof(TileData))]
public class TileHoverSource : MonoBehaviour, IHoverProvider
{
    [Tooltip("Optional: child object that holds the collider (HitArea).")]
    public Transform colliderObject;

    [Tooltip("Optional highlight controller on this tile.")]
    public TileHoverHighlight highlight;

    private TileData _tile;

    void Awake()
    {
        _tile = GetComponent<TileData>();
        if (!highlight && colliderObject)
            highlight = colliderObject.GetComponentInChildren<TileHoverHighlight>();
    }

    public HoverInfo BuildHoverInfo()
    {
        // Build a nice tooltip from TileData + Market
        var info = new HoverInfo();

        switch (_tile.tileType)
        {
            case TileType.Investment:
                info.title = string.IsNullOrWhiteSpace(_tile.companyName) ? "Company" : _tile.companyName;
                // price: prefer live market price if exists
                int price = _tile.companyCost;
                if (MarketManager.Instance &&
                    MarketManager.Instance.companies.TryGetValue(_tile.companyName, out var comp) &&
                    comp != null && comp.currentPrice > 0)
                    price = comp.currentPrice;

                string ownerName = _tile.owner ? _tile.owner.playerName.Value : "Unowned";
                info.lines = new System.Collections.Generic.List<string>
                {
                    $"Sector: {_tile.sector}",
                    $"Price: ${price}M",
                    $"Owner: {ownerName}"
                };
                break;

            case TileType.Event:
                info.title = "Event Tile";
                info.lines = new() { "Trigger a random event." };
                break;

            case TileType.Tax:
                info.title = "Tax";
                info.lines = new()
                {
                    $"Flat: ${_tile.taxFlat}M",
                    $"Rate: {_tile.taxPercent}%"
                };
                break;

            case TileType.Bonus:
                info.title = "Bonus";
                info.lines = new() { $"Gain: ${_tile.bonusAmount}M" };
                break;

            case TileType.Jail:
                info.title = "Jail";
                info.lines = new() { $"Skip turns: {_tile.jailSkipTurns}" };
                break;

            default:
                info.title = "Tile";
                info.lines = new() { "—" };
                break;
        }

        return info;
    }

    // Called by HoverRaycaster if you want to manually toggle highlight (optional)
    public void SetHoverVisual(bool on)
    {
        if (highlight) highlight.Set(on);
    }
}
