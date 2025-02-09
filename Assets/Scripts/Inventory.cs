using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Inventory
{
    [SerializeField]
    private List<Item> Items;
    private Dictionary<string, Commodity> Book { get; set; }
    AgentConfig config;
    public Inventory(AgentConfig config)
    {
        Items = new List<Item>();
        this.config = config;
    }

    internal void AddItem(string name, int cost, int amount = 1)
    {
        if (amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            Item item = new Item(name, cost);
            Items.Add(item);
        }
    }

    internal List<Item> TakeItems(string name, int amount)
    {
        if(amount <= 0)
            return new List<Item>();

        List<Item> items = GetItems(name);
        List<Item> takenItems = items.Take(amount).ToList();
        Items = Items.Where(x => !takenItems.Select(y => y.Id).Contains(x.Id)).ToList();

        return takenItems;
    }

    internal List<Item> GetItems(string key)
    {
        return Items.Where(x => x.Name == key).ToList();
    }

    internal ItemInfo GetItemInfo(string name)
    {
        List<Item> items = GetItems(name);

        int qty = items.Count;

        int availableSlot = config.capacityPerItem - items.Count;
        int defict = Math.Min(availableSlot, 5);
        return new ItemInfo
        {
            ItemName = name,
            Qty = qty,
            Items = items,
            Deficit = availableSlot
        };
    }

    internal List<ItemInfo> GetItemInfos()
    {
        List<ItemInfo> itemInfos = new List<ItemInfo>();
        List<string> itemNames = Items.Select(x => x.Name).Distinct().ToList();
        foreach (string itemName in itemNames)
        {
            ItemInfo itemInfo = GetItemInfo(itemName);
            itemInfos.Add(itemInfo);
        }

        return itemInfos;
    }

    internal void AddItem(List<Item> items)
    {
        Items.AddRange(items);
    }

    internal void RemoveItem(List<Item> items)
    {
        Items = Items.Where(x => !items.Select(y => y.Id).Contains(x.Id)).ToList();
    }
}
