#if UNITY_EDITOR || CODEX_JUMP_DODGE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class JumpDodgeProbe : MonoBehaviour
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
            var defender = attackerIsMine ? enemy : mine;
            string side = attackerIsMine ? "local attacker" : "remote attacker";
            Setup(attackerIsMine, false);
            double start = Time.realtimeSinceStartupAsDouble;
            yield return AttackStep(attackerIsMine, ActionType.LightAttack, ActionType.Jump);
            Check(defender.currentHP == 100, "Jump evades LightAttack without horizontal movement: " + side);
            Check(Time.realtimeSinceStartupAsDouble - start >= .48 && Time.realtimeSinceStartupAsDouble - start < 1, "Jump step retains one-beat timing: " + side);
            yield return AttackStep(attackerIsMine, ActionType.LightAttack, ActionType.None);
            Check(defender.currentHP == 100 - knightConfig.lightAttack.damage, "Jump state clears and next grounded LightAttack hits: " + side);
            Setup(attackerIsMine, true);
            yield return AttackStep(attackerIsMine, ActionType.Rift, ActionType.None);
            Check(defender.currentHP == 100, "Rift charging does not deal damage: " + side);
            yield return AttackStep(attackerIsMine, ActionType.None, ActionType.Jump);
            Check(defender.currentHP == 100, "Jump evades the actual Rift release step: " + side);
            yield return AttackStep(attackerIsMine, ActionType.Rift, ActionType.Jump);
            yield return AttackStep(attackerIsMine, ActionType.None, ActionType.None);
            Check(defender.currentHP == 100 - ((AttackActionData)oracleConfig.GetActionData(ActionType.Rift)).damage, "Jump during charge does not evade next grounded Rift release: " + side);
            Setup(attackerIsMine, true);
            yield return AttackStep(attackerIsMine, ActionType.Bolt, ActionType.Jump);
            Check(defender.currentHP < 100, "Bolt still hits Jump: " + side);
            Setup(attackerIsMine, false);
            yield return AttackStep(attackerIsMine, ActionType.HeavyAttack, ActionType.None);
            yield return AttackStep(attackerIsMine, ActionType.None, ActionType.Jump);
            Check(defender.currentHP == 100 - Mathf.Max(1, knightConfig.heavyAttack.damage / 2), "HeavyAttack retains configured half damage against Jump: " + side);
            Setup(attackerIsMine, false);
            yield return AttackStep(attackerIsMine, ActionType.LowAttack, ActionType.Jump);
            Check(defender.currentHP == 100, "Jump fully evades LowAttack: " + side);
            yield return AttackStep(attackerIsMine, ActionType.LowAttack, ActionType.None);
            Check(defender.currentHP == 100 - knightConfig.lowAttack.damage, "Grounded LowAttack still deals full damage: " + side);
            Setup(attackerIsMine, false);
            yield return AttackStep(attackerIsMine, ActionType.HeavyAttack, ActionType.None);
            yield return AttackStep(attackerIsMine, ActionType.None, ActionType.None);
            Check(defender.currentHP == 100 - knightConfig.heavyAttack.damage, "Grounded HeavyAttack release still deals full damage: " + side);
            foreach (bool oracleDefender in new[] { false, true })
            {
                Setup(attackerIsMine, false);
                if (oracleDefender)
                {
                    defender.className = oracleConfig.className;
                    if (attackerIsMine) peer.CustomProperties["classIndex"] = 1;
                    else PhotonNetwork.LocalPlayer.CustomProperties["classIndex"] = 1;
                }
                string matchup = side + (oracleDefender ? " vs Oracle" : " vs Knight");
                string energyField = attackerIsMine ? "myEnergy" : "enemyEnergy";
                Set(energyField, Get<int>("maxEnergy"));
                yield return AttackStep(attackerIsMine, ActionType.Ultimate, ActionType.Jump);
                Check(defender.currentHP == 100 - knightConfig.ultimate.damage / 2, "Jump halves Ultimate damage: " + matchup);
                Check(Get<int>(energyField) == 0, "Reduced Ultimate still consumes all energy: " + matchup);
                defender.currentHP = 100; Set(energyField, Get<int>("maxEnergy"));
                yield return AttackStep(attackerIsMine, ActionType.Ultimate, ActionType.None);
                Check(defender.currentHP == 100 - knightConfig.ultimate.damage, "Next grounded Ultimate still deals full damage: " + matchup);
            }
        }
        Check(Time.timeScale == 1f, "Jump evasion never changes global time scale");
        Check(errors.Count == 0, "No runtime errors");
    }
    IEnumerator Start()
    {
        Application.runInBackground = true; Application.logMessageReceived += Log;
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-jump-output");
        string output = index >= 0 ? args[index + 1] : "JumpResults"; Directory.CreateDirectory(output);
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
