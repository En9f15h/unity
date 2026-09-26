#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class ActionUsageProbe
{
    static readonly HashSet<int> published = new HashSet<int>();
    public static void Prepare(Action<bool,string> check)
    {
        ActionUsageFileProbe.CheckRecovery(check);
        LocalActionUsage.UseTestStorage(Guid.NewGuid().ToString("N"));
        published.Clear();
        ActionType[][] allowed = {
            new[] { ActionType.LightAttack, ActionType.HeavyAttack, ActionType.LowAttack, ActionType.Parry, ActionType.Defense },
            new[] { ActionType.Bolt, ActionType.Rift, ActionType.Shift, ActionType.Fade, ActionType.Ward }
        };
        string[] classes = { "knight", "oracle" };
        for (int c = 0; c < 2; c++) foreach (ActionType action in Enum.GetValues(typeof(ActionType)))
        {
            check(LocalActionUsage.Record(classes[c], action) == allowed[c].Contains(action), "Exact usage classification: " + classes[c] + "/" + action);
            check(LocalActionUsage.ReadActionCount(classes[c], action) == (allowed[c].Contains(action) ? 1 : 0), "Individual action count: " + classes[c] + "/" + action);
        }
        LocalActionUsage.ReloadTestStorage();
        var knight = LocalActionUsage.Read("knight"); var oracle = LocalActionUsage.Read("oracle");
        check(knight.attack == 3 && knight.defense == 2, "Knight history survives storage reload");
        check(oracle.attack == 2 && oracle.defense == 3, "Oracle has independent persisted history");
        check(LocalActionUsage.PercentageTenths(knight).SequenceEqual(new[] { 600,400,0 }), "Attack and defense percentages share their own denominator");
        check(LocalActionUsage.PercentageTenths(new LocalActionUsage.Snapshot()).Sum() == 0, "Empty history avoids division by zero");
        LocalActionUsage.RecordClaims("knight", new[] { 0,4,-1,8 }, new[] { 0,3,-1,8 });
        LocalActionUsage.RecordClaims("oracle", new[] { 10,12 }, new[] { 10,13 });
        LocalActionUsage.ReloadTestStorage();
        knight = LocalActionUsage.Read("knight"); oracle = LocalActionUsage.Read("oracle");
        check(knight.declaredSlots == 3 && knight.mismatchedSlots == 1, "Only declared slots count; action mismatch is a lie");
        check(oracle.declaredSlots == 2 && oracle.mismatchedSlots == 1, "Lie history is persisted separately per class");
        check(LocalActionUsage.PercentageTenths(knight)[2] == 333 && LocalActionUsage.PercentageTenths(oracle)[2] == 500, "Lie rate has independent per-slot denominator");
        LocalActionUsage.RecordClaims("knight", new[] { -1 }, new[] { 8 });
        check(LocalActionUsage.Read("knight").mismatchedSlots == 2, "Declared action with an empty actual slot is a lie");
    }
    public static void PublishRemote(Player remote)
    {
        int token = LocalActionUsage.EntryToken(PhotonNetwork.LocalPlayer);
        if (!published.Add(token)) return;
        bool empty = published.Count == 3;
        remote.CustomProperties[LocalActionUsage.SnapshotKey] = JsonUtility.ToJson(new LocalActionUsage.Snapshot {
            entryToken = token, characterId = LocalActionUsage.CharacterId(remote), attack = empty ? 0 : 8, defense = empty ? 0 : 2, declaredSlots = empty ? 0 : 10, mismatchedSlots = empty ? 0 : 7
        });
    }
    public static IEnumerator Run(int map, CharacterUnit unit, Player remote, Action<string> capture, Action<bool,string> check)
    {
        var hud = BattleUIManager.Instance.GetComponent<ActionUsageMatchHUD>();
        for (int i = 0; i < 120 && !hud.Frozen; i++) yield return null;
        check(hud.Frozen && hud.MasterPanel != null && hud.ClientPanel != null, "Both health-frame usage panels freeze at scene start");
        var mine = PhotonNetwork.IsMasterClient ? hud.MasterPanel : hud.ClientPanel;
        var theirs = PhotonNetwork.IsMasterClient ? hud.ClientPanel : hud.MasterPanel;
        string mineText = mine.DisplayedText, theirText = theirs.DisplayedText;
        var peer = LocalActionUsage.ReadPeer(remote, LocalActionUsage.EntryToken(PhotonNetwork.LocalPlayer));
        check(peer != null && peer.characterId == LocalActionUsage.CharacterId(remote), "Opponent snapshot uses opponent selected class");
        check(theirText.Contains(map == 2 ? "—" : "80.0%"), "Opponent displays own history or no-history state");
        check(LocalActionUsage.ReadPeer(remote, LocalActionUsage.EntryToken(PhotonNetwork.LocalPlayer) + 1) == null, "Previous-match snapshot is rejected");
        string id = LocalActionUsage.CharacterId(PhotonNetwork.LocalPlayer);
        var own = LocalActionUsage.Read(id); int[] expected = LocalActionUsage.PercentageTenths(own);
        check(mineText.Contains((expected[0] / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"), "Local panel matches selected-class entry history");
        var planner = UnityEngine.Object.FindFirstObjectByType<TurnPlanningManager>();
        var record = typeof(TurnPlanningManager).GetMethod("RecordLocalActionUsage", BindingFlags.Instance | BindingFlags.NonPublic);
        ActionType action = id == "knight" ? ActionType.HeavyAttack : ActionType.Rift;
        long before = LocalActionUsage.ReadActionCount(id, action);
        record.Invoke(planner, new object[] { 99991, 0, action, false });
        record.Invoke(planner, new object[] { 99991, 0, action, false });
        record.Invoke(planner, new object[] { 99991, 1, action, true });
        record.Invoke(planner, new object[] { 99991, 2, ActionType.MoveForward, false });
        check(LocalActionUsage.ReadActionCount(id, action) == before + 1, "Resolution hook counts charge once, ignores replay and release");
        check(LocalActionUsage.Read(id).Total == own.Total + 1, "Excluded action does not enter usage denominator");
        // Confirm captured local claims only once, after the plans-ready confirmation.
        var claimFields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(TurnPlanningManager).GetMethod("EnsureClaimControllerForCurrentUi", claimFields).Invoke(planner, null);
        var controller = (ClaimController)typeof(TurnPlanningManager).GetField("claimController", claimFields).GetValue(planner);
        check(controller != null, "Claims test uses the planner's assigned scene controller");
        var claims = (ClaimSlot[])typeof(ClaimController).GetField("localSlots", claimFields).GetValue(controller);
        var slots = (ActionSlot[])typeof(TurnPlanningManager).GetField("planningSlots", claimFields).GetValue(planner);
        foreach (var claim in claims) claim.Clear();
        var config = (CharacterClassConfig)typeof(TurnPlanningManager).GetMethod("GetMyClassConfig", claimFields).Invoke(planner, null);
        var heavy = config.GetActionData(action);
        slots[1].SetHeavyExtensionOccupied(true, heavy, false);
        claims[0].SetRemoteState(ClaimSlotState.Action, action, -1, null);
        claims[1].SetRemoteState(ClaimSlotState.LockedContinuation, action, 0, null);
        claims[2].SetRemoteState(ClaimSlotState.Action, ActionType.Dance, -1, null);
        var submitted = Enumerable.Repeat(-1, slots.Length).ToArray(); submitted[0] = (int)action; submitted[2] = (int)ActionType.MoveForward;
        typeof(TurnPlanningManager).GetMethod("CaptureUsageClaims", claimFields).Invoke(planner, new object[] { 99992, submitted });
        var captured = (int[])typeof(TurnPlanningManager).GetField("usageActualSlots", claimFields).GetValue(planner);
        check(captured[1] == (int)action && submitted[1] == -1, "Ready capture expands actual continuation without changing transmitted actions");
        foreach (var claim in claims) claim.Clear();
        slots[1].SetHeavyExtensionOccupied(false, null, false);
        var confirm = typeof(TurnPlanningManager).GetMethod("RecordConfirmedUsageClaims", claimFields);
        long oldDeclared = LocalActionUsage.Read(id).declaredSlots, oldLies = LocalActionUsage.Read(id).mismatchedSlots;
        confirm.Invoke(planner, new object[] { 99990 });
        check(LocalActionUsage.Read(id).declaredSlots == oldDeclared, "Unconfirmed or different planning turn cannot record claims");
        confirm.Invoke(planner, new object[] { 99992 }); confirm.Invoke(planner, new object[] { 99992 });
        check(LocalActionUsage.Read(id).declaredSlots == oldDeclared + 3 && LocalActionUsage.Read(id).mismatchedSlots == oldLies + 1, "Confirmed claims count per slot once, including continuation slots");
        remote.CustomProperties[LocalActionUsage.SnapshotKey] = JsonUtility.ToJson(new LocalActionUsage.Snapshot { characterId = LocalActionUsage.CharacterId(remote), entryToken = LocalActionUsage.EntryToken(PhotonNetwork.LocalPlayer), declaredSlots = 100, mismatchedSlots = 100 });
        yield return null; yield return null;
        check(mine.DisplayedText == mineText && theirs.DisplayedText == theirText, "Match HUD stays frozen after local counts and peer properties change");
        LocalActionUsage.ReloadTestStorage();
        check(LocalActionUsage.ReadActionCount(id, action) == before + 1, "Executed action remains persisted for next match");
        capture("ActionUsage-" + map + ".png");
    }
}
#endif
