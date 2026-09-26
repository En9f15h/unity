using UnityEngine;
using UnityEngine.UI;

// One UI mesh: never changes a control's layout, sprite, material or hit area.
[DisallowMultipleComponent]
public sealed class PlanningFocusFrame : MaskableGraphic
{
    private bool pressed;

    public static PlanningFocusFrame Create(Transform parent)
    {
        Transform existing = parent.Find("PlanningFocusFrame");
        if (existing != null && existing.TryGetComponent(out PlanningFocusFrame found)) return found;
        var obj = new GameObject("PlanningFocusFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(PlanningFocusFrame), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<LayoutElement>().ignoreLayout = true;
        var frame = obj.GetComponent<PlanningFocusFrame>();
        frame.rectTransform.anchorMin = Vector2.zero;
        frame.rectTransform.anchorMax = Vector2.one;
        frame.rectTransform.offsetMin = new Vector2(3, 3);
        frame.rectTransform.offsetMax = new Vector2(-3, -3);
        frame.SetVisual(0, Color.white, false);
        return frame;
    }

    public void SetVisual(float amount, Color tint, bool isPressed)
    {
        raycastTarget = false;
        tint.a *= Mathf.Clamp01(amount);
        if (color != tint) color = tint;
        if (pressed != isPressed) { pressed = isPressed; SetVerticesDirty(); }
        if (enabled != (amount > .001f)) enabled = amount > .001f;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0 || r.height <= 0) return;
        float arm = Mathf.Min(pressed ? 17 : 12, Mathf.Min(r.width, r.height) * .25f);
        float width = Mathf.Min(pressed ? 3 : 2, arm);
        Quad(mesh, r.xMin, r.yMin, arm, width);
        Quad(mesh, r.xMin, r.yMin, width, arm);
        Quad(mesh, r.xMax-arm, r.yMin, arm, width);
        Quad(mesh, r.xMax-width, r.yMin, width, arm);
        Quad(mesh, r.xMin, r.yMax-width, arm, width);
        Quad(mesh, r.xMin, r.yMax-arm, width, arm);
        Quad(mesh, r.xMax-arm, r.yMax-width, arm, width);
        Quad(mesh, r.xMax-width, r.yMax-arm, width, arm);
    }

    private void Quad(VertexHelper mesh, float x, float y, float w, float h)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(x,y),color,Vector2.zero);
        mesh.AddVert(new Vector3(x,y+h),color,Vector2.zero);
        mesh.AddVert(new Vector3(x+w,y+h),color,Vector2.zero);
        mesh.AddVert(new Vector3(x+w,y),color,Vector2.zero);
        mesh.AddTriangle(start,start+1,start+2);
        mesh.AddTriangle(start,start+2,start+3);
    }
}
