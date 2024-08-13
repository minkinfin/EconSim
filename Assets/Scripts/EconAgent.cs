﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;

public class EconAgent : MonoBehaviour {
	static int uid_idx = 0;
	int uid;
	public int debug = 0;
	public float cash { get; private set; }
	float prevCash = 0;
	float maxStock = 1;
	ESList profits = new ESList();
	//has a set of commodities in stock
	public Dictionary<string, inventoryItem> inventory = new Dictionary<string, inventoryItem>(); //commodities stockpiled
	Dictionary<string, float> perItemCost = new Dictionary<string, float>(); //commodities stockpiled

	//can produce a set of commodities
	public List<string> outputs { get; private set; }
	//production has dependencies on commodities->populates stock
	//production rate is limited by assembly lines (queues/event lists)
	
	//can use profit to reinvest - produce new commodities
	//switching cost to produce new commodities - zero for now

	//from the paper (base implementation)
	// Use this for initialization
	Dictionary<string, Commodity> com {get; set;}

	public String Stats(String header)
	{
		String msg = "";
		header += uid.ToString() + ", " + outputs[0] + ", "; //profession
		foreach (var stock in inventory)
		{
			msg += stock.Value.Stats(header);
		}
		msg += header + "cash, stock, " + cash + ", n/a\n";
		msg += header + "profit, stock, " + (cash - prevCash) + ", n/a\n";
		return msg; 
	}
	void Start () {
		cash = 0;
	}
	void AddToInventory(string name, float num, float max, float price, float production)
	{
		if (inventory.ContainsKey(name))
			return;

        inventory.Add(name, new inventoryItem(name, num, max, price, production));

		//book keeping
		//Debug.Log(gameObject.name + " adding " + name + " to stockpile");
		perItemCost[name] = com[name].price * num;
	}
	public void Init(float initCash, List<string> b, float initNum=5, float maxstock=10) {
		uid = uid_idx++;
		Reinit(initCash, b, initNum, maxstock);
	}
	public void Reinit(float initCash, List<string> b, float initNum=5, float maxstock=10) {
        if (com == null)
			com = Commodities.Instance.com;
		//list of commodities self can produce
		//get initial stockpiles
		outputs = b;
		cash = initCash;
		prevCash = cash;
		maxStock = maxstock;
		foreach (var buildable in outputs)
		{
			Debug.Log("New " + gameObject.name + " has " + cash.ToString("c2") + " builds: " + buildable);

			if (!com.ContainsKey(buildable))
				Debug.Log("commodity not recognized: " + buildable);

            if (com[buildable].dep == null)
				Debug.Log(buildable + ": null dep!");

			foreach (var dep in com[buildable].dep)
			{
				var commodity = dep.Key;
                //Debug.Log("::" + commodity);
				AddToInventory(commodity, initNum, maxStock, com[commodity].price, com[commodity].production);
				Debug.Log("New " + gameObject.name + " has " + inventory[commodity].Quantity + " " + commodity);
			}
			AddToInventory(buildable, 0, maxStock, com[buildable].price, com[buildable].production);
			Debug.Log("New " + gameObject.name + " has " + inventory[buildable].Quantity + " " + buildable);
		}
    }

	public float TaxProfit(float taxRate)
	{
		var profit = GetProfit();
		if (profit <= 0)
			return profit;
		var taxAmt = profit * taxRate;
		cash -= taxAmt;
		return profit - taxAmt;
	}
	public float GetProfit()
	{
		var profit = cash - prevCash;
		prevCash = cash;
		return profit;
	}
	const float bankruptcyThreshold = 0;
	public bool IsBankrupt()
	{
		return cash < bankruptcyThreshold;
	}
    public float Tick()
	{
		Debug.Log("agents ticking!");
		float taxConsumed = 0;
		bool starving = false;
		#if true
		if (inventory.ContainsKey("Food"))
		{
            var food = inventory["Food"].Decrease(0.5f);
            starving = food < 0;
			//starting round will result in negative food
		}
		#endif
		
		foreach (var entry in inventory)
		{
			entry.Value.Tick();
        }

        if (IsBankrupt() || starving == true)
		{
			Debug.Log(name + ":" + outputs[0] + " is bankrupt: " + cash.ToString("c2") + " or starving where food=" + inventory["Food"].Quantity);
			taxConsumed = ChangeProfession();
		}
		foreach (var buildable in outputs)
		{
			inventory[buildable].cost = GetCostOf(buildable);
		}
		return taxConsumed;
	}

