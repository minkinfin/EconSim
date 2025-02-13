using System;
using System.Collections.Generic;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using UnityEngine.UI;

public class AuctionHouse : MonoBehaviour
{
    public static AuctionHouse Instance { get; internal set; }
    protected AgentConfig config;
    public int round;
    public int maxRounds = 10;
    public Toggle toggle;

    protected List<EconAgent> agents = new List<EconAgent>();
    protected Dictionary<string, List<Offer>> offerTable;
    protected Dictionary<string, List<Bid>> bidTable;
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
        offerTable = new Dictionary<string, List<Offer>>();
        bidTable = new Dictionary<string, List<Bid>>();

        var prefab = Resources.Load("Agent");

        int numAgent = 0;
        foreach (var obj in numAgents)
        {
            for (int i = 0; i < obj.Value; i++)
            {
                GameObject go = Instantiate(prefab) as GameObject;
                go.transform.parent = transform;
                go.name = "agent" + numAgent.ToString();
                var agent = go.GetComponent<EconAgent>();
                InitAgent(agent, obj.Key);
                agents.Add(agent);

                numAgent++;
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

        if (round == config.pauseAtRound - 1)
            toggle.isOn = false;
    }
    public void Tick()
    {
        round++;
        var totalProducedThisRound = new Dictionary<string, int>();
        var totalConsumedThisRound = new Dictionary<string, int>();
        offerTable = new Dictionary<string, List<Offer>>();
        foreach (var entry in config.commodities)
        {
            offerTable.Add(entry.name, new List<Offer>());
        }
        bidTable = new Dictionary<string, List<Bid>>();
        foreach (var entry in config.commodities)
        {
            bidTable.Add(entry.name, new List<Bid>());
        }
        currentAuctionRecord = new Dictionary<string, AuctionRecord>();

        foreach (var entry in config.commodities)
        {
            totalProducedThisRound.Add(entry.name, 0);
            totalConsumedThisRound.Add(entry.name, 0);
            currentAuctionRecord.Add(entry.name, new AuctionRecord());
        }

        //Debug.Log("Round " + round);
        foreach (EconAgent agent in agents)
        {
            if (agent.IsBankrupt)
            {
                continue;
            }
            (var producedThisRound, var consumedThisRound) = agent.Produce();

            var agentOffers = agent.CreateOffers();
            var agentBids = agent.CreateBids();
            foreach (var offers in agentOffers)
            {
                offerTable[offers.Key].AddRange(offers.Value);
            }
            foreach (var bids in agentBids)
            {
                bidTable[bids.Key].AddRange(bids.Value);
            }

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
            var itemBids = bidTable[entry.name];
            var itemOffers = offerTable[entry.name];
            ResolveOffers(entry.name, itemBids, itemOffers);
        }

        //foreach (var entry in config.commodities)
        //{
        //    foreach (var agent in agents)
        //    {
        //        var tradeStats = agent.TradeStats[entry.name];
        //        if (entry.name == "Stick")
        //            Debug.Log($"r={round} {agent.name} {entry.name} {tradeStats.SellBufferDays.ToString("F")} {tradeStats.BuyBufferDays.ToString("F")}");
        //    }
        //}

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

        int totalOfferQty = 0;
        int totalOfferValue = 0;
        int totalBidQty = 0;
        int totalBidValue = 0;
        foreach (var offer in offers)
        {
            totalOfferQty += offer.Qty;
            totalOfferValue += offer.Price * offer.Qty;
        }
        foreach (var bid in bids)
        {
            totalBidQty += bid.Qty;
            totalBidValue += bid.Price * bid.Qty;
        }
        currentAuctionRecord[itemName].Bids = totalBidQty;
        currentAuctionRecord[itemName].Offers = totalOfferQty;
        currentAuctionRecord[itemName].AvgBidPrice = totalBidQty > 0 ? (totalOfferValue / totalBidQty) : 0;
        currentAuctionRecord[itemName].AvgOfferPrice = totalOfferQty > 0 ? (totalBidValue / totalOfferQty) : 0;

        offers.Sort((x, y) => x.Price.CompareTo(y.Price));
        //bids.Sort((x, y) => UnityEngine.Random.Range(-1, 2));
        bids.Sort((x, y) => y.Price.CompareTo(x.Price));

        int moneyExchangedThisRound = 0;
        int goodsExchangedThisRound = 0;

        //if (offers.Count == 0)
        //{
        //    if (itemName == "Stick")
        //        Debug.Log($"<color=#aa0000>r=</color>{round} {itemName} offers=0");
        //}
        //if (bids.Count == 0)
        //{
        //    if (itemName == "Stick")
        //        Debug.Log($"<color=#aa0000>r=</color>{round} {itemName} bids=0");
        //}

        int bidIndex = 0;
        for (int offerIndex = 0; offerIndex < offers.Count; offerIndex++)
        {
            var offer = offers[offerIndex];

            while (bidIndex < bids.Count)
            {
                var bid = bids[bidIndex];

                int acceptedQty = Math.Min(bid.RemainingQty, offer.RemainingQty);
                int acceptedPrice = offer.Price;
                if (offer.Price > bid.Price || acceptedQty == 0 || acceptedQty * acceptedPrice > bid.agent.cash)
                {
                    //if (itemName == ")
                    //    Debug.Log($"<color=#aa0000>r=</color>{round} {itemName}({acceptedQty}) {offer.Price}({offer.agent.name}) {bid.Price}({bid.agent.name})");

                    bidIndex++;
                    if (bidIndex >= bids.Count) break;
                    continue;
                }

                //int acceptedPrice = (offer.Price + bid.Price) / 2;
                //if (config.buyerBuysOfferPrice)
                //    acceptedPrice = offer.Price;
                //int[] possiblePrices = new int[] { offer.Price, bid.Price };
                //int acceptedPrice = possiblePrices[UnityEngine.Random.Range(0, 2)];

                //if (bid.agent.cash < acceptedPrice * acceptedQty)
                //    acceptedQty = bid.agent.cash / acceptedPrice;
                //if (acceptedQty == 0)
                //{
                //    bidIndex++;
                //    if (bidIndex >= bids.Count) break;
                //    continue;
                //}

                ExecuteTrade(bid, offer, acceptedPrice, acceptedQty);
                moneyExchangedThisRound += acceptedPrice * acceptedQty;
                goodsExchangedThisRound += acceptedQty;
                if (itemName == "Food")
                    Debug.Log($"<color=#00aa00>r=</color>{round} {itemName}({acceptedQty}) {offer.Price}({offer.agent.name}) {bid.Price}({bid.agent.name}) ");
                bidIndex++;
            }
        }

        int clearingPrice = auctionStats.GetLastClearingPrice(itemName);
        if (moneyExchangedThisRound > 0 && goodsExchangedThisRound > 0)
            clearingPrice = moneyExchangedThisRound / goodsExchangedThisRound;

        currentAuctionRecord[itemName].ClearingPrice = clearingPrice;
        currentAuctionRecord[itemName].Trades = goodsExchangedThisRound;


        foreach (var agent in agents)
        {
            var tradeStats = agent.TradeStats[itemName];
            var offer = offers.Find(x => x.agent == agent);
            var bid = bids.Find(x => x.agent == agent);

            if (offer != null)
                tradeStats.AddSellRecord(offer.Price, offer.Qty - offer.RemainingQty, round);
            if (bid != null)
                tradeStats.AddBuyRecord(bid.Price, bid.Qty - bid.RemainingQty, round);

            float prevSellBufferQty = tradeStats.SellBufferQty;
            float sellBufferQty = tradeStats.UpdateSellBufferQty(round);
            float prevBuyBufferQty = tradeStats.BuyBufferQty;
            float buyBufferQty = tradeStats.UpdateBuyBufferQty(round);

            int prevPriceBelief = 0;
            int priceBelief = 0;
            string symb = "=";
            string role = "";
            if (offer != null)
            {
                prevPriceBelief = tradeStats.GetPriceBelief();
                priceBelief = tradeStats.UpdateSellerPriceBelief(itemName, offer.Price, offer.Cost, offer.Qty - offer.RemainingQty, round);
                symb = prevPriceBelief < priceBelief ? "↗" : (prevPriceBelief > priceBelief ? "↘" : "=");
                role = "Seller";
                if (itemName == "Food")
                    Debug.Log($"r={round} {role} {agent.name} {itemName} ({prevPriceBelief}){symb}({priceBelief}) {sellBufferQty}({sellBufferQty - prevSellBufferQty})");
            }
            else if (bid != null)
            {
                prevPriceBelief = tradeStats.GetPriceBelief();
                priceBelief = tradeStats.UpdateBuyerPriceBelief(itemName, bid.Price, bid.Qty - bid.RemainingQty, round);
                symb = prevPriceBelief < priceBelief ? "↗" : (prevPriceBelief > priceBelief ? "↘" : "=");
                role = "Buyer";
                if (itemName == "Food")
                    Debug.Log($"r={round} {role} {agent.name} {itemName} ({prevPriceBelief}){symb}({priceBelief}) {buyBufferQty}({buyBufferQty - prevBuyBufferQty})");
            }
            //else
            //{
            //    if (prevSellBufferQty > 0)
            //    {
            //        role = "Seller";
            //        if (itemName == "Food")
            //            Debug.Log($"r={round} {role} {agent.name} {itemName} ({prevPriceBelief}){symb}({priceBelief}) {sellBufferQty}({sellBufferQty - prevSellBufferQty})");
            //    }
            //    else if (prevBuyBufferQty > 0)
            //    {
            //        role = "Buyer";
            //        if (itemName == "Food")
            //            Debug.Log($"r={round} {role} {agent.name} {itemName} ({prevPriceBelief}){symb}({priceBelief}) {buyBufferQty}({buyBufferQty - prevBuyBufferQty})");
            //    }
            //    else
            //    {
            //        if (itemName == "Food")
            //            Debug.Log($"r={round} ------ {agent.name} {itemName} ({prevPriceBelief}){symb}({priceBelief}) 0(0)");
            //    }
            //}
        }

        //foreach (var offer in offers)
        //{
        //    var agent = offer.agent;
        //    var tradeStats = agent.TradeStats[itemName];
        //    tradeStats.AddSellRecord(offer.Price, offer.Qty - offer.RemainingQty, round);
        //}

        //foreach (var bid in bids)
        //{
        //    var agent = bid.agent;
        //    var tradeStats = agent.TradeStats[itemName];
        //    tradeStats.AddBuyRecord(bid.Price, bid.Qty - bid.RemainingQty, round);
        //}

        //foreach (var agent in agents)
        //{
        //    var tradeStats = agent.TradeStats[itemName];
        //    float prevSellBufferQty = tradeStats.SellBufferQty;
        //    float prevBuyBufferQty = tradeStats.BuyBufferQty;
        //    float sellBufferQty = tradeStats.UpdateSellBufferQty(round);
        //    float buyBufferQty = tradeStats.UpdateBuyBufferQty(round);
        //    float priceBelief = tradeStats.GetPriceBelief();
        //    if (itemName == "Food")
        //        Debug.Log($"r={round} {agent.name} {itemName} {priceBelief} {sellBufferQty.ToString("F")} {buyBufferQty.ToString("F")} ({sellBufferQty - prevSellBufferQty}, {buyBufferQty - prevBuyBufferQty})");
        //}

        //foreach (var offer in offers)
        //{
        //    var agent = offer.agent;
        //    var tradeStats = agent.TradeStats[itemName];
        //    int prevPriceBelief = tradeStats.GetPriceBelief();
        //    int priceBelief = tradeStats.UpdateSellerPriceBelief(itemName, offer.Price, offer.Cost, offer.Qty - offer.RemainingQty, round);

        //    string symb = prevPriceBelief < priceBelief ? "↗" : (prevPriceBelief > priceBelief ? "↘" : "=");
        //    //if (itemName == "Food")
        //    //    Debug.Log($"r={round} Seller {agent.name} ({prevPriceBelief}){symb}({priceBelief}) {tradeStats.SellBufferQty.ToString("F")}");
        //}

        //foreach (var bid in bids)
        //{
        //    var agent = bid.agent;
        //    var tradeStats = agent.TradeStats[itemName];
        //    int prevPriceBelief = tradeStats.GetPriceBelief();
        //    int priceBelief = tradeStats.UpdateBuyerPriceBelief(itemName, bid.Price, bid.Qty - bid.RemainingQty, round);
        //    string symb = prevPriceBelief < priceBelief ? "↗" : (prevPriceBelief > priceBelief ? "↘" : "=");
        //    //if (itemName == "Food")
        //    //    Debug.Log($"r={round} Buyer {agent.name} ({prevPriceBelief}){symb}({priceBelief}) {tradeStats.BuyBufferQty.ToString("F")}");
        //}
    }

    private void ExecuteTrade(Bid bid, Offer offer, int price, int qty)
    {
        List<Item> itemsToBuy = offer.Items;
        if (qty != offer.Items.Count)
        {
            itemsToBuy = new List<Item>();
            for (int i = 0; i < qty; i++)
            {
                if (offer.Items.Count == 0)
                    break;
                itemsToBuy.Add(offer.Items[0]);
                offer.Items.RemoveAt(0);
            }
        }

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
        Dictionary<string, int> cashList = new Dictionary<string, int>();
        int totalCash = 0;
        foreach (var entry in config.commodities)
        {
            stockPile.Add(entry.name, 0);
            cashList.Add(entry.name, 0);
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

            cashList[agent.outputs[0]] += agent.cash;
            totalCash += agent.cash;
        }

        foreach (var entry in config.commodities)
        {
            currentAuctionRecord[entry.name].Stocks = stockPile[entry.name];
            currentAuctionRecord[entry.name].Capitals = cashList[entry.name];
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
