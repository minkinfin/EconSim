using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Commodity
{
    public string name;

    public List<Recipe> recipes;

    internal List<string> FindInputs()
    {
        return recipes.SelectMany(x => x.materials.Keys).Distinct().ToList();
    }
}
