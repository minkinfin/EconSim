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
    public int priceBelief;

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

    //int[] fibonacciSeq = new int[] {1, 2, 3, 5, 8, 13, 21, 34 };
    int[] fibonacciSeq = new int[] { 3 };
    private List<TradeRecord> BuyRecords { get; set; }
    private List<TradeRecord> SellRecords { get; set; }

    public List<int> ProducedRecords { get; set; }
    public List<int> ConsumedRecords { get; set; }

    private int maxRecordedTransactions;

    public float BuyBufferQty { get; private set; }
    public float SellBufferQty { get; private set; }

    public TradeStats(string itemName, int _priceBelief, int maxRecordedTransactions)
    {
        this.itemName = itemName;
        minPriceBelief = (int)(_priceBelief * 1.5f);
        maxPriceBelief = _priceBelief * 2;
        this.maxRecordedTransactions = maxRecordedTransactions;

        priceBelief = UnityEngine.Random.Range(minPriceBelief + 1, maxPriceBelief);
        BuyRecords = new List<TradeRecord>();
        SellRecords = new List<TradeRecord>();
        ProducedRecords = new List<int>();
        ConsumedRecords = new List<int>();
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
        if(BuyRecords.Count > maxRecordedTransactions)
            BuyRecords.RemoveAt(0);
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
        if (SellRecords.Count > maxRecordedTransactions)
            SellRecords.RemoveAt(0);
    }

    public void AddProducedRecord(int qty)
    {
        ProducedRecords.Add(qty);
        if (ProducedRecords.Count > maxRecordedTransactions)
            ProducedRecords.RemoveAt(0);
    }

    public void AddConsumedRecord(int qty)
    {
        ConsumedRecords.Add(qty);
        if (ConsumedRecords.Count > maxRecordedTransactions)
            ConsumedRecords.RemoveAt(0);
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

    public int UpdateBuyerPriceBelief(string itemName, int lastBuyAttempPrice, int boughtQty, int round)
    {
        this.lastBuyAttempPrice = lastBuyAttempPrice;
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;

        int lastBoughtPrice = 0;
        if (boughtQty > 0)
            lastBoughtPrice = lastBuyAttempPrice;
        else
            lastBoughtPrice = GetLatestBoughtPrice();
        this.lastBoughtPrice = lastBoughtPrice;

        float avgBought = GetAvgBoughtQty(20);
        float consumptionRate = GetConsumptionRate(20);
        bool cond = BuyBufferQty >= consumptionRate;
        if (priceBelief == 0)
        {
            //maxPriceBelief = lastBuyAttempPrice + (maxPriceBelief - minPriceBelief);
            minPriceBelief = UnityEngine.Random.Range(minPriceBelief + 1, maxPriceBelief);
            maxPriceBelief = maxPriceBelief + (minPriceBelief - prevMinPriceBelief);
            priceBelief = minPriceBelief;
        }
        else
        {
            if (boughtQty > 0 && cond)
            {
                if (roundsBuy == 0)
                    startFiboPrice = priceBelief;

                priceBelief = startFiboPrice - Mathf.FloorToInt((GetFibonacciMultiplier(roundsBuy) * startFiboPrice) / 100f);// + 1;
                //maxPriceBelief = lastBoughtPrice;
                //maxPriceBelief = startFiboPrice;
                roundsBuy++;
                roundsNoBuy = 0;
            }
            else if (boughtQty == 0 && !cond)
            {
                if (roundsNoBuy == 0)
                    startFiboPrice = priceBelief;

                //minPriceBelief = lastBuyAttempPrice + 1;
                //minPriceBelief = startFiboPrice;
                priceBelief = startFiboPrice + Mathf.FloorToInt((GetFibonacciMultiplier(roundsNoBuy) * startFiboPrice) / 100f);// - 1;
                roundsNoBuy++;
                roundsBuy = 0;
            }
            //else
            //{
            //    roundsBuy = 0;
            //    roundsNoBuy = 0;
            //}
        }

        minPriceBelief = Mathf.Max(minPriceBelief, 0);
        priceBelief = Mathf.Max(priceBelief, 0);
        //Assert.IsTrue(minPriceBelief <= maxPriceBelief, $"Buyer {itemName} ({minPriceBelief}-{maxPriceBelief})");

        return priceBelief;
    }
    public int UpdateSellerPriceBelief(string itemName, int lastSellAttempPrice, int itemCost, int soldQty, int round)
    {
        this.lastSellAttempPrice = lastSellAttempPrice;
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;

        int lastSoldPrice = 0;
        if (soldQty > 0)
            lastSoldPrice = lastSellAttempPrice;
        else
            lastSoldPrice = GetLatestSoldPrice();
        this.lastSoldPrice = lastSoldPrice;

        float avgSold = GetAvgSoldQty(20);
        float prodRate = GetProductionRate(20);
        bool cond = SellBufferQty >= prodRate;
        if (priceBelief == 0)
        {
            maxPriceBelief = UnityEngine.Random.Range(minPriceBelief, maxPriceBelief);
            priceBelief = maxPriceBelief;
        }
        else
        {
            if (soldQty > 0 && cond)
            {
                if (roundsSale == 0)
                    startFiboPrice = priceBelief;

                priceBelief = startFiboPrice + Mathf.FloorToInt((GetFibonacciMultiplier(roundsSale) * startFiboPrice) / 100f);// - 1;
                //minPriceBelief = lastSoldPrice + 1;
                //minPriceBelief = startFiboPrice;
                roundsSale++;
                roundsNoSale = 0;
            }
            else if (soldQty == 0 && !cond)
            {
                if (roundsNoSale == 0)
                    startFiboPrice = priceBelief;

                priceBelief = startFiboPrice - Mathf.FloorToInt((GetFibonacciMultiplier(roundsNoSale) * startFiboPrice) / 100f);// + 1;
                //maxPriceBelief = lastSellAttempPrice;
                //maxPriceBelief = startFiboPrice;
                roundsNoSale++;
                roundsSale = 0;
            }
            //else
            //{
            //    roundsSale = 0;
            //    roundsNoSale = 0;
            //}
        }

        //minPriceBelief = Mathf.Max(minPriceBelief, itemCost);
        //maxPriceBelief = Mathf.Max(maxPriceBelief, itemCost);
        //if (minPriceBelief == maxPriceBelief)
        //{
        //    maxPriceBelief = maxPriceBelief + 1;
        //}
        //Assert.IsTrue(minPriceBelief <= maxPriceBelief, $"Seller {itemName} ({minPriceBelief}-{maxPriceBelief})");
        priceBelief = Mathf.Max(priceBelief, itemCost);

        return priceBelief;
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

    public float GetAvgSoldQty(int n)
    {
        int takeCount = Mathf.Min(n, SellRecords.Count);
        if (takeCount == 0)
            return 0;

        var a = SellRecords.Skip(Mathf.Max(0, SellRecords.Count - takeCount)).ToList();

        float c = a.Sum(x => x.Qty);
        return c / takeCount;
    }
    public float GetAvgBoughtQty(int n)
    {
        int takeCount = Mathf.Min(n, BuyRecords.Count);
        if (takeCount == 0)
            return 0;

        var a = BuyRecords.Skip(Mathf.Max(0, BuyRecords.Count - takeCount)).ToList();
        float c = a.Sum(x => x.Qty);
        return c / takeCount;
    }

    //public float GetAvgSoldQty(int n)
    //{
    //    int takeCount = Mathf.Min(n, SellRecords.Count);
    //    if (takeCount == 0)
    //        return 0;

    //    var a = SellRecords.Skip(Mathf.Max(0, SellRecords.Count - takeCount)).ToList();
    //    var b = a.Where(x => x.Qty == a.Max(x => x.Qty)).LastOrDefault();
    //    if (b == null)
    //        return 0;
    //    if (b.Qty == 0)
    //        return 0;

    //    a = a.Where(x => x.Round >= b.Round).ToList();
    //    return b.Qty / (float)a.Count;
    //}
    //public float GetAvgBoughtQty(int n)
    //{
    //    int takeCount = Mathf.Min(n, BuyRecords.Count);
    //    if (takeCount == 0)
    //        return 0;

    //    var a = BuyRecords.Skip(Mathf.Max(0, BuyRecords.Count - takeCount)).ToList();
    //    var b = a.Where(x => x.Qty == a.Max(x => x.Qty)).LastOrDefault();
    //    if (b == null)
    //        return 0;
    //    if (b.Qty == 0)
    //        return 0;

    //    a = a.Where(x => x.Round >= b.Round).ToList();
    //    return b.Qty / (float)a.Count;
    //}

    //public float GetSoldBufferDays(int n, int currentRound)
    //{
    //    return SellBufferDays;
    //    //int takeCount = Mathf.Min(n, SellRecords.Count);
    //    //if (takeCount == 0)
    //    //    return 0;

    //    //var a = SellRecords.Skip(Mathf.Max(0, SellRecords.Count - takeCount)).ToList();
    //    //float prodRate = GetProductionRate(n);

    //    //float value = 0;
    //    //int startRound = a.Min(x => x.Round);
    //    //for (int i = startRound; i <= currentRound; i++)
    //    //{
    //    //    if (value > 0)
    //    //        value -= prodRate;
    //    //    TradeRecord tradeRecord = a.Where(x => x.Round == i).FirstOrDefault();
    //    //    if (tradeRecord != null)
    //    //        value += tradeRecord.Qty;
    //    //}

    //    //return Mathf.Max(0, value) / prodRate;
    //    ////return a.Select(x => Mathf.Max(0, (x.Qty / prodRate) - (currentRound - x.Round))).Sum();
    //}
    //public float GetBoughtBufferDays(int n, int currentRound)
    //{
    //    return BuyBufferDays;
    //    //int takeCount = Mathf.Min(n, BuyRecords.Count);
    //    //if (takeCount == 0)
    //    //    return 0;

    //    //var a = BuyRecords.Skip(Mathf.Max(0, BuyRecords.Count - takeCount)).ToList();
    //    //float consumedRate = GetConsumptionRate(n);
    //    ////return a.Select(x => Mathf.Max(0, (x.Qty / consumedRate) - (currentRound - x.Round))).Sum();

    //    //float value = 0;
    //    //int startRound = a.Min(x => x.Round);
    //    //for (int i = startRound; i <= currentRound; i++)
    //    //{
    //    //    if (value > 0)
    //    //        value -= consumedRate;
    //    //    TradeRecord tradeRecord = a.Where(x => x.Round == i).FirstOrDefault();
    //    //    if (tradeRecord != null)
    //    //        value += tradeRecord.Qty;
    //    //}
    //    //return Mathf.Max(0, value) / consumedRate;
    //}

    internal float GetProductionRate(int n)
    {
        if (ProducedRecords.Count == 0)
            return 0;
        int takeCount = Mathf.Min(n, ProducedRecords.Count);
        var a = ProducedRecords.Skip(Mathf.Max(0, ProducedRecords.Count - takeCount)).ToList();
        var c = a.Sum();
        return (float)c / takeCount;
    }
    internal float GetConsumptionRate(int n)
    {
        if (ConsumedRecords.Count == 0)
            return 0;
        int takeCount = Mathf.Min(n, ConsumedRecords.Count);
        var a = ConsumedRecords.Skip(Mathf.Max(0, ConsumedRecords.Count - takeCount)).ToList();
        var c = a.Sum();
        return (float)c / takeCount;
    }

    internal int GetPriceBelief()
    {
        return priceBelief;
    }

    internal float UpdateSellBufferQty(int round)
    {
        float prodRate = GetProductionRate(round);
        if (SellBufferQty > 0)
            SellBufferQty -= prodRate;
        TradeRecord tradeRecord = SellRecords.LastOrDefault();
        if (tradeRecord != null)
            if (tradeRecord.Round == round)
                SellBufferQty += tradeRecord.Qty;

        return SellBufferQty;
    }

    internal float UpdateBuyBufferQty(int round)
    {
        float consumedRate = GetConsumptionRate(round);
        if (BuyBufferQty > 0)
            BuyBufferQty -= consumedRate;
        TradeRecord tradeRecord = BuyRecords.LastOrDefault();
        if (tradeRecord != null)
            if (tradeRecord.Round == round)
                BuyBufferQty += tradeRecord.Qty;

        return BuyBufferQty;
    }
}
