using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Phase = BattleCountdownPresentation.Phase;

// Only presents manager state. The original Button owns every click and submission.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class ReadyButtonPresentation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private Button button;
    private PlanningFocusFrame frame;
    private Image badge;
    private Text title, detail;
    private Phase phase = Phase.Sync;
    private bool hovered, selected, pressed;
    private float focus, confirmedAt = -1000f;

    public static ReadyButtonPresentation Ensure(Button owner, Font font)
    {
        if (owner == null) return null;
        var view = owner.GetComponent<ReadyButtonPresentation>() ?? owner.gameObject.AddComponent<ReadyButtonPresentation>();
        if (font != null && view.title != null && view.title.font != font) view.title.font = view.detail.font = font;
        return view;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        badge = Make<Image>("ReadyStateBadge", transform, new Vector2(.2f, .16f), new Vector2(.8f, .84f));
        badge.color = new Color(.025f, .045f, .065f, 1);
        badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        title = Make<Text>("StateTitle", badge.transform, new Vector2(.04f, .4f), new Vector2(.96f, .94f));
        detail = Make<Text>("StateDetail", badge.transform, new Vector2(.04f, .08f), new Vector2(.96f, .4f));
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ConfigureText(title, font, 20);
        ConfigureText(detail, font, 11);
        frame = PlanningFocusFrame.Create(transform);
        Present(Phase.Sync);
    }

    public void Present(Phase next)
    {
        if (badge == null) return;
        if (phase != next)
        {
            confirmedAt = phase == Phase.Planning && next == Phase.Waiting ? Time.unscaledTime : -1000f;
            ResetFocus(false);
        }
        phase = next;
        bool showBadge = phase != Phase.Planning && phase != Phase.Ended;
        if (badge.gameObject.activeSelf != showBadge) badge.gameObject.SetActive(showBadge);
        title.text = phase == Phase.Waiting ? "WAITING" : phase == Phase.Resolving ? "RESOLVING" : "SYNC";
        detail.text = phase == Phase.Waiting ? "PLAN LOCKED" : phase == Phase.Resolving ? "ACTIONS IN PLAY" : "GETTING READY";
        Apply();
    }

    private bool CanFocus => phase == Phase.Planning && button != null && button.isActiveAndEnabled && button.IsInteractable();

    private void LateUpdate()
    {
        if (!CanFocus) ResetFocus(false);
        bool active = CanFocus && (hovered || selected);
        focus = Mathf.MoveTowards(focus, active ? 1 : 0, Time.unscaledDeltaTime / (active ? .08f : .12f));
        Apply();
    }

    private void Apply()
    {
        if (frame == null) return;
        Color tint = phase == Phase.Waiting || phase == Phase.Sync ? new Color(.58f, .8f, .88f) : new Color(.96f, .76f, .38f);
        float amount = phase == Phase.Ended ? 0 : phase == Phase.Planning ? focus : .45f;
        if (phase == Phase.Waiting)
        {
            float pulse = 1 - Mathf.Clamp01((Time.unscaledTime - confirmedAt) / .28f);
            amount += .4f * pulse * pulse;
        }
        frame.SetVisual(amount, tint, CanFocus && pressed);
        title.color = tint;
        detail.color = new Color(.8f, .84f, .87f);
    }

    private void ResetFocus(bool clearTracking = true)
    {
        if (clearTracking) hovered = selected = false;
        pressed = false; focus = 0;
    }
    private void OnDisable()
    {
        ResetFocus(); confirmedAt = -1000f;
        if (frame != null) frame.SetVisual(0, Color.white, false);
        if (badge != null) badge.gameObject.SetActive(false);
    }
    private void OnEnable() { if (badge != null) Present(phase); }
    private void OnApplicationFocus(bool focused) { if (!focused) { ResetFocus(); Apply(); } }
    private void OnDestroy()
    {
        if (badge != null) Destroy(badge.gameObject);
        if (frame != null) Destroy(frame.gameObject);
    }
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (CanFocus && e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = false; }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = pressed = false; }

    private static void ConfigureText(Text text, Font font, int size)
    {
        text.font = font; text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true; text.resizeTextMinSize = 8; text.resizeTextMaxSize = size;
        text.supportRichText = false;
    }
    private static T Make<T>(string name, Transform parent, Vector2 min, Vector2 max) where T : Graphic
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        obj.transform.SetParent(parent, false);
        var graphic = obj.GetComponent<T>(); graphic.raycastTarget = false;
        graphic.rectTransform.anchorMin = min; graphic.rectTransform.anchorMax = max;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
        return graphic;
    }
}
