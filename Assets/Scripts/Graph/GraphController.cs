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

    public MultiLineChartManager unitsProducedGraph;
    public MultiLineChartManager unitsExchangedGraph;
    public MultiLineChartManager stockPileGraph;
    public MultiLineChartManager professionsGraph;
    public MultiLineChartManager avgPriceGraph;
    public MultiLineChartManager resouceConsumtionGraph;
    //public MultiLineChartManager cashGraph;
    //public MultiLineChartManager totalCapitalGraph;

    float tickInterval = .05f;
    float tick = 0;

    [SerializedDictionary("Comm", "Color")]
    public SerializedDictionary<string, Material> commColor;

    private Dictionary<string, List<GraphMe>> graphs;

    private void Start()
    {
        unitsProducedGraph.Init(AuctionStats, commColor);
        unitsExchangedGraph.Init(AuctionStats, commColor);
        stockPileGraph.Init(AuctionStats, commColor);
        professionsGraph.Init(AuctionStats, commColor);
        avgPriceGraph.Init(AuctionStats, commColor);
        resouceConsumtionGraph.Init(AuctionStats, commColor);
        //cashGraph.Init(AuctionStats, commColor);
        //totalCapitalGraph.Init(AuctionStats, commColor);
    }

    private void FixedUpdate()
    {
        if (tick > tickInterval)
        {
            Tick();
            tick = 0;
        }

        tick += Time.deltaTime;
    }

    public void Tick()
    {
        var book = AuctionStats.book;
        unitsProducedGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.produced.ToList()));
        unitsExchangedGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.trades.ToList()));
        stockPileGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.stocks.ToList()));
        professionsGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.professions.ToList()));
        avgPriceGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.avgClearingPrice.ToList()));
        resouceConsumtionGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.consumed.ToList()));
        //cashGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.cashs.ToList()));
        //totalCapitalGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.capitals.ToList()));

    }
}
