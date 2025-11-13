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
        var info = new HoverInfo();

        switch (_tile.tileType)
        {
            case TileType.Investment:
            {
                info.title = string.IsNullOrWhiteSpace(_tile.companyName)
                    ? "บริษัท"
                    : _tile.companyName;
                int price = _tile.companyCost;
                if (MarketManager.Instance != null)
                {
                    price = MarketManager.Instance.ComputeEffectivePrice(_tile);
                }


                // เจ้าของ
                string ownerName = _tile.owner
                    ? _tile.owner.playerName.Value
                    : "ยังไม่มีเจ้าของ";

                info.lines = new System.Collections.Generic.List<string>
                {
                    $"ราคา: ${price}M",
                    $"เจ้าของ: {ownerName}"
                };
                break;
            }

            case TileType.Event:
                info.title = "ช่องเหตุการณ์";
                info.lines = new()
                {
                    "เมื่อมาลงช่องนี้จะเกิดเหตุการณ์สุ่ม",
                    "อาจได้เงิน เสียเงิน หรือเกิดเอฟเฟกต์พิเศษ"
                };
                break;

            case TileType.Tax:
                info.title = "ช่องภาษี";
                info.lines = new()
                {
                    $"ภาษีคงที่: ${_tile.taxFlat}M",
                    $"อัตราตามทรัพย์สิน: {_tile.taxPercent}%"
                };
                break;

            case TileType.Bonus:
                info.title = "ช่องโบนัส";
                info.lines = new()
                {
                    $"ได้รับเงิน: ${_tile.bonusAmount}M",
                    "ถือว่าเป็นรางวัลพิเศษรอบนี้"
                };
                break;

            case TileType.Jail:
                info.title = "คุก";
                info.lines = new()
                {
                    $"ต้องข้ามเทิร์น: {_tile.jailSkipTurns} เทิร์น",
                    "ระหว่างอยู่ในคุกจะทอยลูกเต๋าไม่ได้"
                };
                break;

            default:
                info.title = "ช่องว่าง";
                info.lines = new()
                {
                    "ช่องนี้ยังไม่มีเอฟเฟกต์พิเศษ"
                };
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
