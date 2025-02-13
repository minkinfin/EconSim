using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using static UnityEditor.Progress;

[Serializable]
public class Inventory
{
    [SerializeField]
    private SerializedDictionary<string, List<Item>> Items;
    private Dictionary<string, Commodity> Book { get; set; }
    AgentConfig config;
    public Inventory(AgentConfig config)
    {
        Items = new SerializedDictionary<string, List<Item>>();
        this.config = config;
    }

    internal void AddItem(string name, int cost, int amount = 1)
    {
        if (amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            Item item = new Item(name, cost);
            if (!Items.ContainsKey(name))
                Items.Add(name, new List<Item>());
            Items[name].Add(item);
        }
    }

    internal void AddItem(List<Item> items)
    {
        foreach (var item in items)
        {
            if (!Items.ContainsKey(item.Name))
                Items.Add(item.Name, new List<Item>());
            Items[item.Name].Add(item);
        }
    }

    internal List<Item> TakeItems(string name, int amount)
    {
        List<Item> results = new List<Item>();
        if (!Items.ContainsKey(name))
            return results;

        amount = Math.Min(amount, Items[name].Count);
        if (amount <= 0)
            return results;

        for (int i = 0; i < amount; i++)
        {
            results.Add(Items[name][0]);
            Items[name].RemoveAt(0);
        }

        return results;
    }

    internal ItemInfo GetItemInfo(string name)
    {
        if (!Items.ContainsKey(name))
            Items.Add(name, new List<Item>());
        List<Item> items = Items[name];
        int availableSlot = config.capacityPerItem - items.Count;
        int defict = Math.Min(availableSlot, 5);
        return new ItemInfo
        {
            ItemName = name,
            Qty = items.Count,
            Items = items,
            Deficit = availableSlot
        };
    }

    internal List<ItemInfo> GetItemInfos()
    {
        List<ItemInfo> itemInfos = new List<ItemInfo>();
        foreach (string itemName in Items.Keys)
        {
            ItemInfo itemInfo = GetItemInfo(itemName);
            itemInfos.Add(itemInfo);
        }

        return itemInfos;
    }

    internal void RemoveItem(List<Item> items)
    {
        foreach (var item in items)
        {
            if (!Items.ContainsKey(item.Name))
                Items.Add(item.Name, new List<Item>());
            int index = Items[item.Name].FindIndex(x => x.Id == item.Id);
            if (index >= 0)
                Items[item.Name].RemoveAt(index);
        }
    }
}
