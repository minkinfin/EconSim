using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using AYellowpaper.SerializedCollections;
using UnityEditor.Build.Reporting;
using static UnityEditor.Progress;

public class EconAgent : MonoBehaviour
{
    AgentConfig config;
    static int uid_idx = 0;
    public int uid;
    public int cash;
    public int starvationDay;
    int prevCash = 0;
    [SerializedDictionary("Name", "Item")]
    public SerializedDictionary<string, TradeStats> TradeStats;
    public Inventory Inventory;
    Dictionary<string, int> perItemCost = new Dictionary<string, int>(); //commodities stockpiled
    int taxesPaidThisRound = 0;

    public List<string> outputs { get; private set; } //can produce commodities
    HashSet<string> inputs = new HashSet<string>();

    //production has dependencies on commodities->populates stock
    //production rate is limited by assembly lines (queues/event lists)

    //can use profit to reinvest - produce new commodities
    //switching cost to produce new commodities - zero for now

    //from the paper (base implementation)
    // Use this for initialization
    public bool IsBankrupt { get; set; }
    private Dictionary<string, Commodity2> book => config.book;

    void Awake()
    {
        TradeStats = new SerializedDictionary<string, TradeStats>();
    }

    public void Init(AgentConfig cfg, int initCash, List<string> b)
    {
        config = cfg;
        Inventory = new Inventory(book);
        config = cfg;
        uid = uid_idx++;

        //list of commodities self can produce
        //get initial stockpiles
        outputs = b;
        cash = initCash;
        prevCash = cash;
        inputs.Clear();
        foreach (var com in book)
        {
            int guessCost = config.initCost;
            if (outputs.Contains(com.Key))
            {
                int costFromDep = 0;
                foreach (var dep in book[com.Key].recipes)
                {
                    var depInfo = Inventory.GetItemInfo(dep.Key);
                    costFromDep += config.initCost * dep.Value;
                    inputs.Add(dep.Key);
                }
                guessCost = (guessCost + costFromDep) / book[com.Key].productionRate;
            }

            Inventory.AddItem(com.Key, guessCost, 0);
            TradeStats.Add(com.Key, new TradeStats(com.Key, guessCost));
        }

        IsBankrupt = false;
    }
    public float PayTax(float taxRate)
    {
        int idleTax = Mathf.RoundToInt(cash * taxRate);
        cash -= idleTax;
        taxesPaidThisRound = idleTax;
        return idleTax;
    }

    //public float TaxProfit(float taxRate)
    //{
    //    var profit = GetProfit();
    //    if (profit <= 0)
    //        return profit;
    //    var taxAmt = profit * taxRate;
    //    cash -= taxAmt;
    //    return profit - taxAmt;
    //}
    //public float GetProfit()
    //{
    //    UpdateProfitMarkup();
    //    var profit = cash - prevCash;
    //    prevCash = cash;
    //    return profit;
    //}
    //private void UpdateProfitMarkup()
    //{
    //    //    var deficitProfit = 0f;
    //    //    if (cash < prevCash)
    //    //        deficitProfit = Mathf.Clamp((prevCash - cash) / prevCash, 0, config.profitMarkup);
    //    //    profitMarkup = config.profitMarkup + deficitProfit;
    //    var profit = cash - prevCash;
    //    var deficitProfit = 0f;
    //    if (cash < prevCash/* && soldLastRound.Any(x => x.Value)*/)
    //    {
    //        deficitProfit = Mathf.Clamp((prevCash - cash) / prevCash, 0, config.profitMarkup - 1f);
    //        //profitMarkup += ((prevCash - cash) / prevCash);
    //    }
    //    config.profitMarkup = config.profitMarkup + deficitProfit;
    //}
    public float Tick()
    {
        //Debug.Log("agents ticking!");
        float tax = 0;

        if (config.backruptThreshold > 0 && cash < config.backruptThreshold)
        {
            GoBankrupt();
            //Debug.Log(name + " producing " + outputs[0] + " is bankrupt: " + cash.ToString("c2") + " or starving where food=" + inventory["Food"].Quantity);
            tax = -cash;// + ChangeProfession(); //existing debt + 
        }
        //foreach (var buildable in outputs)
        //{
        //    Inventory[buildable].cost = GetCostOf(buildable);
        //}
        return tax;
    }

