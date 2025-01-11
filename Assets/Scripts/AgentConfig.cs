using System.Collections.Generic;
using UnityEngine;

public class AgentConfig : MonoBehaviour
{
    [Header("Init")]
    public int seed;
    public float tickInterval = .001f;
    public int initCash = 100;
    public int initStock = 10;
    public int maxStock = 20;

    [Header("Decision Parameters")]
    public float backruptThreshold = 10;
    public int foodConsumptionRate = 1;
    public bool enablePriceFavorability = false;
    public bool buyerBuysOfferPrice = true;
    public int historySize = 10;
    public float idleTaxRate = 0f;
    public float profitMarkup = 1.05f;
    public float lossMarkup = 1.03f;
    public bool randomInitStock = false;
    public int maxStavationDays = 5;
    public bool pauseIfBankrupt = true;

    public List<Commodity2> commodities;

    public Dictionary<string, Commodity2> book;

    public int initCost = 100;

    public void Awake()
    {
        UnityEngine.Random.InitState(seed);

        book = new Dictionary<string, Commodity2>();
        foreach (var item in commodities)
        {
            book.Add(item.name, item);
        }
    }
}
