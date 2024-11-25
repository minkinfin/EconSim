using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using UnityEngine.XR;
using System;
using AYellowpaper.SerializedCollections;

public class EconAgent : MonoBehaviour
{
    AgentConfig config;
    static int uid_idx = 0;
    public int uid;
    public float cash;
    public int starvationDay;
    float initCash = 100f;
    int initStock = 1;
    float prevCash = 0;
    int maxStock = 1;
    ESList profits = new ESList();
    [SerializedDictionary("Name", "Item")]
    public SerializedDictionary<string, InventoryItem> Inventory;
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
    Dictionary<string, Commodity> Commodities { get; set; }
    public bool IsBankrupt { get; set; }
    private Dictionary<string, bool> soldLastRound;
    float profitMarkup, lossMarkup;

    void Awake()
    {
        Inventory = new SerializedDictionary<string, InventoryItem>();
        soldLastRound = new Dictionary<string, bool>();
    }

    public void Init(AgentConfig cfg, float _initCash, List<string> b, int _initStock, int maxstock)
    {
        config = cfg;
        uid = uid_idx++;
        initStock = _initStock;
        initCash = _initCash;
        maxStock = maxstock;

        Commodities = AuctionStats.Instance.book;
        //list of commodities self can produce
        //get initial stockpiles
        outputs = b;
        cash = initCash;
        prevCash = cash;
        inputs.Clear();
        foreach (var com in Commodities)
        {
            float priceFromDep = Commodities[com.Key].price;
            foreach (var dep in Commodities[com.Key].dep)
            {
                priceFromDep += dep.Value * Commodities[dep.Key].price;
                if (outputs.Contains(com.Key))
                    inputs.Add(dep.Key);

            }
            priceFromDep = priceFromDep / Math.Max(Commodities[com.Key].ProductionRate - 1, 1);
            AddToInventory(com.Key, 0, maxStock, priceFromDep, Commodities[com.Key].ProductionRate);
            soldLastRound.Add(com.Key, false);
        }
        IsBankrupt = false;
        profitMarkup = config.profitMarkup;
        lossMarkup = config.profitMarkup;
    }
    void AddToInventory(string name, int num, int max, float price, int production)
    {
        if (Inventory.ContainsKey(name))
            return;

        Inventory.Add(name, new InventoryItem(name, num, max, price, production));

        perItemCost[name] = Commodities[name].price * num;
    }
    public float PayTax(float taxRate)
    {
        var idleTax = cash * taxRate;
        cash -= idleTax;
        taxesPaidThisRound = idleTax;
        return idleTax;
    }
    //public void Reinit(float initCash, List<string> b)
    //{
    //    if (Commodities == null)
    //        Commodities = AuctionStats.Instance.book;
    //    //list of commodities self can produce
    //    //get initial stockpiles
    //    outputs = b;
    //    cash = initCash;
    //    prevCash = cash;
    //    inputs.Clear();
    //    foreach (var buildable in outputs)
    //    {

    //        //if (!com.ContainsKey(buildable))
    //        //Debug.Log("commodity not recognized: " + buildable);

    //        //if (com[buildable].dep == null)
    //        //Debug.Log(buildable + ": null dep!");

    //        string msg = "";
    //        foreach (var entry in Inventory)
    //        {
    //            msg += entry.Value.Quantity + " " + entry.Key + ", ";
    //        }
    //        //Debug.Log(AuctionStats.Instance.round + ": " + name + " reinit2: " + msg );
    //        //don't give bankrupt agents more goods! just money and maybe food?

    //        foreach (var dep in Commodities[buildable].dep)
    //        {
    //            var commodity = dep.Key;
    //            inputs.Add(commodity);
    //            //Debug.Log("::" + commodity);
    //            AddToInventory(commodity, 0, maxStock, Commodities[commodity].price, Commodities[commodity].ProductionRate);
    //        }
    //        AddToInventory(buildable, 0, maxStock, Commodities[buildable].price, Commodities[buildable].ProductionRate);

    //        //Debug.Log("New " + gameObject.name + " has " + inventory[buildable].Quantity + " " + buildable);
    //        //Debug.Log(AuctionStats.Instance.round + ": " + name + " post reinit2: " + msg );
    //    }
    //}

