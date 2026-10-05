#if UNITY_EDITOR || CODEX_READY_GATE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Runs in an isolated player with an offline Photon room and a simulated peer.
public sealed class ReadyGateProbe : MonoBehaviour
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    sealed class Peer : Player { public Peer() : base("Ready gate peer", 2, false) { } }
    TurnPlanningManager manager;
    Peer peer;
    ActionDragData card;
    int resolutions;
    object[] resolved;
    Hashtable Mine => PhotonNetwork.LocalPlayer.CustomProperties;
    object Call(string name, params object[] args) => typeof(TurnPlanningManager).GetMethod(name, Private).Invoke(manager, args);
    T Get<T>(string name) => (T)typeof(TurnPlanningManager).GetField(name, Private).GetValue(manager);
    void Set(string name, object value) => typeof(TurnPlanningManager).GetField(name, Private).SetValue(manager, value);
    void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add("PASS: " + label); }
    void Log(string text, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + trace); }
    void Event(EventData data) { if (data.Code == 11) { resolutions++; resolved = (object[])data.CustomData; } }
    void Round(int turn, bool expired = false)
    {
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        props["turnIndex"] = turn; props["turnStart"] = PhotonNetwork.Time - (expired ? 100d : 0d); props["turnDur"] = 30d;
        Call("ResetLocalTurnProps", turn); Set("localSubmitted", false); Set("receivedResolution", false);
        peer.CustomProperties.Clear();
    }
    void PeerReady(int turn)
    {
        var delta = new Hashtable { { "turnReady", true }, { "submitTurn", turn } };
        foreach (var entry in delta) peer.CustomProperties[entry.Key] = entry.Value;
        manager.OnPlayerPropertiesUpdate(peer, delta);
    }
    void PeerActions(int turn, object actions)
    {
        var delta = new Hashtable { { "actionsTurn", turn }, { "turnActions", actions } };
        foreach (var entry in delta) peer.CustomProperties[entry.Key] = entry.Value;
        manager.OnPlayerPropertiesUpdate(peer, delta);
    }
    IEnumerator Start()
    {
        Application.runInBackground = true; Application.logMessageReceived += Log;
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-ready-output");
        string output = index >= 0 ? args[index + 1] : "ReadyGateResults"; Directory.CreateDirectory(output);
        PhotonNetwork.OfflineMode = true; PhotonNetwork.CreateRoom("Ready gate isolated", new RoomOptions { MaxPlayers = 2 });
        yield return null; yield return null;
        int result = 0;
        try { Run(); Check(errors.Count == 0, "No runtime errors"); }
        catch (Exception e) { checks.Add("FAILED: " + e); result = 1; }
        PhotonNetwork.NetworkingClient.EventReceived -= Event;
        File.WriteAllLines(output + "/validation.txt", checks); File.WriteAllLines(output + "/errors.txt", errors);
        Application.logMessageReceived -= Log; Application.Quit(result);
    }
    void Run()
    {
        Check(PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient, "Offline host room initialized");
        peer = new Peer(); PhotonNetwork.CurrentRoom.AddPlayer(peer);
        manager = new GameObject("Planning manager").AddComponent<TurnPlanningManager>(); manager.enabled = false;
        var slot = new GameObject("Slot", typeof(RectTransform)).AddComponent<ActionSlot>(); slot.enabled = false;
        var item = new GameObject("Action", typeof(RectTransform)); item.transform.SetParent(slot.transform, false);
        item.AddComponent<DraggableItem>(); card = item.AddComponent<ActionDragData>(); card.actionType = ActionType.LightAttack;
        Set("planningSlots", new[] { slot });
        PhotonNetwork.NetworkingClient.EventReceived += Event;
        Round(10);
        Call("OnClickReady");
        Check((bool)Mine["turnReady"] && (int)Mine["submitTurn"] == 10 && Get<bool>("localSubmitted"), "Ready records readiness for the current round");
        Check(Mine["turnActions"] == null && (int)Mine["actionsTurn"] == -1, "First Ready does not publish an action payload");
        Check(Get<int[]>("pendingLocalPlan").SequenceEqual(new[] { (int)ActionType.LightAttack }), "Ready freezes local slot snapshot");
        card.actionType = ActionType.HeavyAttack; Call("OnClickReady");
        Check(Get<int[]>("pendingLocalPlan")[0] == (int)ActionType.LightAttack, "Repeated Ready cannot overwrite frozen actions");
        Call("TryFinalizePlanning", 10, true);
        Check(resolutions == 0 && Mine["turnActions"] == null, "Timeout cannot bypass a missing peer Ready");
        PeerReady(9);
        Check(Mine["turnActions"] == null, "Previous-round peer Ready cannot trigger publication");
        PeerReady(10);
        Check(Mine["turnActions"] is int[] payload && payload[0] == (int)ActionType.LightAttack && (int)Mine["actionsTurn"] == 10, "Peer Ready callback publishes the frozen current-round actions");
        var sent = Mine["turnActions"]; PeerReady(10);
        Check(ReferenceEquals(sent, Mine["turnActions"]), "Repeated callbacks do not republish actions");
        Call("TryFinalizePlanning", 10, true);
        Check(resolutions == 0, "Host waits for the peer payload even after timeout");
        PeerActions(9, new[] { (int)ActionType.Parry }); Call("TryFinalizePlanning", 10, true);
        Check(resolutions == 0, "Host rejects stale action payload despite current Ready");
        PeerActions(10, null); Call("TryFinalizePlanning", 10, true);
        Check(resolutions == 0, "Current marker without action payload cannot resolve");
        PeerActions(10, new object[] { (int)ActionType.Defense }); Call("TryFinalizePlanning", 10, false);
        Check(resolutions == 1 && (int)resolved[0] == 10, "Host resolves only after both current-round payloads arrive");
        int ownOffset = (int)resolved[2] == PhotonNetwork.LocalPlayer.ActorNumber ? 3 : 5;
        Check(((int[])resolved[ownOffset])[0] == (int)ActionType.LightAttack && ((int[])resolved[ownOffset == 3 ? 5 : 3])[0] == (int)ActionType.Defense, "Resolution carries both correct plans including object-array conversion");
        Call("TryFinalizePlanning", 10, true); Check(resolutions == 1, "Host broadcasts only once per round");
        Round(11, true); card.actionType = ActionType.Bolt; Call("Update");
        Check(Get<bool>("localSubmitted") && (int)Mine["submitTurn"] == 11 && Mine["turnActions"] == null && resolutions == 1, "Expired countdown auto-readies locally without revealing or resolving early");
        PeerReady(11); PeerActions(11, new[] { (int)ActionType.Ward }); Call("Update");
        Check(resolutions == 2 && (int)resolved[0] == 11, "Timed-out round resolves once the peer is ready and published");
        Round(12); PeerReady(12);
        Check(Mine["turnActions"] == null, "Peer-first Ready does not publish local draft");
        Call("OnClickReady");
        Check((int)Mine["actionsTurn"] == 12 && Mine["turnActions"] is int[], "Local second Ready publishes immediately once both are ready");
        // Exercise the actual round-change callback with empty slots so no delayed Destroy alters the fixture.
        Set("planningSlots", new ActionSlot[0]); PhotonNetwork.CurrentRoom.CustomProperties["turnIndex"] = 13;
        manager.OnRoomPropertiesUpdate(new Hashtable { { "turnIndex", 13 } });
        Check(!Get<bool>("localSubmitted") && Get<int[]>("pendingLocalPlan") == null && Mine["turnActions"] == null && (int)Mine["actionsTurn"] == -1 && !(bool)Mine["turnReady"], "New-round callback clears old payload, readiness and pending snapshot");
        Round(14); PhotonNetwork.CurrentRoom.Players.Remove(2); Call("OnClickReady"); Call("TryPublishLocalPlan", 14);
        Check(Mine["turnActions"] == null, "Alone player cannot reveal actions");
        Call("ClearLocalTransmittedActionPayload");
        Check(Get<int[]>("pendingLocalPlan") == null && Mine["turnActions"] == null, "Leaving-scene cleanup discards pending and transmitted actions");
    }
}
#endif
