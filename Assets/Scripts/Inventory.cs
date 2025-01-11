using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Inventory
{
    [SerializeField]
    private List<Item> Items;
    private Dictionary<string, Commodity2> Book { get; set; }

    private int capacityPerItem = 4;

    public Inventory(Dictionary<string, Commodity2> book)
    {
        Items = new List<Item>();
        Book = book;
    }

    internal void AddItem(string name, int cost, int amount = 1)
    {
        for (int i = 0; i < amount; i++)
        {
            Item item = new Item(name, cost);
            Items.Add(item);
        }
    }

    internal List<Item> TakeItems(string name, int amount)
    {
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

        int quantity = items.Count;

        int availableSlot = capacityPerItem - items.Count;
        //int defict = Math.Min(availableSlot, 3);
        return new ItemInfo
        {
            ItemName = name,
            Quantity = quantity,
            ProductionRate = Book[name].productionRate,
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
}
