using System;

[Serializable]
public class Item
{
    public Guid Id { get; set; }
    public string Name;
    public float Cost;

    public Item(string name, float cost)
    {
        Id = Guid.NewGuid();
        Name = name;
        Cost = cost;
    }
}