    public float TaxProfit(float taxRate)
    {
        var profit = GetProfit();
        if (profit <= 0)
            return profit;
        var taxAmt = profit * taxRate;
        cash -= taxAmt;
        return profit - taxAmt;
    }
    public float GetProfit()
    {
        UpdateProfitMarkup();
        var profit = cash - prevCash;
        prevCash = cash;
        return profit;
    }
    private void UpdateProfitMarkup()
    {
        //    var deficitProfit = 0f;
        //    if (cash < prevCash)
        //        deficitProfit = Mathf.Clamp((prevCash - cash) / prevCash, 0, config.profitMarkup);
        //    profitMarkup = config.profitMarkup + deficitProfit;
        var profit = cash - prevCash;
        var deficitProfit = 0f;
        if (cash < prevCash && soldLastRound.Any(x => x.Value))
        {
            deficitProfit = Mathf.Clamp((prevCash - cash) / prevCash, 0, config.profitMarkup - 1f);
            //profitMarkup += ((prevCash - cash) / prevCash);
        }
        profitMarkup = config.profitMarkup + deficitProfit;
    }
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

    public (Dictionary<string, float>, Dictionary<string, float>) Produce(Dictionary<string, Commodity> com)
    {
        //TODO sort buildables by profit
        Dictionary<string, float> producedThisRound = new Dictionary<string, float>();
        Dictionary<string, float> consumedThisRound = new Dictionary<string, float>();
        //build as many as one can TODO don't build things that won't earn a profit
        foreach (var buildable in outputs)
        {
            //Debug.Log(AuctionStats.Instance.round + " " + name + " producing " + buildable + " currently in stock " + inventory[buildable].Quantity);
            //get list of dependent commodities
            int numProduced = int.MaxValue; //amt agent can produce for commodity buildable
                                            //find max that can be made w/ available stock
                                            //Assert.IsTrue(com.ContainsKey(buildable));
            foreach (var dep in com[buildable].dep)
            {
                var numNeeded = dep.Value;
                //inventory.Add(dep.Key, new InventoryItem(dep.Key, 0, maxStock, com[dep.Key].price, com[dep.Key].production));

                var numAvail = Inventory[dep.Key].Quantity;
                numProduced = Math.Min(numProduced, (numAvail / numNeeded) * com[buildable].ProductionRate);
                //Debug.Log(AuctionStats.Instance.round + " " + name + "can produce " + numProduced + " w/" + numAvail + "/" + numNeeded + dep.Key );
            }
            //can only build fixed rate at a time
            //can't produce more than what's in stock
            int upperBound = Math.Min(Inventory[buildable].productionRate, Inventory[buildable].Deficit());
            numProduced = Math.Clamp(numProduced, 0, upperBound);
            //Debug.Log(AuctionStats.Instance.round + " " + name + " upperbound: " + upperBound + " production rate: " + inventory[buildable].productionRate + " room: " + inventory[buildable].Deficit());
            //Debug.Log(AuctionStats.Instance.round + " " + name + " upperbound: " + upperBound + " producing: " + numProduced);
            //Assert.IsTrue(numProduced >= 0);

            //build and add to stockpile
            if (numProduced > 0)
            {
                var buildable_cost = 0f;
                foreach (var dep in com[buildable].dep)
                {
                    var stock = Inventory[dep.Key].Quantity;
                    int numUsed = dep.Value;
                    numUsed = Mathf.Clamp(numUsed, 0, stock);
                    buildable_cost += numUsed * Inventory[dep.Key].price;
                    Inventory[dep.Key].Decrease(numUsed);

                    if (consumedThisRound.ContainsKey(dep.Key))
                        consumedThisRound[dep.Key] += numUsed;
                    else
                        consumedThisRound[dep.Key] = numUsed;
                }
                Inventory[buildable].Increase(numProduced);
                //Debug.Log(AuctionStats.Instance.round + " " + name + " has " + cash.ToString("c2") + " made " + numProduced.ToString("n2") + " " + buildable + " total: " + inventory[buildable].Quantity);
                producedThisRound[buildable] = numProduced;
            }
            //Assert.IsFalse(float.IsNaN(numProduced));
            //this condition is worrisome 
            //			//Assert.IsTrue(inventory[buildable].Quantity >= 0);

            //create offer outside
        }

        return (producedThisRound, consumedThisRound);
    }
    public Dictionary<string, float> Consume()
    {
        var consumed = new Dictionary<string, float>();

        if (config.foodConsumptionRate > 0 && Inventory.ContainsKey("Food"))
        {
            if (Inventory["Food"].Quantity < config.foodConsumptionRate)
            {
                starvationDay++;
                if (starvationDay > config.maxStavationDays && config.maxStavationDays > 0)
                    GoBankrupt();
            }
            else
            {
                starvationDay = 0;
                Inventory["Food"].Decrease(config.foodConsumptionRate);

                if (consumed.ContainsKey("Food"))
                    consumed["Food"] += config.foodConsumptionRate;
                else
                    consumed["Food"] = config.foodConsumptionRate;
            }
        }

        return consumed;
    }

