using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

public class Dependency : Dictionary<string, float> { }
public class Commodity
{
    const float defaultPrice = 1;
    public ESList buyers, sellers, bids, asks, avgBidPrice, avgAskPrice, avgClearingPrice, trades, profits, professions, stocks, cashs, capitals;

    float avgPrice = 1;
    public float GetAvgPrice(int history) //average of last history size
    {
        var skip = Mathf.Max(0, avgClearingPrice.Count - history);
        avgPrice = avgClearingPrice.Skip(skip).Average();
        return avgPrice;
    }
    public Commodity(string n, float p, Dependency d)
    {
        name = n;
        production = p;
        price = defaultPrice;
        dep = d;
        demand = 1;

        buyers = new ESList { 1 };
        sellers = new ESList { 1 };
        bids = new ESList { 1 };
        asks = new ESList { 1 };
        trades = new ESList { 1 };
        avgAskPrice = new ESList { 1 };
        avgBidPrice = new ESList { 1 };
        avgClearingPrice = new ESList { 1 };
        profits = new ESList { 1 };
        professions = new ESList { 1 };
        stocks = new ESList { 1 };
        cashs = new ESList { 1 };
        capitals = new ESList { 1 };
    }
    public void Update(float p, float dem)
    {
        price = p;
        demand = dem;
    }
    public string name { get; private set; }
    public float price { get; private set; } //market price
    public float demand { get; private set; }
    public float production { get; private set; }
    public Dependency dep { get; private set; }
}