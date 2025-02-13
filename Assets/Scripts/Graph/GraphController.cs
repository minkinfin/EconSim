using AYellowpaper.SerializedCollections;
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
        unitsProducedGraph.DrawGraph(AuctionStats.GetProducedData(unitsProducedGraph.maxPlot));
        unitsExchangedGraph.DrawGraph(AuctionStats.GetTradesData(unitsExchangedGraph.maxPlot));
        stockPileGraph.DrawGraph(AuctionStats.GetStocksData(stockPileGraph.maxPlot));
        professionsGraph.DrawGraph(AuctionStats.GetProfessionData(professionsGraph.maxPlot));
        avgPriceGraph.DrawGraph(AuctionStats.GetAvgClearingPriceData(avgPriceGraph.maxPlot));
        resouceConsumtionGraph.DrawGraph(AuctionStats.GetConsumedData(resouceConsumtionGraph.maxPlot));
        //cashGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.cashs.ToList()));
        //totalCapitalGraph.DrawGraph(book.ToDictionary(x => x.Key, x => x.Value.capitals.ToList()));
    }
}