    float ChangeProfession()
    {
        string bestGood = AuctionStats.Instance.GetHottestGood();
        string bestProf = AuctionStats.Instance.GetMostProfitableProfession(outputs[0]);

        string mostDemand = bestProf;
        //Debug.Log("bestGood: " + bestGood + " bestProfession: " + bestProf);
        Assert.AreEqual(outputs.Count, 1);
        //Debug.Log(name + " changing from " + outputs[0] + " to " + mostDemand);

        if (bestGood != "invalid")
        {
            mostDemand = bestGood;
            outputs[0] = mostDemand;
        }

        Inventory.Clear();
        List<string> b = new List<string>() { mostDemand };
        //Reinit(initCash, b);
        return initCash;
    }

    /*********** Trading ************/
    public void modify_cash(float quant)
    {
        cash += quant;
    }
    public void ClearRoundStats()
    {
        foreach (var item in Inventory)
        {
            item.Value.ClearRoundStats();
        }
        taxesPaidThisRound = 0;
    }
    public int Buy(string commodity, int quantity, float price)
    {
        //Assert.IsFalse(outputs.Contains(commodity)); //agents shouldn't buy what they produce
        int boughtQuantity = Inventory[commodity].Buy(quantity, price);
        //Debug.Log(name + " has " + cash.ToString("c2") + " want to buy " + quantity.ToString("n2") + " " + commodity + " for " + price.ToString("c2") + " bought " + boughtQuantity.ToString("n2"));
        cash -= price * boughtQuantity;
        return boughtQuantity;
    }
    public void Sell(string commodity, int quantity, float price)
    {
        Inventory[commodity].Sell(quantity, price);
        cash += price * quantity;
        soldLastRound[commodity] = true;
    }
    public void UpdateSellerPriceBelief(in Offer offer, in Commodity commodity)
    {
        Inventory[commodity.name].UpdateSellerPriceBelief(name, in offer, in commodity);
    }
    public void UpdateBuyerPriceBelief(in Bid bid, in Commodity commodity)
    {
        Inventory[commodity.name].UpdateBuyerPriceBelief(name, in bid, in commodity);
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
        if (Inventory[c].Surplus() < 1)
        {
            return 0;
        }

        int numOffers = Inventory[c].Surplus();
        if (config.enablePriceFavorability)
        {
            var avgPrice = Commodities[c].avgBidPrice.LastAverage(config.historySize);
            var lowestPrice = Inventory[c].sellHistory.Min();
            var highestPrice = Inventory[c].sellHistory.Max();
            float favorability = 1f;
            if (lowestPrice != highestPrice)
            {
                favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
                favorability = Mathf.Clamp(favorability, 0, 1);
                //sell at least 1
                numOffers = Mathf.Max(1, (int)(favorability * Inventory[c].Surplus()));
            }
            //leave some to eat if food
            if (c == "Food" && config.foodConsumptionRate > 0)
            {
                numOffers = Mathf.Max(numOffers, Inventory[c].Surplus() - 1);
            }

            //Debug.Log(AuctionStats.Instance.round + " " + name + " FindSellCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + favorability.ToString("n2") + " numAsks: " + numAsks.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
            //Assert.IsTrue(numAsks <= inventory[c].Quantity);
        }
        return numOffers;
    }
    int FindBuyCount(string c)
    {
        int numBids = Inventory[c].Deficit();
        if (config.enablePriceFavorability)
        {
            var avgPrice = Commodities[c].avgBidPrice.LastAverage(config.historySize);
            var lowestPrice = Inventory[c].buyHistory.Min();
            var highestPrice = Inventory[c].buyHistory.Max();
            //todo SANITY check
            float favorability = .5f;
            if (lowestPrice != highestPrice)
            {
                favorability = Mathf.InverseLerp(lowestPrice, highestPrice, avgPrice);
                favorability = Mathf.Clamp(favorability, 0, 1);
                //float favorability = FindTradeFavorability(c);
                numBids = Mathf.Max(0, (int)((1f - favorability) * Inventory[c].Deficit()));
            }
            if (c == "Food" && config.foodConsumptionRate > 0 && Inventory[c].Quantity == 0)
            {
                numBids = Mathf.Max(numBids, 1);
            }

            //Debug.Log(AuctionStats.Instance.round + " " + name + " FindBuyCount " + c + ": avgPrice: " + avgPrice.ToString("c2") + " favorability: " + (1 - favorability).ToString("n2") + " numBids: " + numBids.ToString("n2") + " highestPrice: " + highestPrice.ToString("c2") + ", lowestPrice: " + lowestPrice.ToString("c2"));
            //Assert.IsTrue(numBids <= inventory[c].Deficit());
        }
        return numBids;
    }
    public List<Bid> CreateBids(Dictionary<string, Commodity> com)
    {
        var bids = new List<Bid>();
        //replenish depended commodities
        foreach (var stock in Inventory)
        {
            bool bidAttempt = false;
            if (stock.Key == "Food")
            {
                if (!outputs.Contains(stock.Key))
                    bidAttempt = true;
            }
            else if (inputs.Contains(stock.Key) && Inventory[stock.Key].Quantity < 2)
            {
                bidAttempt = true;
            }

            if (!bidAttempt)
                continue;

            int numBids = FindBuyCount(stock.Key);
            numBids = Mathf.Clamp(numBids, 0, 1);
            if (numBids > 0 && cash > 0)
            {
                //maybe buy less if expensive?
                float buyPrice = stock.Value.GetPrice();
                if (config.onlyBuyWhatsAffordable)
                {
                    buyPrice = Mathf.Min(cash / numBids, buyPrice);
                }
                //Debug.Log(AuctionStats.Instance.round + ": " + this.name + " wants to buy " + numBids.ToString("n2") + stock.Key + " for " + buyPrice.ToString("c2") + " each" + " min/maxPriceBeliefs " + stock.Value.minPriceBelief.ToString("c2") + "/" + stock.Value.maxPriceBelief.ToString("c2"));
                //Assert.IsFalse(numBids < 0);
                bids.Add(new Bid(stock.Value.commodityName, buyPrice, numBids, this));
            }
        }

        return bids;
    }

