//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.Assertions;
//using System.Linq;
//using UnityEngine.XR;
//using System;
//using System.Net.WebSockets;

//public class Utilities : MonoBehaviour
//{
//    public static void TransferQuantity(float qty, EconAgent from, EconAgent to)
//    {
//        from.modify_cash(-qty);
//        to.modify_cash(qty);
//    }
//}

//public class ESList : List<float>
//{
//    float avg;
//    AuctionStats comInstance;
//    int maxLength = 100;
//    public ESList()
//    {
//        comInstance = AuctionStats.Instance;
//    }
//    new public void Add(float num)
//    {
//        base.Add(num);
//        if(base.Count > maxLength)
//        {
//            base.RemoveAt(0);
//        }
//    }
//    public float LastHighest(int history)
//    {
//        if (base.Count == 0)
//        {
//            return 0;
//        }
//        var skip = Mathf.Max(0, base.Count - history);
//        var end = Math.Min(history, base.Count);
//        if (skip == end)
//        {
//            return 0;
//        }
//        return base.GetRange(skip, end).Max();
//    }
//    public float LastAverage(int history)
//    {
//        if (base.Count == 0)
//        {
//            return 0;
//        }
//        var skip = Mathf.Max(0, base.Count - history);
//        var end = Math.Min(history, base.Count);
//        return base.GetRange(skip, end).Average();
//    }

//    public float LastSum(int history)
//    {
//        if (base.Count == 0)
//        {
//            return -1;
//        }
//        var skip = Mathf.Max(0, base.Count - history);
//        var end = Math.Min(history, base.Count);
//        if (skip == end)
//        {
//            return -2;
//        }
//        return base.GetRange(skip, end).Sum();
//    }
//}

//public class WeightedRandomPicker<T>
//{
//    private List<(T item, float weight)> items = new List<(T, float)>();
//    private float totalWeight = 0;

//    public void AddItem(T item, float weight)
//    {
//        items.Add((item, weight));
//        totalWeight += weight;
//    }
//    public void Clear()
//    {
//        items.Clear();
//        totalWeight = 0;
//    }
//    public float GetWeight(T t)
//    {
//        foreach (var (item, weight) in items)
//        {
//            if (EqualityComparer<T>.Default.Equals(item, t))
//            {
//                return weight;
//            }
//        }
//        return -1f;
//    }
//    public (T, float) PickRandom()
//    {
//        float randomValue = UnityEngine.Random.Range(0, totalWeight);
//        //Debug.Log("random value: " + randomValue + " total weight: " + totalWeight);
//        float cumulativeWeight = 0;

//        foreach (var item in items)
//        {
//            cumulativeWeight += item.weight;
//            //Debug.Log("randompicker: good: " + item + " cumweight: " + cumulativeWeight);
//            if (randomValue < cumulativeWeight)
//                return item;
//        }

//        return items[items.Count - 1]; // Fallback, should rarely happen
//    }
//    public List<(T item, float weight)> GetItems()
//    {
//        return items;
//    }
//}