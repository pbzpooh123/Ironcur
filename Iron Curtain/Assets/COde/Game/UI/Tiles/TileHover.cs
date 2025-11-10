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
                info.title = string.IsNullOrEmpty(_td.companyName) ? "บริษัท" : _td.companyName;
                info.subtitle = string.IsNullOrEmpty(_td.sector) ? "เป็นบริษัทประเภท: —" : $"เป็นบริษัทประเภท: {_td.sector}";

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

                info.line1 = $"เจ้าของ: {ownerName}{ownerPct}";
                info.line2 = $"ราคา: ${price}M   •   รายได้พื้นฐาน: ${_td.baseFactoryIncome}M";

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
                info.subtitle = "อะไรจะเกิดขึ้นก็ได้";
                info.line1 = "—";
                info.line2 = "—";
                info.tint = new Color(0.6f, 0.8f, 1f, 1f);
                break;

            case TileType.Tax:
                info.title = "ภาษี";
                info.subtitle = "จ่ายเมื่อคุณลงที่นี่";
                info.line1 = $"ราคาเริ่มต้น: ${_td.taxFlat}M  •  อัตรา: {_td.taxPercent}%";
                info.line2 = "จ่ายมากขึ้นถ้าคุณรวย.";
                info.tint = new Color(1f, 0.6f, 0.4f, 1f);
                break;

            case TileType.Bonus:
                info.title = "โบนัส";
                info.subtitle = "รับเงินพิเศษ!";
                info.line1 = $"ได้รับ: ${_td.bonusAmount}M";
                info.line2 = "วันโชคดี.";
                info.tint = new Color(0.5f, 1f, 0.6f, 1f);
                break;

            case TileType.GoToJail:
                info.title = "ไปคุก";
                info.subtitle = "ไปที่ช่องคุก";
                info.line1 = $"ข้าม {_td.jailSkipTurns} เทิร์น";
                info.line2 = "—";
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
