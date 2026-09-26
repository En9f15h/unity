using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PaletteInteractionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private ActionDragSource source;
    private PlanningFocusFrame frame;
    private bool hovered, selected, pressed, canUse, canDrag;
    private float amount;

    public static PaletteInteractionFeedback Ensure(ActionDragSource owner)
    {
        var feedback = owner.GetComponent<PaletteInteractionFeedback>() ?? owner.gameObject.AddComponent<PaletteInteractionFeedback>();
        feedback.source = owner;
        feedback.frame = PlanningFocusFrame.Create(owner.transform);
        return feedback;
    }

    public void SetAvailability(bool usable, bool draggable)
    {
        canUse = usable;
        canDrag = draggable;
        if (!canDrag) { pressed = false; amount = 0; Apply(); }
    }

    private void LateUpdate()
    {
        if (source == null || !source.isActiveAndEnabled || !ActionDragSource.GlobalDragEnabled)
        { ResetVisuals(); return; }
        bool active = canDrag && (hovered || selected);
        amount = Mathf.MoveTowards(amount, active ? 1 : 0, Time.unscaledDeltaTime / (active ? .08f : .12f));
        Apply();
    }

    private void Apply()
    {
        if (frame == null) return;
        // The palette can still be dragged for a claim when its actual action is unavailable.
        Color tint = canUse ? new Color(.96f,.76f,.38f,.85f) : new Color(.48f,.72f,.78f,.7f);
        frame.SetVisual(amount, tint, pressed);
    }

    public void ResetVisuals()
    {
        hovered = selected = pressed = false;
        amount = 0;
        Apply();
    }
    private void OnDisable() => ResetVisuals();
    private void OnApplicationFocus(bool focused) { if (!focused) ResetVisuals(); }
    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e)
    { if (canDrag && e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = false; }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = false; pressed = false; }
}
