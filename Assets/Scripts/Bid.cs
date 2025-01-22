public class Bid
{
    public string CommodityName { get; private set; }
    public int Price { get; private set; }
    public int Qty { get; private set; }
    public bool IsMatched { get; set; }
    public int RemainingQty { get; set; }
    public EconAgent agent { get; private set; }
    public Bid(string commodityName, int p, int q, EconAgent a)
	{
		CommodityName = commodityName;
		Price = p;
        RemainingQty = q;
        Qty = q;
		agent = a;
	}
	public void Accepted(int q)
	{
        IsMatched = true;
        RemainingQty -= q;
    }
}