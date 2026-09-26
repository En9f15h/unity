using UnityEngine;
using UnityEngine.UI;

// Displays the action that owns this reserved slot; never creates a draggable item.
[DisallowMultipleComponent]
public sealed class ActionContinuationPresentation : MonoBehaviour
{
    private GameObject root;
    private Image echo;

    public static void Refresh(ActionSlot slot, bool occupied, Sprite sprite)
    {
        var view = slot.GetComponent<ActionContinuationPresentation>();
        if (view == null && !occupied) return;
        if (view == null) view = slot.gameObject.AddComponent<ActionContinuationPresentation>();
        if (view.root == null) view.Build();
        view.echo.sprite = sprite;
        view.echo.enabled = sprite != null;
        view.root.SetActive(occupied);
    }

    private void Build()
    {
        root = new GameObject("ContinuationPresentation", typeof(RectTransform), typeof(LayoutElement));
        root.transform.SetParent(transform, false);
        root.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)root.transform;
        Fit(rect, Vector2.zero, Vector2.one);
        rect.offsetMin = new Vector2(6, 6);
        rect.offsetMax = new Vector2(-6, -6);

        var fill = Image("MutedFill", Vector2.zero, Vector2.one);
        fill.color = new Color(.035f, .055f, .085f, .76f);
        echo = Image("EchoIcon", new Vector2(.14f,.22f), new Vector2(.86f,.94f));
        echo.preserveAspect = true;
        echo.color = new Color(.72f, .79f, .83f, .62f);

        var link = new GameObject("LinkMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(ContinuationLinkGraphic));
        link.transform.SetParent(root.transform, false);
        Fit((RectTransform)link.transform, new Vector2(.06f,.03f), new Vector2(.94f,.18f));
        var graphic = link.GetComponent<ContinuationLinkGraphic>();
        graphic.raycastTarget = false;
        graphic.color = new Color(.56f,.75f,.8f,.85f);
    }

    private Image Image(string label, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(root.transform, false);
        Fit((RectTransform)obj.transform, min, max);
        var image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Fit(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
