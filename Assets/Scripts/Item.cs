using System;

[Serializable]
public class Item
{
    public Guid Id { get; set; }
    public string Name;
    public int Cost;
    public int ProdRate;

    public Item(string name, int cost, int prodRate)
    {
        Id = Guid.NewGuid();
        Name = name;
        Cost = cost;
        ProdRate = prodRate;
    }
}
