using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TradeStats
{
    public float minPriceBelief;
    public float maxPriceBelief;

    public string itemName;
    public float lastBidAttempPrice;
    public float lastOfferAttempPrice;
    private List<TradeRecord> TradeRecords { get; set; }

    public TradeStats(string itemName, float priceBelief)
    {
        //Assert.IsTrue(_meanPrice >= 0); //TODO really should never be 0???
        minPriceBelief = priceBelief / 2f;
        maxPriceBelief = priceBelief * 2f;

        this.itemName = itemName;
        TradeRecords = new List<TradeRecord>();
        lastBidAttempPrice = float.NaN;
        lastOfferAttempPrice = float.NaN;
    }

    public void AddRecord(TransactionType transactionType, float price, int quantity, int round)
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

    void SanePriceBeliefs()
    {
        //minPriceBelief = Mathf.Max(cost, minPriceBelief); TODO maybe consider this eventually?
        minPriceBelief = Mathf.Clamp(minPriceBelief, 0.1f, 900f);
        maxPriceBelief = Mathf.Max(minPriceBelief * 1.1f, maxPriceBelief);
        maxPriceBelief = Mathf.Clamp(maxPriceBelief, 1.1f, 1000f);
        //Assert.IsTrue(minPriceBelief < maxPriceBelief);
    }

    public void UpdateBuyerPriceBelief(string itemName, int productionRate, in Bid bid, AuctionStats auctionStats)
    {
        // implementation following paper
        var quantityBought = bid.Quantity - bid.remainingQuantity;

        var historicalMeanPrice = auctionStats.GetAvgClearingPrice(itemName, 10);
        if (bid.IsMatched)
        {
            maxPriceBelief = historicalMeanPrice * 2f - (historicalMeanPrice * 0.1f / productionRate);
            minPriceBelief = historicalMeanPrice / 2f - (historicalMeanPrice * 0.1f / productionRate);
        }
        else
        {
            maxPriceBelief = historicalMeanPrice * 2f + (historicalMeanPrice * 0.1f / productionRate);
            minPriceBelief = historicalMeanPrice / 2f + (historicalMeanPrice * 0.1f / productionRate);
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
    }
    public void UpdateSellerPriceBelief(string itemName, int productionRate, in Offer offer, AuctionStats auctionStats)
    {
        var prevMinPriceBelief = minPriceBelief;
        var prevMaxPriceBelief = maxPriceBelief;
        //SanePriceBeliefs();

        //var meanBeliefPrice = (minPriceBelief + maxPriceBelief) / 2;
        //var deltaMean = meanBeliefPrice - offer.Price; //TODO or use auction house mean price?
        var quantitySold = offer.Quantity - offer.remainingQuantity;
        var historicalMeanPrice = auctionStats.GetAvgClearingPrice(itemName, 10);
        var lastTradeQuantity = auctionStats.GetLastTradeQuantity(itemName);
        var market_share = lastTradeQuantity == 0 ? 0 : quantitySold / (float)lastTradeQuantity;
        var weight = quantitySold / (float)offer.Quantity; //quantitySold / quantityOffered
        var displacement = (1 - weight) * historicalMeanPrice;

        if (offer.IsMatched)
        {
            maxPriceBelief = historicalMeanPrice * 2f + (historicalMeanPrice * 0.1f / productionRate);
            minPriceBelief = historicalMeanPrice / 2f + (historicalMeanPrice * 0.1f / productionRate);
        }
        else
        {
            maxPriceBelief = historicalMeanPrice * 2f - (historicalMeanPrice * 0.1f / productionRate);
            minPriceBelief = historicalMeanPrice / 2f - (historicalMeanPrice * 0.1f / productionRate);
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
    }
    //TODO change quantity based on historical price ranges & deficit
    internal float GetLowestBuyPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Select(x => x.Price).Min();
    }

    internal float GetHighestBuyPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Select(x => x.Price).Max();
    }

    internal float GetLatestBuyPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Select(x => x.Price).Last();
    }

    internal float GetLowestSellPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Select(x => x.Price).Min();
    }

    internal float GetHighestSellPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Select(x => x.Price).Max();
    }

    internal float GetLatestSellPrice()
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Count() == 0)
            return 0;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Sell).Select(x => x.Price).Last();
    }

    internal float GetAvgBuyPrice(int historySize)
    {
        if (TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Count() == 0)
            return 1;

        return TradeRecords.Where(x => x.TransactionType == TransactionType.Buy).Skip(Mathf.Max(0, TradeRecords.Count - historySize)).Average(x => x.Price);
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