    public (Dictionary<string, int>, Dictionary<string, int>) Produce(Dictionary<string, Commodity2> com)
    {
        Dictionary<string, int> producedThisRound = new Dictionary<string, int>();
        Dictionary<string, int> consumedThisRound = new Dictionary<string, int>();

        foreach (var buildItemName in outputs)
        {
            var itemInfo = Inventory.GetItemInfo(buildItemName);
            int prodRate = com[buildItemName].productionRate;
            int numProduced = prodRate;
            foreach (var dep in com[buildItemName].recipes)
            {
                var numNeeded = dep.Value;

                var depInfo = Inventory.GetItemInfo(dep.Key);
                var numAvail = depInfo.Quantity;
                numProduced = Math.Min(numProduced, numAvail / numNeeded);
            }

            numProduced = Math.Min(numProduced, itemInfo.Deficit / prodRate);
            if (numProduced > 0)
            {
                int buildCost = config.initCost;
                foreach (var dep in com[buildItemName].recipes)
                {
                    int numUsed = dep.Value * numProduced;
                    var items = Inventory.TakeItems(dep.Key, numUsed);
                    buildCost += items.Sum(x => x.Cost);

                    if (consumedThisRound.ContainsKey(dep.Key))
                        consumedThisRound[dep.Key] += numUsed;
                    else
                        consumedThisRound[dep.Key] = numUsed;
                }

                Inventory.AddItem(buildItemName, buildCost / prodRate, numProduced * prodRate);
                producedThisRound[buildItemName] = numProduced * prodRate;
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
        return quantity;
    }
    public void Sell(string itemName, int quantity, int price, int round)
    {
        Inventory.TakeItems(itemName, quantity);
        TradeStats[itemName].AddRecord(TransactionType.Sell, price, quantity, round);
        cash += price * quantity;
    }

    //public void UpdateSellerPriceBelief(List<Offer> offers, in Commodity commodity)
    //{
    //    Inventory[commodity.name].UpdateSellerPriceBelief(offers, in commodity);
    //}
    //public void UpdateBuyerPriceBelief(List<Bid> bids, in Commodity commodity)
    //{
    //    Inventory[commodity.name].UpdateBuyerPriceBelief(name, bids, in commodity);
    //}


    /*********** Produce and consume; enter offers and bids to auction house *****/
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
            numOffers = Mathf.Max(0, numOffers - config.foodConsumptionRate);
        }
        return numOffers;
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
                numBids = Mathf.Max(numBids, 1);
            }
        }

        //numBids = Mathf.Min(numBids, 1);
        return numBids;
    }
    public List<Bid> CreateBids(Dictionary<string, Commodity2> com)
    {
        var bids = new List<Bid>();
        int remainBidCash = cash;
        foreach (var stock in com)
        {
            var itemInfo = Inventory.GetItemInfo(stock.Key);
            bool bidAttempt = false;
            string itemName = itemInfo.ItemName;
            if (itemName == "Food")
            {
                if (!outputs.Contains(itemName))
                    bidAttempt = true;
            }
            else if (inputs.Contains(itemName) && itemInfo.Quantity < 2)
            {
                bidAttempt = true;
            }

            if (!bidAttempt)
                continue;

            int buyCount = FindBuyCount(itemInfo.ItemName);
            TradeStats tradeStats = TradeStats[itemName];
            int buyPrice = CalculateBuyPrice(remainBidCash, tradeStats);
            while (buyPrice * buyCount > remainBidCash && buyCount > 0)
            {
                buyCount--;
            }

            if (buyCount > 0 && remainBidCash > 0)
            {
                remainBidCash -= buyPrice * buyCount;

                bids.Add(new Bid(itemName, buyPrice, buyCount, this));
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

    private int CalculateBuyPrice(int remainBidCash, TradeStats tradeStats)
    {
        int buyPrice = UnityEngine.Random.Range(tradeStats.minPriceBelief, tradeStats.maxPriceBelief);

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
