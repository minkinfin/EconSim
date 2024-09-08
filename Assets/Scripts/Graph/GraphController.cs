using AYellowpaper.SerializedCollections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GraphController : MonoBehaviour
{
    private LineGraphRenderer lineGraphRenderer;
    public AuctionStats AuctionStats;

    public MultiLineChartManager avgPriceGraph;
    public MultiLineChartManager unitsExchangedGraph;
    public MultiLineChartManager professionsGraph;
    public MultiLineChartManager stockPileGraph;
    public MultiLineChartManager cashGraph;
    public MultiLineChartManager totalCapitalGraph;

    float tickInterval = .05f;
    float tick = 0;

    [SerializedDictionary("Comm", "Color")]
    public SerializedDictionary<string, Material> commColor;

    private Dictionary<string, List<GraphMe>> graphs;

    private void Start()
    {
        avgPriceGraph.Init(AuctionStats, commColor);
        unitsExchangedGraph.Init(AuctionStats, commColor);
        professionsGraph.Init(AuctionStats, commColor);
        stockPileGraph.Init(AuctionStats, commColor);
        cashGraph.Init(AuctionStats, commColor);
        totalCapitalGraph.Init(AuctionStats, commColor);
    }

    private void FixedUpdate()
    {
        if (tick > tickInterval)
        {
            var book = AuctionStats.book;
            avgPriceGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.avgClearingPrice.ToList()));
            unitsExchangedGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.trades.ToList()));
            professionsGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.professions.ToList()));
            stockPileGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.stocks.ToList()));
            cashGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.cashs.ToList()));
            totalCapitalGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.capitals.ToList()));

            tick = 0;
        }

        tick += Time.deltaTime;
    }
}
