﻿using System.Collections.Generic;
using UnityEngine;
using System;
using AYellowpaper.SerializedCollections;

public class EconAgent : MonoBehaviour
{
    AgentConfig config;
    static int uid_idx = 0;
    public int uid;
    public int cash;
    public int starvationDay;
    [SerializedDictionary("Name", "Item")]
    public SerializedDictionary<string, TradeStats> TradeStats;
    public Inventory Inventory;
    public List<string> outputs { get; private set; } //can produce commodities

    //production has dependencies on commodities->populates stock
    //production rate is limited by assembly lines (queues/event lists)

    //can use profit to reinvest - produce new commodities
    //switching cost to produce new commodities - zero for now

    //from the paper (base implementation)
    // Use this for initialization
    public bool IsBankrupt { get; set; }

    void Awake()
    {
        TradeStats = new SerializedDictionary<string, TradeStats>();
    }

    public void Init(AgentConfig cfg, int initCash, List<string> b)
    {
        config = cfg;
        Inventory = new Inventory(config);
        uid = uid_idx++;

        //list of commodities self can produce
        //get initial stockpiles
        outputs = b;
        cash = initCash;
        foreach (var com in config.commodities)
        {
            int guessCost = 0;
            int guessCostFromRecipe = config.initCost;
            if (outputs.Contains(com.name))
            {
                foreach (var recipe in com.recipes)
                {
                    foreach (var material in recipe.materials)
                    {
                        guessCostFromRecipe += config.initCost * material.Value;
                    }
                    guessCostFromRecipe /= recipe.outputRate;
                    guessCost += guessCostFromRecipe;
                }

                guessCost /= com.recipes.Count;
            }
            else
            {
                guessCost = config.initCost;
            }

            //Inventory.AddItem(com.name, guessCost, com.recipes, 0);
            TradeStats.Add(com.name, new TradeStats(com.name, guessCost, config.MaxRecordedTransactions));
        }
    }

    public (Dictionary<string, int>, Dictionary<string, int>) Produce()
    {
        Dictionary<string, int> expecedProducedThisRound = new Dictionary<string, int>();
        Dictionary<string, int> expecedConsumedThisRound = new Dictionary<string, int>();
        Dictionary<string, int> actualProducedThisRound = new Dictionary<string, int>();
        Dictionary<string, int> actualConsumedThisRound = new Dictionary<string, int>();
        foreach (var com in config.commodities)
        {
            expecedProducedThisRound[com.name] = 0;
            expecedConsumedThisRound[com.name] = 0;
            actualProducedThisRound[com.name] = 0;
            actualConsumedThisRound[com.name] = 0;
        }

        foreach (var buildItemName in outputs)
        {
            var itemInfo = Inventory.GetItemInfo(buildItemName);
            var com = config.commodities.Find(x => x.name == buildItemName);
            foreach (var recipe in com.recipes)
            {
                int prodRate = recipe.productionRate;
                int outputRate = recipe.outputRate;
                int actualNumProduced = prodRate;
                expecedProducedThisRound[buildItemName] += prodRate * outputRate;
                foreach (var material in recipe.materials)
                {
                    var numNeeded = material.Value;
                    var materialInfo = Inventory.GetItemInfo(material.Key);
                    var numAvail = materialInfo.Qty;
                    actualNumProduced = Math.Min(actualNumProduced, numAvail / numNeeded);
                }
                actualNumProduced = Math.Min(actualNumProduced, itemInfo.Deficit);

                int buildCost = config.initCost;
                foreach (var material in recipe.materials)
                {
                    int expectedNumUsed = material.Value;
                    int actualNumUsed = material.Value * actualNumProduced;
                    var items = Inventory.TakeItems(material.Key, actualNumUsed);
                    foreach (var item in items)
                        buildCost += item.Cost;

                    expecedConsumedThisRound[material.Key] += expectedNumUsed;
                    actualConsumedThisRound[material.Key] += actualNumUsed;
                }
                Inventory.AddItem(buildItemName, buildCost / outputRate, actualNumProduced * outputRate);
                actualProducedThisRound[buildItemName] += actualNumProduced * outputRate;
            }
        }

        foreach (var com in config.commodities)
        {
            if (com.name == "Food")
                expecedProducedThisRound[com.name] = Mathf.Max(0, expecedProducedThisRound[com.name] - config.foodConsumptionRate);

            TradeStats[com.name].AddProducedRecord(expecedProducedThisRound[com.name]);

            if (com.name != "Food")
                TradeStats[com.name].AddConsumedRecord(expecedConsumedThisRound[com.name]);
        }

        return (actualProducedThisRound, actualConsumedThisRound);
    }
    public Dictionary<string, int> Consume(Dictionary<string, int> consumed)
    {
        int expectedFoodConsumption = config.foodConsumptionRate;
        int consumedThisRound = 0;
        if (Inventory.GetItemInfo("Food").Qty < config.foodConsumptionRate)
        {
            starvationDay++;
            if (starvationDay > config.maxStavationDays && config.maxStavationDays != -1)
                GoBankrupt();
        }
        else
        {
            starvationDay = 0;
            Inventory.TakeItems("Food", config.foodConsumptionRate);
            consumedThisRound = config.foodConsumptionRate;
        }

        if (consumed.ContainsKey("Food"))
            consumed["Food"] += consumedThisRound;
        else
            consumed["Food"] = consumedThisRound;
        TradeStats["Food"].AddConsumedRecord(expectedFoodConsumption);

        return consumed;
    }

