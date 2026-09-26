using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HexRadarChartGraphic : MaskableGraphic
{
    private const int AxisCount = 6;

    [Header("Chart Values")]
    [SerializeField] private float[] values = new float[AxisCount];
    [SerializeField] private bool chartVisible = true;

    [Header("Grid")]
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.22f);
    [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.28f);
    [SerializeField] private Color fillColor = new Color(0.35f, 0.8f, 1f, 0.32f);
    [SerializeField] private Color outlineColor = new Color(0.95f, 0.78f, 0.25f, 0.92f);
    [SerializeField] private Color markerColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private float padding = 8f;
    [SerializeField] private float gridLineThickness = 1.5f;
    [SerializeField] private float axisLineThickness = 1.2f;
    [SerializeField] private float outlineThickness = 2.5f;
    [SerializeField] private float markerSize = 5f;

    public void SetValues(float vitality, float attack, float defense, float mobility, float range, float prediction)
    {
        values[0] = vitality;
        values[1] = attack;
        values[2] = defense;
        values[3] = mobility;
        values[4] = range;
        values[5] = prediction;
        SetVerticesDirty();
    }

    public void SetValues(IReadOnlyList<float> newValues)
    {
        if (newValues == null || newValues.Count != AxisCount)
        {
            Debug.LogWarning("HexRadarChartGraphic: values list must contain exactly six entries.", this);
            ClearValues();
            return;
        }

        for (int i = 0; i < AxisCount; i++)
            values[i] = newValues[i];

        SetVerticesDirty();
    }

    public void ClearValues()
    {
        for (int i = 0; i < AxisCount; i++)
            values[i] = 0f;

        SetVerticesDirty();
    }

    public void SetChartVisible(bool visible)
    {
        chartVisible = visible;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (!chartVisible)
            return;

        Rect rect = rectTransform.rect;
        float baseRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float radius = Mathf.Max(0f, baseRadius - Mathf.Max(0f, padding));
        if (radius <= 0f)
            return;

        Vector2 center = rect.center;
        Vector2[] unitPoints = BuildUnitPoints();

        for (int level = 1; level <= 5; level++)
        {
            float levelRadius = radius * (level / 5f);
            DrawPolygonLine(vh, center, unitPoints, levelRadius, gridLineThickness, gridColor);
        }

        for (int i = 0; i < AxisCount; i++)
            AddLine(vh, center, center + unitPoints[i] * radius, axisLineThickness, axisColor);

        Vector2[] statPoints = new Vector2[AxisCount];
        for (int i = 0; i < AxisCount; i++)
        {
            float normalized = Mathf.Clamp01(values[i] / 5f);
            statPoints[i] = center + unitPoints[i] * radius * normalized;
        }

        AddFilledPolygon(vh, center, statPoints, fillColor);
        DrawPointLine(vh, statPoints, outlineThickness, outlineColor);

        for (int i = 0; i < AxisCount; i++)
            AddQuad(vh, statPoints[i], new Vector2(markerSize, markerSize), markerColor);
    }

    private Vector2[] BuildUnitPoints()
    {
        Vector2[] points = new Vector2[AxisCount];
        for (int i = 0; i < AxisCount; i++)
        {
            float angle = Mathf.Deg2Rad * (90f - i * 60f);
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        return points;
    }

    private void DrawPolygonLine(VertexHelper vh, Vector2 center, Vector2[] unitPoints, float radius, float thickness, Color lineColor)
    {
        Vector2[] points = new Vector2[unitPoints.Length];
        for (int i = 0; i < unitPoints.Length; i++)
            points[i] = center + unitPoints[i] * radius;

        DrawPointLine(vh, points, thickness, lineColor);
    }

    private void DrawPointLine(VertexHelper vh, Vector2[] points, float thickness, Color lineColor)
    {
        if (points == null || points.Length < 2)
            return;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            AddLine(vh, start, end, thickness, lineColor);
        }
    }

    private void AddFilledPolygon(VertexHelper vh, Vector2 center, Vector2[] points, Color polygonColor)
    {
        int centerIndex = AddVertex(vh, center, polygonColor);
        for (int i = 0; i < points.Length; i++)
        {
            int a = AddVertex(vh, points[i], polygonColor);
            int b = AddVertex(vh, points[(i + 1) % points.Length], polygonColor);
            vh.AddTriangle(centerIndex, a, b);
        }
    }

    private void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color lineColor)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * Mathf.Max(0.1f, thickness) * 0.5f;
        int index = vh.currentVertCount;

        AddVertex(vh, start - normal, lineColor);
        AddVertex(vh, start + normal, lineColor);
        AddVertex(vh, end + normal, lineColor);
        AddVertex(vh, end - normal, lineColor);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private void AddQuad(VertexHelper vh, Vector2 center, Vector2 size, Color quadColor)
    {
        Vector2 half = size * 0.5f;
        int index = vh.currentVertCount;

        AddVertex(vh, center + new Vector2(-half.x, -half.y), quadColor);
        AddVertex(vh, center + new Vector2(-half.x, half.y), quadColor);
        AddVertex(vh, center + new Vector2(half.x, half.y), quadColor);
        AddVertex(vh, center + new Vector2(half.x, -half.y), quadColor);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private int AddVertex(VertexHelper vh, Vector2 position, Color vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vh.AddVert(vertex);
        return vh.currentVertCount - 1;
    }

    protected new void OnValidate()
    {
        if (values == null || values.Length != AxisCount)
            values = new float[AxisCount];

        padding = Mathf.Max(0f, padding);
        gridLineThickness = Mathf.Max(0.1f, gridLineThickness);
        axisLineThickness = Mathf.Max(0.1f, axisLineThickness);
        outlineThickness = Mathf.Max(0.1f, outlineThickness);
        markerSize = Mathf.Max(0.1f, markerSize);
        SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }
}
