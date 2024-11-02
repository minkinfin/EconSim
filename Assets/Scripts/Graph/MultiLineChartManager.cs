using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class MultiLineChartManager : MonoBehaviour
{
    Dictionary<string, LineGraphRenderer> lineGraphRenderers;
    public int maxPlot = 50;
    public TextMeshProUGUI title;
    void Awake()
    {
        lineGraphRenderers = new Dictionary<string, LineGraphRenderer>();
        title.SetText(name);
    }

    public void Init(AuctionStats stats, Dictionary<string, Material> commColors)
    {
        for (int i = 0; i < stats.book.Count; i++)
        {
            string key = stats.book.ElementAt(i).Key;
            var lineGraphRenderer = transform.Find("Line" + i).GetComponent<LineGraphRenderer>();
            lineGraphRenderer.color = commColors[key].color;
            lineGraphRenderer.maxPlot = maxPlot;
            lineGraphRenderers.Add(key, lineGraphRenderer);
        }
    }

    public void DrawGraph(Dictionary<string, List<float>> graphData)
    {
        float maxHeight = graphData.Values.SelectMany(x => x.Count > maxPlot ? x.Skip(x.Count - maxPlot).ToList() : x).Max();
        for (int i = 0; i < graphData.Count; i++)
        {
            string key = graphData.ElementAt(i).Key;
            List<float> data = graphData[key];
            if (data.Count > maxPlot)
                data = data.Skip(data.Count - maxPlot).ToList();
            lineGraphRenderers[key].ShowGraph(data, maxHeight);
        }
    }
}
