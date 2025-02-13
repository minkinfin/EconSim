using System;
using System.Collections.Generic;

[Serializable]
public class Commodity
{
    public string name;

    public List<Recipe> recipes;

    internal List<string> FindInputs()
    {
    List<string> inputs = new List<string>();
        foreach (var recipe in recipes)
            foreach (var material in recipe.materials.Keys)
                if (!inputs.Contains(material))
                    inputs.Add(material);
        return inputs;
    }
}