	float ChangeProfession()
	{
		string bestGood = Commodities.Instance.GetHottestGood(10);
		string bestProf = Commodities.Instance.GetMostProfitableProfession(outputs[0], 10);

		string mostDemand = bestProf;
		if (bestGood != "invalid")
        {
            mostDemand = bestGood;
		} else {
			//no good profession?? probably should assert
			return 0;
		}
				
		Assert.AreEqual(outputs.Count, 1);
		Debug.Log(name + " changing from " + outputs[0] + " to " + mostDemand);
		outputs[0] = mostDemand;
		inventory.Clear();
		List<string> b = new List<string>();
		b.Add(mostDemand);
		var initCash = 100f;
		// todo take from irs?
		Reinit(initCash, b);
		return initCash;
	}

	/*********** Trading ************/
	public void modify_cash(float quant)
	{
		cash += quant;
	}
	public void Modify_Quantity(string commodity, float quantity)
	{
	//	stockPile[commodity].quantity += quantity;
	}
	public void ClearRoundStats()
	{
		foreach( var item in inventory)
		{
			item.Value.ClearRoundStats();
		}
	}
	public float Buy(string commodity, float quantity, float price)
	{
	Debug.Log(name + " has " + cash.ToString("c2") + " want to buy " + quantity.ToString("n2") + " " + commodity + " for " + price.ToString("c2"));
		var boughtQuantity = inventory[commodity].Buy(quantity, price);
	Debug.Log(name + " has " + cash.ToString("c2") + " want to buy " + quantity.ToString("n2") + " " + commodity + " for " + price.ToString("c2") + " bought " + boughtQuantity.ToString("n2"));
		cash -= price * boughtQuantity;
		return boughtQuantity;
	}
	public void Sell(string commodity, float quantity, float price)
	{
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		inventory[commodity].Sell(quantity, price);
		Assert.IsTrue(inventory[commodity].Quantity >= 0);
		//		Debug.Log(name + " has " + cash.ToString("c2") + " selling " + quantity.ToString("n2") +" " +  commodity + " for " + price.ToString("c2"));
		cash += price * quantity;
	}
	public void UpdateSellerPriceBelief(in Trade trade, in Commodity commodity) 
	{
		inventory[commodity.name].UpdateSellerPriceBelief(name, in trade, in commodity);
	}
	public void UpdateBuyerPriceBelief(in Trade trade, in Commodity commodity) 
	{
		inventory[commodity.name].UpdateBuyerPriceBelief(name, in trade, in commodity);
	}

