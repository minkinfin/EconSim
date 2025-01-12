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
    private List<TradeRecord> TradeRecords { get; set; }

    public TradeStats(string itemName, int priceBelief)
    {
        //Assert.IsTrue(_meanPrice >= 0); //TODO really should never be 0???
        minPriceBelief = priceBelief;
        maxPriceBelief = priceBelief * 2;

        this.itemName = itemName;
        TradeRecords = new List<TradeRecord>();
    }

    public void AddRecord(TransactionType transactionType, int price, int quantity, int round)
    {
        var record = new TradeRecord()
        {
            ItemName = itemName,
            TransactionType = transactionType,
            Price = price,
            Quantity = quantity,
            Round = round
        };

        TradeRecords.Add(record);
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

        if (lastBoughtPrice == 0)
            lastBoughtPrice = GetLatestBoughtPrice();
        this.lastBoughtPrice = lastBoughtPrice;

        if (lastBoughtPrice == 0)
        {
            minPriceBelief = lastBuyAttempPrice;
        }
        else
        {
            float direction = 0;
            if (unBoughtQty != 0 && boughtQty != 0)
            {
                direction = (boughtQty - unBoughtQty) / (float)(boughtQty + unBoughtQty);
            }
            else if (unBoughtQty == 0)
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

                minPriceBelief = lastBuyAttempPrice;
                maxPriceBelief = startFiboPrice + Mathf.RoundToInt((GetFibonacciMultiplier(roundsNoBuy) * startFiboPrice) / 100f);

                roundsBuy = 0;
            }
            else
            {
                roundsBuy = 0;
                roundsNoBuy = 0;
                int priceWindow = Mathf.RoundToInt((maxPriceBelief - minPriceBelief) * direction * 3f / 100);
                maxPriceBelief += priceWindow;
                minPriceBelief += priceWindow;
            }
        }


        minPriceBelief = Mathf.Max(minPriceBelief, 0);
        maxPriceBelief = Mathf.Max(maxPriceBelief, 0);
        Assert.IsTrue(minPriceBelief <= maxPriceBelief, $"{round} {agentName} {itemName} minP: {minPriceBelief} maxP: {maxPriceBelief}");
    }
    public void UpdateSellerPriceBelief(int round, string agentName, string itemName, int soldQty, int unSoldQty, int lastSellAttempPrice, int itemCost, int lastSoldPrice)
    {
        this.lastSellAttempPrice = lastSellAttempPrice;

        if (lastSoldPrice == 0)
            lastSoldPrice = GetLatestSoldPrice();
        this.lastSoldPrice = lastSoldPrice;

        if (lastSoldPrice == 0)
        {
            maxPriceBelief = lastSellAttempPrice;
        }
        else
        {
            float direction = 0;
            if (unSoldQty != 0 && soldQty != 0)
            {
                direction = (soldQty - unSoldQty) / (float)(soldQty + unSoldQty);
            }
            else if (unSoldQty == 0)
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
                minPriceBelief = lastSoldPrice;

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
            else
            {
                roundsSale = 0;
                roundsNoSale = 0;
                int priceWindow = Mathf.RoundToInt((maxPriceBelief - minPriceBelief) * direction * 3f / 100);
                maxPriceBelief += priceWindow;
                minPriceBelief += priceWindow;
            }
        }

        minPriceBelief = Mathf.Max(minPriceBelief, itemCost);
        Assert.IsTrue(minPriceBelief <= maxPriceBelief, $"{round} {agentName} {itemName} minP: {minPriceBelief} maxP: {maxPriceBelief}");
    }

    private int GetFibonacciMultiplier(int index)
    {
        index = Mathf.Clamp(index, 0, fibonacciSeq.Length - 1);
        return fibonacciSeq[index];
    }

    internal int GetLowestBuyPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Select(x => x.Price).Min();
    }

    internal int GetHighestBuyPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Select(x => x.Price).Max();
    }

    internal int GetLatestBoughtPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Select(x => x.Price).Last();
    }

    internal int GetLowestSoldPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Select(x => x.Price).Min();
    }

    internal int GetHighestSoldPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Select(x => x.Price).Max();
    }

    internal int GetLatestSoldPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Count() == 0)
            return 0;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Select(x => x.Price).Last();
    }

    internal int GetAvgBuyPrice(int historySize)
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return (int)TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Skip(Mathf.Max(0, TradeRecords.Count - historySize)).Average(x => x.Price);
    }

    internal int GetLastSoldDay()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Count() == 0)
            return 0;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Last().Round;
    }

    internal int GetLastBoughtDay()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return -1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Last().Round;
    }
}
