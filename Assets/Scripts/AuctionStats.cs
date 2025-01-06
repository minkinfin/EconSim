using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AuctionStats : MonoBehaviour
{
    private Dictionary<string, List<AuctionRecord>> auctionRecords;
    protected AgentConfig config;

    public void Awake()
    {
        auctionRecords = new Dictionary<string, List<AuctionRecord>>();
    }
    public void Start()
    {
        config = GetComponent<AgentConfig>();
    }

    public void AddRecord(Dictionary<string, AuctionRecord> record)
    {
        foreach (var item in record)
        {
            if (!auctionRecords.ContainsKey(item.Key))
                auctionRecords.Add(item.Key, new List<AuctionRecord>());

            auctionRecords[item.Key].Add(item.Value);
        }
    }

    internal float GetAvgClearingPrice(string itemNamem, int v)
    {
        if (!auctionRecords.ContainsKey(itemNamem) || auctionRecords[itemNamem].Count == 0)
            return 0;

        return auctionRecords[itemNamem].Skip(Mathf.Max(0, auctionRecords[itemNamem].Count - v)).Average(x => x.AvgClearingPrice);
    }

    internal float GetLastClearingPrice(string itemName)
    {
        if (!auctionRecords.ContainsKey(itemName) || auctionRecords[itemName].Count == 0)
            return 0;

        return auctionRecords[itemName].Last().AvgClearingPrice;
    }

    internal int GetLastTradeQuantity(string name)
    {
        if (!auctionRecords.ContainsKey(name) || auctionRecords[name].Count == 0)
            return 0;

        return auctionRecords[name].Last().Trades;
    }

    internal Dictionary<string, List<float>> GetProducedData()
    {
        var data = new Dictionary<string, List<float>>();
        foreach (var item in auctionRecords)
        {
            data.Add(item.Key, item.Value.Select(x => (float)x.Produced).ToList());
        }
        return data;
    }

    internal Dictionary<string, List<float>> GetTradesData()
    {
        var data = new Dictionary<string, List<float>>();
        foreach (var item in auctionRecords)
        {
            data.Add(item.Key, item.Value.Select(x => (float)x.Trades).ToList());
        }
        return data;
    }

    internal Dictionary<string, List<float>> GetStocksData()
    {
        var data = new Dictionary<string, List<float>>();
        foreach (var item in auctionRecords)
        {
            data.Add(item.Key, item.Value.Select(x => x.Stocks).ToList());
        }
        return data;
    }

    internal Dictionary<string, List<float>> GetProfessionData()
    {
        var data = new Dictionary<string, List<float>>();
        foreach (var item in auctionRecords)
        {
            data.Add(item.Key, item.Value.Select(x => (float)x.Professions).ToList());
        }
        return data;
    }

    internal Dictionary<string, List<float>> GetAvgClearingPriceData()
    {
        var data = new Dictionary<string, List<float>>();
        foreach (var item in auctionRecords)
        {
            data.Add(item.Key, item.Value.Select(x => x.AvgClearingPrice).ToList());
        }
        return data;
    }

    internal Dictionary<string, List<float>> GetConsumedData()
    {
        var data = new Dictionary<string, List<float>>();
        foreach (var item in auctionRecords)
        {
            data.Add(item.Key, item.Value.Select(x => (float)x.Consumed).ToList());
        }
        return data;
    }
}
