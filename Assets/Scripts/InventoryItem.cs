using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Assertions.Must;

[Serializable]
public class InventoryItem
{
    public string commodityName;
    const float significant = 0.25f;
    const float sig_imbalance = .33f;
    const float lowInventory = .1f;
    const float highInventory = 2f;
    public TransactionHistory buyHistory;
    public TransactionHistory sellHistory;
    public float originalCost { get; set; }
    public float price = 1;
    public int Quantity;
    public float quantityTradedThisRound { get; set; }
    public float costThisRound { get; set; }
    public int maxQuantity { get; set; }
    public float minPriceBelief;
    public float maxPriceBelief;
    //number of units produced per turn = production * productionRate
    public int productionRate { get; set; }
    List<string> debug_msgs = new List<string>();
    bool boughtThisRound = false;
    bool soldThisRound = false;
    protected AgentConfig config;

    public InventoryItem(string _name, int _quantity = 1, int _maxQuantity = 10, float _meanPrice = 1, int _production = 1, AgentConfig config = null)
    {
        this.config = config;
        buyHistory = new TransactionHistory();
        sellHistory = new TransactionHistory();
        commodityName = _name;
        Quantity = _quantity;
        maxQuantity = _maxQuantity;
        //Assert.IsTrue(_meanPrice >= 0); //TODO really should never be 0???
        minPriceBelief = _meanPrice / 2f;
        maxPriceBelief = _meanPrice * 2f;
        originalCost = _meanPrice;
        price = _meanPrice;
        buyHistory.Add(new Transaction(1, _meanPrice));
        sellHistory.Add(new Transaction(1, _meanPrice));
        productionRate = _production;
    }
    public void Tick()
    {
    }
    public float Increase(int quant)
    {
        Quantity += quant;
        //Assert.IsTrue(quant >= 0);
        return Quantity;
    }
    public float Decrease(int quant)
    {
        Quantity -= quant;
        ////Assert.IsTrue(Quantity >= 0);
        return Quantity;
    }
    public void ClearRoundStats()
    {
        costThisRound = 0;
        quantityTradedThisRound = 0;
        soldThisRound = false;
        boughtThisRound = false;
    }
    public int Buy(int quant, float price)
    {
        // UnityEngine.Debug.Log("buying " + commodityName + " " + quant.ToString("n2") + " for " + price.ToString("c2") + " currently have " + Quantity.ToString("n2"));
        //update meanCost of units in stock
        //Assert.IsTrue(quant > 0);

        quantityTradedThisRound += quant;
        costThisRound += price;
        this.price = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;
        Quantity += quant;
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
        if (boughtThisRound)
        {
            buyHistory.UpdateLast(new Transaction(price, quant));
        }
        else
        {
            buyHistory.Add(new Transaction(price, quant));
        }
        boughtThisRound = true;
        //return adjusted quant;
        return quant;
    }
    public void Sell(int quant, float price)
    {
        //update meanCost of units in stock
        //Assert.IsTrue(Quantity >= 0);
        if (quant == 0 || Surplus() == 0)
            return;
        // UnityEngine.Debug.Log("sell quant is " + quant + " and surplus is " + Surplus());
        quant = Mathf.Min(quant, Surplus());
        //Assert.IsTrue(quant > 0);
        Quantity -= quant;
        quantityTradedThisRound += quant;
        costThisRound += price;
        this.price = (quantityTradedThisRound == 0) ? 0 : costThisRound / quantityTradedThisRound;
        if (soldThisRound)
        {
            sellHistory.UpdateLast(new Transaction(price, quant));
        }
        else
        {
            sellHistory.Add(new Transaction(price, quant));
        }
        soldThisRound = true;
    }

    //public float GetCostFromDependency(Dictionary<string, InventoryItem> Inventory)
    //{
    //    if (dependency == null)
    //    {
    //        return meanCostThisRound;
    //    }

