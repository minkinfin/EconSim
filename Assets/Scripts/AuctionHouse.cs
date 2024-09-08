using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using static UnityEngine.EventSystems.EventTrigger;

public class AuctionHouse : MonoBehaviour
{
    protected AgentConfig config;
    public int seed = 42;
    public bool appendTimeToLog = false;
    public float tickInterval = .001f;
    public int maxRounds = 10;
    public bool exitAfterNoTrade = true;
    public int numRoundsNoTrade = 100;

    [SerializedDictionary("Comm", "numAgents")]
    public SerializedDictionary<string, int> numAgents = new()
    {
        { "Food", 3 },
        { "Wood", 3 },
        { "Ore", 3 },
        { "Metal", 4 },
        { "Tool", 4 }
    };

    protected List<EconAgent> agents = new List<EconAgent>();
    protected float irs;
    protected bool timeToQuit = false;
    protected OfferTable askTable, bidTable;
    protected StreamWriter sw;
    protected AuctionStats auctionTracker;

    protected Dictionary<string, Dictionary<string, float>> trackBids = new();
    protected float lastTick;
    public bool EnableDebug = false;
    void Start()
    {
        Debug.unityLogger.logEnabled = EnableDebug;
        config = GetComponent<AgentConfig>();
        OpenFileForWrite();

        UnityEngine.Random.InitState(seed);
        lastTick = 0;
        auctionTracker = AuctionStats.Instance;
        var com = auctionTracker.book;

        irs = 0;
        var prefab = Resources.Load("Agent");

        for (int i = transform.childCount; i < numAgents.Values.Sum(); i++)
        {
            GameObject go = Instantiate(prefab) as GameObject;
            go.transform.parent = transform;
            go.name = "agent" + i.ToString();
        }

        int agentIndex = 0;
        var professions = numAgents.Keys;
        var resoucrces = auctionTracker.initialization.Select(x => x.Key).ToArray();
        foreach (string profession in professions)
        {
            if (!resoucrces.Contains(profession))
                continue;

            for (int i = 0; i < numAgents[profession]; ++i)
            {
                GameObject child = transform.GetChild(agentIndex).gameObject;
                var agent = child.GetComponent<EconAgent>();
                InitAgent(agent, profession);
                agents.Add(agent);
                ++agentIndex;
            }
        }
        askTable = new OfferTable();
        bidTable = new OfferTable();

        foreach (var entry in com)
        {
            trackBids.Add(entry.Key, new Dictionary<string, float>());
            foreach (var item in com)
            {
                trackBids[entry.Key].Add(item.Key, 0);
            }
        }
    }
    void InitAgent(EconAgent agent, string type)
    {
        List<string> buildables = new List<string>();
        buildables.Add(type);
        float initStock = config.initStock;
        float initCash = config.initCash;
        if (config.randomInitStock)
        {
            initStock = UnityEngine.Random.Range(config.initStock / 2, config.initStock * 2);
            initStock = Mathf.Floor(initStock);
        }

        var maxStock = Mathf.Max(initStock, config.maxStock);

        agent.Init(config, initCash, buildables, initStock, maxStock);
    }

    void FixedUpdate()
    {
        //        if (auctionTracker.round > maxRounds || timeToQuit)
        //        {
        //            CloseWriteFile();
        //#if UNITY_EDITOR
        //            UnityEditor.EditorApplication.isPlaying = false;
        //#elif UNITY_WEBPLAYER
        //        Application.OpenURL("127.0.0.1");
        //#else
        //        Application.Quit();
        //#endif
        //            return;
        //        }
        //wait before update
        if (Time.time - lastTick > tickInterval)
        {
            //Debug.Log("v1.4 Round: " + auctionTracker.round);
            //sampler.BeginSample("AuctionHouseTick");
            Tick();
            //sampler.EndSample();
            lastTick = Time.time;
            auctionTracker.nextRound();
        }
    }
    void Tick()
    {
        var com = auctionTracker.book;
        foreach (var agent in agents)
        {
            var numProduced = agent.Produce(com);
            float idleTax = 0;
            if (numProduced == 0)
            {
                idleTax = agent.PayTax(config.idleTaxRate);
                irs += idleTax;
            }
            askTable.Add(agent.CreateAsks());
            bidTable.Add(agent.Consume(com));
        }

        foreach (var entry in com)
        {
            ResolveOffers(entry.Value);
        }

        CountProfits();
        CountProfessions();
        CountStockPileAndCash();

        foreach (var agent in agents)
        {
            agent.ClearRoundStats();
        }
        EnactBankruptcy();
        QuitIf();
    }

