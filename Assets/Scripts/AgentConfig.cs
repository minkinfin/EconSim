using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using AYellowpaper.SerializedCollections;

public class AgentConfig : MonoBehaviour
{
    [Header("Init")]
    public int seed;
    public float tickInterval = .001f;
    public float initCash = 100;
    public int initStock = 10;
    public int maxStock = 20;

    [Header("Decision Parameters")]
    public float backruptThreshold = 10;
    public int foodConsumptionRate = 1;
    public int maxStavationDays = 5;
    public bool onlyBuyWhatsAffordable = false;
    public bool enablePriceFavorability = false;
    public bool buyerBuysOfferPrice = true;
    public ChangeProductionMode changeProductionMode;
    public int historySize = 10;
    public float idleTaxRate = 0f;
    public float profitMarkup = 1.05f;
    public bool randomInitStock = false;
    public bool starvation = false;
    public float minMaxPriceBeliefOffset = 2f;

    public void Start()
    {
        UnityEngine.Random.InitState(seed);
    }
}

public enum ChangeProductionMode
{
    HighestBidPrice,
    ProbabilisticHottestGood
}