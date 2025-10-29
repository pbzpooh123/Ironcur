// TileHover.cs
using UnityEngine;

[RequireComponent(typeof(TileData))]
public class TileHover : MonoBehaviour, IHoverProvider
{
    private TileData _td;

    void Awake() => _td = GetComponent<TileData>();

    public HoverInfo BuildHoverInfo()
    {
        var info = new HoverInfo();

        switch (_td.tileType)
        {
            case TileType.Investment:
                info.title = string.IsNullOrEmpty(_td.companyName) ? "Company" : _td.companyName;
                info.subtitle = string.IsNullOrEmpty(_td.sector) ? "Sector: —" : $"Sector: {_td.sector}";

                // price/current price
                int price = _td.companyCost;
                if (MarketManager.Instance != null &&
                    MarketManager.Instance.companies.TryGetValue(_td.companyName, out var comp) &&
                    comp != null && comp.currentPrice > 0)
                {
                    price = comp.currentPrice;
                }

                // owner + percent
                string ownerName = "—";
                string ownerPct  = "";
                if (MarketManager.Instance != null &&
                    MarketManager.Instance.companies.TryGetValue(_td.companyName, out var c2) &&
                    c2 != null && c2.owner != null)
                {
                    ownerName = c2.owner.playerName.Value;
                    int pct = c2.GetOwnership(c2.owner);
                    ownerPct = pct > 0 ? $" ({pct}%)" : "";
                }
                else if (_td.owner != null)
                {
                    ownerName = _td.owner.playerName.Value;
                }

                info.line1 = $"Owner: {ownerName}{ownerPct}";
                info.line2 = $"Price: ${price}M   •   Base Income: ${_td.baseFactoryIncome}M";

                // tint by owner color if available
                if (_td.owner != null)
                {
                    var col = PlayerColors.GetOr(Color.white, _td.owner.colorIndex.Value);
                    col.a = 1f;
                    info.tint = col;
                }
                break;

            case TileType.Event:
                info.title = "Event";
                info.subtitle = "Random event tile";
                info.line1 = "Something unexpected may happen.";
                info.line2 = "—";
                info.tint = new Color(0.6f, 0.8f, 1f, 1f);
                break;

            case TileType.Tax:
                info.title = "Tax";
                info.subtitle = "Pay when you land here";
                info.line1 = $"Flat: ${_td.taxFlat}M  •  Rate: {_td.taxPercent}%";
                info.line2 = "Bailout may trigger if short on cash.";
                info.tint = new Color(1f, 0.6f, 0.4f, 1f);
                break;

            case TileType.Bonus:
                info.title = "Bonus";
                info.subtitle = "Receive funds";
                info.line1 = $"Gain: ${_td.bonusAmount}M";
                info.line2 = "Lucky day.";
                info.tint = new Color(0.5f, 1f, 0.6f, 1f);
                break;

            case TileType.GoToJail:
                info.title = "Go To Jail";
                info.subtitle = "Move to Jail tile";
                info.line1 = $"Skip {_td.jailSkipTurns} turns";
                info.line2 = "Use events or effects to get out earlier.";
                info.tint = new Color(1f, 0.4f, 0.4f, 1f);
                break;

            case TileType.Jail:
                info.title = "Jail";
                info.subtitle = "You’re stuck here";
                info.line1 = $"Remaining skip turns: (server decides)";
                info.line2 = "Some effects can release you.";
                info.tint = new Color(1f, 0.7f, 0.4f, 1f);
                break;

            default:
                info.title = "Tile";
                info.subtitle = "";
                info.line1 = "";
                info.line2 = "";
                info.tint = Color.white;
                break;
        }

        return info;
    }
}
