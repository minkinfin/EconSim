
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using System.Linq;
using System;
using System.Security.Cryptography.X509Certificates;

//Assumes all offers in list are of same commodity
public class OfferList : List<Offer> { 
	public new void RemoveAt(int index)
	{
		int before = base.Count;
		base.RemoveAt(index);
        //if (before != base.Count + 1) 
			//Debug.Log("did not remove trade correctly! before: "+ before + " after: " + base.Count);
    }
    public void Shuffle()
    {
        var count = base.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i) {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = base[i];
            base[i] = base[r];
            base[r] = tmp;
        }
    }
    /*
    public float AverageOfferPrice()
    {
        float totalQuantity = 0;
        float totalPrice = 0;
        foreach (var offer in base)
        {
            totalQuantity += offer.offerQuantity;
            totalPrice += offer.offerPrice;
        }
        if (totalQuantity == 0)
        {
            return 0;
        }
        return totalPrice / totalQuantity;
    }
    */
	public void Print()
	{
		var enumerator = base.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
			item.Print();
		}
	}


}