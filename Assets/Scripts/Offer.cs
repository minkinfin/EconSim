
using System.Collections.Generic;

public class Offer
{
    public string CommodityName { get; private set; }
    public int Price { get; private set; }
    public int Qty { get; private set; }
    public bool IsMatched { get; set; }
    public int RemainingQty { get; set; }
    public EconAgent agent { get; private set; }
    public int Cost { get; private set; }
    public List<Item> Items { get; private set; }
    public Offer(string commodityName, int p, int q, EconAgent a, int c, List<Item> items)
    {
        CommodityName = commodityName;
        Price = p;
        RemainingQty = q;
        Qty = q;
        agent = a;
        Cost = c;
        Items = items;
    }
    public void Accepted(int q)
    {
        IsMatched = true;
        RemainingQty -= q;
    }
}