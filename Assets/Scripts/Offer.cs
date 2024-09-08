using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

public class Offers : Dictionary<string, Offer> { }
public class Offer
{
    public string CommodityName { get; private set; }
    public float offerPrice { get; private set; }
    public float offerQuantity { get; private set; }
    public float clearingPrice { get; private set; }
    float clearingPriceVolume; // total price of traded goods; sum of price of each good traded over multiple trades
    public float remainingQuantity { get; private set; }
    public EconAgent agent { get; private set; }
    public Offer(string commodityName, float p, float q, EconAgent a)
	{
		CommodityName = commodityName;
		offerPrice = p;
		clearingPrice = p;
		remainingQuantity = q;
		offerQuantity = q;
		agent = a;
	}
	public void Accepted(float p, float q)
	{
		remainingQuantity -= q;
		clearingPriceVolume += p * q;
		CalculateClearingPrice();
	}

	public void CalculateClearingPrice()
	{
		var tradedQuantity = offerQuantity - remainingQuantity;
		if (tradedQuantity == 0)
		{
			return;
		}
		clearingPrice = clearingPriceVolume / tradedQuantity;
	}
}