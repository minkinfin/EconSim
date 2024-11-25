using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

public class Bid
{
    public string CommodityName { get; private set; }
    public float Price { get; private set; }
    public bool IsMatched { get; private set; }
    public float AcceptPrice { get; private set; }
    public int Quantity { get; private set; }
    float AcceptPriceVolume; // total price of traded goods; sum of price of each good traded over multiple trades
    public int remainingQuantity { get; private set; }
    public EconAgent agent { get; private set; }
    public Bid(string commodityName, float p, int q, EconAgent a)
	{
		CommodityName = commodityName;
		Price = p;
		remainingQuantity = q;
		Quantity = q;
		agent = a;
	}
	public void Accepted(float p, int q)
	{
        IsMatched = true;
        remainingQuantity -= q;
        AcceptPriceVolume += p * q;
        CalculateAcceptPrice();
    }

    public void CalculateAcceptPrice()
    {
        var tradedQuantity = Quantity - remainingQuantity;
        if (tradedQuantity == 0)
        {
            return;
        }
        AcceptPrice = AcceptPriceVolume / tradedQuantity;
    }
}