#if UNITY_EDITOR || CODEX_INITIAL_IDLE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using UnityEngine;

public sealed class InitialIdleSyncProbe : MonoBehaviour
{
    public RuntimeAnimatorController[] controllers;
    readonly List<string> checks = new List<string>();
    readonly List<string> errors = new List<string>();
    void Check(bool ok, string label) => checks.Add((ok ? "PASS: " : "FAILED: ") + label);
    void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + trace);
    }
    CharacterUnit Create(RuntimeAnimatorController controller)
    {
        var go = new GameObject(controller.name);
        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var unit = go.AddComponent<CharacterUnit>();
        unit.enabled = false; // Registration/network are exercised separately below.
        return unit;
    }
    float Phase(Animator animator) => Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
    void Run()
    {
        Check(controllers.Length == 4, "All four playable character controllers loaded");
        foreach (var controller in controllers)
        {
            string label = controller.name;
            foreach (int delay in new[] { 0, 125, 500, 999, 1000, 2250, 600125 })
            {
                var unit = Create(controller);
                var animator = unit.GetAnimator();
                Check(unit.SynchronizeInitialIdle(delay), label + " first seek at " + delay + " ms");
                Check(Mathf.Abs(Phase(animator) - delay % 1000 / 1000f) < .002f,
                    label + " catches up to shared 1-second Idle, delay=" + delay);
                animator.Update(.05f);
                float phase = Phase(animator);
                Check(!unit.SynchronizeInitialIdle(delay + 250) && Mathf.Abs(Phase(animator) - phase) < .002f,
                    label + " duplicate registration does not restart Idle");
                Check(Mathf.Approximately(animator.speed, 1f), label + " preserves animation speed");
                DestroyImmediate(unit.gameObject);
            }
            var waiting = Create(controller);
            Check(!waiting.SynchronizeInitialIdle(-100), label + " future anchor does not consume first sync");
            waiting.gameObject.SetActive(false);
            Check(!waiting.SynchronizeInitialIdle(200), label + " inactive animator can retry later");
            waiting.gameObject.SetActive(true);
            Check(waiting.SynchronizeInitialIdle(500), label + " activation seeks directly to half-beat pose");
            DestroyImmediate(waiting.gameObject);

            var acting = Create(controller);
            var a = acting.GetAnimator();
            a.Update(0);
            string hit = a.HasState(0, Animator.StringToHash("hit")) ? "hit" : "Hit";
            a.Play(hit, 0, .25f); a.Update(0);
            int actionHash = a.GetCurrentAnimatorStateInfo(0).fullPathHash;
            Check(a.GetCurrentAnimatorStateInfo(0).IsName(hit) && !acting.SynchronizeInitialIdle(500) &&
                  a.GetCurrentAnimatorStateInfo(0).fullPathHash == actionHash,
                label + " first registration during hit preserves the action");
            DestroyImmediate(acting.gameObject);

            var sync = new GameObject("Shared start integration").AddComponent<GameSceneStartSync>();
            sync.enabled = false;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            int anchor = unchecked(PhotonNetwork.ServerTimestamp - 375);
            typeof(GameSceneStartSync).GetField("beatStartServerTimestamp", flags).SetValue(sync, anchor);
            typeof(GameSceneStartSync).GetField("gameStartedOnBeat", flags).SetValue(sync, true);
            var local = Create(controller);
            sync.RegisterMyCharacter(local.gameObject);
            var remote = Create(controller);
            sync.RegisterEnemyCharacter(remote.gameObject);
            Check(Mathf.Abs(Phase(local.GetAnimator()) - .375f) < .05f &&
                  Mathf.Abs(Phase(remote.GetAnimator()) - Phase(local.GetAnimator())) < .05f,
                label + " local and remote registration both use shared Photon anchor");
            DestroyImmediate(local.gameObject); DestroyImmediate(remote.gameObject); DestroyImmediate(sync.gameObject);
            GameSceneStartSync.Instance = null;
        }
        int wrappedAnchor = int.MaxValue - 200;
        int wrappedNow = unchecked(wrappedAnchor + 500);
        var wrapped = Create(controllers[0]);
        Check(wrapped.SynchronizeInitialIdle(unchecked(wrappedNow - wrappedAnchor)) &&
              Mathf.Abs(Phase(wrapped.GetAnimator()) - .5f) < .002f, "Photon timestamp wrap preserves half-cycle phase");
        DestroyImmediate(wrapped.gameObject);
    }
    IEnumerator Start()
    {
        Application.logMessageReceived += Log;
        yield return null;
        try { Run(); } catch (Exception e) { errors.Add(e.ToString()); }
        string output = Path.GetFullPath("C:/Users/1/Downloads/unity/CodexLogs/InitialIdleSync-20261011/Results");
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "validation.txt"), checks);
        File.WriteAllLines(Path.Combine(output, "errors.txt"), errors);
        Application.logMessageReceived -= Log;
        Application.Quit(errors.Count == 0 && !checks.Exists(c => c.StartsWith("FAILED")) ? 0 : 1);
    }
}
#endif
