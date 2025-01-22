using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TradeStats
{
    public int minPriceBelief;
    public int maxPriceBelief;

    public string itemName;
    public int lastBuyAttempPrice;
    public int lastSellAttempPrice;
    public int lastBoughtPrice;
    public int lastSoldPrice;

    public int roundsNoBuy = 0;
    public int roundsBuy = 0;

    public int roundsNoSale = 0;
    public int roundsSale = 0;
    int startFiboPrice = 0;

    int[] fibonacciSeq = new int[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89 };
    private List<TradeRecord> BuyRecords { get; set; }
    private List<TradeRecord> SellRecords { get; set; }


    public TradeStats(string itemName, int priceBelief)
    {
        //Assert.IsTrue(_meanPrice >= 0); //TODO really should never be 0???
        minPriceBelief = priceBelief;
        maxPriceBelief = priceBelief * 2;

        this.itemName = itemName;
        BuyRecords = new List<TradeRecord>();
        SellRecords = new List<TradeRecord>();

    }

    public void AddBuyRecord(int price, int qty, int round)
    {
        var record = new TradeRecord()
        {
            ItemName = itemName,
            Price = price,
            Qty = qty,
            Round = round
        };

        BuyRecords.Add(record);
    }

    public void AddSellRecord(int price, int qty, int round)
    {
        var record = new TradeRecord()
        {
            ItemName = itemName,
            Price = price,
            Qty = qty,
            Round = round
        };

        SellRecords.Add(record);
    }

    //void SanePriceBeliefs(int itemCost)
    //{
    //    //minPriceBelief = Mathf.Max(cost, minPriceBelief); TODO maybe consider this eventually?
    //    //minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900f);
    //    //maxPriceBelief = Mathf.Max(minPriceBelief * 1.1f, maxPriceBelief);
    //    //maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000f);

    //    minPriceBelief = Mathf.Clamp(minPriceBelief, itemCost, maxPriceBelief);
    //    consecutiveRoundsBuy = Mathf.Max(0, consecutiveRoundsBuy);
    //    consecutiveRoundsWithoutBuy = Mathf.Max(0, consecutiveRoundsWithoutBuy);
    //}

    public void UpdateBuyerPriceBelief(int round, string agentName, string itemName, int boughtQty, int unBoughtQty, int lastBuyAttempPrice, int lastBoughtPrice)
    {
        this.lastBuyAttempPrice = lastBuyAttempPrice;
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;

        if (lastBoughtPrice == 0)
            lastBoughtPrice = GetLatestBoughtPrice();
        this.lastBoughtPrice = lastBoughtPrice;

        if (lastBoughtPrice == 0)
        {
            maxPriceBelief = lastBuyAttempPrice + (maxPriceBelief - minPriceBelief);
            minPriceBelief = lastBuyAttempPrice;
        }
        else
        {
            float direction = 0;
            //if (unBoughtQty != 0 && boughtQty != 0)
            //{
            //    direction = (boughtQty - unBoughtQty) / (float)(boughtQty + unBoughtQty);
            //}
            //else if (unBoughtQty == 0)
            //{
            //    direction = 1;
            //}
            //else
            //{
            //    direction = -1;
            //}

            if (boughtQty != 0)
            {
                direction = 1;
            }
            else
            {
                direction = -1;
            }

            if (direction == 1)
            {
                if (roundsBuy == 0)
                    startFiboPrice = minPriceBelief;
                roundsBuy++;

                minPriceBelief = startFiboPrice - Mathf.RoundToInt((GetFibonacciMultiplier(roundsBuy) * startFiboPrice) / 100f);
                maxPriceBelief = lastBoughtPrice;

                roundsNoBuy = 0;
            }
            else if (direction == -1)
            {
                if (roundsNoBuy == 0)
                    startFiboPrice = maxPriceBelief;
                roundsNoBuy++;

                //int nextBuyAttempPrice = lastBuyAttempPrice + Mathf.Max(1, Mathf.RoundToInt(lastBuyAttempPrice * nextAttemptPriceMultiplier));
                minPriceBelief = lastBuyAttempPrice + 1;
                maxPriceBelief = startFiboPrice + Mathf.RoundToInt((GetFibonacciMultiplier(roundsNoBuy) * startFiboPrice) / 100f);

                roundsBuy = 0;
            }
            //else
            //{
            //    roundsBuy = 0;
            //    roundsNoBuy = 0;
            //    int priceWindow = Mathf.RoundToInt((maxPriceBelief - minPriceBelief) * direction * 3f / 100);
            //    maxPriceBelief += priceWindow;
            //    minPriceBelief += priceWindow;
            //}
        }


        minPriceBelief = Mathf.Max(minPriceBelief, 0);
        Assert.IsTrue(minPriceBelief <= maxPriceBelief, $"{round} {agentName} {itemName} ({minPriceBelief}-{maxPriceBelief})");

        string change = prevMaxPriceBelief < maxPriceBelief ? "↗" : "↘";
        if (itemName == "Food")
            Debug.Log($"r={round} Buyer {agentName} ({prevMinPriceBelief}-{prevMaxPriceBelief}){change}({minPriceBelief}-{maxPriceBelief}) B({boughtQty})");
    }
    public void UpdateSellerPriceBelief(int round, string agentName, string itemName, int soldQty, int unSoldQty, int lastSellAttempPrice, int itemCost, int lastSoldPrice, int prodRate)
    {
        this.lastSellAttempPrice = lastSellAttempPrice;
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;

        if (lastSoldPrice == 0)
            lastSoldPrice = GetLatestSoldPrice();
        this.lastSoldPrice = lastSoldPrice;

        float avgSold = GetAvgSoldQty(10);
        if (lastSoldPrice == 0)
        {
            maxPriceBelief = lastSellAttempPrice;
        }
        else
        {
            float direction = 0;
            //if (unSoldQty != 0 && soldQty != 0)
            //{
            //    direction = (soldQty - unSoldQty) / (float)(soldQty + unSoldQty);
            //}
            //else if (unSoldQty == 0)
            //{
            //    direction = 1;
            //}
            //else
            //{
            //    direction = -1;
            //}

            //if(soldQty != 0)
            //{
            //    direction = 1;
            //}
            //else
            //{
            //    direction = -1;
            //}

            if (avgSold >= prodRate)
            {
                direction = 1;
            }
            else
            {
                direction = -1;
            }

            if (direction == 1)
            {
                if (roundsSale == 0)
                    startFiboPrice = maxPriceBelief;
                roundsSale++;

                maxPriceBelief = startFiboPrice + Mathf.RoundToInt((GetFibonacciMultiplier(roundsSale) * startFiboPrice) / 100f);

                //int nextSellAttempPrice = lastSoldPrice + Mathf.Max(1, Mathf.RoundToInt(lastSoldPrice * nextAttemptPriceMultiplier));
                minPriceBelief = lastSoldPrice + 1;

                roundsNoSale = 0;
            }
            else if (direction == -1)
            {
                if (roundsNoSale == 0)
                    startFiboPrice = minPriceBelief;
                roundsNoSale++;

                minPriceBelief = startFiboPrice - Mathf.RoundToInt((GetFibonacciMultiplier(roundsNoSale) * startFiboPrice) / 100f);
                maxPriceBelief = lastSellAttempPrice;
                roundsSale = 0;
            }
            //else
            //{
            //    roundsSale = 0;
            //    roundsNoSale = 0;
            //    int priceWindow = Mathf.RoundToInt((maxPriceBelief - minPriceBelief) * direction * 3f / 100);
            //    maxPriceBelief += priceWindow;
            //    minPriceBelief += priceWindow;
            //}
        }

        minPriceBelief = Mathf.Max(minPriceBelief, itemCost);
        maxPriceBelief = Mathf.Max(maxPriceBelief, itemCost);
        Assert.IsTrue(minPriceBelief <= maxPriceBelief, $"{round} {agentName} {itemName} ({minPriceBelief}-{maxPriceBelief})");

        string change = prevMaxPriceBelief < maxPriceBelief ? "↗" : "↘";
        if (itemName == "Food")
            Debug.Log($"r={round} Seller {agentName} ({prevMinPriceBelief}-{prevMaxPriceBelief}){change}({minPriceBelief}-{maxPriceBelief}) S({avgSold})");
    }

    private int GetFibonacciMultiplier(int index)
    {
        if (index >= fibonacciSeq.Length)
        {
            int result = fibonacciSeq[fibonacciSeq.Length - 1];
            int lefIndex = index - fibonacciSeq.Length;
            result += lefIndex * fibonacciSeq[fibonacciSeq.Length - 1];
            return result;
        }
        else
            return fibonacciSeq[index];
    }

    internal int GetLowestBuyPrice()
    {
        if (BuyRecords.Count() == 0)
            return 0;

        return BuyRecords.Select(x => x.Price).Min();
    }

    internal int GetHighestBuyPrice()
    {
        if (BuyRecords.Count() == 0)
            return 0;

        return BuyRecords.Select(x => x.Price).Max();
    }

    internal int GetLatestBoughtPrice()
    {
        if (BuyRecords.Where(x => x.Qty > 0).Count() == 0)
            return 0;

        return BuyRecords.Where(x => x.Qty > 0).Select(x => x.Price).Last();
    }

    internal int GetLatestSoldPrice()
    {
        if (SellRecords.Where(x => x.Qty > 0).Count() == 0)
            return 0;

        return SellRecords.Where(x => x.Qty > 0).Select(x => x.Price).Last();
    }

    internal int GetAvgBuyPrice(int historySize)
    {
        if (BuyRecords.Count() == 0)
            return 0;

        return (int)BuyRecords.Skip(Mathf.Max(0, BuyRecords.Count - historySize)).Average(x => x.Price);
    }

    private float GetAvgSoldQty(int v)
    {
        if (SellRecords.Count == 0)
            return 0;
        int takeCount = Mathf.Min(v, SellRecords.Count);

        var a = SellRecords.Skip(Mathf.Max(0, SellRecords.Count - takeCount)).ToList();
        var c = a.Sum(x => x.Qty);
        return (float)c / takeCount;
    }
}
