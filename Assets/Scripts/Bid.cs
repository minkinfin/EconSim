public class Bid
{
    public string CommodityName { get; private set; }
    public int Price { get; private set; }
    public bool IsMatched { get; private set; }
    public int Quantity { get; private set; }
    public int remainingQuantity { get; private set; }
    public EconAgent agent { get; private set; }
    public Bid(string commodityName, int p, int q, EconAgent a)
	{
		CommodityName = commodityName;
		Price = p;
		remainingQuantity = q;
		Quantity = q;
		agent = a;
	}
	public void Accepted(int q)
	{
        IsMatched = true;
        remainingQuantity -= q;
    }
}