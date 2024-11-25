using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

using AYellowpaper.SerializedCollections;
using System.Security.Cryptography.X509Certificates;
using System.Net.WebSockets;
using UnityEditor.Sprites;
//using Dependency = System.Collections.Generic.Dictionary<string, float>;

public class AuctionStats : MonoBehaviour
{
    AgentConfig config;
    public static AuctionStats Instance { get; private set; }

    public Dictionary<string, Commodity> book { get; private set; }

    [SerializedDictionary("ID", "Dependency")]
    public SerializedDictionary<string, SerializedKeyValuePair<int, SerializedDictionary<string, int>>> recipes;
    string log_msg = "";
    public string GetLog()
    {
        var ret = log_msg;
        log_msg = "";
        return ret;
    }

    private void Awake()
    {
        Instance = this;
        book = new Dictionary<string, Commodity>(); //names, market price
        Init();
    }

    private void Start()
    {
        config = GetComponent<AgentConfig>();
    }

    public string GetMostProfitableProfession(String exclude_key)
    {
        string prof = "invalid";
        float most = 0;

        foreach (var entry in book)
        {
            var commodity = entry.Key;
            if (exclude_key == commodity)
            {
                continue;
            }
            var profitHistory = entry.Value.profits;
            //WARNING this history refers to the last # agents' profits, not last # rounds... short history if popular profession...
            var profit = profitHistory.LastAverage(config.historySize);
            if (profit > most)
            {
                prof = commodity;
                most = profit;
            }
        }
        return prof;
    }
    string mostDemand = "invalid";
    public string GetHottestGood()
    {
        switch (config.changeProductionMode)
        {
            case ChangeProductionMode.HighestBidPrice:
                mostDemand = GetHottestGoodByHighestBidPrice();
                break;
            case ChangeProductionMode.ProbabilisticHottestGood:
                mostDemand = GetHottestGoodByProbabilisticHottestGood();
                break;
        }

        if (mostDemand == "invalid")
            mostDemand = GetHottestGoodByRandom();

        return mostDemand;
    }
    string GetHottestGoodByHighestBidPrice()
    {
        mostDemand = "invalid";
        float mostBid = 0;
        foreach (var c in book)
        {
            var bid = c.Value.avgBidPrice.LastAverage(config.historySize);
            if (bid > mostBid)
            {
                mostBid = bid;
                mostDemand = c.Key;
            }
        }
        //log_msg += round + ", auction, " + mostDemand + ", none, mostBid, " + mostBid + ", n/a\n";

        return mostDemand;
    }
    string GetHottestGoodByProbabilisticHottestGood()
    {
        float best_ratio = 1.5f;

        foreach (var c in book)
        {
            var offers = c.Value.offers.LastAverage(config.historySize);
            var bids = c.Value.bids.LastAverage(config.historySize);
            offers = Mathf.Max(.5f, offers);
            var ratio = bids / offers;

            if (best_ratio < ratio)
            {
                best_ratio = ratio;
                mostDemand = c.Key;
            }
        }
        //log_msg += round + ", auction, " + mostDemand + ", none, demandsupplyratio, " + Mathf.Sqrt(best_ratio) + ", n/a\n";
        return mostDemand;
    }
    string GetHottestGoodByRandom()
    {
        var picker = new WeightedRandomPicker<string>();
        picker.Clear();
        foreach (var c in book)
        {
            picker.AddItem(c.Key, 1);//Mathf.Sqrt(ratio)); //less likely a profession dies out
        }
        var itemWeight = picker.PickRandom();
        //log_msg += round + ", auction, " + mostDemand + ", none, random, " + itemWeight.Item2 + ", n/a\n";
        return mostDemand;
    }
    bool Add(string name, int production, Dependency dep)
    {
        if (book.ContainsKey(name)) { return false; }
        Assert.IsNotNull(dep);

        book.Add(name, new Commodity(name, production, dep));
        return true;
    }
    void PrintStat()
    {
        foreach (var item in book)
        {
            //Debug.Log(item.Key + ": " + item.Value.price);
            if (item.Value.dep != null)
            {
                //Debug.Log("Dependencies: " );
                foreach (var depItem in item.Value.dep)
                {
                    //Debug.Log(" -> " + depItem.Key + ": " + depItem.Value);
                }
            }
        }
    }
    // Use this for initialization
    void Init()
    {
        //Debug.Log("Initializing commodities");
        foreach (var item in recipes)
        {
            Dependency dep = new Dependency();
            int prod_rate = item.Value.Key;
            foreach (var field in item.Value.Value)
            {
                dep.Add(field.Key, field.Value);
            }
            if (!Add(item.Key, prod_rate, dep))
            {
                Debug.LogError("Failed to add commodity; duplicate?");
            }
        }
        //PrintStat();
    }
}