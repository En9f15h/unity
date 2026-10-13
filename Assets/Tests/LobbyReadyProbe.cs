#if UNITY_EDITOR || CODEX_LOBBY_READY
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
public sealed class LobbyReadyProbe : MonoBehaviour
{
    public CharacterSelectionDefinition[] definitions;
    public MapSelectionDefinition map;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    sealed class Peer : Player { public Peer() : base("Simulated guest", 2, false) {} }
    void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add("PASS: " + label); }
    void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    void Log(string text, string trace, LogType type) { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(text+"\n"+trace); }
    void Run()
    {
        var lobby = new GameObject("Lobby").AddComponent<LobbyManager>(); lobby.enabled = false;
        var roomName = new GameObject("Name",typeof(RectTransform)).AddComponent<InputField>(); roomName.text="Public regression room";
        var password = new GameObject("Password",typeof(RectTransform)).AddComponent<InputField>(); password.text="";
        Set(lobby,"InputRoomName",roomName); Set(lobby,"InputRoomPassword",password);
        Check((bool)Call(lobby,"CanStartMatchmaking"),"Idle offline Master state permits matchmaking");
        lobby.CreateRoomButton();
        var room=PhotonNetwork.CurrentRoom;
        Check(room!=null && room.Name==roomName.text,"Actual create button creates the room");
        Check(room.IsVisible && room.IsOpen,"Passwordless room is visible and open");
        Check(room.MaxPlayers==2 && (int)room.CustomProperties["hasPw"]==0,"Public room matches random-join capacity and hasPw filter");
        Check(Array.IndexOf(room.PropertiesListedInLobby,"hasPw")>=0,"Password marker is published for lobby matchmaking");
        Check((bool)Call(lobby,"ValidateJoinedRoomPassword"),"Public room requires no password");
        Check(!(bool)Call(lobby,"CanStartMatchmaking"),"Already joined room blocks another matchmaking request");
        lobby.CreateRoomButton(); lobby.JoinRoomButton(); lobby.RandomJoinButton(); lobby.JoinPublicRoom("Another room");
        Check(PhotonNetwork.CurrentRoom==room,"All four matchmaking buttons preserve an existing room");

        var selection = new GameObject("Selection").AddComponent<CharacterSelectionManager>(); selection.enabled=false;
        Set(selection,"characters",definitions); Set(selection,"maps",new[]{map}); Call(selection,"BuildLookupTables");
        Set(selection,"selectedMapId",map.mapId);
        foreach(var definition in definitions)
        {
            PhotonNetwork.LocalPlayer.CustomProperties.Clear();
            Set(selection,"currentPhase",CharacterSelectionPhase.Selecting); Set(selection,"localReady",false);
            selection.setToggle(false);
            selection.CommitCharacterSelection(definition);
            Check(!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(CharacterSelectPhotonKeys.SelectedCharacterId),definition.name+": disabled preview sharing stays local before Ready");
            selection.ConfirmReady();
            var props=PhotonNetwork.LocalPlayer.CustomProperties;
            Check((bool)props[CharacterSelectPhotonKeys.IsReady] && (bool)props[CharacterSelectPhotonKeys.LegacyReady],definition.name+": Ready publishes both ready keys while toggle is off");
            Check(!props.ContainsKey(CharacterSelectPhotonKeys.SelectedCharacterId) && !props.ContainsKey(CharacterSelectPhotonKeys.SelectedSkinIndex),definition.name+": first Ready keeps character and skin private");
            Check(!props.ContainsKey(CharacterSelectPhotonKeys.LegacyClassIndex) && !props.ContainsKey(CharacterSelectPhotonKeys.LegacySkinIndex),definition.name+": first Ready keeps GameScene legacy keys private too");
            Check(!(bool)props[CharacterSelectPhotonKeys.SelectionFinalized],definition.name+": first Ready is not a finalized selection");
            Check(!(bool)Call(selection,"CanStartReveal"),definition.name+": one player alone cannot start reveal");
            selection.CancelReady();
            Check(!(bool)props[CharacterSelectPhotonKeys.IsReady],definition.name+": cancel clears Ready");
        }
        selection.setToggle(true);
        selection.OnRoomPropertiesUpdate(new Hashtable{{CharacterSelectPhotonKeys.PublicSelection,true}});
        selection.CommitCharacterSelection(definitions[0]);
        Check((string)PhotonNetwork.LocalPlayer.CustomProperties[CharacterSelectPhotonKeys.SelectedCharacterId]==definitions[0].characterId,"Enabled preview sharing still publishes selection");
        selection.setToggle(false);
        selection.OnRoomPropertiesUpdate(new Hashtable{{CharacterSelectPhotonKeys.PublicSelection,false}});
        Check(!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(CharacterSelectPhotonKeys.SelectedCharacterId) &&
              !PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(CharacterSelectPhotonKeys.LegacyClassIndex),"Turning sharing off removes previously public character and legacy keys");
        selection.CommitCharacterSelection(definitions[1]);
        selection.OnPlayerPropertiesUpdate(PhotonNetwork.LocalPlayer,new Hashtable{{CharacterSelectPhotonKeys.IsReady,false}});
        Check((CharacterSelectionDefinition)selection.GetType().GetField("committedLocalCharacter",Private).GetValue(selection)==definitions[1],"Ready-only property echo preserves the private local draft");
        Set(selection,"pendingLocalSelectionProperties",true); Call(selection,"TryFlushPendingLocalProperties");
        Check(!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(CharacterSelectPhotonKeys.SelectedCharacterId),"Queued property flush respects disabled sharing");
        var peer=new Peer(); room.AddPlayer(peer);
        selection.ConfirmReady();
        Check(!(bool)Call(selection,"CanStartReveal"),"Host Ready waits for guest Ready");
        var peerProps=new Hashtable{{CharacterSelectPhotonKeys.IsReady,true}};
        foreach(var p in peerProps)peer.CustomProperties[p.Key]=p.Value;
        selection.OnPlayerPropertiesUpdate(peer,peerProps);
        var published=PhotonNetwork.LocalPlayer.CustomProperties;
        Check((string)published[CharacterSelectPhotonKeys.SelectedCharacterId]==definitions[1].characterId &&
              (bool)published[CharacterSelectPhotonKeys.SelectionFinalized],"Both Ready publish the latest private draft");
        Check((int)published[CharacterSelectPhotonKeys.LegacyClassIndex]==definitions[1].legacyClassIndex &&
              (int)published[CharacterSelectPhotonKeys.LegacySkinIndex]==(int)published[CharacterSelectPhotonKeys.SelectedSkinIndex],"Final selection includes matching GameScene class and skin");
        Check(!(bool)Call(selection,"CanStartReveal"),"Both Ready still wait for guest selection data");
        peerProps=new Hashtable{{CharacterSelectPhotonKeys.SelectedCharacterId,definitions[0].characterId},{CharacterSelectPhotonKeys.SelectedSkinIndex,0}};
        foreach(var p in peerProps)peer.CustomProperties[p.Key]=p.Value;
        selection.OnPlayerPropertiesUpdate(peer,peerProps);
        Check(!(bool)Call(selection,"CanStartReveal"),"Stale guest choice without finalized marker cannot start reveal");
        peerProps=new Hashtable{{CharacterSelectPhotonKeys.SelectionFinalized,true}};
        peer.CustomProperties[CharacterSelectPhotonKeys.SelectionFinalized]=true;
        selection.OnPlayerPropertiesUpdate(peer,peerProps);
        Check((int)room.CustomProperties[CharacterSelectPhotonKeys.SelectionPhase]==(int)CharacterSelectionPhase.Revealing,"Both finalized selections enter shared reveal phase");
        Set(selection,"revealDuration",0f); Call(selection,"AdvanceTimedPhaseIfNeeded");
        Check((int)room.CustomProperties[CharacterSelectPhotonKeys.SelectionPhase]==(int)CharacterSelectionPhase.Countdown,"Reveal advances to shared countdown");
        selection.CancelReady();
        Check((int)room.CustomProperties[CharacterSelectPhotonKeys.SelectionPhase]==(int)CharacterSelectionPhase.Selecting,"Cancel during countdown restores selecting");
        Check(!published.ContainsKey(CharacterSelectPhotonKeys.SelectedCharacterId) &&
              !published.ContainsKey(CharacterSelectPhotonKeys.LegacyClassIndex) && !(bool)published[CharacterSelectPhotonKeys.SelectionFinalized],"Cancelling private Ready clears published choice and finalized marker");
        DestroyImmediate(selection.gameObject);
        PhotonNetwork.LeaveRoom();
        password.text="sample"; lobby.CreateRoomButton(); room=PhotonNetwork.CurrentRoom;
        Check(room.IsVisible && (int)room.CustomProperties["hasPw"]==1,"Password rooms retain visibility and remain outside public random filter");
        Check((bool)Call(lobby,"ValidateJoinedRoomPassword"),"Correct password still passes validation");
        PhotonNetwork.LeaveRoom(); PhotonNetwork.OfflineMode=false;
        var client=PhotonNetwork.NetworkingClient;
        var stateProperty=client.GetType().GetProperty("State");
        foreach(var state in new[]{ClientState.Joining,ClientState.Leaving,ClientState.ConnectingToGameServer,ClientState.Disconnected,ClientState.Disconnecting})
        {
            stateProperty.SetValue(client,state);
            Check(!(bool)Call(lobby,"CanStartMatchmaking"),"Blocks matchmaking in state "+state);
        }
        stateProperty.SetValue(client,ClientState.ConnectedToMasterServer);
        Set(lobby,"isChangingRegion",true);
        Check(!(bool)Call(lobby,"CanStartMatchmaking"),"Region change blocks matchmaking even in Master state");
        Set(lobby,"isChangingRegion",false);
        Check((bool)Call(lobby,"CanStartMatchmaking"),"Connected Master state permits matchmaking");
        stateProperty.SetValue(client,ClientState.JoinedLobby);
        Check((bool)Call(lobby,"CanStartMatchmaking"),"Joined lobby state permits matchmaking");
        stateProperty.SetValue(client,ClientState.Disconnected);
        DestroyImmediate(lobby.gameObject);
    }
    IEnumerator Start()
    {
        Application.logMessageReceived+=Log;
        yield return LocalizationSettings.InitializationOperation;
        PhotonNetwork.OfflineMode=true;
        yield return null;
        try { Run(); } catch(Exception e) { checks.Add("FAILED: "+e); }
        string output="C:/Users/1/Downloads/unity/CodexLogs/PrivateSelection-20261012/Results";
        Directory.CreateDirectory(output); File.WriteAllLines(output+"/validation.txt",checks); File.WriteAllLines(output+"/errors.txt",errors);
        Application.logMessageReceived-=Log;
        Application.Quit(errors.Count==0 && !checks.Exists(c=>c.StartsWith("FAILED"))?0:1);
    }
}
#endif
