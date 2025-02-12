using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LineGraphRenderer : Graphic
{
    public float thickness = 10f;
    public List<int> points;
    public bool center = true;
    public int maxPlot = 50;
    public TextMeshProUGUI label;

    float width;
    float height;
    float maxHeight;
    float unitWidth;
    float unitHeight;

    void Update()
    {
        if (label != null && points.Count > 0)
        {
            float pointValue = points[points.Count - 1];
            if (pointValue % 1 == 0)
            {
                label.SetText(((int)pointValue).ToString());
            }
            else
            {
                label.SetText(pointValue.ToString("0.00"));
            }
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points.Count == 0)
            return;
        if (points.Count == 1){
            points.Add(points[0]);
        }

        //if (points.All(x => x == 0))
        //{
        //    points[points.Count - 1] = 0.1f;
        //}

        width = rectTransform.rect.width;
        height = rectTransform.rect.height;

        unitWidth = width / maxPlot;
        if (maxHeight != 0)
            unitHeight = height / maxHeight;


        for (int i = 0; i < points.Count - 1; i++)
        {
            // Create a line segment between the next two points
            Vector3 point1 = new Vector3(unitWidth * i, unitHeight * points[i], 0);
            Vector3 point2 = new Vector3(unitWidth * (i + 1), unitHeight * points[i + 1], 0);
            CreateLineSegment(point1, point2, vh);

            int index = i * 5;

            // Add the line segment to the triangles array
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index + 3, index + 2, index);

            // These two triangles create the beveled edges
            // between line segments using the end point of
            // the last line segment and the start points of this one
            if (i != 0)
            {
                vh.AddTriangle(index, index - 1, index - 3);
                vh.AddTriangle(index + 1, index - 1, index - 2);
            }
        }
    }

    private void CreateLineSegment(Vector3 point1, Vector3 point2, VertexHelper vh)
    {
        Vector3 offset = center ? (rectTransform.sizeDelta / 2) : Vector2.zero;

        // Create vertex template
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        // Create the start of the segment
        Quaternion point1Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point1, point2) + 90);
        vertex.position = point1Rotation * new Vector3(-thickness / 2, 0);
        vertex.position += point1 - offset;
        vh.AddVert(vertex);
        vertex.position = point1Rotation * new Vector3(thickness / 2, 0);
        vertex.position += point1 - offset;
        vh.AddVert(vertex);

        // Create the end of the segment
        Quaternion point2Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point2, point1) - 90);
        vertex.position = point2Rotation * new Vector3(-thickness / 2, 0);
        vertex.position += point2 - offset;
        vh.AddVert(vertex);
        vertex.position = point2Rotation * new Vector3(thickness / 2, 0);
        vertex.position += point2 - offset;
        vh.AddVert(vertex);

        // Also add the end point
        vertex.position = point2 - offset;


        if (label != null)
        {
            label.rectTransform.localPosition = new Vector3(label.transform.localPosition.x, vertex.position.y, label.transform.localPosition.z);
        }
        vh.AddVert(vertex);
    }

    /// <summary>
    /// Gets the angle that a vertex needs to rotate to face target vertex
    /// </summary>
    /// <param name="vertex">The vertex being rotated</param>
    /// <param name="target">The vertex to rotate towards</param>
    /// <returns>The angle required to rotate vertex towards target</returns>
    private float RotatePointTowards(Vector2 vertex, Vector2 target)
    {
        return Mathf.Atan2(target.y - vertex.y, target.x - vertex.x) * (180 / Mathf.PI);
    }

    public void ShowGraph(List<int> values, float height)
    {
        points = values;
        maxHeight = height;
        SetVerticesDirty();
    }
}
