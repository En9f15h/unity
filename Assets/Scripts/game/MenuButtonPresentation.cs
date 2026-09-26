using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The Button retains its callbacks, navigation, hit area and authored artwork.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class MenuButtonPresentation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    Button button;
    UIShaderFeedback shine;
    PlanningFocusFrame frame;
    ColorBlock originalColors;
    Selectable.Transition originalTransition;
    bool primary, hovered, selected, pressed, hadFocus, configured;
    float focus, submitUntil;
    readonly List<Graphic> labels = new List<Graphic>();
    readonly List<Color> labelColors = new List<Color>();

    public static MenuButtonPresentation Ensure(Button owner)
    {
        if (owner == null || !(owner.targetGraphic is Image) || owner.GetComponent<CharacterSelectionCard>() != null || owner.GetComponent<MapSelectionCard>() != null) return null;
        return owner.GetComponent<MenuButtonPresentation>() ?? owner.gameObject.AddComponent<MenuButtonPresentation>();
    }

    void Awake()
    {
        button = GetComponent<Button>();
        originalColors = button.colors; originalTransition = button.transition;
        string key = button.name.ToLowerInvariant();
        primary = gameObject.scene.name == "StartScene" || key == "confirmbutton" || key == "roombutton" || key.Contains("create") || key.Contains("join");
        shine = UIShaderFeedback.Ensure(button.targetGraphic as Image);
        frame = PlanningFocusFrame.Create(transform);
        frame.name = "MenuButtonFocus";
        foreach (var label in GetComponentsInChildren<Graphic>(true))
        {
            if (!(label is Text) && !(label is TMPro.TMP_Text)) continue;
            if (label.GetComponentInParent<Button>() != button || Mathf.Max(label.color.r, Mathf.Max(label.color.g, label.color.b)) >= .35f) continue;
            labels.Add(label); labelColors.Add(label.color);
        }
        configured = true;
    }

    void OnEnable()
    {
        if (!configured) return;
        // Keep sprite swaps and Animator transitions owned by their existing controllers.
        if (originalTransition == Selectable.Transition.ColorTint)
        {
            var colors = originalColors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .95f, .82f, 1f);
            colors.selectedColor = new Color(1f, .90f, .67f, 1f);
            colors.pressedColor = new Color(.72f, .62f, .44f, 1f);
            colors.disabledColor = new Color(.43f, .45f, .48f, .58f);
            colors.fadeDuration = .10f;
            button.colors = colors;
        }
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        Apply();
    }

    bool CanInteract => button != null && button.isActiveAndEnabled && button.IsInteractable();
    void LateUpdate()
    {
        if (!CanInteract) { hovered = selected = pressed = hadFocus = false; submitUntil = 0; }
        bool active = CanInteract && (hovered || selected);
        if (active && !hadFocus && shine != null) shine.Pulse(.32f);
        hadFocus = active;
        focus = Mathf.MoveTowards(focus, active ? 1f : 0f, Time.unscaledDeltaTime / (active ? .09f : .16f));
        Apply();
    }

    void Apply()
    {
        if (frame == null) return;
        bool down = CanInteract && (pressed || Time.unscaledTime < submitUntil);
        float idle = primary && CanInteract ? .16f : 0f;
        float strength = CanInteract ? Mathf.Max(idle, Mathf.Max(focus * .88f, down ? 1f : 0f)) : 0f;
        frame.SetVisual(strength, new Color(1f, .79f, .40f), down);
        // Only the decorative frame moves; controls and text keep their original layout.
        float inset = down ? 6f : 3f;
        frame.rectTransform.offsetMin = Vector2.one * inset;
        frame.rectTransform.offsetMax = -Vector2.one * inset;
        for (int i = 0; i < labels.Count; i++)
            if (labels[i] != null) labels[i].color = CanInteract ? new Color(1f, .93f, .76f, labelColors[i].a) : new Color(.60f, .62f, .65f, labelColors[i].a * .65f);
    }

    void ResetFeedback()
    {
        hovered = selected = pressed = hadFocus = false;
        focus = submitUntil = 0;
        if (frame != null) frame.SetVisual(0, Color.white, false);
    }
    void OnDisable()
    {
        ResetFeedback();
        if (configured && button != null) button.colors = originalColors;
        for (int i = 0; i < labels.Count; i++) if (labels[i] != null) labels[i].color = labelColors[i];
    }
    void OnApplicationFocus(bool value) { if (!value) ResetFeedback(); }
    void OnDestroy() { if (frame != null) Destroy(frame.gameObject); }
    public void OnPointerEnter(PointerEventData e) { if (CanInteract) hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData e)
    {
        if (!CanInteract || e.button != PointerEventData.InputButton.Left) return;
        pressed = true;
        if (shine != null) shine.Pulse(.22f);
        Apply();
    }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { pressed = false; Apply(); } }
    public void OnSelect(BaseEventData e) { if (CanInteract) selected = true; }
    public void OnDeselect(BaseEventData e) { selected = pressed = false; }
    public void OnSubmit(BaseEventData e)
    {
        if (!CanInteract) return;
        submitUntil = Time.unscaledTime + .12f;
        if (shine != null) shine.Pulse(.22f);
        Apply();
    }
}
