using System.Collections.Generic;

internal class ItemInfo
{
    public string ItemName { get; internal set; }
    public int Qty { get; internal set; }
    public int ProductionRate { get; internal set; }
    public List<Item> Items { get; internal set; }
    public int Deficit { get; internal set; }
}
