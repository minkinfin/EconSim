using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MultiLineChartManager : MonoBehaviour
{
    Dictionary<string, LineGraphRenderer> lineGraphRenderers;
    public int maxPlot = 50;
    public TextMeshProUGUI title;
    protected AgentConfig config;

    void Awake()
    {
        config = FindFirstObjectByType<AgentConfig>();
        lineGraphRenderers = new Dictionary<string, LineGraphRenderer>();
        title.SetText(name);
    }

    public void Init(AuctionStats stats, Dictionary<string, Material> commColors)
    {
        if (lineGraphRenderers == null)
            lineGraphRenderers = new Dictionary<string, LineGraphRenderer>();

        for (int i = 0; i < config.commodities.Count; i++)
        {
            string itemName = config.commodities[i].name;
            var lineGraphRenderer = transform.Find("Line" + i).GetComponent<LineGraphRenderer>();
            lineGraphRenderer.color = commColors[itemName].color;
            lineGraphRenderer.maxPlot = maxPlot;
            lineGraphRenderers.Add(itemName, lineGraphRenderer);
        }
    }

    public void DrawGraph(Dictionary<string, List<int>> graphData)
    {
        int maxHeight = 1;

        foreach (var values in graphData.Values)
            foreach (var value in values)
                maxHeight = Mathf.Max(maxHeight, value);

        foreach (var obj in graphData)
        {
            lineGraphRenderers[obj.Key].ShowGraph(obj.Value, maxHeight);
        }
    }
}