    public List<Offer> CreateOffers()
    {
        //sell everything not needed by output
        var offers = new List<Offer>();

        foreach (var item in Inventory)
        {
            var commodityName = item.Key;
            if (inputs.Contains(commodityName) || !outputs.Contains(commodityName))
            {
                continue;
            }
            var sellStock = Inventory[commodityName];
            int sellQuantity = FindSellCount(commodityName);
            float sellPrice = GetCostOf(commodityName);
            //float sellPrice = item.Value.GetPrice();
            if (soldLastRound[commodityName])
            {
                sellPrice *= profitMarkup;
                soldLastRound[commodityName] = false;
            }
            else
            {
                sellPrice = Mathf.Max(sellPrice / lossMarkup, Inventory[commodityName].price);
            }

            if (sellQuantity > 0 && sellPrice > 0)
            {
                //Debug.Log(AuctionStats.Instance.round + ": " + name + " wants to sell " + sellQuantity + " " + commodityName + " for " + sellPrice.ToString("c2") + ", has in stock" + inventory[commodityName].Surplus());
                //Assert.IsTrue(sellQuantity <= inventory[commodityName].Quantity);
                offers.Add(new Offer(commodityName, sellPrice, sellQuantity, this));
            }
        }
        return offers;
    }
    //get the cost of a commodity
    float GetCostOf(string commodity)
    {
        var com = AuctionStats.Instance.book;
        float cost = 1;
        foreach (var dep in com[commodity].dep)
        {
            var depCommodity = dep.Key;
            var numDep = dep.Value;
            var depCost = Inventory[depCommodity].price;
            cost += numDep * depCost;
        }
        cost = cost / Math.Max(com[commodity].ProductionRate - 1, 1);

        return cost;
    }

    private void GoBankrupt()
    {
        IsBankrupt = true;
        gameObject.SetActive(false);
    }
}
