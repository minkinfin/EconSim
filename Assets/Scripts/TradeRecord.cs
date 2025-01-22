using System;
using UnityEngine;

[Serializable]
public class TradeRecord
{
    public string ItemName { get; internal set; }
    public int Price { get; internal set; }
    public int Qty { get; internal set; }
    public int Round { get; internal set; }
}