    public Dictionary<string, List<Bid>> CreateBids()
    {
        var bids = new Dictionary<string, List<Bid>>();
        int cheapestRecipeCost = int.MaxValue;
        foreach (var com in config.commodities)
        {
            string itemName = com.name;
            if (outputs.Contains(itemName))
            {
                Dictionary<string, int> materialBuyPriceDict = new Dictionary<string, int>();
                for (int i = 0; i < com.recipes.Count; i++)
                {
                    var recipe = com.recipes[i];
                    var bidsByRecipe = new Dictionary<string, List<Bid>>();
                    foreach (var material in recipe.materials)
                    {
                        int buyCount = FindBuyCount(material.Key);
                        int buyPrice = 0;
                        if (materialBuyPriceDict.ContainsKey(material.Key))
                        {
                            buyPrice = materialBuyPriceDict[material.Key];
                        }
                        else
                        {
                            buyPrice = CalculateBuyPrice(material.Key);
                            materialBuyPriceDict[material.Key] = buyPrice;
                        }

                        if (buyCount > 0)
                        {
                            if (!bidsByRecipe.ContainsKey(material.Key))
                                bidsByRecipe.Add(material.Key, new List<Bid>());
                            bidsByRecipe[material.Key].Add(new Bid(material.Key, buyPrice, buyCount, this));
                            buyCount -= material.Value;
                        }
                    }

                    int currentRecipeCost = 0;
                    foreach (var bid in bidsByRecipe)
                        foreach (var item in bid.Value)
                            currentRecipeCost += item.Price * item.Qty;
                    if (currentRecipeCost < cheapestRecipeCost)
                    {
                        bids = bidsByRecipe;
                        cheapestRecipeCost = currentRecipeCost;
                    }
                }
            }
            else if (itemName == "Food")
            {
                int buyCount = FindBuyCount(itemName);
                if (buyCount > 0)
                {
                    int buyPrice = CalculateBuyPrice(itemName);
                    if (!bids.ContainsKey(itemName))
                        bids.Add(itemName, new List<Bid>());
                    bids[itemName].Add(new Bid(itemName, buyPrice, buyCount, this));
                }
            }
        }

        return bids;
    }
    public Dictionary<string, List<Offer>> CreateOffers()
    {
        //sell everything not needed by output
        var offers = new Dictionary<string, List<Offer>>();

        foreach (var itemInfo in Inventory.GetItemInfos())
        {
            string itemName = itemInfo.ItemName;
            var com = config.commodities.Find(x => x.name == itemName);
            var inputs = com.FindInputs();
            if (inputs.Contains(itemName) || !outputs.Contains(itemName))
            {
                continue;
            }
            int sellQuantity = FindSellCount(itemName);
            if (sellQuantity == 0)
                continue;

            var sellItems = new List<Item> ();
            for (int i = 0; i < itemInfo.Items.Count; i++)
            {
                sellItems.Add(itemInfo.Items[i]);
            }
            TradeStats tradeStats = TradeStats[itemName];
            //var sellItemsGroupByCost = sellItems
            //.GroupBy(x => x.Cost)
            //.Select(x => new { Cost = x.Key, Count = x.Count(), Items = x.ToList() }).ToList();
            //foreach (var group in sellItemsGroupByCost)
            //{
            //    int sellPrice = CalculateSellPrice(tradeStats, group.Cost);
            //    offers.Add(new Offer(itemName, sellPrice, group.Count, this, group.Cost, group.Items));
            //}

            int sumSellCost = 0;
            foreach (var item in sellItems)
            {
                sumSellCost += item.Cost;
            }
            int avgCost = sumSellCost / sellItems.Count;
            int sellPrice = CalculateSellPrice(tradeStats, avgCost);

            if (!offers.ContainsKey(itemName))
                offers.Add(itemName, new List<Offer>());
            offers[itemName].Add(new Offer(itemName, sellPrice, sellQuantity, this, avgCost, sellItems));
        }

        return offers;
    }

