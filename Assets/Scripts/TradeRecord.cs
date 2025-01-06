using System;
using UnityEngine;

[Serializable]
public class TradeRecord
{
    public string ItemName { get; internal set; }
    public TransactionType TransactionType { get; internal set; }
    public float Price { get; internal set; }
    public int Quantity { get; internal set; }
    public int Round { get; internal set; }
}
