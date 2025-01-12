using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using AYellowpaper.SerializedCollections;
using UnityEditor.Build.Reporting;
using static UnityEditor.Progress;
using UnityEngine.Windows;
using NUnit.Framework;

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
    Dictionary<string, int> perItemCost = new Dictionary<string, int>(); //commodities stockpiled
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
            int guessCost = config.initCost;
            if (outputs.Contains(com.name))
            {
                foreach (var recipe in com.recipes)
                {
                    int costFromDep = 0;
                    foreach (var material in recipe.materials)
                    {
                        costFromDep += config.initCost * material.Value;
                    }
                    guessCost += costFromDep / recipe.productionRate;
                }
            }

            Inventory.AddItem(com.name, guessCost, 0);
            TradeStats.Add(com.name, new TradeStats(com.name, guessCost));
        }
    }

    public (Dictionary<string, int>, Dictionary<string, int>) Produce()
    {
        Dictionary<string, int> producedThisRound = new Dictionary<string, int>();
        Dictionary<string, int> consumedThisRound = new Dictionary<string, int>();

        foreach (var buildItemName in outputs)
        {
            var itemInfo = Inventory.GetItemInfo(buildItemName);
            var com = config.commodities.Find(x => x.name == buildItemName);
            foreach (var recipe in com.recipes)
            {
                int prodRate = recipe.productionRate;
                int numProduced = prodRate;
                foreach (var material in recipe.materials)
                {
                    var numNeeded = material.Value;
                    var materialInfo = Inventory.GetItemInfo(material.Key);
                    var numAvail = materialInfo.Quantity;
                    numProduced = Math.Min(numProduced, numAvail / numNeeded);
                }
                numProduced = Math.Min(numProduced, itemInfo.Deficit / prodRate);

                if (numProduced == 0)
                    continue;

                int buildCost = config.initCost;
                foreach (var material in recipe.materials)
                {
                    int numUsed = material.Value * numProduced;
                    var items = Inventory.TakeItems(material.Key, numUsed);
                    buildCost += items.Sum(x => x.Cost);

                    if (!consumedThisRound.ContainsKey(material.Key))
                        consumedThisRound[material.Key] = 0;
                    consumedThisRound[material.Key] += numUsed;
                }
                Inventory.AddItem(buildItemName, buildCost / prodRate, numProduced * prodRate);
                if (!producedThisRound.ContainsKey(buildItemName))
                    producedThisRound[buildItemName] = 0;
                producedThisRound[buildItemName] += numProduced * prodRate;
            }
        }

        return (producedThisRound, consumedThisRound);
    }
    public Dictionary<string, int> Consume(Dictionary<string, int> consumed)
    {
        if (Inventory.GetItemInfo("Food").Quantity < config.foodConsumptionRate)
        {
            starvationDay++;
            if (starvationDay > config.maxStavationDays && config.maxStavationDays != -1)
                GoBankrupt();
        }
        else
        {
            starvationDay = 0;
            Inventory.TakeItems("Food", config.foodConsumptionRate);

            if (consumed.ContainsKey("Food"))
                consumed["Food"] += config.foodConsumptionRate;
            else
                consumed["Food"] = config.foodConsumptionRate;
        }

        return consumed;
    }

    public int Buy(string itemName, int quantity, int price, int round)
    {
        Inventory.AddItem(itemName, price, quantity);
        TradeStats[itemName].AddRecord(TransactionType.Buy, price, quantity, round);
        cash -= price * quantity;
        Assert.IsTrue(cash >= 0, $"{round} {itemName}");
        return quantity;
    }
    public void Sell(string itemName, int quantity, int price, int round)
    {
        Inventory.TakeItems(itemName, quantity);
        TradeStats[itemName].AddRecord(TransactionType.Sell, price, quantity, round);
        cash += price * quantity;
    }

    public List<Bid> CreateBids()
    {
        var bids = new List<Bid>();
        int remainBidCash = cash;
        foreach (var com in config.commodities)
        {
            if (remainBidCash <= 0)
                break;

            string itemName = com.name;
            if (itemName == "Food" && !outputs.Contains(itemName))
            {
                int buyCount = FindBuyCount(itemName);
                int buyPrice = CalculateBuyPrice(itemName);

                if (buyPrice * buyCount > remainBidCash)
                    buyCount = Mathf.FloorToInt(remainBidCash / buyPrice);

                if (buyCount > 0)
                {
                    remainBidCash -= buyPrice * buyCount;
                    bids.Add(new Bid(itemName, buyPrice, buyCount, this));
                }
            }
            else
            {
                List<List<Bid>> bidsByRecipes = new List<List<Bid>>();
                Dictionary<string, int> materialBuyPriceDict = new Dictionary<string, int>();
                for (int i = 0; i < com.recipes.Count; i++)
                {
                    int remainBidCashForRecipe = remainBidCash;
                    var recipe = com.recipes[i];
                    bidsByRecipes.Add(new List<Bid>());
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

                        if (buyPrice * buyCount > remainBidCashForRecipe)
                            buyCount = Mathf.FloorToInt(remainBidCashForRecipe / buyPrice);

                        if (buyCount > 0)
                        {
                            remainBidCashForRecipe -= buyPrice * buyCount;
                            bidsByRecipes[i].Add(new Bid(material.Key, buyPrice, buyCount, this));
                        }
                    }
                }
                if (bidsByRecipes.Count > 0)
                {
                    var cheapestRecipe = bidsByRecipes.OrderBy(x => x.Sum(y => y.Price * y.Quantity)).First();
                    if (cheapestRecipe.Count > 0)
                    {
                        bids.AddRange(cheapestRecipe);
                        remainBidCash -= cheapestRecipe.Sum(x => x.Price * x.Quantity);
                    }
                }

                //int buyCount = FindBuyCount(itemName);
                //int buyPrice = CalculateBuyPrice(itemName);
                //while (buyPrice * buyCount > remainBidCash)
                //{
                //    buyCount--;
                //}

                //if (buyCount > 0)
                //{
                //    remainBidCash -= buyPrice * buyCount;
                //    bids.Add(new Bid(itemName, buyPrice, buyCount, this));
                //}
            }
        }

        return bids;
    }
    public List<Offer> CreateOffers()
    {
        //sell everything not needed by output
        var offers = new List<Offer>();

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

            var sellItems = itemInfo.Items.Take(sellQuantity).ToList();
            var sellItemsGroupByCost = sellItems.GroupBy(x => x.Cost).Select(x => new { Cost = x.Key, Count = x.Count() }).ToList();

            foreach (var group in sellItemsGroupByCost)
            {
                TradeStats tradeStats = TradeStats[itemName];
                int sellPrice = CalculateSellPrice(tradeStats, group.Cost);
                offers.Add(new Offer(itemName, sellPrice, group.Count, this, group.Cost));
            }
        }
        return offers;
    }

    int FindBuyCount(string c)
    {
        var itemInfo = Inventory.GetItemInfo(c);
        int numBids = itemInfo.Deficit;
        if (config.enablePriceFavorability)
        {
            var avgPrice = TradeStats[c].GetAvgBuyPrice(config.historySize);
            var lowestPrice = TradeStats[c].GetLowestBuyPrice();
            var highestPrice = TradeStats[c].GetHighestBuyPrice();
            //todo SANITY check
            float favorability = .5f;
            if (lowestPrice != highestPrice)
            {
                favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
                favorability = Mathf.Clamp(favorability, 0, 1);
                //float favorability = FindTradeFavorability(c);
                numBids = Mathf.Max(0, (int)((1f - favorability) * numBids));
            }
            if (c == "Food" && config.foodConsumptionRate > 0 && itemInfo.Quantity == 0)
            {
                float buyProb = Mathf.Lerp(0, config.maxStavationDays, starvationDay);
                float rand = UnityEngine.Random.value;

                if (rand < buyProb)
                    numBids = Mathf.Max(numBids, 1);
            }
        }

        //numBids = Mathf.Min(numBids, 1);
        return numBids;
    }
    int FindSellCount(string c)
    {
        int quantity = Inventory.GetItemInfo(c).Quantity;
        if (quantity < 1)
        {
            return 0;
        }

        int numOffers = quantity;
        if (config.enablePriceFavorability)
        {
            var avgPrice = TradeStats[c].GetAvgBuyPrice(config.historySize);
            var lowestPrice = TradeStats[c].GetLowestSoldPrice();
            var highestPrice = TradeStats[c].GetHighestSoldPrice();
            float favorability = 1f;
            if (lowestPrice != highestPrice)
            {
                favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
                favorability = Mathf.Clamp(favorability, 0, 1);
                //sell at least 1
                numOffers = Mathf.Max(1, (int)(favorability * quantity));
            }
        }
        //leave some to eat if food
        if (c == "Food" && config.foodConsumptionRate > 0)
        {
            //float rand = UnityEngine.Random.value;
            //float starvationProb = Mathf.InverseLerp(0, config.maxStavationDays, starvationDay);
            //if (rand < starvationProb)
            //    numOffers = Mathf.Max(0, numOffers - config.foodConsumptionRate);
            numOffers = Mathf.Max(0, numOffers - config.foodConsumptionRate);
        }
        return numOffers;
    }

    private int CalculateBuyPrice(string itemName)
    {
        int buyPrice = UnityEngine.Random.Range(TradeStats[itemName].minPriceBelief, TradeStats[itemName].maxPriceBelief);

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

        int sellPrice = UnityEngine.Random.Range(tradeStats.minPriceBelief, tradeStats.maxPriceBelief);

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
