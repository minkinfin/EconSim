using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using AYellowpaper.SerializedCollections;
using UnityEditor.Build.Reporting;

public class EconAgent : MonoBehaviour
{
    AgentConfig config;
    static int uid_idx = 0;
    public int uid;
    public float cash;
    public int starvationDay;
    float initCash = 100f;
    float prevCash = 0;
    [SerializedDictionary("Name", "Item")]
    public SerializedDictionary<string, TradeStats> TradeStats;
    public Inventory Inventory;
    Dictionary<string, float> perItemCost = new Dictionary<string, float>(); //commodities stockpiled
    float taxesPaidThisRound = 0;

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

    public void Init(AgentConfig cfg, float _initCash, List<string> b)
    {
        config = cfg;
        Inventory = new Inventory(book);
        config = cfg;
        uid = uid_idx++;
        initCash = _initCash;

        //list of commodities self can produce
        //get initial stockpiles
        outputs = b;
        cash = initCash;
        prevCash = cash;
        inputs.Clear();
        foreach (var com in book)
        {
            float costFromDep = 1;
            foreach (var dep in book[com.Key].recipes)
            {
                costFromDep += dep.Value;
                if (outputs.Contains(com.Key))
                    inputs.Add(dep.Key);
            }
            costFromDep = costFromDep / Math.Max(book[com.Key].productionRate - 1, 1);
            Inventory.AddItem(com.Key, costFromDep, 0);

            TradeStats.Add(com.Key, new TradeStats(com.Key, costFromDep));
        }
        IsBankrupt = false;
    }
    public float PayTax(float taxRate)
    {
        var idleTax = cash * taxRate;
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
            int numProduced = 100;
            foreach (var dep in com[buildItemName].recipes)
            {
                var numNeeded = dep.Value;

                var depInfo = Inventory.GetItemInfo(dep.Key);
                var numAvail = depInfo.Quantity;
                numProduced = Math.Min(numProduced, (numAvail / numNeeded) * prodRate);
            }

            int upperBound = Math.Min(prodRate, itemInfo.Deficit);
            numProduced = Math.Clamp(numProduced, 0, upperBound);
            if (numProduced > 0)
            {
                float buildCost = 1;
                foreach (var dep in com[buildItemName].recipes)
                {
                    int numUsed = dep.Value * (numProduced / prodRate);
                    var items = Inventory.TakeItems(dep.Key, numUsed);
                    buildCost += items.Sum(x => x.Cost);

                    if (consumedThisRound.ContainsKey(dep.Key))
                        consumedThisRound[dep.Key] += numUsed;
                    else
                        consumedThisRound[dep.Key] = numUsed;
                }

                Inventory.AddItem(buildItemName, buildCost, numProduced);
                producedThisRound[buildItemName] = numProduced;
            }
        }

        return (producedThisRound, consumedThisRound);
    }
    public Dictionary<string, int> Consume()
    {
        var consumed = new Dictionary<string, int>();

        if (Inventory.GetItemInfo("Food").Quantity < config.foodConsumptionRate)
        {
            starvationDay++;
            if (starvationDay > config.maxStavationDays && config.maxStavationDays > 0)
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

    public int Buy(string itemName, int quantity, float price, int round)
    {
        Inventory.AddItem(itemName, price, quantity);
        TradeStats[itemName].AddRecord(TransactionType.Buy, price, quantity, round);
        cash -= price * quantity;
        return quantity;
    }
    public void Sell(string itemName, int quantity, float price, int round)
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
            var lowestPrice = TradeStats[c].GetLowestSellPrice();
            var highestPrice = TradeStats[c].GetHighestSellPrice();
            float favorability = 1f;
            if (lowestPrice != highestPrice)
            {
                favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
                favorability = Mathf.Clamp(favorability, 0, 1);
                //sell at least 1
                numOffers = Mathf.Max(1, (int)(favorability * quantity));
            }
            //leave some to eat if food
            if (c == "Food" && config.foodConsumptionRate > 0)
            {
                numOffers = Mathf.Max(numOffers, quantity - 1);
            }

            //Debug.Log(AuctionStats.Instance.round + " " + name + " FindSellCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + favorability.ToString("n2") + " numAsks: " + numAsks.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
            //Assert.IsTrue(numAsks <= inventory[c].Quantity);
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

        numBids = Mathf.Min(numBids, 1);
        return numBids;
    }
    public List<Bid> CreateBids(Dictionary<string, Commodity2> com)
    {
        var bids = new List<Bid>();
        float bidCash = cash;
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
            if (buyCount > 0 && bidCash > 0)
            {
                //maybe buy less if expensive
                int lastBoughtRound = TradeStats[itemName].GetLastBoughtDay();
                float lastBoughtPrice = TradeStats[itemName].GetLatestBuyPrice();
                float lastBuyAttemptPrice = TradeStats[itemName].lastBidAttempPrice;

                float buyPrice = float.MaxValue;
                if (lastBoughtRound == AuctionHouse.Instance.round - 1)
                {
                    buyPrice = lastBoughtPrice / config.profitMarkup;
                }
                else
                {
                    buyPrice = lastBuyAttemptPrice * config.lossMarkup;
                }

                if (buyPrice * buyCount > bidCash)
                    buyPrice = bidCash / buyCount;

                bidCash -= buyPrice * buyCount;

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

            foreach (var item in sellItems)
            {
                int lastSoldRound = TradeStats[itemName].GetLastSoldDay();
                float lastSoldPrice = TradeStats[itemName].GetLatestSellPrice();
                float lastSellAttemptPrice = TradeStats[itemName].lastOfferAttempPrice;

                float sellPrice = float.MaxValue;
                if (lastSoldRound == -1 || lastSoldRound == AuctionHouse.Instance.round - 1)
                {
                    sellPrice = lastSoldPrice * config.profitMarkup;
                }
                else
                {
                    sellPrice = lastSellAttemptPrice / config.lossMarkup;
                }

                sellPrice = Mathf.Max(sellPrice, item.Cost / itemInfo.ProductionRate);

                offers.Add(new Offer(itemName, sellPrice, sellQuantity, this));
            }
        }
        return offers;
    }

    private void GoBankrupt()
    {
        IsBankrupt = true;
        gameObject.SetActive(false);
    }
}