	int historyCount = 10;
    /*********** Produce and consume; enter asks and bids to auction house *****/
    float FindSellCount(string c)
	{
		var avgPrice = com[c].GetAvgPrice(historyCount);
		var lowestPrice = inventory[c].sellHistory.Min();
		var highestPrice = inventory[c].sellHistory.Max();
		//todo SANITY check
		float favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
		favorability = Mathf.Clamp(favorability, 0, 1);
		//float numAsks = (favorability) * stockPile[c].Surplus();
		//TODO only do this for self consumables
		float numAsks = Mathf.Max(0, inventory[c].Surplus()); //make sure to leave some to eat if food
		//why max of 1??? numAsks = Mathf.Max(1, numAsks);

//		Debug.Log("avgPrice: " + avgPrice.ToString("c2") + " favoribility: " + favorability + " numAsks: " + numAsks.ToString("0.00"));
		return Mathf.Floor(numAsks);
	}
	float FindBuyCount(string c)
	{
		var avgPrice = com[c].GetAvgPrice(historyCount);
		var lowestPrice = inventory[c].buyHistory.Min();
		var highestPrice = inventory[c].buyHistory.Max();
		//todo SANITY check
		float favoribility = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
		favoribility = Mathf.Clamp(favoribility, 0, 1);
		//TODO float numBids = (1 - favorability) * stockPile[c].Deficit();
		float numBids = inventory[c].Deficit();
		//WHY??? 
		//TODO find prices of all dependent commodities, then get relative importance, and split numBids on all commodities proportional to value (num needed/price)
		//numBids = Mathf.Max(1, numBids);
		

//		Debug.Log("avgPrice: " + avgPrice.ToString("c2") + " favoribility: " + favoribility + " numBids: " + numBids.ToString("n2"));
		return Mathf.Floor(numBids);
	}
	public TradeSubmission Consume(Dictionary<string, Commodity> com) {
        var bids = new TradeSubmission();
        //replenish depended commodities
        foreach (var stock in inventory)
		{
			//don't buy agent output
			// TODO speculate and buy if it's cheaper than historical average 
			// (for 1 or more commodities)
			if (outputs.Contains(stock.Key)) continue;

			var numBids = FindBuyCount(stock.Key);
			if (numBids > 0)
			{
				//maybe buy less if expensive?
				float buyPrice = stock.Value.GetPrice();
                float maxPrice = Mathf.Min(cash / numBids, stock.Value.GetPrice());
				if (buyPrice > 1000)
				{
					Debug.Log(stock.Key + "buyPrice: " + buyPrice.ToString("c2") + " : " + stock.Value.minPriceBelief.ToString("n2") + "<" + stock.Value.maxPriceBelief.ToString("n2"));
					Assert.IsFalse(buyPrice > 1000);
				}
				Debug.Log(this.name + " wants to buy " + numBids.ToString("n2") + stock.Key + " for " + buyPrice.ToString("c2") + " each");
				Assert.IsFalse(numBids < 0);
				bids.Add(stock.Key, new Trade(stock.Value.commodityName, buyPrice, numBids, this));
			}
        }
        return bids;
	}
	public TradeSubmission Produce(Dictionary<string, Commodity> com, ref float idleTax) {
        var asks = new TradeSubmission();
		//TODO sort buildables by profit

		//build as many as one can TODO don't build things that won't earn a profit
		float producedThisRound = 0;
		float revenueThisRound = 0;
		foreach (var buildable in outputs)
		{
			Debug.Log(name + " producing " + buildable + " currently in stock " + inventory[buildable].Quantity);
			//get list of dependent commodities
			float numProduced = float.MaxValue; //amt agent can produce for commodity buildable
			string sStock = ", has in stock";
			//find max that can be made w/ available stock
			Assert.IsTrue(com.ContainsKey(buildable));
			foreach (var dep in com[buildable].dep)
			{
				//get num commodities you can build
				var numNeeded = dep.Value;
				var numAvail = inventory[dep.Key].Quantity;
				numProduced = Mathf.Min(numProduced, numAvail/numNeeded);
				Debug.Log(name + "can produce " + numProduced + " w/" + numAvail + "/" + numNeeded + dep.Key );
			}
			//can only build fixed rate at a time
			//can't produce more than what's in stock
			var upperBound = Mathf.Min(inventory[buildable].productionRate, inventory[buildable].Deficit());
			numProduced = Mathf.Clamp(numProduced, 0, upperBound);
			Debug.Log(name + " upperbound: " + upperBound + " production rate: " + inventory[buildable].productionRate + " room: " + inventory[buildable].Deficit());
			numProduced = Mathf.Floor(numProduced);
			Debug.Log(name + " upperbound: " + upperBound + " can produce: " + numProduced);
			if (numProduced == 0)
			{
				continue;
			}
			Assert.IsTrue(numProduced > 0);
			//build and add to stockpile
			var buildable_cost = 0f;
			foreach (var dep in com[buildable].dep)
			{
				var stock = inventory[dep.Key].Quantity;
				var numUsed = dep.Value * numProduced;
				numUsed = Mathf.Clamp(numUsed, 0, stock);
				buildable_cost += numUsed * inventory[dep.Key].meanPriceThisRound;
                inventory[dep.Key].Decrease(numUsed);
			}
			// WTF is production?? numProduced *= stockPile[buildable].production;
			//numProduced = Mathf.Max(numProduced, 0);
			inventory[buildable].Increase(numProduced);
			Assert.IsFalse(float.IsNaN(numProduced));
			Assert.IsTrue(numProduced >= 0);
			Assert.IsTrue(inventory[buildable].Quantity >= 0);

            var buildStock = inventory[buildable];
            float sellQuantity = FindSellCount(buildable);
            float sellPrice = buildStock.GetPrice(); 
			//HACK, so economy is always flowing somewhere
			//numProduced = Mathf.Max(1, numProduced);
			//sellPrice = Mathf.Max(1, sellPrice);


			if (sellQuantity > 0 && sellPrice > 0)
			{
				Debug.Log(name + " has " + cash.ToString("c2") + " made " + numProduced.ToString("n2") + " " + buildable + " wants to sell for " + sellQuantity + " for " + sellPrice.ToString("c2") + sStock + inventory[buildable].Surplus());
				asks.Add(buildable, new Trade(buildable, sellPrice, sellQuantity, this));
			}

			producedThisRound += numProduced;
		}

#if false
		if (producedThisRound > 0 && revenueThisRound > 0)
		{
			idleTax = Mathf.Abs(cash * .05f); 
		}
#endif

		//alternative build: only make what is most profitable...?
		foreach (var buildable in outputs)
		{
			//cost to make c = price of dependents + overhead (labor, other)
			//profit = price of c - cost of c

			//number to produce c = f(profit/stock) (want to maximize!)

			//if c_price < c_cost 
			//just buy c, don't produce any
		}

		return asks;
	}
    //get the cost of a commodity
    float GetCostOf(string commodity)
	{
		var com = Commodities.Instance.com;
		float cost = 0;
		foreach (var dep in com[commodity].dep)
		{
			var depCommodity = dep.Key;
			var numDep = dep.Value;
			// TODO take average of stock pile history instead of last price
			var depCost = inventory[depCommodity].buyHistory.Last();
			cost += numDep * depCost.price;
		}
		return cost;
	}
	// Update is called once per frame
	void Update () {
	}
}
