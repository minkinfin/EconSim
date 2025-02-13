using System;
using System.Collections.Generic;
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
        if (BuyRecords.Count > maxRecordedTransactions)
            BuyRecords.RemoveAt(0);

        if (record.Qty > 0)
            lastBoughtPrice = record.Price;
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

        if (record.Qty > 0)
            lastSoldPrice = record.Price;
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

        float avgBought = GetAvgBoughtQty(20);
        float consumptionRate = GetConsumptionRate(20);
        bool cond = BuyBufferQty > 0;
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

        float avgSold = GetAvgSoldQty(20);
        float prodRate = GetProductionRate(20);
        bool cond = SellBufferQty > 0;
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

    public float GetAvgSoldQty(int n)
    {
        int takeCount = Mathf.Min(n, SellRecords.Count);
        if (takeCount == 0)
            return 0;

        float sumqty = 0;
        for (int i = SellRecords.Count - 1; i >= SellRecords.Count - takeCount; i--)
        {
            sumqty += SellRecords[i].Qty;
        }
        return sumqty / takeCount;
    }
    public float GetAvgBoughtQty(int n)
    {
        int takeCount = Mathf.Min(n, BuyRecords.Count);
        if (takeCount == 0)
            return 0;

        float sumqty = 0;
        for (int i = BuyRecords.Count - 1; i >= BuyRecords.Count - takeCount; i--)
        {
            sumqty += BuyRecords[i].Qty;
        }
        return sumqty / takeCount;
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
        int takeCount = Mathf.Min(n, ProducedRecords.Count);
        if (takeCount <= 0)
            return 0;

        float sumqty = 0;
        for (int i = ProducedRecords.Count - 1; i >= ProducedRecords.Count - takeCount; i--)
        {
            sumqty += ProducedRecords[i];
        }
        return sumqty / takeCount;
    }
    internal float GetConsumptionRate(int n)
    {
        int takeCount = Mathf.Min(n, ConsumedRecords.Count);
        if (takeCount <= 0)
            return 0;

        float sumqty = 0;
        for (int i = ConsumedRecords.Count - 1; i >= ConsumedRecords.Count - takeCount; i--)
        {
            sumqty += ConsumedRecords[i];
        }
        return sumqty / takeCount;
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

        if (SellRecords.Count == 0)
            return SellBufferQty;
        TradeRecord tradeRecord = SellRecords[SellRecords.Count - 1];
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

        if (BuyRecords.Count == 0)
            return BuyBufferQty;
        TradeRecord tradeRecord = BuyRecords[BuyRecords.Count - 1];
        if (tradeRecord != null)
            if (tradeRecord.Round == round)
                BuyBufferQty += tradeRecord.Qty;

        return BuyBufferQty;
    }
}
