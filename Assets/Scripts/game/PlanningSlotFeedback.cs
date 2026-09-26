using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlanningSlotFeedback : MonoBehaviour
{
    private PlanningFocusFrame frame;
    private float started;
    private bool continuation, playing;

    public static void Confirm(ActionSlot slot, bool isContinuation = false)
    {
        var feedback = slot.GetComponent<PlanningSlotFeedback>() ?? slot.gameObject.AddComponent<PlanningSlotFeedback>();
        if (feedback.frame == null) feedback.frame = PlanningFocusFrame.Create(slot.transform);
        feedback.frame.transform.SetAsLastSibling();
        feedback.continuation = isContinuation;
        feedback.started = Time.unscaledTime;
        feedback.playing = true;
        feedback.Apply(1);
    }

    private void LateUpdate()
    {
        if (!playing) return;
        float progress = Mathf.Clamp01((Time.unscaledTime - started) / .32f);
        Apply((1 - progress) * (1 - progress));
        if (progress >= 1) playing = false;
    }

    private void Apply(float amount)
    {
        if (frame != null) frame.SetVisual(amount, continuation
            ? new Color(.56f,.71f,.76f,.65f) : new Color(.97f,.8f,.43f,.9f), false);
    }

    public void Clear() { playing = false; Apply(0); }
    private void OnDisable() => Clear();
}
