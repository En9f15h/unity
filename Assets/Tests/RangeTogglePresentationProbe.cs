#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RangeTogglePresentationProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator Run(int map, CharacterUnit unit, Action<string> capture, Action<bool, string> check)
    {
        var manager = UnityEngine.Object.FindFirstObjectByType<AttackRangePreviewManager>();
        var mine = Field<Button>(manager, "myRangeToggleButton");
        var enemy = Field<Button>(manager, "enemyRangeToggleButton");
        var myLabel = Field<Text>(manager, "myRangeToggleLabel");
        var enemyLabel = Field<Text>(manager, "enemyRangeToggleLabel");
        var view = mine.GetComponent<RangeTogglePresentation>();
        var frame = mine.transform.Find("PlanningFocusFrame").GetComponent<PlanningFocusFrame>();
        bool savedMine = manager.ShowMyRange, savedEnemy = manager.ShowEnemyRange;
        bool savedInteractable = mine.interactable;
        float savedScale = Time.timeScale;
        var selection = EventSystem.current.currentSelectedGameObject;
        var rect = (RectTransform)mine.transform;
        Vector2 size = rect.sizeDelta, position = rect.anchoredPosition;
        Vector3 scale = rect.localScale;
        var sprite = mine.GetComponent<Image>().sprite;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        var reference = unit.GetComponentInChildren<CharacterShaderFeedback>().BodyRenderers.First(r => r != null);
        Vector3 origin = unit.transform.position, target = origin + Vector3.right * 5;
        Time.timeScale = 0;
        manager.ClearAll(); manager.SetVisibleEnabled(true);
        yield return null;
        try
        {
            check(view != null && enemy.GetComponent<RangeTogglePresentation>() != null, "Both actual range buttons have presentation " + map);
            check(myLabel.text == "MY RANGE\nON" && enemyLabel.text == "ENEMY RANGE\nON", "Both independent enabled states have explicit ON labels " + map);
            check(!myLabel.raycastTarget && !enemyLabel.raycastTarget && !frame.raycastTarget, "Range labels and frames cannot intercept input " + map);
            ExecuteEvents.Execute(mine.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return Delay(.15f);
            check(frame.color.a > .9f, "Range hover focus fades in at timeScale zero " + map);
            ExecuteEvents.Execute(mine.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            if (map < 2) capture("RangeToggle-" + map + "-focus.png");
            check(rect.sizeDelta == size && rect.anchoredPosition == position && rect.localScale == scale && mine.GetComponent<Image>().sprite == sprite,
                "Range press preserves original control geometry and sprite " + map);
            ExecuteEvents.Execute(mine.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(mine.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(null);
            yield return Delay(.2f);
            check(frame.color.a > .39f && frame.color.a < .41f, "Range pointer exit settles to enabled-state frame " + map);
            EventSystem.current.SetSelectedGameObject(mine.gameObject);
            yield return Delay(.15f);
            check(frame.color.a > .9f, "Keyboard selection highlights range button " + map);
            mine.interactable = false;
            yield return null; yield return null;
            check(frame.color.a < .41f, "Interaction lock suppresses focus without misreporting range visibility " + map);
            ExecuteEvents.Execute(mine.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            mine.interactable = savedInteractable;
            EventSystem.current.SetSelectedGameObject(null);
            yield return Delay(.15f);
            check(frame.color.a > .9f, "Hover focus returns when range control becomes available " + map);
            ExecuteEvents.Execute(mine.gameObject, pointer, ExecuteEvents.pointerExitHandler);

            // Rebinding must not multiply the original toggle callbacks.
            var ensure = typeof(AttackRangePreviewManager).GetMethod("EnsureCanvasToggles", Private);
            ensure.Invoke(manager, null); ensure.Invoke(manager, null);
            manager.ShowRange(origin, target, 1, 1, 10, .12f, reference, AttackRangeDisplayType.My);
            manager.ShowRange(origin, target, 2, 2, 10, .12f, reference, AttackRangeDisplayType.Enemy);
            yield return null;
            check(Renderers(manager).Length == 4, "Both enabled sides create their existing range cells " + map);
            mine.OnPointerClick(pointer); yield return null;
            check(!manager.ShowMyRange && manager.ShowEnemyRange && myLabel.text.EndsWith("OFF") && enemyLabel.text.EndsWith("ON"),
                "Actual mouse click toggles only local range once after rebinding " + map);
            check(Renderers(manager).Length == 2, "Local range toggle clears local timed cells and preserves enemy cells " + map);
            if (map < 2) capture("RangeToggle-" + map + "-enemy-only.png");
            enemy.OnSubmit(new BaseEventData(EventSystem.current)); yield return null;
            check(!manager.ShowAttackRange && myLabel.text.EndsWith("OFF") && enemyLabel.text.EndsWith("OFF") && Renderers(manager).Length == 0,
                "Keyboard toggle can disable the remaining side and all timed cells " + map);
            if (map < 2) capture("RangeToggle-" + map + "-off.png");
            mine.OnPointerClick(pointer); enemy.OnPointerClick(pointer);
            check(manager.ShowMyRange && manager.ShowEnemyRange && myLabel.text.EndsWith("ON") && enemyLabel.text.EndsWith("ON"),
                "Both ranges can be independently re-enabled " + map);

            manager.ShowHoverRange(origin, target, 1, 2, reference, AttackRangeDisplayType.Enemy);
            manager.SetMyRangeEnabled(false); yield return null;
            check(Renderers(manager).Length == 4, "Disabling local range preserves an enemy hover preview " + map);
            manager.SetEnemyRangeEnabled(false); yield return null;
            check(Renderers(manager).Length == 0, "Disabling enemy range clears its already-visible hover preview " + map);
            manager.SetVisibleEnabled(true);
            manager.ShowHoverRange(origin, target, 1, 2, reference, AttackRangeDisplayType.My);
            manager.SetEnemyRangeEnabled(false); yield return null;
            check(Renderers(manager).Length == 4, "Disabling enemy range preserves a local hover preview " + map);
            manager.SetMyRangeEnabled(false); yield return null;
            check(Renderers(manager).Length == 0, "Disabling local range clears its already-visible hover preview " + map);
            manager.ShowHoverRange(origin, target, 1, 2, reference, AttackRangeDisplayType.My);
            check(Renderers(manager).Length == 0, "Disabled range cannot create a new hover preview " + map);
            check(mine.GetComponents<RangeTogglePresentation>().Length == 1 && mine.GetComponentsInChildren<PlanningFocusFrame>(true).Length == 1,
                "Repeated binding reuses one range presentation and frame " + map);
            view.enabled = false;
            check(!frame.enabled, "Disabling range presentation clears its frame " + map);
            view.enabled = true;
        }
        finally
        {
            manager.ClearAll(); manager.SetMyRangeEnabled(savedMine); manager.SetEnemyRangeEnabled(savedEnemy);
            mine.interactable = savedInteractable; Time.timeScale = savedScale;
            EventSystem.current.SetSelectedGameObject(selection);
        }
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static SpriteRenderer[] Renderers(AttackRangePreviewManager manager) => manager.GetComponentsInChildren<SpriteRenderer>();
    private static IEnumerator Delay(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
}
#endif
