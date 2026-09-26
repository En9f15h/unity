#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PlanningPresentationProbe
{
    public static IEnumerator Run(int map, Action<string> capture, Action<bool,string> check)
    {
        var manager = TurnPlanningManager.Instance;
        var slots = (ActionSlot[])typeof(TurnPlanningManager).GetField("planningSlots",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager);
        var sources = UnityEngine.Object.FindObjectsByType<ActionDragSource>(FindObjectsSortMode.None).Where(s=>s.enabled && s.GetActionData()!=null).ToArray();
        check(slots.Length >= 2 && sources.Length > 0, "Actual planning controls available " + map);
        check(sources.All(s=>s.GetComponent<PaletteInteractionFeedback>()!=null), "All active palette sources have interaction feedback " + map);
        var source = sources.First(s=>s.GetActionType()==ActionType.MoveForward);
        var frame = source.GetComponentInChildren<PlanningFocusFrame>(true);
        var rect = (RectTransform)source.transform;
        var originalScale = rect.localScale;
        var originalSize = rect.sizeDelta;
        var originalSprite = source.GetComponent<Image>()?.sprite;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        pointer.position = RectTransformUtility.WorldToScreenPoint(null,rect.position);
        float scale = Time.timeScale;
        bool drag = ActionDragSource.GlobalDragEnabled;
        Time.timeScale = 0;
        try
        {
            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return Delay(.15f);
            check(frame.enabled && frame.color.a > .7f, "Hover reaches visible focus while timeScale is zero " + map);
            check(!frame.raycastTarget && frame.GetComponent<LayoutElement>().ignoreLayout, "Focus graphic cannot intercept input or alter layout " + map);
            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            yield return null;
            if(map<2) capture("Planning-"+map+"-pressed.png");
            check(rect.localScale==originalScale && rect.sizeDelta==originalSize && source.GetComponent<Image>()?.sprite==originalSprite,
                "Pressed feedback preserves authored icon and hit-area geometry " + map);
            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.pointerExitHandler);
            yield return Delay(.2f);
            check(!frame.enabled, "Pointer exit releases focus " + map);

            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            check(slots[0].GetCurrentActionData()==source.GetActionData(), "Click still places the action in first logical slot " + map);
            var slotFrame = slots[0].transform.Find("PlanningFocusFrame").GetComponent<PlanningFocusFrame>();
            check(slotFrame != null && slotFrame.enabled && !slotFrame.raycastTarget, "Successful placement emits a non-interactive confirmation " + map);
            if(map<2) capture("Planning-"+map+"-placed.png");
            yield return Delay(.5f);
            check(!slotFrame.enabled, "Placement confirmation finishes without changing game time " + map);
            slots[0].ClearPlacedItemOnly(); yield return null;
            check(!slots[0].HasPlacedItem() && slots[0].GetCurrentActionData()==null, "Clearing a slot still removes its action " + map);

            var heavy = sources.First(s=>manager.GetSlotCostForAction(s.GetActionData(),s.GetActionType())>=2 && manager.CanDragOrPlaceAction(s.GetActionData()));
            ExecuteEvents.Execute(heavy.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            check(slots[0].GetCurrentActionData()==heavy.GetActionData() && slots[1].IsOccupiedByHeavyExtension,
                "Two-slot action preserves original occupancy " + map);
            check(slots[0].GetComponent<PlanningSlotFeedback>()!=null && slots[1].GetComponent<PlanningSlotFeedback>()!=null,
                "Both primary and continuation slots show confirmation " + map);
            var continuation = slots[1].transform.Find("ContinuationPresentation");
            check(continuation!=null && continuation.gameObject.activeSelf, "Reserved slot displays continuation presentation " + map);
            var echo = continuation.Find("EchoIcon").GetComponent<Image>();
            check(echo.enabled && echo.sprite!=null && echo.color.a<.8f, "Continuation uses a muted skill echo " + map);
            check(continuation.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget), "Continuation decoration never intercepts input " + map);
            check(!slots[1].HasPlacedItem() && slots[1].GetCurrentActionData()==null, "Visual echo is not a second queued action " + map);
            check(!slots[1].CanAcceptAction(source.GetActionData(),source.GetActionType()), "Reserved continuation still rejects incoming actions " + map);
            var legacy = (GameObject)typeof(ActionSlot).GetField("lockOverlay",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(slots[1]);
            check(legacy==null || !legacy.activeSelf, "Legacy white overlay is hidden " + map);
            if(map<2) capture("Planning-"+map+"-two-slot.png");
            slots[0].ClearPlacedItemOnly(); yield return null;
            check(!slots[1].IsOccupiedByHeavyExtension && !slots[1].transform.Find("PlanningFocusFrame").GetComponent<PlanningFocusFrame>().enabled,
                "Removing two-slot action clears continuation and its feedback " + map);
            check(!continuation.gameObject.activeSelf && echo.sprite==null, "Removing the owner clears the continuation icon " + map);
            slots[1].SetHeavyExtensionOccupied(true,null,false);
            check(continuation.gameObject.activeSelf && !echo.enabled && echo.sprite==null,
                "Missing action artwork uses link mark without stale icon " + map);
            slots[1].SetHeavyExtensionOccupied(false,null,false);
            ExecuteEvents.Execute(heavy.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            check(slots[1].GetComponentsInChildren<ActionContinuationPresentation>(true).Length==1 && echo.sprite!=null,
                "Repeated reservation reuses presentation and restores its icon " + map);
            slots[0].ClearPlacedItemOnly(); yield return null;

            // Exercise the original drag clone, drop, and drag-end handlers.
            source.OnBeginDrag(pointer); yield return null;
            var clone = ActionDragSource.CurrentDraggedClone;
            check(clone!=null, "Original drag flow still creates a clone " + map);
            check(clone.GetComponentsInChildren<PlanningFocusFrame>().All(f=>!f.enabled), "Drag clones do not inherit active palette focus " + map);
            slots[0].OnDrop(pointer);
            source.OnEndDrag(pointer); yield return null;
            check(slots[0].GetCurrentActionData()==source.GetActionData() && ActionDragSource.CurrentDraggedClone==null,
                "Drop retains original ownership and drag-end cleanup " + map);
            check(slots[0].GetComponentsInChildren<PlanningFocusFrame>(true).Count(f=>f.transform.parent==slots[0].transform)==1,
                "Repeated placement reuses one confirmation frame " + map);
            var placedItem = slots[0].GetComponentInChildren<DraggableItem>();
            placedItem.OnBeginDrag(pointer);
            check(!slotFrame.enabled, "Dragging a placed action clears its old slot confirmation " + map);
            slots[0].OnDrop(pointer);
            placedItem.OnEndDrag(pointer);
            yield return null;
            check(slots[0].GetCurrentActionData()==source.GetActionData(), "Placed action can return to original slot after drag " + map);
            slots[0].ClearPlacedItemOnly(); yield return null;

            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return Delay(.12f);
            ActionDragSource.GlobalDragEnabled = false;
            yield return null; yield return null;
            check(!frame.enabled, "Planning lock immediately suppresses stale focus " + map);
            ActionDragSource.GlobalDragEnabled = drag;
            yield return null;
            var claimOnly = sources.First(s=>!manager.CanDragOrPlaceAction(s.GetActionData()) && manager.CanStartActionPaletteDrag(s.GetActionData()));
            ExecuteEvents.Execute(claimOnly.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return Delay(.15f);
            var claimFrame = claimOnly.GetComponentInChildren<PlanningFocusFrame>(true);
            check(claimFrame.enabled && claimFrame.color.b>claimFrame.color.r, "Actual claim-only draggable action uses cool focus colour " + map);
            ExecuteEvents.Execute(claimOnly.gameObject,pointer,ExecuteEvents.pointerExitHandler);
            source.GetComponent<PaletteInteractionFeedback>().ResetVisuals();
            source.OnPointerExit(pointer);
        }
        finally
        {
            ActionDragSource.GlobalDragEnabled = drag;
            Time.timeScale = scale;
            source.GetComponent<PaletteInteractionFeedback>().ResetVisuals();
        }
    }

    private static IEnumerator Delay(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
}
#endif
