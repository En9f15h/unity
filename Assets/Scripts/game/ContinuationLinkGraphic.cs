using UnityEngine;
using UnityEngine.UI;

// Two linked diamonds remain readable even when the reserved slot has no icon.
public sealed class ContinuationLinkGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        float h = Mathf.Min(5, r.height * .4f);
        if (h <= 0 || r.width <= 0) return;
        Vector2 center = r.center;
        float w = Mathf.Min(7, r.width * .1f);
        Line(mesh, new Vector2(r.xMin,center.y), center-new Vector2(w*2,0));
        Diamond(mesh,center-new Vector2(w*.65f,0),w,h);
        Diamond(mesh,center+new Vector2(w*.65f,0),w,h);
        Line(mesh,center+new Vector2(w*2,0),new Vector2(r.xMax,center.y));
    }

    private void Diamond(VertexHelper mesh, Vector2 p, float w, float h)
    {
        Line(mesh,p+Vector2.left*w,p+Vector2.up*h);
        Line(mesh,p+Vector2.up*h,p+Vector2.right*w);
        Line(mesh,p+Vector2.right*w,p+Vector2.down*h);
        Line(mesh,p+Vector2.down*h,p+Vector2.left*w);
    }

    private void Line(VertexHelper mesh, Vector2 a, Vector2 b)
    {
        Vector2 n = new Vector2(-(b-a).y,(b-a).x).normalized * .65f;
        int i=mesh.currentVertCount;
        mesh.AddVert(a-n,color,Vector2.zero); mesh.AddVert(a+n,color,Vector2.zero);
        mesh.AddVert(b+n,color,Vector2.zero); mesh.AddVert(b-n,color,Vector2.zero);
        mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
    }
}
