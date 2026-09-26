using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// View only: the range manager remains the owner of visibility and click callbacks.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class RangeTogglePresentation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private Button button;
    private PlanningFocusFrame frame;
    private bool shown, mine, hovered, selected, pressed;
    private float focus;

    public static void Refresh(Button owner, Image background, Text label, bool visible, bool local)
    {
        if (owner == null) return;
        var view = owner.GetComponent<RangeTogglePresentation>() ?? owner.gameObject.AddComponent<RangeTogglePresentation>();
        view.button = owner;
        if (view.frame == null) view.frame = PlanningFocusFrame.Create(owner.transform);
        view.shown = visible; view.mine = local;
        if (background != null)
            background.color = visible ? new Color(.035f, .065f, .08f, .96f) : new Color(.035f, .045f, .055f, .93f);
        if (label != null)
        {
            label.text = (local ? "MY RANGE\n" : "ENEMY RANGE\n") + (visible ? "ON" : "OFF");
            label.fontSize = 11; label.lineSpacing = .95f;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 9; label.resizeTextMaxSize = 11;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.color = visible ? new Color(.94f, .95f, .91f) : new Color(.62f, .67f, .71f);
        }
        view.Apply();
    }

    private bool CanFocus => button != null && button.isActiveAndEnabled && button.IsInteractable();
    private void LateUpdate()
    {
        if (!CanFocus) { pressed = false; focus = 0; }
        bool active = CanFocus && (hovered || selected);
        focus = Mathf.MoveTowards(focus, active ? 1 : 0, Time.unscaledDeltaTime / (active ? .08f : .12f));
        Apply();
    }
    private void Apply()
    {
        if (frame == null) return;
        Color tint = mine ? new Color(.44f, .9f, .6f) : new Color(.98f, .52f, .4f);
        if (!shown && focus <= .001f) tint = new Color(.56f, .62f, .68f);
        frame.SetVisual((shown ? .4f : .12f) + focus * .55f, tint, CanFocus && pressed);
    }
    private void ClearFocus() { hovered = selected = pressed = false; focus = 0; }
    private void OnDisable() { ClearFocus(); if (frame != null) frame.SetVisual(0, Color.white, false); }
    private void OnEnable() => Apply();
    private void OnApplicationFocus(bool focused) { if (!focused) { ClearFocus(); Apply(); } }
    private void OnDestroy() { if (frame != null) Destroy(frame.gameObject); }
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (CanFocus && e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = false; }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = pressed = false; }
}
