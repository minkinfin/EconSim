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

    int consecutiveRoundsWithoutBuy = 0;
    int consecutiveRoundsBuy = 0;
    int startConsecutiveBuyPrice = 0;

    int consecutiveRoundsWithoutSale = 0;
    int consecutiveRoundsSale = 0;
    int startConsecutiveSellPrice = 0;

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

    public void UpdateBuyerPriceBelief(int round, string agentName, string itemName, int productionRate, int boughtQty, int unBoughtQty, int lastBuyAttempPrice, int lastBoughtPrice)
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
                startConsecutiveBuyPrice = Mathf.Min(lastBoughtPrice, lastBuyAttempPrice);

                consecutiveRoundsBuy++;
                minPriceBelief = startConsecutiveBuyPrice - Mathf.RoundToInt((GetFibonacciMultiplier(consecutiveRoundsBuy) * startConsecutiveBuyPrice) / 100f);
                maxPriceBelief = Mathf.Max(lastBoughtPrice, lastBuyAttempPrice);

                consecutiveRoundsWithoutBuy = 0;
            }
            else if (direction == -1)
            {
                if (consecutiveRoundsWithoutBuy == 0)
                    startConsecutiveBuyPrice = maxPriceBelief;

                consecutiveRoundsWithoutBuy++;
                minPriceBelief = lastBuyAttempPrice;
                maxPriceBelief = startConsecutiveBuyPrice + Mathf.RoundToInt((GetFibonacciMultiplier(consecutiveRoundsWithoutBuy) * startConsecutiveBuyPrice) / 100f);

                consecutiveRoundsBuy = 0;
            }
            else
            {
                consecutiveRoundsBuy = 0;
                consecutiveRoundsWithoutBuy = 0;
                maxPriceBelief = Mathf.RoundToInt(maxPriceBelief * direction * 5);
                minPriceBelief = Mathf.RoundToInt(minPriceBelief * direction * 5);
            }
        }


        minPriceBelief = Mathf.Max(minPriceBelief, 0);
        maxPriceBelief = Mathf.Max(maxPriceBelief, 0);
        Assert.IsTrue(minPriceBelief <= maxPriceBelief, $"{round} {agentName} {itemName} minP: {minPriceBelief} maxP: {maxPriceBelief}");
    }
    public void UpdateSellerPriceBelief(int round, string agentName, string itemName, int productionRate, int soldQty, int unSoldQty, int lastSellAttempPrice, int itemCost, int lastSoldPrice)
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
                startConsecutiveSellPrice = Mathf.Max(lastSoldPrice, lastSellAttempPrice);

                consecutiveRoundsSale++;
                maxPriceBelief = startConsecutiveSellPrice + Mathf.RoundToInt((GetFibonacciMultiplier(consecutiveRoundsSale) * startConsecutiveSellPrice) / 100f);
                minPriceBelief = Mathf.Min(lastSoldPrice, lastSellAttempPrice);

                consecutiveRoundsWithoutSale = 0;
            }
            else if (direction == -1)
            {
                if (consecutiveRoundsWithoutSale == 0)
                    startConsecutiveSellPrice = minPriceBelief;

                consecutiveRoundsWithoutSale++;

                minPriceBelief = startConsecutiveSellPrice - Mathf.RoundToInt((GetFibonacciMultiplier(consecutiveRoundsWithoutSale) * startConsecutiveSellPrice) / 100f);
                maxPriceBelief = lastSellAttempPrice;
                consecutiveRoundsSale = 0;
            }
            else
            {
                consecutiveRoundsSale = 0;
                consecutiveRoundsWithoutSale = 0;
                maxPriceBelief += Mathf.RoundToInt(maxPriceBelief * direction * 5f / 100);
                minPriceBelief += Mathf.RoundToInt(minPriceBelief * direction * 5f / 100);
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
