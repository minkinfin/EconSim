using System.Collections.Generic;
using UnityEngine;

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
        if (config == null)
            config = GetComponent<AgentConfig>();
        foreach (var item in record)
        {
            if (!auctionRecords.ContainsKey(item.Key))
                auctionRecords.Add(item.Key, new List<AuctionRecord>());

            auctionRecords[item.Key].Add(item.Value);
            if (auctionRecords[item.Key].Count > config.MaxRecordedTransactions)
                auctionRecords[item.Key].RemoveAt(0);
        }
    }

    internal int GetAvgClearingPrice(string itemNamem, int num)
    {
        if (!auctionRecords.ContainsKey(itemNamem))
            return 0;

        var records = auctionRecords[itemNamem];
        num = Mathf.Min(num, records.Count);
        if (num == 0)
            return 0;
        int skipCount = Mathf.Max(0, auctionRecords[itemNamem].Count - num);
        int sum = 0;
        for (int i = skipCount; i < records.Count; i++)
        {
            sum += records[i].ClearingPrice;
        }

        return sum / num;
    }

    internal int GetLastClearingPrice(string itemName)
    {
        if (!auctionRecords.ContainsKey(itemName) || auctionRecords[itemName].Count == 0)
            return 0;

        return auctionRecords[itemName][auctionRecords[itemName].Count - 1].ClearingPrice;
    }

    internal int GetLastTradeQuantity(string name)
    {
        if (!auctionRecords.ContainsKey(name) || auctionRecords[name].Count == 0)
            return 0;

        return auctionRecords[name][auctionRecords[name].Count - 1].Trades;
    }

    internal Dictionary<string, List<int>> GetProducedData(int num)
    {
        var data = new Dictionary<string, List<int>>();
        foreach (var item in auctionRecords)
        {
            int skipCount = Mathf.Max(0, item.Value.Count - num);
            data.Add(item.Key, new List<int>());
            for (int i = skipCount; i < item.Value.Count; i++)
            {
                data[item.Key].Add(item.Value[i].Produced);
            }
        }
        return data;
    }

    internal Dictionary<string, List<int>> GetTradesData(int num)
    {
        var data = new Dictionary<string, List<int>>();
        foreach (var item in auctionRecords)
        {
            int skipCount = Mathf.Max(0, item.Value.Count - num);
            data.Add(item.Key, new List<int>());
            for (int i = skipCount; i < item.Value.Count; i++)
            {
                data[item.Key].Add(item.Value[i].Trades);
            }
        }
        return data;
    }

    internal Dictionary<string, List<int>> GetStocksData(int num)
    {
        var data = new Dictionary<string, List<int>>();
        foreach (var item in auctionRecords)
        {
            int skipCount = Mathf.Max(0, item.Value.Count - num);
            data.Add(item.Key, new List<int>());
            for (int i = skipCount; i < item.Value.Count; i++)
            {
                data[item.Key].Add(item.Value[i].Stocks);
            }
        }
        return data;
    }

    internal Dictionary<string, List<int>> GetProfessionData(int num)
    {
        var data = new Dictionary<string, List<int>>();
        foreach (var item in auctionRecords)
        {
            int skipCount = Mathf.Max(0, item.Value.Count - num);
            data.Add(item.Key, new List<int>());
            for (int i = skipCount; i < item.Value.Count; i++)
            {
                data[item.Key].Add(item.Value[i].Professions);
            }
        }
        return data;
    }

    internal Dictionary<string, List<int>> GetAvgClearingPriceData(int num)
    {
        var data = new Dictionary<string, List<int>>();
        foreach (var item in auctionRecords)
        {
            int skipCount = Mathf.Max(0, item.Value.Count - num);
            data.Add(item.Key, new List<int>());
            for (int i = skipCount; i < item.Value.Count; i++)
            {
                data[item.Key].Add(item.Value[i].ClearingPrice);
            }
        }
        return data;
    }

    internal Dictionary<string, List<int>> GetConsumedData(int num)
    {
        var data = new Dictionary<string, List<int>>();
        foreach (var item in auctionRecords)
        {
            int skipCount = Mathf.Max(0, item.Value.Count - num);
            data.Add(item.Key, new List<int>());
            for (int i = skipCount; i < item.Value.Count; i++)
            {
                data[item.Key].Add(item.Value[i].Consumed);
            }
        }
        return data;
    }
}
