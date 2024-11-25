using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;

public class Dependency : Dictionary<string, int> { }
public class Commodity
{
    const float defaultPrice = 1;
    public ESList buyers, sellers, bids, offers, avgBidPrice, avgOfferPrice, avgClearingPrice, trades, profits, professions, stocks, cashs, capitals, produced, consumed;

    float avgPrice = 1;
    public float GetAvgPrice(int history) //average of last history size
    {
        var skip = Mathf.Max(0, avgClearingPrice.Count - history);
        avgPrice = avgClearingPrice.Skip(skip).Average();
        return avgPrice;
    }
    public Commodity(string n, int p, Dependency d)
    {
        name = n;
        ProductionRate = p;
        price = defaultPrice;
        dep = d;
        demand = 1;

        buyers = new ESList();
        sellers = new ESList();
        bids = new ESList();
        offers = new ESList();
        trades = new ESList();
        avgOfferPrice = new ESList();
        avgBidPrice = new ESList();
        avgClearingPrice = new ESList();
        profits = new ESList();
        professions = new ESList();
        stocks = new ESList();
        cashs = new ESList();
        capitals = new ESList();
        produced = new ESList();
        consumed = new ESList();
    }
    public void Update(float p, float dem)
    {
        price = p;
        demand = dem;
    }
    public string name { get; private set; }
    public float price { get; private set; } //market price
    public float demand { get; private set; }
    public int ProductionRate { get; private set; }
    public Dependency dep { get; private set; }
}
