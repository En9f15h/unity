#if UNITY_EDITOR || CODEX_WARD_FIX
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class WardDefenseProbe : MonoBehaviour
{
    public CharacterClassConfig knightConfig, oracleConfig;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    TurnPlanningManager manager;
    CharacterUnit mine, enemy;
    sealed class Peer : Player { public Peer() : base("Jump test peer", 2, false) { } }
    readonly Peer peer = new Peer();
    void Set(string name, object value) => typeof(TurnPlanningManager).GetField(name, Private).SetValue(manager, value);
    T Get<T>(string name) => (T)typeof(TurnPlanningManager).GetField(name, Private).GetValue(manager);
    void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add("PASS: " + message); }
    void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + trace); }
    IEnumerator Step(ActionType a, ActionType b) => (IEnumerator)typeof(TurnPlanningManager).GetMethod("ResolveSingleStepCoroutine", Private).Invoke(manager, new object[] { 0, a, b, new[] { (int)b } });
    CharacterUnit Unit(string label)
    {
        var unit = new GameObject(label, typeof(PhotonView)).AddComponent<CharacterUnit>(); unit.enabled = false;
        unit.maxHP = unit.currentHP = 100;
        return unit;
    }
    void Setup(bool attackerIsMine, bool oracle)
    {
        mine.currentHP = enemy.currentHP = 100;
        mine.className = attackerIsMine && oracle ? oracleConfig.className : knightConfig.className;
        enemy.className = !attackerIsMine && oracle ? oracleConfig.className : knightConfig.className;
        PhotonNetwork.LocalPlayer.CustomProperties["classIndex"] = attackerIsMine && oracle ? 1 : 0;
        peer.CustomProperties["classIndex"] = !attackerIsMine && oracle ? 1 : 0;
        mine.transform.position = Vector3.zero; enemy.transform.position = Vector3.right * (oracle ? 3 : 1);
        foreach (var name in new[] { "myHeavyPendingThisTurn", "enemyHeavyPendingThisTurn", "myRiftPendingThisTurn", "enemyRiftPendingThisTurn" }) Set(name, false);
    }
    IEnumerator AttackStep(bool attackerIsMine, ActionType attack, ActionType defense) => Step(attackerIsMine ? attack : defense, attackerIsMine ? defense : attack);
    IEnumerator Run()
    {
        manager = new GameObject("Production jump resolver").AddComponent<TurnPlanningManager>(); manager.enabled = false;
        mine = Unit("Mine"); enemy = Unit("Enemy");
        typeof(PhotonView).GetProperty("Owner").SetValue(enemy.photonView, peer);
        Set("myUnit", mine); Set("enemyUnit", enemy); Set("classConfigs", new[] { knightConfig, oracleConfig });
        Set("battleStepPlayer", manager.gameObject.AddComponent<BattleStepPlayer>());
        Set("bpm", 120f); Set("normalStepBeats", 1f);
        yield return null;
        foreach (bool attackerIsMine in new[] { true, false })
        {
            Setup(attackerIsMine, false);
            var defender = attackerIsMine ? enemy : mine;
            defender.className = oracleConfig.className;
            if(attackerIsMine) peer.CustomProperties["classIndex"] = 1;
            else PhotonNetwork.LocalPlayer.CustomProperties["classIndex"] = 1;
            string side = attackerIsMine ? "remote Oracle" : "local Oracle";
            string energy = attackerIsMine ? "enemyEnergy" : "myEnergy";
            Set(energy,0);
            yield return AttackStep(attackerIsMine,ActionType.LowAttack,ActionType.Ward);
            Check(defender.currentHP==100,"Ward fully blocks authored LowAttack: "+side);
            Check(Get<int>(energy)==Get<int>("energyPerBlock"),"Complete Ward block grants one block reward: "+side);
            yield return AttackStep(attackerIsMine,ActionType.LowAttack,ActionType.None);
            Check(defender.currentHP==100-knightConfig.lowAttack.damage,"Next undefended LowAttack still hits: "+side);
            yield return new WaitForSecondsRealtime(.2f);
            defender.currentHP=100; Set(energy,0);
            mine.transform.position=Vector3.zero; enemy.transform.position=Vector3.right*(knightConfig.lowAttack.range+2);
            yield return AttackStep(attackerIsMine,ActionType.LowAttack,ActionType.Ward);
            Check(defender.currentHP==100 && Get<int>(energy)==0,"Out-of-range attack grants no Ward reward: "+side);
            mine.transform.position=Vector3.zero; enemy.transform.position=Vector3.right;
            yield return AttackStep(attackerIsMine,ActionType.LightAttack,ActionType.Ward);
            Check(defender.currentHP==100,"Ward still blocks LightAttack: "+side);
            yield return AttackStep(attackerIsMine,ActionType.HeavyAttack,ActionType.Ward);
            Check(defender.currentHP==100,"Heavy charge deals no damage: "+side);
            yield return AttackStep(attackerIsMine,ActionType.None,ActionType.Ward);
            Check(defender.currentHP==96,"Released Heavy still deals 4 against Ward: "+side);
            yield return new WaitForSecondsRealtime(.2f);
            defender.currentHP=100; Set(energy,0);
            int original=knightConfig.lowAttack.damage;
            try
            {
                knightConfig.lowAttack.damage=5;
                yield return AttackStep(attackerIsMine,ActionType.LowAttack,ActionType.Ward);
                Check(defender.currentHP==98,"Ward subtracts 3 rather than hardcoding immunity: "+side);
                Check(Get<int>(energy)==0,"Partial reduction does not grant a full-block reward: "+side);
            }
            finally { knightConfig.lowAttack.damage=original; }
            yield return new WaitForSecondsRealtime(.2f);
        }
        Check(Time.timeScale==1f,"Ward does not change battle time scale");
        Check(errors.Count==0,"No runtime errors");
    }
    IEnumerator Start()
    {
        Application.runInBackground = true; Application.logMessageReceived += Log;
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-ward-output");
        string output = index >= 0 ? args[index + 1] : "WardResults"; Directory.CreateDirectory(output);
        var stack = new Stack<IEnumerator>(); stack.Push(Run()); int code = 0;
        while (stack.Count > 0)
        {
            object next;
            try {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
            } catch (Exception e) { checks.Add("FAILED: " + e); code = 1; break; }
            yield return next;
        }
        if (errors.Count > 0) code = 1;
        File.WriteAllLines(output + "/validation.txt", checks); File.WriteAllLines(output + "/errors.txt", errors);
        Application.logMessageReceived -= Log; Application.Quit(code);
    }
}
#endif
