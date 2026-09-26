#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class CountdownPresentationProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator Run(int map, Action<string> capture, Action<bool, string> check)
    {
        var manager = TurnPlanningManager.Instance;
        var type = typeof(TurnPlanningManager);
        var text = (Text)type.GetField("countdownText", Private).GetValue(manager);
        var view = text.GetComponent<BattleCountdownPresentation>();
        check(view != null, "Actual battle timer is bound to presentation " + map);
        var root = text.transform.Find("PhasePresentation");
        var caption = root.Find("PhaseLabel").GetComponent<Text>();
        var value = root.Find("CountdownValue").GetComponent<Text>();
        var progress = root.Find("TimeTrack/TimeRemaining").GetComponent<Image>();
        var size = text.rectTransform.sizeDelta;
        var position = text.rectTransform.anchoredPosition;
        var update = type.GetMethod("Update", Private);
        var submitted = type.GetField("localSubmitted", Private);
        var resolving = type.GetField("isResolving", Private);
        var ended = type.GetField("gameEnded", Private);
        var received = type.GetField("receivedResolution", Private);
        bool savedSubmitted = (bool)submitted.GetValue(manager), savedResolving = (bool)resolving.GetValue(manager);
        bool savedEnded = (bool)ended.GetValue(manager), savedReceived = (bool)received.GetValue(manager);
        bool enabled = manager.enabled;
        float scale = Time.timeScale;
        manager.enabled = false;
        // Suppress finalization during controlled state checks; no room/gameplay state is submitted.
        received.SetValue(manager, true);
        Time.timeScale = 0;
        try
        {
            check(caption.text == "PLAN" && value.text == text.text, "Initial timer matches the actual shared deadline " + map);
            check(!text.enabled && root.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget),
                "Timer view and decorations cannot intercept planning input " + map);
            check(!text.transform.parent.GetComponent<Image>().enabled, "Dedicated legacy gray timer backing is suppressed " + map);
            submitted.SetValue(manager, true);
            update.Invoke(manager, null);
            check(caption.text == "WAIT" && value.text == text.text && caption.color.b > caption.color.r,
                "Submitted state keeps countdown and uses calm waiting presentation " + map);
            if (map < 2) capture("Countdown-" + map + "-waiting.png");
            resolving.SetValue(manager, true);
            update.Invoke(manager, null);
            check(caption.text == "RESOLVE" && value.text == ">>" && !progress.transform.parent.gameObject.activeSelf,
                "Actual resolving state replaces underscore placeholder and hides time bar " + map);
            if (map < 2) capture("Countdown-" + map + "-resolve.png");
            ended.SetValue(manager, true);
            update.Invoke(manager, null);
            check(!root.gameObject.activeSelf, "Ended match hides the entire countdown view " + map);
            ended.SetValue(manager, false);
            resolving.SetValue(manager, false);
            submitted.SetValue(manager, false);
            update.Invoke(manager, null);
            check(root.gameObject.activeSelf && caption.text == "PLAN", "Planning state restores the countdown after hidden state " + map);

            view.Present(BattleCountdownPresentation.Phase.Planning, 4.01, 30);
            yield return null;
            check(value.text == "5" && value.color.r > value.color.b && value.rectTransform.localScale.x > 1,
                "Final five seconds use ceil rounding, amber colour and a short pulse " + map);
            check(Mathf.Abs(progress.rectTransform.anchorMax.x - 4.01f / 30f) < .001f,
                "Time bar uses the provided shared deadline fraction " + map);
            if (map < 2) capture("Countdown-" + map + "-urgent.png");
            float until = Time.realtimeSinceStartup + .3f;
            while (Time.realtimeSinceStartup < until)
            {
                view.Present(BattleCountdownPresentation.Phase.Planning, 4.01, 30);
                yield return null;
            }
            check(value.rectTransform.localScale == Vector3.one, "Repeated same-second updates do not restart pulse at timeScale zero " + map);
            view.Present(BattleCountdownPresentation.Phase.Waiting, 2, 30);
            check(value.rectTransform.localScale == Vector3.one && caption.color.b > caption.color.r,
                "Waiting cancels urgency even in final seconds " + map);
            view.Present(BattleCountdownPresentation.Phase.Planning, -2, 30);
            check(value.text == "0" && progress.rectTransform.anchorMax.x == 0 && value.rectTransform.localScale == Vector3.one,
                "Expired deadline clamps to zero without persistent pulsing " + map);
            view.Present(BattleCountdownPresentation.Phase.Sync);
            check(caption.text == "SYNC" && value.text == "..." && !progress.transform.parent.gameObject.activeSelf,
                "Before a shared turn exists timer shows synchronization state " + map);
            check(BattleCountdownPresentation.Ensure(text) == view && text.GetComponents<BattleCountdownPresentation>().Length == 1,
                "Repeated binding reuses one timer presentation " + map);
            check(text.rectTransform.sizeDelta == size && text.rectTransform.anchoredPosition == position,
                "All phase changes preserve original timer geometry " + map);
        }
        finally
        {
            submitted.SetValue(manager, savedSubmitted);
            resolving.SetValue(manager, savedResolving);
            ended.SetValue(manager, savedEnded);
            received.SetValue(manager, savedReceived);
            Time.timeScale = scale;
            manager.enabled = enabled;
            update.Invoke(manager, null);
        }
    }
}
#endif
