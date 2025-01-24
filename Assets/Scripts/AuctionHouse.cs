using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine.UI;

public class AuctionHouse : MonoBehaviour
{
    public static AuctionHouse Instance { get; internal set; }
    protected AgentConfig config;
    public int round;
    public int maxRounds = 10;
    public Toggle toggle;

    protected List<EconAgent> agents = new List<EconAgent>();
    protected List<Offer> offerTable;
    protected List<Bid> bidTable;
    protected AuctionStats auctionStats;
    protected float lastTick;

    private SerializedDictionary<string, int> numAgents => config.numAgents;
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
            ClearingPrice = 0,
            Consumed = 0
        };
        var recordDict = new Dictionary<string, AuctionRecord>();
        foreach (var entry in config.commodities)
        {
            recordDict.Add(entry.name, record);
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


    void FixedUpdate()
    {
        if (Time.time - lastTick > config.tickInterval && toggle.isOn)
        {
            Tick();
            lastTick = Time.time;
        }

        if (round == config.pauseAtRound)
            toggle.isOn = false;
    }
    public void Tick()
    {
        round++;
        var totalProducedThisRound = new Dictionary<string, int>();
        var totalConsumedThisRound = new Dictionary<string, int>();
        offerTable = new List<Offer>();
        bidTable = new List<Bid>();
        currentAuctionRecord = new Dictionary<string, AuctionRecord>();

        foreach (var entry in config.commodities)
        {
            totalProducedThisRound.Add(entry.name, 0);
            totalConsumedThisRound.Add(entry.name, 0);
            currentAuctionRecord.Add(entry.name, new AuctionRecord());
        }
        agents = agents.Where(x => !x.IsBankrupt).ToList();
        //Debug.Log("Round " + round);
        foreach (EconAgent agent in agents)
        {
            (var producedThisRound, var consumedThisRound) = agent.Produce();
            foreach (var entry in producedThisRound)
            {
                totalProducedThisRound[entry.Key] += entry.Value;
            }

            var agentOffers = agent.CreateOffers();
            var agentBids = agent.CreateBids();
            offerTable.AddRange(agentOffers);
            bidTable.AddRange(agentBids);

            foreach (var entry in producedThisRound)
            {
                totalProducedThisRound[entry.Key] += entry.Value;
                //Debug.Log("--" + agent.name + " produced " + entry.Key + "(" + entry.Value + ")");
            }
            foreach (var entry in consumedThisRound)
            {
                totalConsumedThisRound[entry.Key] += entry.Value;
                //Debug.Log("--" + agent.name + " consumed " + entry.Key + "(" + entry.Value + ")");
            }
        }

        foreach (var entry in config.commodities)
        {
            var itemBids = bidTable.Where(x => x.CommodityName == entry.name).ToList();
            var itemOffers = offerTable.Where(x => x.CommodityName == entry.name).ToList();
            ResolveOffers(entry.name, itemBids, itemOffers);
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

        foreach (var entry in config.commodities)
        {
            var totalProduced = totalProducedThisRound[entry.name];
            currentAuctionRecord[entry.name].Produced = totalProduced;

            var totalConsumed = totalConsumedThisRound[entry.name];
            currentAuctionRecord[entry.name].Consumed = totalConsumed;
        }

        //CountProfits();
        CountProfessions();
        CountStockPileAndCash();

        EnactBankruptcy();
        auctionStats.AddRecord(currentAuctionRecord);
    }

    protected void ResolveOffers(string itemName, List<Bid> bids, List<Offer> offers)
    {
        int totalOfferQty = offers.Sum(offer => offer.Qty);
        int totalBidQty = bids.Sum(bid => bid.Qty);
        currentAuctionRecord[itemName].Bids = totalBidQty;
        currentAuctionRecord[itemName].Offers = totalOfferQty;
        currentAuctionRecord[itemName].AvgBidPrice = totalBidQty > 0 ? (bids.Sum((x) => x.Price * x.Qty) / totalBidQty) : 0;
        currentAuctionRecord[itemName].AvgOfferPrice = totalOfferQty > 0 ? (offers.Sum((x) => x.Price * x.Qty) / totalOfferQty) : 0;

        offers = offers.OrderBy(x => x.Price).ToList();
        bids = bids.OrderBy(x => UnityEngine.Random.value).ToList();

        int moneyExchangedThisRound = 0;
        int goodsExchangedThisRound = 0;

        for (int i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            for (int j = 0; j < bids.Count && offer.RemainingQty > 0; j++)
            {
                var bid = bids[j];
                if (bid.RemainingQty <= 0)
                    continue;

                int acceptedQty = Math.Min(bid.RemainingQty, offers[i].RemainingQty);
                if (offer.Price > bid.Price)
                {
                    if (itemName == "Food")
                        Debug.Log($"<color=#aa0000>r=</color>{round} {itemName}({acceptedQty}) {offer.Price}({offer.agent.name}) {bid.Price}({bid.agent.name})");
                    continue;
                }

                int acceptedPrice = (offer.Price + bid.Price) / 2;
                if (config.buyerBuysOfferPrice)
                    acceptedPrice = offer.Price;
                //int[] possiblePrices = new int[] { offer.Price, bid.Price };
                //int acceptedPrice = possiblePrices[UnityEngine.Random.Range(0, 2)];

                if (bid.agent.cash < acceptedPrice * acceptedQty)
                    acceptedQty = bid.agent.cash / acceptedPrice;
                if (acceptedQty == 0)
                    continue;

                if (itemName == "Food")
                    Debug.Log($"<color=#00aa00>r=</color>{round} {itemName}({acceptedQty}) {offer.Price}({offer.agent.name}) {bid.Price}({bid.agent.name}) ");
                ExecuteTrade(bid, offer, acceptedPrice, acceptedQty);
                moneyExchangedThisRound += acceptedPrice * acceptedQty;
                goodsExchangedThisRound += acceptedQty;
            }
        }

        int clearingPrice = auctionStats.GetLastClearingPrice(itemName);
        if (moneyExchangedThisRound > 0 && goodsExchangedThisRound > 0)
            clearingPrice = moneyExchangedThisRound / goodsExchangedThisRound;

        currentAuctionRecord[itemName].ClearingPrice = clearingPrice;
        currentAuctionRecord[itemName].Trades = goodsExchangedThisRound;

        foreach (var offer in offers)
        {
            offer.agent.TradeStats[itemName].AddSellRecord(offer.Price, offer.Qty - offer.RemainingQty, round);
        }
        foreach (var bid in bids)
        {
            bid.agent.TradeStats[itemName].AddBuyRecord(bid.Price, bid.Qty - bid.RemainingQty, round);
        }

        var offerInfos = offers.GroupBy(x => x.agent.name).Select(x => new
        {
            AgentName = x.Key,
            SoldQty = x.Sum(y => y.Qty) - x.Sum(y => y.RemainingQty),
            UnSoldQty = x.Sum(y => y.RemainingQty),
            lastOfferAttempPrice = x.Last().Price,
            lastSoldPrice = x.Any(y => y.RemainingQty == 0) ? x.Last(x => x.RemainingQty == 0).Price : 0,
            AvgCost = (int)x.Average(y => y.Cost),
            ProdRate = (int)x.SelectMany(y => y.Items.Select(z => z.ProdRate)).Average()
        }).ToList();
        foreach (var obj in offerInfos)
        {
            var agent = agents.First(x => x.name == obj.AgentName);
            agent.TradeStats[itemName].UpdateSellerPriceBelief(round, obj.AgentName, itemName, obj.SoldQty, obj.UnSoldQty, obj.lastOfferAttempPrice, obj.AvgCost, obj.lastSoldPrice, obj.ProdRate);
        }
        offers.Clear();

        var bidInfos = bids.GroupBy(x => x.agent.name).Select(x => new
        {
            AgentName = x.Key,
            BoughtQty = x.Sum(y => y.Qty) - x.Sum(y => y.RemainingQty),
            UnBoughtQty = x.Sum(y => y.RemainingQty),
            lastBidAttempPrice = x.Max(y => y.Price),
            lastBoughtPrice = x.Where(y => y.IsMatched).Any() ? x.Where(y => y.IsMatched).Last().Price : 0
        }).ToList();
        foreach (var bidInfo in bidInfos)
        {
            var agent = agents.First(x => x.name == bidInfo.AgentName);
            agent.TradeStats[itemName].UpdateBuyerPriceBelief(round, bidInfo.AgentName, itemName, bidInfo.BoughtQty, bidInfo.UnBoughtQty, bidInfo.lastBidAttempPrice, bidInfo.lastBoughtPrice);
        }

        bids.Clear();
    }

    private void ExecuteTrade(Bid bid, Offer offer, int price, int qty)
    {
        List<Item> itemsToBuy = offer.Items.Take(qty).ToList();

        bid.agent.Inventory.AddItem(itemsToBuy);
        offer.agent.Inventory.RemoveItem(itemsToBuy);
        bid.agent.cash -= price * qty;
        offer.agent.cash += price * qty;
        bid.RemainingQty -= qty;
        offer.RemainingQty -= qty;
    }

    protected void CountStockPileAndCash()
    {
        Dictionary<string, int> stockPile = new Dictionary<string, int>();
        Dictionary<string, List<int>> cashList = new Dictionary<string, List<int>>();
        int totalCash = 0;
        foreach (var entry in config.commodities)
        {
            stockPile.Add(entry.name, 0);
            cashList.Add(entry.name, new List<int>());
        }

        foreach (var agent in agents)
        {
            //count stocks in all stocks of agent
            var itemInfos = agent.Inventory.GetItemInfos();
            foreach (var itemInfo in itemInfos)
            {
                if (agent.outputs.Contains(itemInfo.ItemName))
                    stockPile[itemInfo.ItemName] += itemInfo.Qty;
            }

            cashList[agent.outputs[0]].Add(agent.cash);
            totalCash += agent.cash;
        }

        foreach (var entry in config.commodities)
        {
            currentAuctionRecord[entry.name].Stocks = stockPile[entry.name];
            currentAuctionRecord[entry.name].Capitals = cashList[entry.name].Sum();
        }
    }

    protected void EnactBankruptcy()
    {
        foreach (var agent in agents)
        {
            if (config.pauseWhenBankrupt && agent.IsBankrupt)
            {
                toggle.isOn = false;
            }
        }
    }
    protected void CountProfessions()
    {
        //count number of agents per professions
        Dictionary<string, int> professions = new Dictionary<string, int>();
        //initialize professions
        foreach (var item in config.commodities)
        {
            var commodity = item.name;
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
