
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;


public class TransactionHistory : List<Transaction>
{
    float min = 0;
    float max = 0;
    float avg = 0;
    public int history_size = 10;
    public float Min() { return min; }
	public float Max() { return max; }
    public new void Add(Transaction t)
    {
        base.Add(t);
  
        float sum = 0;
        int count = 0;
        min = t.price;
        max = t.price;
        for (int i = base.Count-1; i > 0 && i > (base.Count - history_size); i--)
        {
            min = Mathf.Min(base[i].price, min);
            max = Mathf.Max(base[i].price, max);
            sum += base[i].price;
            count++;
        }
        avg = (count == 0) ? 0 : sum / (float)count;
    }
    public void UpdateLast(Transaction t)
    {
        var priceVolume = base[^1].quantity * base[^1].price;
        priceVolume += t.price * t.quantity;
        base[^1].quantity += t.quantity;
        base[^1].price = priceVolume / base[^1].quantity;
    }
}