    protected void PrintAuctionStats(string c, float buy, float sell)
    {
        string header = auctionTracker.round + ", auction, none, " + c + ", ";
        string msg = header + "bid, " + buy + ", n/a\n";
        msg += header + "ask, " + sell + ", n/a\n";
        msg += header + "avgAskPrice, " + auctionTracker.book[c].avgAskPrice[^1] + ", n/a\n";
        msg += header + "avgBidPrice, " + auctionTracker.book[c].avgBidPrice[^1] + ", n/a\n";
        header = auctionTracker.round + ", auction, none, none, ";
        msg += header + "irs, " + irs + ", n/a\n";
        msg += auctionTracker.GetLog();

        PrintToFile(msg);
    }
    protected void ResolveOffers(Commodity commodity)
    {
        var asks = askTable[commodity.name];
        var bids = bidTable[commodity.name];
        var agentDemandRatio = bids.Count / Mathf.Max(.01f, (float)asks.Count);

        var quantityToBuy = bids.Sum(item => item.offerQuantity);
        var quantityToSell = asks.Sum(item => item.offerQuantity);
        commodity.bids.Add(quantityToBuy);
        commodity.asks.Add(quantityToSell);
        commodity.buyers.Add(bids.Count);
        commodity.sellers.Add(asks.Count);
        if (quantityToSell == 0)
        {
            commodity.avgAskPrice.Add(0);
        }
        else
        {
            commodity.avgAskPrice.Add(asks.Sum((x) => x.offerPrice * x.offerQuantity) / quantityToSell);
        }
        if (quantityToBuy == 0)
        {
            commodity.avgBidPrice.Add(0);
        }
        else
        {
            commodity.avgBidPrice.Add(bids.Sum((x) => x.offerPrice * x.offerQuantity) / quantityToBuy);
        }

        asks.Shuffle();
        bids.Shuffle();

        asks.Sort((x, y) => x.offerPrice.CompareTo(y.offerPrice));

        float moneyExchangedThisRound = 0;
        float goodsExchangedThisRound = 0;

        int askIdx = 0;
        int bidIdx = 0;

        while (askIdx < asks.Count && bidIdx < bids.Count)
        {
            var ask = asks[askIdx];
            var bid = bids[bidIdx];

            var clearingPrice = ask.offerPrice;
            var tradeQuantity = Mathf.Min(bid.remainingQuantity, ask.remainingQuantity);


            var boughtQuantity = bid.agent.Buy(commodity.name, tradeQuantity, clearingPrice);
            ask.agent.Sell(commodity.name, boughtQuantity, clearingPrice);

            //track who bought what
            var buyers = trackBids[commodity.name];
            buyers[bid.agent.outputs[0]] += clearingPrice * boughtQuantity;

            moneyExchangedThisRound += clearingPrice * boughtQuantity;
            goodsExchangedThisRound += boughtQuantity;

            //this is necessary for price belief updates after the big loop
            ask.Accepted(clearingPrice, boughtQuantity);
            bid.Accepted(clearingPrice, boughtQuantity);

            //go to next ask/bid if fullfilled
            if (ask.remainingQuantity == 0)
            {
                askIdx++;
            }
            if (bid.remainingQuantity == 0)
            {
                bidIdx++;
            }
        }

        var denom = (goodsExchangedThisRound == 0) ? 1 : goodsExchangedThisRound;
        var averagePrice = moneyExchangedThisRound / denom;
        commodity.trades.Add(goodsExchangedThisRound);
        commodity.avgClearingPrice.Add(averagePrice);
        commodity.Update(averagePrice, agentDemandRatio);

        //var excessDemand = asks.Sum(ask => ask.quantity);
        //var excessSupply = bids.Sum(bid => bid.quantity);
        //var demand = (goodsExchanged + excessDemand) 
        //					 / (goodsExchanged + excessSupply);

        foreach (var ask in asks)
        {
            ask.agent.UpdateSellerPriceBelief(in ask, in commodity);
        }
        asks.Clear();
        foreach (var bid in bids)
        {
            bid.agent.UpdateBuyerPriceBelief(in bid, in commodity);
        }
        bids.Clear();
    }

    protected void Trade(string commodity, float clearingPrice, float quantity, EconAgent bidder, EconAgent seller)
    {
        //transfer commodity
        //transfer cash
        var boughtQuantity = bidder.Buy(commodity, quantity, clearingPrice);
        seller.Sell(commodity, boughtQuantity, clearingPrice);
        var cashQuantity = quantity * clearingPrice;

    }

    protected void OpenFileForWrite()
    {
        if (!config.enableWriteFile)
            return;

        var datepostfix = DateTime.Now.ToString(@"yyyy-MM-dd-h_mm_tt");
        if (appendTimeToLog)
        {
            sw = new StreamWriter("log_" + datepostfix + ".csv");
        }
        else
        {
            sw = new StreamWriter("log.csv");
        }
        string header_row = "round, agent, produces, inventory_items, type, amount, reason\n";
        PrintToFile(header_row);
    }
    protected void PrintToFile(string msg)
    {
        if (!config.enableWriteFile)
            return;
        sw.Write(msg);
    }

    protected void CloseWriteFile()
    {
        if (!config.enableWriteFile)
            return;
        sw.Close();
    }

