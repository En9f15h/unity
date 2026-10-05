#if UNITY_EDITOR || CODEX_CHARACTER_GRAPH
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public sealed class CombatFlowProbe : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    string output;
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Flags).GetValue(obj);
    static IEnumerator Call(object obj, string name, params object[] args) => (IEnumerator)obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add("PASS: " + message); }
    void Log(string text, string trace, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(text + "\n" + trace); }
    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-flow-output");
        output = i >= 0 ? args[i + 1] : "FlowResults"; Directory.CreateDirectory(output);
        Application.logMessageReceived += Log; Application.runInBackground = true;
        var stack = new Stack<IEnumerator>(); stack.Push(Run()); int code = 0;
        while (stack.Count > 0)
        {
            object next = null;
            try {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
            } catch (Exception e) { checks.Add("FAILED: " + e); code = 1; break; }
            yield return next;
        }
        if (errors.Count > 0) { checks.Add("FAILED: runtime errors"); code = 1; }
        File.WriteAllLines(output + "/validation.txt", checks); File.WriteAllLines(output + "/errors.txt", errors);
        Application.logMessageReceived -= Log; Application.Quit(code);
    }
    CharacterUnit Unit(string name, int skin)
    {
        var go = new GameObject(name); var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = Resources.Load<GameObject>("Prefab/knight/knight_" + skin).GetComponent<CharacterUnit>().GetAnimator().runtimeAnimatorController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var unit = go.AddComponent<CharacterUnit>(); unit.enabled = false; unit.className = "knight"; unit.maxHP = unit.currentHP = 100;
        return unit;
    }
    IEnumerator Run()
    {
        var manager = new GameObject("Production resolver under test").AddComponent<TurnPlanningManager>(); manager.enabled = false;
        var step = manager.gameObject.AddComponent<BattleStepPlayer>();
        var stop = new GameObject("Hit timing").AddComponent<HitStopManager>();
        var mine = Unit("Attacker", 0); var enemy = Unit("Defender", 1);
        mine.transform.position = Vector3.zero; enemy.transform.position = Vector3.right;
        var config = ScriptableObject.CreateInstance<CharacterClassConfig>(); config.className = "Knight";
        config.lightAttack = new AttackActionData { actionType = ActionType.LightAttack, damage = 2, range = 100, canBeParried = true };
        config.parry = new ActionData { actionType = ActionType.Parry };
        Set(manager, "classConfigs", new[] { config }); Set(manager, "myUnit", mine); Set(manager, "enemyUnit", enemy); Set(manager, "battleStepPlayer", step);
        Set(manager, "bpm", 120f); Set(manager, "normalStepBeats", 1f); Set(manager, "parryCounterStepBeats", 1f);
        Set(manager, "effectDelayBeats", 2f); // Make any accidental post-hit wait unmistakable.
        yield return null;
        float fixedStep = Time.fixedDeltaTime;
        double start = Time.realtimeSinceStartupAsDouble;
        for (int n = 0; n < 3; n++)
        {
            yield return Call(manager, "ResolveSingleStepCoroutine", n, ActionType.LightAttack, ActionType.None, new[] { 0, 0, 0 });
            Check(enemy.currentHP == 100 - 2 * (n + 1), "Real resolver applies consecutive hit " + n);
            Check(Time.timeScale == 1f && Time.fixedDeltaTime == fixedStep, "Real hit preserves clocks " + n);
        }
        double hits = Time.realtimeSinceStartupAsDouble - start;
        Check(hits >= 1.45 && hits < 1.9, "Three hit steps fit three beats: " + hits.ToString("F4") + "s");
        start = Time.realtimeSinceStartupAsDouble;
        yield return Call(manager, "ResolveSingleStepCoroutine", 0, ActionType.LightAttack, ActionType.Parry, new[] { (int)ActionType.Parry });
        Check(Get<bool>(manager, "pendingParryCounter"), "Actual parry queues a counter");
        Check(mine.GetAnimator().GetCurrentAnimatorStateInfo(0).IsName("getParry"), "Actual parried attack enters getParry");
        Check(enemy.currentHP == 94, "Parry prevents ordinary attack damage");
        yield return Call(manager, "ResolveParryCounterStepCoroutine");
        Check(mine.currentHP == 98 && !Get<bool>(manager, "pendingParryCounter"), "Counter damage resolves exactly once");
        double parry = Time.realtimeSinceStartupAsDouble - start;
        Check(parry >= .95 && parry < 1.35, "Attack plus counter fits two beats: " + parry.ToString("F4") + "s");
        yield return new WaitForSecondsRealtime(.08f);
        Check(Mathf.Abs(mine.GetAnimator().speed - 1) < .001f, "GetParry restores original animation speed");
        yield return Call(manager, "ResolveSingleStepCoroutine", 1, ActionType.LightAttack, ActionType.None, new[] { 0, 0 });
        Check(enemy.currentHP == 92, "Next action after getParry resolves normally");
        // A hit shake must not undo movement performed after the hit.
        var target = new GameObject("Concurrent movement target").transform;
        var shake = Call(manager, "PlayHitShakeCoroutine", target);
        Check(shake.MoveNext(), "Hit shake begins"); target.position += Vector3.right * 10;
        while (shake.MoveNext()) yield return shake.Current;
        Check(Mathf.Abs(target.position.x - 10) < .001f, "Hit shake preserves movement instead of restoring stale position");
        Check(errors.Count == 0, "No runtime errors in production combat flow");
        Destroy(manager.gameObject); Destroy(mine.gameObject); Destroy(enemy.gameObject); Destroy(stop.gameObject); Destroy(target.gameObject); Destroy(config);
    }
}
#endif
