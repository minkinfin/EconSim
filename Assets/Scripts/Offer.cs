public class Offer
{
    public string CommodityName { get; private set; }
    public int Price { get; private set; }
    public bool IsMatched { get; private set; }
    public int Quantity { get; private set; }
    public int remainingQuantity { get; private set; }
    public EconAgent agent { get; private set; }
    public int Cost { get; private set; }
    public Offer(string commodityName, int p, int q, EconAgent a, int c)
    {
        CommodityName = commodityName;
        Price = p;
        remainingQuantity = q;
        Quantity = q;
        agent = a;
        Cost = c;
    }
    public void Accepted(int q)
    {
        IsMatched = true;
        remainingQuantity -= q;
    }
}