    protected void AgentsStats()
    {
        string header = auctionTracker.round + ", ";
        string msg = "";
        foreach (var agent in agents)
        {
            msg += agent.Stats(header);
        }
        PrintToFile(msg);
    }
    protected void CountStockPileAndCash()
    {
        Dictionary<string, float> stockPile = new Dictionary<string, float>();
        Dictionary<string, List<float>> stockList = new Dictionary<string, List<float>>();
        Dictionary<string, List<float>> cashList = new Dictionary<string, List<float>>();
        var com = auctionTracker.book;
        float totalCash = 0;
        foreach (var entry in com)
        {
            stockPile.Add(entry.Key, 100);
            stockList.Add(entry.Key, new List<float>());
            cashList.Add(entry.Key, new List<float>());
        }
        foreach (var agent in agents)
        {
            //count stocks in all stocks of agent
            foreach (var c in agent.inventory)
            {
                stockPile[c.Key] += c.Value.Surplus();
                var surplus = c.Value.Surplus();
                if (surplus > 20)
                {
                    //Debug.Log(agent.name + " has " + surplus + " " + c.Key);
                }
                stockList[c.Key].Add(surplus);
            }
            cashList[agent.outputs[0]].Add(agent.cash);
            totalCash += agent.cash;
        }
        foreach (var stock in stockPile)
        {
            int bucket = 1, index = 0;
            var avg = GetQuantile(stockList[stock.Key], bucket, index);
            auctionTracker.book[stock.Key].stocks.Add(avg);
            auctionTracker.book[stock.Key].capitals.Add(cashList[stock.Key].Sum());
        }
    }
    protected float GetQuantile(List<float> list, int buckets = 4, int index = 0) //default lowest quartile
    {
        float avg = 0;
        if (buckets == 1)
        {
            if (list.Count > 0)
                return list.Average();
            else
                return 0;
        }

        var numPerQuantile = list.Count / buckets;
        var numQuantiles = buckets;
        var begin = Mathf.Max(0, index * numPerQuantile);
        var end = Mathf.Min(list.Count - 1, begin + numPerQuantile);
        if (list.Count != 0 && end > 0)
        {
            list.Sort();
            var newList = list.GetRange(begin, end);
            avg = newList.Average();
        }
        return avg;
    }
    protected void CountProfits()
    {
        var com = auctionTracker.book;
        //count profit per profession/commodity
        //first get total profit earned this round
        Dictionary<string, float> totalProfits = new Dictionary<string, float>();
        //and number of agents per commodity
        Dictionary<string, int> numAgents = new Dictionary<string, int>();
        //initialize
        foreach (var entry in com)
        {
            var commodity = entry.Key;
            totalProfits.Add(commodity, 0);
            numAgents.Add(commodity, 0);
        }
        //accumulate
        foreach (var agent in agents)
        {
            var commodity = agent.outputs[0];
            //totalProfits[commodity] += agent.TaxProfit(taxRate);
            totalProfits[commodity] += agent.GetProfit();
            numAgents[commodity]++;
        }
        //average
        foreach (var entry in com)
        {
            var commodity = entry.Key;
            var profit = totalProfits[commodity];
            if (profit == 0)
            {
                //Debug.Log(commodity + " no profit earned this round");
            }
            else
            {
                entry.Value.profits.Add(profit);
            }
            if (auctionTracker.round > 100 && entry.Value.profits.LastAverage(100) < 0)
            {
                //Debug.Log("quitting!! last 10 round average was : " + entry.Value.profits.LastAverage(100));
                //TODO should be no trades in n rounds
            }
            else
            {
                //Debug.Log("last 10 round average was : " + entry.Value.profits.LastAverage(100));
            }
            if (float.IsNaN(profit) || profit > 10000)
            {
                profit = -42; //special case
            }
            auctionTracker.book[commodity].cashs.Add(profit);
        }
    }
    protected void QuitIf()
    {
        if (!exitAfterNoTrade)
        {
            return;
        }
        foreach (var entry in auctionTracker.book)
        {
            var commodity = entry.Key;
            var tradeVolume = entry.Value.trades.LastSum(numRoundsNoTrade);
            if (auctionTracker.round > numRoundsNoTrade && tradeVolume == 0)
            {
                //Debug.Log("quitting!! last " + numRoundsNoTrade + " round average " + commodity + " was : " + tradeVolume);
                timeToQuit = true;
                //TODO should be no trades in n rounds
            }
            else
            {
                //Debug.Log("last " + numRoundsNoTrade + " round trade average for " + commodity + " was : " + tradeVolume);
            }
        }
    }

    float defaulted = 0;
    protected void EnactBankruptcy()
    {
        foreach (var agent in agents)
        {
            //agent.Tick();
            irs -= agent.Tick();
        }
    }
    protected void CountProfessions()
    {
        var com = auctionTracker.book;
        //count number of agents per professions
        Dictionary<string, int> professions = new Dictionary<string, int>();
        //initialize professions
        foreach (var item in com)
        {
            var commodity = item.Key;
            professions.Add(commodity, 0);
        }
        //bin professions
        foreach (var agent in agents)
        {
            professions[agent.outputs[0]] += 1;
        }

        foreach (var entry in professions)
        {
            //Debug.Log("Profession: " + entry.Key + ": " + entry.Value);
            auctionTracker.book[entry.Key].professions.Add(entry.Value);
        }
    }
}