    //    float cost = 0;
    //    foreach (var dep in dependency)
    //    {
    //        cost += (Inventory[dep.Key].bidPrice * dep.Value);
    //    }
    //    cost = cost / Math.Max(productionRate - 1, 1);

    //    return cost;
    //}

    public float GetPrice()
    {
        SanePriceBeliefs();
        var p = UnityEngine.Random.Range(minPriceBelief, maxPriceBelief);
        price = p;
        return p;
    }
    void SanePriceBeliefs()
    {
        //minPriceBelief = Mathf.Max(cost, minPriceBelief); TODO maybe consider this eventually?
        minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900f);
        maxPriceBelief = Mathf.Max(minPriceBelief * 1.1f, maxPriceBelief);
        maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000f);
        //Assert.IsTrue(minPriceBelief < maxPriceBelief);
    }

    public void UpdateBuyerPriceBelief(string agentName, in Bid bid, in Commodity commodity)
    {
        // implementation following paper
        var quantityBought = bid.Quantity - bid.remainingQuantity;
        string reason_msg = "none";

        var historicalMeanPrice = commodity.avgClearingPrice.LastAverage(10);
        if (bid.IsMatched)
        {
            maxPriceBelief = historicalMeanPrice * 2f - (historicalMeanPrice * 0.1f / commodity.ProductionRate);
            minPriceBelief = historicalMeanPrice / 2f - (historicalMeanPrice * 0.1f / commodity.ProductionRate);
        }
        else
        {
            maxPriceBelief = historicalMeanPrice * 2f + (historicalMeanPrice * 0.1f / commodity.ProductionRate);
            minPriceBelief = historicalMeanPrice / 2f + (historicalMeanPrice * 0.1f / commodity.ProductionRate);
        }

        //if (quantityBought * 2 > bid.Quantity) //at least 50% offer filled
        //{
        //    // move limits inward by 10 of upper limit%
        //    var adjustment = maxPriceBelief * 0.1f;
        //    maxPriceBelief -= adjustment;
        //    minPriceBelief += adjustment;
        //    reason_msg = "buy>.5";
        //}
        //else
        //{
        //    // move upper limit by 10%
        //    maxPriceBelief *= 1.1f;
        //    reason_msg = "buy<=.5";
        //}

        //if (Quantity < maxQuantity / 4) //bid more than total offers and inventory < 1/4 max
        //{
        //    //var deltaMean = Mathf.Abs(historicalMeanPrice - bid.Price); //TODO or use auction house mean price?
        //    //var displacement = deltaMean / historicalMeanPrice;
        //    //maxPriceBelief += displacement;
        //    //minPriceBelief += displacement;
        //    maxPriceBelief += historicalMeanPrice / 5;
        //    minPriceBelief += historicalMeanPrice / 5;
        //    reason_msg += "_supply<demand_and_low_inv";
        //}
        //else if (bid.Price > bid.AcceptPrice && bid.IsMatched   //bid price > trade price
        //    || (commodity.offers[^1] > commodity.bids[^1] && bid.Price > historicalMeanPrice))   // or (supply > demand and offer > historical mean)
        //{
        //    var overbid = Mathf.Abs(bid.Price - bid.AcceptPrice);
        //    maxPriceBelief -= overbid * 1.1f;
        //    minPriceBelief -= overbid * 1.1f;
        //    reason_msg += "_supply>demand_and_overbid";
        //}
        //else if (commodity.bids[^1] > commodity.offers[^1])     //demand > supply
        //{
        //    //translate belief range up 1/5th of historical mean price
        //    maxPriceBelief += historicalMeanPrice / 5;
        //    minPriceBelief += historicalMeanPrice / 5;
        //    reason_msg += "_supply<demand";
        //}
        //else if (commodity.offers[^1] > commodity.bids[^1])     //supply > demand
        //{
        //    //translate belief range down 1/5th of historical mean price
        //    maxPriceBelief -= historicalMeanPrice / 5;
        //    minPriceBelief -= historicalMeanPrice / 5;
        //    reason_msg += "_supply>demand";
        //}

        SanePriceBeliefs();
        // UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        // UnityEngine.Debug.Log("buyer " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
        //Assert.IsTrue(minPriceBelief < maxPriceBelief);
        debug_msgs.Add(reason_msg);
    }
    public void UpdateSellerPriceBelief(string agentName, in Offer offer, in Commodity commodity)
    {
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
        //SanePriceBeliefs();

        //var meanBeliefPrice = (minPriceBelief + maxPriceBelief) / 2;
        //var deltaMean = meanBeliefPrice - offer.Price; //TODO or use auction house mean price?
        var quantitySold = offer.Quantity - offer.remainingQuantity;
        var historicalMeanPrice = commodity.avgClearingPrice.LastAverage(10);
        var market_share = quantitySold / (float)commodity.trades[^1];
        var weight = quantitySold / (float)offer.Quantity; //quantitySold / quantityOffered
        var displacement = (1 - weight) * historicalMeanPrice;

        string reason_msg = "none";

        if (offer.IsMatched)
        {
            maxPriceBelief = historicalMeanPrice * 2f + (historicalMeanPrice * 0.1f / commodity.ProductionRate);
            minPriceBelief = historicalMeanPrice / 2f + (historicalMeanPrice * 0.1f / commodity.ProductionRate);
        }
        else
        {
            maxPriceBelief = historicalMeanPrice * 2f - (historicalMeanPrice * 0.1f / commodity.ProductionRate);
            minPriceBelief = historicalMeanPrice / 2f - (historicalMeanPrice * 0.1f / commodity.ProductionRate);
        }

        //if (weight == 0)
        //{
        //    //maxPriceBelief -= displacement / 5f;
        //    //minPriceBelief -= displacement / 5f;
        //    maxPriceBelief -= historicalMeanPrice / 5;
        //    minPriceBelief -= historicalMeanPrice / 5;
        //    reason_msg = "seller_sold_none";
        //}
        //else if (market_share < .75f)
        //{
        //    maxPriceBelief -= displacement / 6f;
        //    minPriceBelief -= displacement / 6f;
        //    reason_msg = "seller_market_share_<.75";
        //}
        //else if (offer.Price < offer.AcceptPrice && offer.IsMatched)
        //{
        //    var underbid = offer.AcceptPrice - offer.Price;
        //    maxPriceBelief += underbid * 1.1f;
        //    minPriceBelief += underbid * 1.1f;
        //    reason_msg = "seller_under_bid";
        //}
        //else if (commodity.bids[^1] > commodity.offers[^1])     //demand > supply
        //{
        //    //translate belief range up 1/5th of historical mean price
        //    maxPriceBelief += historicalMeanPrice / 5;
        //    minPriceBelief += historicalMeanPrice / 5;
        //    reason_msg = "seller_demand>supply";
        //}
        //else if (commodity.offers[^1] > commodity.bids[^1])     //supply > demand
        //{
        //    //translate belief range down 1/5th of historical mean price
        //    maxPriceBelief -= historicalMeanPrice / 5;
        //    minPriceBelief -= historicalMeanPrice / 5;
        //    reason_msg = "seller_demand<=supply";
        //}

        //ensure buildable price at least cost of input commodities

        SanePriceBeliefs();
        //Assert.IsFalse(float.IsNaN(minPriceBelief));
        // UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " min price belief: " + prevMinPriceBelief + " -> " + minPriceBelief);
        // UnityEngine.Debug.Log("seller " + agentName + " stock: " + commodityName + " max price belief: " + prevMaxPriceBelief + " -> " + maxPriceBelief);
        //Assert.IsTrue(minPriceBelief < maxPriceBelief);
        debug_msgs.Add(reason_msg);
    }
    //TODO change quantity based on historical price ranges & deficit
    public int Deficit()
    {
        int shortage = maxQuantity - Quantity;
        return Math.Max(0, shortage);
    }
    public int Surplus() { return Quantity; }
}