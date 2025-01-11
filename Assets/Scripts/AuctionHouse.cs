using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using TMPro;

public class AuctionHouse : MonoBehaviour
{
    public static AuctionHouse Instance { get; internal set; }

    protected AgentConfig config;
    public int round;
    public int maxRounds = 10;

    [SerializedDictionary("Comm", "numAgents")]
    public SerializedDictionary<string, int> numAgents;

    protected List<EconAgent> agents = new List<EconAgent>();
    protected float irs;
    protected List<Offer> offerTable;
    protected List<Bid> bidTable;
    protected AuctionStats auctionStats;
    protected float lastTick;
    private bool autoRun = false;
    public TextMeshProUGUI roundText;

    private Dictionary<string, Commodity2> book => config.book;
    private Dictionary<string, AuctionRecord> currentAuctionRecord;

    private void Awake()
    {
        Instance = this;
        config = GetComponent<AgentConfig>();
        auctionStats = GetComponent<AuctionStats>();
    }

    void Start()
    {
        round = 0;

        lastTick = 0;
        offerTable = new List<Offer>();
        bidTable = new List<Bid>();

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
        foreach (string profession in professions)
        {
            if (!config.book.ContainsKey(profession))
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

        var record = new AuctionRecord()
        {
            Produced = 0,
            Trades = 0,
            AvgClearingPrice = 0,
            Consumed = 0
        };
        var recordDict = new Dictionary<string, AuctionRecord>();
        foreach (var entry in config.book)
        {
            recordDict.Add(entry.Key, record);
        }
        auctionStats.AddRecord(recordDict);
    }
    void InitAgent(EconAgent agent, string type)
    {
        List<string> buildables = new List<string> { type };
        int initStock = config.initStock;
        int initCash = config.initCash;
        if (config.randomInitStock)
        {
            initStock = UnityEngine.Random.Range(config.initStock / 2, config.initStock * 2);
        }

        agent.Init(config, initCash, buildables);
    }

    public void AutoRun()
    {
        autoRun = !autoRun;
    }

    void FixedUpdate()
    {
        if (Time.time - lastTick > config.tickInterval && autoRun)
        {
            Tick();
            lastTick = Time.time;
        }
    }
    public void Tick()
    {
        var totalProducedThisRound = new Dictionary<string, int>();
        var totalConsumedThisRound = new Dictionary<string, int>();
        offerTable = new List<Offer>();
        bidTable = new List<Bid>();
        currentAuctionRecord = new Dictionary<string, AuctionRecord>();

        foreach (var entry in book)
        {
            totalProducedThisRound.Add(entry.Key, 0);
            totalConsumedThisRound.Add(entry.Key, 0);
            currentAuctionRecord.Add(entry.Key, new AuctionRecord());
        }
        agents = agents.Where(x => !x.IsBankrupt).ToList();
        foreach (EconAgent agent in agents)
        {
            (var producedThisRound, var consumedThisRound) = agent.Produce(book);

            float idleTax = 0;
            if (producedThisRound.Count == 0)
            {
                idleTax = agent.PayTax(config.idleTaxRate);
                irs += idleTax;
            }
            var agentOffers = agent.CreateOffers();
            var agentBids = agent.CreateBids(book);
            offerTable.AddRange(agentOffers);
            bidTable.AddRange(agentBids);

            foreach (var entry in producedThisRound)
            {
                totalProducedThisRound[entry.Key] += entry.Value;
            }
            foreach (var entry in consumedThisRound)
            {
                totalConsumedThisRound[entry.Key] += entry.Value;
            }
        }

        foreach (var entry in book)
        {
            var itemBids = bidTable.Where(x => x.CommodityName == entry.Key).ToList();
            var itemOffers = offerTable.Where(x => x.CommodityName == entry.Key).ToList();
            ResolveOffers(entry.Key, itemBids, itemOffers);
        }

        var consumed = new Dictionary<string, int>();
        foreach (var agent in agents)
        {
            agent.Consume(consumed);
        }
        foreach (var entry in consumed)
        {
            totalConsumedThisRound[entry.Key] += entry.Value;
        }

        foreach (var entry in book)
        {
            var totalProduced = totalProducedThisRound[entry.Key];
            currentAuctionRecord[entry.Key].Produced = totalProduced;

            var totalConsumed = totalConsumedThisRound[entry.Key];
            currentAuctionRecord[entry.Key].Consumed = totalConsumed;
        }

        //CountProfits();
        CountProfessions();
        CountStockPileAndCash();

        EnactBankruptcy();
        round++;
        roundText.text = round.ToString();
        auctionStats.AddRecord(currentAuctionRecord);
    }

    protected void ResolveOffers(string itemName, List<Bid> bids, List<Offer> offers)
    {
        int totalOfferQuantity = offers.Sum(offer => offer.Quantity);
        int totalBidQuantity = bids.Sum(bid => bid.Quantity);
        currentAuctionRecord[itemName].Bids = totalBidQuantity;
        currentAuctionRecord[itemName].Offers = totalOfferQuantity;
        currentAuctionRecord[itemName].AvgBidPrice = totalBidQuantity > 0 ? (bids.Sum((x) => x.Price * x.Quantity) / totalBidQuantity) : 0;
        currentAuctionRecord[itemName].AvgOfferPrice = totalOfferQuantity > 0 ? (offers.Sum((x) => x.Price * x.Quantity) / totalOfferQuantity) : 0;

        offers = offers.OrderBy(x => x.Price).ToList();
        bids = bids.OrderBy(x => UnityEngine.Random.value).ToList();

        int moneyExchangedThisRound = 0;
        int goodsExchangedThisRound = 0;

        int maxLoop = 100;
        int j = 0;
        for (int i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            int bidIdx = 0;
            while (offer.remainingQuantity > 0 && bidIdx < bids.Count && j < maxLoop)
            {
                if (bidIdx >= bids.Count)
                {
                    bidIdx = bidIdx % bids.Count;
                }
                var bid = bids[bidIdx];

                int tradeQuantity = Math.Min(bid.remainingQuantity, offers[i].remainingQuantity);
                if (offer.Price > bid.Price || bid.agent.cash < offer.Price || tradeQuantity <= 0)
                {
                    bidIdx++;
                    continue;
                }
                //int acceptedPrice = (offer.Price + bid.Price) / 2;
                //if (config.buyerBuysOfferPrice)
                //{
                //    acceptedPrice = offer.Price;
                //}

                int[] possiblePrices = new int[] { offer.Price, bid.Price };
                int acceptedPrice = possiblePrices[UnityEngine.Random.Range(0, 2)];

                int boughtQuantity = bid.agent.Buy(itemName, tradeQuantity, acceptedPrice, round);
                offer.agent.Sell(itemName, boughtQuantity, acceptedPrice, round);
                offer.Accepted(acceptedPrice, boughtQuantity);
                bid.Accepted(acceptedPrice, boughtQuantity);

                moneyExchangedThisRound += acceptedPrice * boughtQuantity;
                goodsExchangedThisRound += boughtQuantity;

                //this is necessary for price belief updates after the big loop

                //go to next offer/bid if fullfilled
                if (bid.remainingQuantity == 0)
                {
                    bidIdx++;
                }

                j = (j + 1) % bids.Count;
            }
        }

        int averagePrice = 0;
        int lastClearingPrice = auctionStats.GetLastClearingPrice(itemName);
        if (moneyExchangedThisRound == 0)
        {
            averagePrice = lastClearingPrice;
        }
        else if (goodsExchangedThisRound != 0)
            averagePrice = moneyExchangedThisRound / goodsExchangedThisRound;

        currentAuctionRecord[itemName].AvgClearingPrice = averagePrice;
        currentAuctionRecord[itemName].Trades = goodsExchangedThisRound;

        var offerInfos = offers.GroupBy(x => x.agent.name).Select(x => new
        {
            AgentName = x.Key,
            SoldQty = x.Where(y => y.IsMatched).Sum(y => y.Quantity),
            UnSoldQty = x.Where(y => !y.IsMatched).Sum(y => y.Quantity),
            lastOfferAttempPrice = x.Last().Price,
            lastSoldPrice = x.Where(y => y.IsMatched).Any() ? x.Where(y => y.IsMatched).Last().AcceptedPrice : 0,
            AvgCost = (int)x.Average(y => y.Cost)
        }).ToList();
        foreach (var offerInfo in offerInfos)
        {
            var agent = agents.First(x => x.name == offerInfo.AgentName);
            agent.TradeStats[itemName].UpdateSellerPriceBelief(round, offerInfo.AgentName, itemName, book[itemName].productionRate, offerInfo.SoldQty, offerInfo.UnSoldQty, offerInfo.lastOfferAttempPrice, offerInfo.AvgCost, offerInfo.lastSoldPrice);
        }
        offers.Clear();

        var bidInfos = bids.GroupBy(x => x.agent.name).Select(x => new
        {
            AgentName = x.Key,
            BoughtQty = x.Where(y => y.IsMatched).Sum(y => y.Quantity),
            UnBoughtQty = x.Where(y => !y.IsMatched).Sum(y => y.Quantity),
            lastBidAttempPrice = x.Max(y => y.Price),
            lastBoughtPrice = x.Where(y => y.IsMatched).Any() ? x.Where(y => y.IsMatched).Last().AcceptedPrice : 0
        }).ToList();
        foreach (var bidInfo in bidInfos)
        {
            var agent = agents.First(x => x.name == bidInfo.AgentName);
            agent.TradeStats[itemName].UpdateBuyerPriceBelief(round, bidInfo.AgentName, itemName, book[itemName].productionRate, bidInfo.BoughtQty, bidInfo.UnBoughtQty, bidInfo.lastBidAttempPrice, bidInfo.lastBoughtPrice);
        }

        bids.Clear();
    }

    protected void CountStockPileAndCash()
    {
        Dictionary<string, int> stockPile = new Dictionary<string, int>();
        Dictionary<string, List<int>> cashList = new Dictionary<string, List<int>>();
        int totalCash = 0;
        foreach (var entry in book)
        {
            stockPile.Add(entry.Key, 0);
            cashList.Add(entry.Key, new List<int>());
        }

        foreach (var agent in agents)
        {
            //count stocks in all stocks of agent
            var itemInfos = agent.Inventory.GetItemInfos();
            foreach (var itemInfo in itemInfos)
            {
                if (agent.outputs.Contains(itemInfo.ItemName))
                    stockPile[itemInfo.ItemName] += itemInfo.Quantity;
            }

            cashList[agent.outputs[0]].Add(agent.cash);
            totalCash += agent.cash;
        }

        foreach (var entry in book)
        {
            currentAuctionRecord[entry.Key].Stocks = stockPile[entry.Key];
            currentAuctionRecord[entry.Key].Capitals = cashList[entry.Key].Sum();
        }
    }

    protected void EnactBankruptcy()
    {
        foreach (var agent in agents)
        {
            //agent.Tick();
            irs -= agent.Tick();
            if (config.pauseIfBankrupt && agent.IsBankrupt)
            {
                autoRun = false;
            }
        }
    }
    protected void CountProfessions()
    {
        //count number of agents per professions
        Dictionary<string, int> professions = new Dictionary<string, int>();
        //initialize professions
        foreach (var item in book)
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
            currentAuctionRecord[entry.Key].Professions = entry.Value;
        }
    }
}
