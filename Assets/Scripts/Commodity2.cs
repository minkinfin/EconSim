using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;

[Serializable]
public class Commodity2
{
    public string name;
    public int productionRate;

    [SerializedDictionary("Name", "Properties")]
    public SerializedDictionary<string, int> recipes;
}