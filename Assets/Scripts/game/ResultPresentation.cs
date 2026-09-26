using UnityEngine;
using UnityEngine.UI;

// Presentation only: ShowResult still decides the result and owns the return action.
[DisallowMultipleComponent]
public sealed class ResultPresentation : MonoBehaviour
{
    private const float Duration = 0.34f;
    private RectTransform root, panel;
    private CanvasGroup panelGroup;
    private Image dim, emblem;
    private Text title;
    private Image[] ornaments;
    private UIShaderFeedback emblemFeedback;
    private float started;
    private bool entering;

    public void Configure(Image backdrop, RectTransform content, Image result, Text heading)
    {
        root = (RectTransform)transform;
        dim = backdrop;
        panel = content;
        emblem = result;
        title = heading;
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panel.GetComponent<Image>().color = new Color(0.055f, 0.07f, 0.09f, 0.97f);

        var caption = new GameObject("ResultCaption", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        caption.transform.SetParent(panel, false);
        caption.font = heading.font;
        caption.fontSize = 17;
        caption.alignment = TextAnchor.MiddleCenter;
        caption.text = "DUEL COMPLETE";
        caption.color = new Color(0.64f, 0.68f, 0.71f);
        caption.raycastTarget = false;
        Place(caption.rectTransform, new Vector2(.5f, .93f), new Vector2(300, 30));
        Place(emblem.rectTransform, new Vector2(.5f, .66f), new Vector2(250, 250));

        ornaments = new[] {
            Bar("TopRule", new Vector2(.5f,.98f), new Vector2(240,2)),
            Bar("BottomRule", new Vector2(.5f,.02f), new Vector2(240,2)),
            Bar("TopLeft", new Vector2(.04f,.90f), new Vector2(2,48)),
            Bar("TopRight", new Vector2(.96f,.90f), new Vector2(2,48)),
            Bar("BottomLeft", new Vector2(.04f,.10f), new Vector2(2,48)),
            Bar("BottomRight", new Vector2(.96f,.10f), new Vector2(2,48)),
            Bar("DividerLeft", new Vector2(.36f,.235f), new Vector2(128,1)),
            Bar("DividerRight", new Vector2(.64f,.235f), new Vector2(128,1)),
            Bar("DividerGem", new Vector2(.5f,.235f), new Vector2(6,6))
        };
        ornaments[8].rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        emblemFeedback = UIShaderFeedback.Ensure(emblem);
    }

    public void Present(bool won, bool draw, bool opponentLeft)
    {
        Color accent = opponentLeft || draw ? new Color(.53f,.72f,.76f)
            : won ? new Color(.90f,.68f,.32f) : new Color(.79f,.39f,.36f);
        title.color = Color.Lerp(Color.white, accent, .45f);
        foreach (var ornament in ornaments)
            ornament.color = new Color(accent.r, accent.g, accent.b, .65f);

        // A draw has no authored emblem: keep its title in the central focal area.
        title.rectTransform.anchorMin = new Vector2(0, draw && !opponentLeft ? .47f : .28f);
        title.rectTransform.anchorMax = new Vector2(1, draw && !opponentLeft ? .65f : .43f);
        title.rectTransform.offsetMin = new Vector2(40, 0);
        title.rectTransform.offsetMax = new Vector2(-40, 0);
        emblemFeedback.SetStyle(Resources.Load<Material>("Combat/Materials/" + (won && !opponentLeft ? "Result_Gold" : "Result_Cool")));
        // One short highlight only; no perpetual flashing of the result screen.
        emblemFeedback.Pulse(.55f);
        started = Time.unscaledTime;
        entering = true;
        ApplyFrame(0);
    }

    private void LateUpdate()
    {
        if (panel == null) return;
        float progress = entering ? Mathf.Clamp01((Time.unscaledTime - started) / Duration) : 1;
        ApplyFrame(progress);
        if (progress >= 1) entering = false;
    }

    private void ApplyFrame(float progress)
    {
        float eased = 1 - Mathf.Pow(1 - progress, 3);
        panelGroup.alpha = eased;
        // The full-screen raycast blocker is independent of the fading panel.
        dim.color = new Color(.015f, .025f, .045f, .52f * eased);
        float fit = Mathf.Min(1, Mathf.Min(Mathf.Max(1, root.rect.width - 48) / panel.sizeDelta.x,
                                          Mathf.Max(1, root.rect.height - 72) / panel.sizeDelta.y));
        panel.localScale = Vector3.one * (fit * Mathf.Lerp(.975f, 1, eased));
        panel.anchoredPosition = Vector2.up * ((1 - eased) * 18 * fit);
    }

    private void OnDisable()
    {
        entering = false;
        if (panel != null) ApplyFrame(1);
    }

    private Image Bar(string label, Vector2 anchor, Vector2 size)
    {
        var image = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(panel, false);
        image.raycastTarget = false;
        Place(image.rectTransform, anchor, size);
        return image;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }
}
