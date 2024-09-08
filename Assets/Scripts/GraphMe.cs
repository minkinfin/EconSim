using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;
using System;
using UnityEngine.UI;
using UnityEngine.Windows;

public class GraphMe : MonoBehaviour
{
    LineRenderer line;
    List<float> inputs = new List<float>();
    Vector2 lMin = new Vector2();
    Vector2 lMax = new Vector2();
    //physical min/max
    GraphLead gl;
    // Use this for initialization
    List<GameObject> lines = new List<GameObject>();    
    void Start()
    {
        inputs.Add(0);
        gl = transform.parent.GetComponent<GraphLead>();

        lMin = Vector2.zero;
        lMax = new Vector2(gl.maxPoints, 10);

        Assert.IsNotNull(gl);
        line = GetComponent<LineRenderer>();
        line.positionCount = gl.maxPoints;
    }

    // Update is called once per frame
    public void Tick(float input)
    {
        if (input == -42)
        {
            if (inputs.Count > 0)
                input = inputs[inputs.Count - 1];
        }
        if (inputs.Count >= gl.maxPoints)
        {
            inputs.RemoveAt(0);
            inputs.Add(input);
            //scan for max point
            lMax.y = inputs.Max();
            lMin.y = inputs.Min();
            gl.ResetYBounds();

            lMin.x += 1;
            lMax.x += 1;
        }
        else
        {
            lMax.y = Mathf.Max(lMax.y, input);
            lMin.y = Mathf.Min(lMin.y, input);
            inputs.Add(input);
        }
        gl.UpdateLBounds(lMin, lMax);

        //if (gameObject.transform.parent.name == "AvgPriceGraph" && gameObject.name == "line0")
        //{
        //    GameObject line = new GameObject("line", typeof(Image));
        //    line.transform.SetParent(GetComponent<RectTransform>(), true);
        //    var rect = line.GetComponent<RectTransform>();
        //    //rect.localPosition = new Vector2(0, 0);
        //    //rect.position = new Vector2(0, 0);
        //    //rect.anchorMin = new Vector2(0, 0);
        //    //rect.anchorMax = new Vector2(0, 0);
        //    rect.sizeDelta = new Vector2(10, 10);
        //    rect.anchoredPosition = lMax + new Vector2(0, input*10 );
        //    lines.Add(line);
        //}
        for (int i = 0; i < lines.Count; i++)
        {
            lines[i].transform.position = lines[i].transform.position - new Vector3(2, 0, 0);
            if (lines.Count > gl.maxPoints)
            {
                Destroy(lines[0]);
                lines.RemoveAt(0);
            }
        }
    }
    public void LateUpdate()
    {
        //while (transform.childCount != 0)
        //{
        //    Destroy(transform.GetChild(0));
        //}

        for (int i = 0; i < inputs.Count; i++)
        {
            line.SetPosition(i, gl.L2PC_RelX(i, inputs[i]));

            //if (gameObject.transform.parent.name == "AvgPriceGraph" && gameObject.name == "line0")
            //{
            //    GameObject line = new GameObject("line", typeof(Image));
            //    line.transform.SetParent(GetComponent<RectTransform>(), true);
            //    var rect = line.GetComponent<RectTransform>();
            //    //rect.localPosition = new Vector2(0, 0);
            //    //rect.position = new Vector2(0, 0);
            //    rect.anchorMin = new Vector2(0, 0);
            //    rect.anchorMax = new Vector2(0, 0);
            //    rect.sizeDelta = new Vector2(10, 10);
            //    rect.anchoredPosition = new Vector2(i, inputs[i]);
            //}
        }
        int x = inputs.Count - 1;
        var lastPos = gl.L2PC_RelX(x, inputs[inputs.Count - 1]);
        line.SetPosition(x, lastPos);
        //set all points after
        for (int i = x + 1; i < gl.maxPoints; i++)
        {
            line.SetPosition(i, lastPos);
        }

        //set text
    }
}
