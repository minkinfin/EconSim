using AYellowpaper.SerializedCollections;
using System;

[Serializable]
public class Recipe
{
    public int productionRate;
    [SerializedDictionary("Name", "Properties")]
    public SerializedDictionary<string, int> materials;
}
