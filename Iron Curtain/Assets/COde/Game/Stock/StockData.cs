[System.Serializable]
public class StockData
{
    public string stockName;
    public int basePrice;
    public float priceMultiplier = 1f; // for dynamic events

    public StockData(string name, int basePrice)
    {
        this.stockName = name;
        this.basePrice = basePrice;
    }
}