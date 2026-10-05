#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public static class ReadyPresentationProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator Run(int map, Action<string> capture, Action<bool, string> check)
    {
        var manager = TurnPlanningManager.Instance;
        var type = typeof(TurnPlanningManager);
        var button = (Button)type.GetField("readyButton", Private).GetValue(manager);
        var view = button.GetComponent<ReadyButtonPresentation>();
        check(view != null, "Actual class Ready button has presentation " + map);
        var badge = button.transform.Find("ReadyStateBadge");
        var title = badge.Find("StateTitle").GetComponent<Text>();
        var frame = button.transform.Find("PlanningFocusFrame").GetComponent<PlanningFocusFrame>();
        var rect = (RectTransform)button.transform;
        var sprite = button.GetComponent<Image>().sprite;
        var position = rect.anchoredPosition; var size = rect.sizeDelta; var localScale = rect.localScale;
        var originalColors = button.colors;
        var savedSelection = EventSystem.current.currentSelectedGameObject;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        var submitted = type.GetField("localSubmitted", Private);
        var resolving = type.GetField("isResolving", Private);
        var ended = type.GetField("gameEnded", Private);
        var received = type.GetField("receivedResolution", Private);
        var update = type.GetMethod("Update", Private);
        var setInteractable = type.GetMethod("SetPlanningInteractable", Private);
        bool oldSubmitted = (bool)submitted.GetValue(manager), oldResolving = (bool)resolving.GetValue(manager);
        bool oldEnded = (bool)ended.GetValue(manager), oldReceived = (bool)received.GetValue(manager);
        bool oldEnabled = manager.enabled, oldInteractable = button.interactable;
        float oldScale = Time.timeScale;
        var properties = PhotonNetwork.LocalPlayer.CustomProperties;
        var savedProps = new Hashtable { {"turnReady", properties["turnReady"]}, {"submitTurn", properties["submitTurn"]}, {"turnActions", properties["turnActions"]}, {"actionsTurn", properties["actionsTurn"]} };
        var pendingFields = new[] { "pendingLocalPlan", "pendingLocalPlanTurn", "localPlanPublished" };
        var savedPending = pendingFields.Select(n => type.GetField(n, Private).GetValue(manager)).ToArray();
        int clicks = 0;
        UnityAction countClick = () => clicks++;
        button.onClick.AddListener(countClick);
        manager.enabled = false; received.SetValue(manager, true); Time.timeScale = 0;
        try
        {
            check(button.IsInteractable() && !badge.gameObject.activeSelf, "Planning retains original Ready artwork and availability " + map);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return Delay(.15f);
            check(frame.enabled && frame.color.a > .9f, "Ready hover reaches gold focus with timeScale zero " + map);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            check(rect.anchoredPosition == position && rect.sizeDelta == size && rect.localScale == localScale,
                "Press feedback leaves Ready button geometry unchanged " + map);
            if (map < 2) capture("Ready-" + map + "-pressed.png");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(null);
            yield return Delay(.2f);
            check(!frame.enabled, "Pointer exit and deselection release Ready focus " + map);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return Delay(.15f);
            check(frame.enabled, "Keyboard selection shows Ready focus " + map);
            button.interactable = false;
            yield return null; yield return null;
            check(!frame.enabled, "External interaction lock clears stale Ready focus " + map);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            button.interactable = oldInteractable;
            EventSystem.current.SetSelectedGameObject(null);
            yield return Delay(.15f);
            check(frame.enabled, "Pointer remaining over a locked button regains focus when availability returns " + map);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);

            check(badge.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget) && !frame.raycastTarget,
                "Ready decorations never intercept pointer events " + map);
            var expectedActions = (int[])type.GetMethod("ReadLocalSlotActions", Private).Invoke(manager, null);
            // Use the real Button path for both mouse and keyboard across the seven scene routes.
            if (map % 2 == 0) button.OnPointerClick(pointer);
            else button.OnSubmit(new BaseEventData(EventSystem.current));
            update.Invoke(manager, null);
            check(clicks == 1 && (bool)submitted.GetValue(manager) && !button.IsInteractable(),
                "Original Ready callback submits exactly once and locks the button " + map);
            check((bool)properties["turnReady"] && ((int[])type.GetField("pendingLocalPlan", Private).GetValue(manager)).SequenceEqual(expectedActions),
                "Ready locks the original action snapshot locally " + map);
            bool bothReady = (bool)type.GetMethod("AreBothPlayersReady", Private).Invoke(manager, new object[] { properties["submitTurn"] });
            check(bothReady ? properties["turnActions"] is int[] sent && sent.SequenceEqual(expectedActions) : properties["turnActions"] == null,
                "Actions are published only when both players are ready " + map);
            check(badge.gameObject.activeSelf && title.text == "WAITING" && title.color.b > title.color.r,
                "Actual submission shows cool waiting badge " + map);
            if (map < 2) capture("Ready-" + map + "-waiting.png");
            button.OnPointerClick(pointer);
            button.OnSubmit(new BaseEventData(EventSystem.current));
            check(clicks == 1, "Locked Ready button rejects repeated mouse and keyboard submission " + map);
            yield return Delay(.4f);
            check(frame.color.a > .4f && frame.color.a < .5f, "Submit highlight settles to a steady waiting frame " + map);

            resolving.SetValue(manager, true); update.Invoke(manager, null);
            check(title.text == "RESOLVING" && badge.gameObject.activeSelf && !button.IsInteractable(),
                "Resolving state has its own locked button presentation " + map);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            if (map < 2) capture("Ready-" + map + "-resolve.png");
            ended.SetValue(manager, true); update.Invoke(manager, null);
            check(!badge.gameObject.activeSelf && !frame.enabled, "Ended match hides Ready decorations " + map);
            ended.SetValue(manager, false); resolving.SetValue(manager, false); submitted.SetValue(manager, false);
            setInteractable.Invoke(manager, new object[] { true }); update.Invoke(manager, null);
            check(!badge.gameObject.activeSelf && button.IsInteractable(), "Next planning state restores original Ready presentation " + map);
            yield return Delay(.15f);
            check(frame.enabled && frame.color.r > frame.color.b, "Pointer held over Ready across phase changes restores planning focus " + map);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            check(ReadyButtonPresentation.Ensure(button, title.font) == view && button.GetComponents<ReadyButtonPresentation>().Length == 1,
                "Repeated Ready binding reuses a single presentation " + map);
            check(button.GetComponent<Image>().sprite == sprite && button.colors.Equals(originalColors),
                "Ready state feedback preserves authored sprite and Button colour transitions " + map);
            view.OnPointerEnter(pointer); yield return Delay(.12f);
            view.enabled = false;
            check(!frame.enabled && !badge.gameObject.activeSelf, "Disabling presentation clears focus immediately " + map);
            view.enabled = true;
        }
        finally
        {
            button.onClick.RemoveListener(countClick);
            submitted.SetValue(manager, oldSubmitted); resolving.SetValue(manager, oldResolving);
            ended.SetValue(manager, oldEnded); received.SetValue(manager, oldReceived);
            for (int i = 0; i < pendingFields.Length; i++) type.GetField(pendingFields[i], Private).SetValue(manager, savedPending[i]);
            PhotonNetwork.LocalPlayer.SetCustomProperties(savedProps);
            setInteractable.Invoke(manager, new object[] { oldInteractable });
            type.GetMethod("HideAllPlanningReadyIndicators", Private).Invoke(manager, null);
            Time.timeScale = oldScale; manager.enabled = oldEnabled;
            EventSystem.current.SetSelectedGameObject(savedSelection);
            update.Invoke(manager, null);
        }
    }
    private static IEnumerator Delay(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
}
#endif
