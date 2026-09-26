using UnityEngine;
using UnityEngine.UI;

// A view of TurnPlanningManager's shared deadline; this component never owns a clock.
[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public sealed class BattleCountdownPresentation : MonoBehaviour
{
    public enum Phase { Sync, Planning, Waiting, Resolving, Ended }

    private Text source, caption, value;
    private Image legacyPanel, progress;
    private RectTransform root;
    private PlanningFocusFrame frame;
    private bool sourceWasEnabled, panelWasEnabled;
    private Phase lastPhase = Phase.Ended;
    private int lastSecond = -1;
    private float pulseStarted = -1f;

    public static BattleCountdownPresentation Ensure(Text text)
    {
        if (text == null) return null;
        return text.GetComponent<BattleCountdownPresentation>() ?? text.gameObject.AddComponent<BattleCountdownPresentation>();
    }

    private void Awake()
    {
        source = GetComponent<Text>();
        sourceWasEnabled = source.enabled;
        source.enabled = false;
        // Only replace a dedicated timer backing, never a shared HUD/container image.
        var parent = source.transform.parent as RectTransform;
        if (parent != null && parent.childCount == 1 &&
            Vector2.Distance(parent.rect.size, source.rectTransform.rect.size) < 1f)
        {
            legacyPanel = parent.GetComponent<Image>();
            if (legacyPanel != null) { panelWasEnabled = legacyPanel.enabled; legacyPanel.enabled = false; }
        }

        var panel = Make<Image>("PhasePresentation", transform, Vector2.zero, Vector2.one);
        root = panel.rectTransform;
        root.offsetMin = new Vector2(4, 4);
        root.offsetMax = new Vector2(-4, -4);
        panel.color = new Color(.025f, .045f, .065f, .92f);
        panel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        caption = Make<Text>("PhaseLabel", root, new Vector2(.08f, .72f), new Vector2(.92f, .94f));
        ConfigureText(caption, 13);
        value = Make<Text>("CountdownValue", root, new Vector2(.08f, .19f), new Vector2(.92f, .78f));
        ConfigureText(value, 38);
        var track = Make<Image>("TimeTrack", root, new Vector2(.14f, .1f), new Vector2(.86f, .125f));
        track.color = new Color(.7f, .8f, .85f, .15f);
        progress = Make<Image>("TimeRemaining", track.transform, Vector2.zero, Vector2.one);
        frame = PlanningFocusFrame.Create(root);
        Present(Phase.Sync);
    }

    public void Present(Phase phase, double remaining = 0, double duration = 1)
    {
        if (root == null) return;
        bool visible = phase != Phase.Ended;
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        int second = Mathf.CeilToInt(Mathf.Max(0f, (float)remaining));
        bool hasCountdown = phase == Phase.Planning || phase == Phase.Waiting;
        bool urgent = phase == Phase.Planning && second <= 5;
        Color accent = urgent ? new Color(.98f, .65f, .3f) :
            phase == Phase.Waiting ? new Color(.58f, .8f, .88f) : new Color(.91f, .77f, .47f);
        if (phase != lastPhase || second != lastSecond)
        {
            caption.text = phase == Phase.Planning ? "PLAN" : phase == Phase.Waiting ? "WAIT" :
                phase == Phase.Resolving ? "RESOLVE" : "SYNC";
            value.text = hasCountdown ? second.ToString() : phase == Phase.Resolving ? ">>" : "...";
            // One short pulse per changing second. No continuous flashing, including at zero.
            pulseStarted = urgent && second > 0 ? Time.unscaledTime : -1f;
            value.rectTransform.localScale = Vector3.one;
        }
        caption.color = accent;
        value.color = urgent ? accent : new Color(.96f, .94f, .88f);
        frame.SetVisual(.55f, accent, false);
        progress.color = accent;
        progress.transform.parent.gameObject.SetActive(hasCountdown);
        float fraction = hasCountdown && duration > 0 ? Mathf.Clamp01((float)(remaining / duration)) : 0;
        progress.rectTransform.anchorMax = new Vector2(fraction, 1);
        lastPhase = phase;
        lastSecond = second;
        if (!visible) pulseStarted = -1f;
    }

    private void LateUpdate()
    {
        if (pulseStarted < 0 || value == null) return;
        float t = Mathf.Clamp01((Time.unscaledTime - pulseStarted) / .22f);
        value.rectTransform.localScale = Vector3.one * (1f + .07f * (1f - t) * (1f - t));
        if (t >= 1) pulseStarted = -1f;
    }

    private void OnDisable()
    {
        pulseStarted = -1f;
        if (value != null) value.rectTransform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (source != null) source.enabled = sourceWasEnabled;
        if (legacyPanel != null) legacyPanel.enabled = panelWasEnabled;
        if (root != null) Destroy(root.gameObject);
    }

    private void ConfigureText(Text text, int size)
    {
        text.font = source.font;
        text.fontStyle = FontStyle.Normal;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = size;
        text.supportRichText = false;
    }

    private static T Make<T>(string name, Transform parent, Vector2 min, Vector2 max) where T : Graphic
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        obj.transform.SetParent(parent, false);
        var graphic = obj.GetComponent<T>();
        graphic.raycastTarget = false;
        graphic.rectTransform.anchorMin = min;
        graphic.rectTransform.anchorMax = max;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
        return graphic;
    }
}
