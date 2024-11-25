using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using System.Collections;
using TMPro;

public class AuctionHouse : MonoBehaviour
{
    protected AgentConfig config;
    public int maxRounds = 10;
    public bool exitAfterNoTrade = true;
    public int numRoundsNoTrade = 100;

    [SerializedDictionary("Comm", "numAgents")]
    public SerializedDictionary<string, int> numAgents;

    protected List<EconAgent> agents = new List<EconAgent>();
    protected float irs;
    protected bool timeToQuit = false;
    protected List<Offer> offerTable;
    protected List<Bid> bidTable;
    protected StreamWriter sw;
    protected AuctionStats auctionStats;
    protected float lastTick;
    public int round { get; private set; }
    private bool autoRun = false;
    public TextMeshProUGUI roundText;
    void Start()
    {
        round = 0;
        config = GetComponent<AgentConfig>();

        lastTick = 0;
        auctionStats = AuctionStats.Instance;
        var com = auctionStats.book;
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
        var resources = auctionStats.recipes.Select(x => x.Key).ToArray();
        foreach (string profession in professions)
        {
            if (!resources.Contains(profession))
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

        foreach (var entry in com)
        {
            entry.Value.produced.Add(0);
            entry.Value.trades.Add(0);
            entry.Value.avgClearingPrice.Add(1);
            entry.Value.consumed.Add(0);
        }
        CountStockPileAndCash();
        CountProfessions();
        CountProfits();
    }
    void InitAgent(EconAgent agent, string type)
    {
        List<string> buildables = new List<string> { type };
        int initStock = config.initStock;
        float initCash = config.initCash;
        if (config.randomInitStock)
        {
            initStock = UnityEngine.Random.Range(config.initStock / 2, config.initStock * 2);
        }

        var maxStock = Mathf.Max(initStock, config.maxStock);

        agent.Init(config, initCash, buildables, initStock, maxStock);
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
        var com = auctionStats.book;
        var totalProducedThisRound = new Dictionary<string, float>();
        var totalConsumedThisRound = new Dictionary<string, float>();
        offerTable = new List<Offer>();
        bidTable = new List<Bid>();
        foreach (var entry in com)
        {
            totalProducedThisRound.Add(entry.Key, 0);
            totalConsumedThisRound.Add(entry.Key, 0);
        }
        agents = agents.Where(x => !x.IsBankrupt).ToList();
        foreach (EconAgent agent in agents)
        {
            (var producedThisRound, var consumedThisRound) = agent.Produce(com);

            float idleTax = 0;
            if (producedThisRound.Count == 0)
            {
                idleTax = agent.PayTax(config.idleTaxRate);
                irs += idleTax;
            }
            var offers = agent.CreateOffers();
            var bids = agent.CreateBids(com);
            offerTable.AddRange(offers);
            bidTable.AddRange(bids);

            foreach (var entry in producedThisRound)
            {
                totalProducedThisRound[entry.Key] += entry.Value;
            }
            foreach (var entry in consumedThisRound)
            {
                totalConsumedThisRound[entry.Key] += entry.Value;
            }
        }

        foreach (var entry in com)
        {
            ResolveOffers(entry.Value);
        }

        foreach (var agent in agents)
        {
            var need = agent.Consume();
            foreach (var entry in need)
            {
                totalConsumedThisRound[entry.Key] += entry.Value;
            }
        }

        foreach (var entry in com)
        {
            var totalProduced = totalProducedThisRound[entry.Key];
            entry.Value.produced.Add(totalProduced);

            var totalConsumed = totalConsumedThisRound[entry.Key];
            entry.Value.consumed.Add(totalConsumed);
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
        round++;
        roundText.text = round.ToString();
    }

    protected void ResolveOffers(Commodity commodity)
    {
        var offers = offerTable.Where(x => x.CommodityName == commodity.name).ToList();
        var bids = bidTable.Where(x => x.CommodityName == commodity.name).ToList();
        var agentDemandRatio = 0;
        if (offers.Count > 0)
            agentDemandRatio = bids.Count / offers.Count;


        int totalOfferQuantity = offers.Sum(offer => offer.Quantity);
        int totalBidQuantity = bids.Sum(bid => bid.Quantity);
        commodity.offers.Add(totalOfferQuantity);
        commodity.bids.Add(totalBidQuantity);
        commodity.sellers.Add(offers.Count);
        commodity.buyers.Add(bids.Count);
        commodity.avgOfferPrice.Add(totalOfferQuantity > 0 ? (offers.Sum((x) => x.Price * x.Quantity) / totalOfferQuantity) : 0);
        commodity.avgBidPrice.Add(totalBidQuantity > 0 ? (bids.Sum((x) => x.Price * x.Quantity) / totalBidQuantity) : 0);

        offers = offers.OrderBy(x => x.Price).ToList();
        bids = bids.OrderBy(x => new Guid()).ToList();

        float moneyExchangedThisRound = 0;
        float goodsExchangedThisRound = 0;

        int maxLoop = 100;
        int ii = 0;
        for (int i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            int bidIdx = 0;
            while (offer.remainingQuantity > 0 && bidIdx < bids.Count && ii < maxLoop)
            {
                if (bidIdx >= bids.Count)
                {
                    bidIdx = bidIdx % bids.Count;
                }
                var bid = bids[bidIdx];

                if (offer.Price > bid.Price || bid.agent.cash < offer.Price)
                {
                    bidIdx++;
                    continue;
                }
                var acceptedPrice = (offer.Price + bid.Price) / 2;
                if(config.buyerBuysOfferPrice)
                {
                    acceptedPrice = offer.Price;
                }

                int tradeQuantity = Math.Min(bid.remainingQuantity, offers[i].remainingQuantity);

                int boughtQuantity = bid.agent.Buy(commodity.name, tradeQuantity, acceptedPrice);
                offer.agent.Sell(commodity.name, boughtQuantity, acceptedPrice);

                moneyExchangedThisRound += acceptedPrice * boughtQuantity;
                goodsExchangedThisRound += boughtQuantity;

                //this is necessary for price belief updates after the big loop
                offer.Accepted(acceptedPrice, boughtQuantity);
                bid.Accepted(acceptedPrice, boughtQuantity);

                //go to next offer/bid if fullfilled
                if (bid.remainingQuantity == 0)
                {
                    bidIdx++;
                }
                ii++;
            }
        }

        float averagePrice = 0;
        //if (goodsExchangedThisRound == 0 && commodity.trades.Count > 0)
        //{
        //    goodsExchangedThisRound = commodity.trades.Last();
        //}

        if (moneyExchangedThisRound == 0 && commodity.avgClearingPrice.Count > 0)
        {
            averagePrice = commodity.avgClearingPrice.Last();
        }
        else if (goodsExchangedThisRound != 0)
            averagePrice = moneyExchangedThisRound / goodsExchangedThisRound;

        commodity.avgClearingPrice.Add(averagePrice);
        commodity.trades.Add(goodsExchangedThisRound);
        commodity.Update(averagePrice, agentDemandRatio);

        //var excessDemand = offers.Sum(offer => offer.quantity);
        //var excessSupply = bids.Sum(bid => bid.quantity);
        //var demand = (goodsExchanged + excessDemand) 
        //					 / (goodsExchanged + excessSupply);


        foreach (var offer in offers)
        {
            offer.agent.UpdateSellerPriceBelief(in offer, in commodity);
        }
        offers.Clear();
        foreach (var bid in bids)
        {
            bid.agent.UpdateBuyerPriceBelief(in bid, in commodity);
        }
        bids.Clear();

        //var offerAgents = offers.GroupBy(x => x.agent.name).Select(x => (x.Select(y => y.agent).First(), x.ToList())).ToList();
        //foreach (var offerAgent in offerAgents)
        //{
        //    offerAgent.Item1.UpdateSellerPriceBelief(offerAgent.Item2, in commodity);
        //}
        //offers.Clear();

        //var bidAgents = bids.GroupBy(x => x.agent.name).Select(x => (x.Select(y => y.agent).First(), x.ToList())).ToList();
        //foreach (var bidAgent in bidAgents)
        //{
        //    bidAgent.Item1.UpdateSellerPriceBelief(bidAgent.Item2, in commodity);
        //}
        //bids.Clear();
    }

    protected void Trade(string commodity, float clearingPrice, int quantity, EconAgent bidder, EconAgent seller)
    {
        //transfer commodity
        //transfer cash
        var boughtQuantity = bidder.Buy(commodity, quantity, clearingPrice);
        seller.Sell(commodity, boughtQuantity, clearingPrice);
        var cashQuantity = quantity * clearingPrice;

    }

    protected void CountStockPileAndCash()
    {
        Dictionary<string, float> stockPile = new Dictionary<string, float>();
        Dictionary<string, List<float>> cashList = new Dictionary<string, List<float>>();
        var com = auctionStats.book;
        float totalCash = 0;
        foreach (var entry in com)
        {
            stockPile.Add(entry.Key, 0);
            cashList.Add(entry.Key, new List<float>());
        }
        foreach (var agent in agents)
        {
            //count stocks in all stocks of agent
            foreach (var c in agent.Inventory)
            {
                stockPile[c.Key] += c.Value.Surplus();
            }
            cashList[agent.outputs[0]].Add(agent.cash);
            totalCash += agent.cash;
        }

        foreach (var entry in com)
        {
            auctionStats.book[entry.Key].stocks.Add(stockPile[entry.Key]);
            auctionStats.book[entry.Key].capitals.Add(cashList[entry.Key].Sum());
        }
    }
    protected void CountProfits()
    {
        var com = auctionStats.book;
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
            if (round > 100 && entry.Value.profits.LastAverage(100) < 0)
            {
                //Debug.Log("quitting!! last 10 round average was : " + entry.Value.profits.LastAverage(100));
                //TODO should be no trades in n rounds
            }
            else
            {
                //Debug.Log("last 10 round average was : " + entry.Value.profits.LastAverage(100));
            }

            if (float.IsNaN(profit))
            {
                profit = 0; //special case
            }
            auctionStats.book[commodity].cashs.Add(profit);
        }
    }
    protected void QuitIf()
    {
        if (!exitAfterNoTrade)
        {
            return;
        }
        foreach (var entry in auctionStats.book)
        {
            var commodity = entry.Key;
            var tradeVolume = entry.Value.trades.LastSum(numRoundsNoTrade);
            if (round > numRoundsNoTrade && tradeVolume == 0)
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
        var com = auctionStats.book;
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
            auctionStats.book[entry.Key].professions.Add(entry.Value);
        }
    }
}
