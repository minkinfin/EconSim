using System;

[Serializable]
public class Item
{
    public Guid Id { get; set; }
    public string Name;
    public int Cost;

    public Item(string name, int cost)
    {
        Id = Guid.NewGuid();
        Name = name;
        Cost = cost;
    }
}
