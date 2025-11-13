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
                info.subtitle = string.IsNullOrEmpty(_td.sector)
                    ? "ประเภทธุรกิจ: —"
                    : $"ประเภทธุรกิจ: {_td.sector}";

                // ราคา / ราคาล่าสุด
                int price = _td.companyCost;
                if (MarketManager.Instance != null &&
                    MarketManager.Instance.companies.TryGetValue(_td.companyName, out var comp) &&
                    comp != null && comp.currentPrice > 0)
                {
                    price = comp.currentPrice;
                }

                // เจ้าของ + เปอร์เซ็นต์
                string ownerName = "ไม่มี";
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

                // สีเจ้าของ (ถ้ามี)
                if (_td.owner != null)
                {
                    var col = PlayerColors.GetOr(Color.white, _td.owner.colorIndex.Value);
                    col.a = 1f;
                    info.tint = col;
                }
                else
                {
                    info.tint = Color.white;
                }
                break;

            case TileType.Event:
                info.title = "ช่องเหตุการณ์";
                info.subtitle = "สุ่มเหตุการณ์ดี / ร้าย";
                info.line1 = "ผลจะขึ้นกับการ์ดเหตุการณ์ในเทิร์นนี้.";
                info.line2 = "อาจได้เงิน เสียเงิน หรือเกิดผลพิเศษอื่น ๆ";
                info.tint = new Color(0.6f, 0.8f, 1f, 1f);
                break;

            case TileType.Tax:
                info.title = "ช่องภาษี";
                info.subtitle = "ต้องจ่ายเมื่อเดินมาลงที่นี่";
                info.line1 = $"ภาษีคงที่: ${_td.taxFlat}M   •   อัตราตามทรัพย์สิน: {_td.taxPercent}%";
                info.line2 = "ยิ่งมีเงินมาก ยิ่งจ่ายภาษีเยอะขึ้น.";
                info.tint = new Color(1f, 0.6f, 0.4f, 1f);
                break;

            case TileType.Bonus:
                info.title = "ช่องโบนัส";
                info.subtitle = "รับเงินพิเศษเมื่อมาลงช่องนี้";
                info.line1 = $"ได้รับ: ${_td.bonusAmount}M";
                info.line2 = "ถือว่าเป็นวันโชคดีของคุณ!";
                info.tint = new Color(0.5f, 1f, 0.6f, 1f);
                break;

            case TileType.GoToJail:
                info.title = "ไปคุก";
                info.subtitle = "ย้ายตัวไปที่ช่องคุกทันที";
                info.line1 = $"ต้องข้าม {_td.jailSkipTurns} เทิร์น";
                info.line2 = "ระหว่างอยู่ในคุกจะทอยลูกเต๋าไม่ได้.";
                info.tint = new Color(1f, 0.4f, 0.4f, 1f);
                break;

            case TileType.Jail:
                info.title = "คุก";
                info.subtitle = "อยู่ในคุกจนกว่าจะถูกปล่อย";
                info.line1 = "จำนวนเทิร์นที่ต้องข้าม: (ระบบจะคำนวณให้)";
                info.line2 = "บางเหตุการณ์ / เอฟเฟกต์สามารถช่วยให้คุณออกจากคุกได้.";
                info.tint = new Color(1f, 0.7f, 0.4f, 1f);
                break;

            default:
                info.title = "ช่องว่าง";
                info.subtitle = "";
                info.line1 = "";
                info.line2 = "";
                info.tint = Color.white;
                break;
        }

        return info;
    }

}
