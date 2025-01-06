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
    protected AgentConfig config;

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
        var AuctionStats = FindFirstObjectByType<AuctionStats>();
        unitsProducedGraph.DrawGraph(AuctionStats.GetProducedData());
        unitsExchangedGraph.DrawGraph(AuctionStats.GetTradesData());
        stockPileGraph.DrawGraph(AuctionStats.GetStocksData());
        professionsGraph.DrawGraph(AuctionStats.GetProfessionData());
        avgPriceGraph.DrawGraph(AuctionStats.GetAvgClearingPriceData());
        resouceConsumtionGraph.DrawGraph(AuctionStats.GetConsumedData());
        //cashGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.cashs.ToList()));
        //totalCapitalGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.capitals.ToList()));

    }
}