    int FindBuyCount(string c)
    {
        var itemInfo = Inventory.GetItemInfo(c);
        int numBids = itemInfo.Deficit;
        //if (config.enablePriceFavorability)
        //{
        //    var avgPrice = TradeStats[c].GetAvgBuyPrice(config.historySize);
        //    var lowestPrice = TradeStats[c].GetLowestBuyPrice();
        //    var highestPrice = TradeStats[c].GetHighestBuyPrice();
        //    //todo SANITY check
        //    float favorability = .5f;
        //    if (lowestPrice != highestPrice)
        //    {
        //        favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
        //        favorability = Mathf.Clamp(favorability, 0, 1);
        //        //float favorability = FindTradeFavorability(c);
        //        numBids = Mathf.Max(0, (int)((1f - favorability) * numBids));
        //    }
        //}
        //if (c == "Food" && config.foodConsumptionRate > 0 && itemInfo.Qty == 0)
        //{
        //    float buyProb = Mathf.Lerp(0, config.maxStavationDays, starvationDay);
        //    float rand = UnityEngine.Random.value;

        //    if (rand < buyProb)
        //        numBids = Mathf.Max(numBids, 1);
        //}

        if (c == "Food" && itemInfo.Qty > 0)
            numBids = 0;

        return numBids;
    }
    int FindSellCount(string c)
    {
        int qty = Inventory.GetItemInfo(c).Qty;
        if (qty < 1)
        {
            return 0;
        }

        int numOffers = qty;
        //if (config.enablePriceFavorability)
        //{
        //    var avgPrice = TradeStats[c].GetAvgBuyPrice(config.historySize);
        //    var lowestPrice = TradeStats[c].GetLowestSoldPrice();
        //    var highestPrice = TradeStats[c].GetHighestSoldPrice();
        //    float favorability = 1f;
        //    if (lowestPrice != highestPrice)
        //    {
        //        favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
        //        favorability = Mathf.Clamp(favorability, 0, 1);
        //        //sell at least 1
        //        numOffers = Mathf.Max(1, (int)(favorability * qty));
        //    }
        //}
        //leave some to eat if food
        //if (c == "Food" && config.foodConsumptionRate > 0)
        //{
        //    float rand = UnityEngine.Random.value;
        //    float starvationProb = Mathf.InverseLerp(0, config.maxStavationDays, starvationDay);
        //    if (rand < starvationProb)
        //        numOffers = Mathf.Max(0, numOffers - config.foodConsumptionRate);
        //}

        //numOffers = Mathf.Min(numOffers, 2);
        if (c == "Food" && config.foodConsumptionRate > 0)
        {
            numOffers = Mathf.Max(0, numOffers - config.foodConsumptionRate);
        }
        return numOffers;
    }

    private int CalculateBuyPrice(string itemName)
    {
        //int buyPrice = UnityEngine.Random.Range(TradeStats[itemName].minPriceBelief, TradeStats[itemName].maxPriceBelief);
        //int buyPrice = Mathf.Min(TradeStats[itemName.minPriceBelief, TradeStats[itemName].maxPriceBelief);
        int buyPrice = TradeStats[itemName].priceBelief;

        return buyPrice;
    }
    private int CalculateSellPrice(TradeStats tradeStats, int itemCost)
    {
        //int sellPrice = int.MaxValue;
        //int lastSoldRound = tradeStats.GetLastSoldDay();
        //int lastSoldPrice = tradeStats.GetLatestSoldPrice();
        //int lastSellAttemptPrice = tradeStats.lastOfferAttempPrice;
        int diff = Mathf.Max(0, itemCost - tradeStats.minPriceBelief);
        if (diff > 0)
        {
            tradeStats.maxPriceBelief += diff;
            tradeStats.minPriceBelief = itemCost;
        }

        //int sellPrice = UnityEngine.Random.Range(tradeStats.minPriceBelief, tradeStats.maxPriceBelief);
        //int sellPrice = Mathf.Max(tradeStats.minPriceBelief, tradeStats.maxPriceBelief);
        int sellPrice = tradeStats.priceBelief;

        //if (AuctionHouse.Instance.round == 0)
        //{
        //    //sellPrice = Mathf.RoundToInt(lastSoldPrice * config.profitMarkup);
        //}
        //else
        //{
        //    sellPrice = Mathf.RoundToInt(lastSellAttemptPrice / config.lossMarkup);
        //}
        //sellPrice = Mathf.Max(sellPrice, item.Cost / itemInfo.ProductionRate);

        return sellPrice;
    }

    private void GoBankrupt()
    {
        IsBankrupt = true;
        gameObject.SetActive(false);
    }
}
