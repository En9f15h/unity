#if UNITY_EDITOR || CODEX_SELECTION_SOCIAL
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
public sealed class SelectionSocialProbe : MonoBehaviour
{
    public CharacterSelectionMessages messages;
    public CharacterSelectionHostTransfer host;
    readonly List<string> checks=new List<string>(), errors=new List<string>();
    sealed class Peer : Player { public Peer():base("Test peer",2,false){} }
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    void Check(bool b,string s){if(!b)throw new Exception(s);checks.Add("PASS: "+s);}
    void Log(string s,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors.Add(s+"\n"+trace);}
    void Receive(byte code,int sender,object text){var ev=new EventData{Code=code};ev.Parameters[ParameterCode.ActorNr]=sender;ev.Parameters[ParameterCode.Data]=text;PhotonNetwork.NetworkingClient.OnEvent(ev);}
    IEnumerator Start()
    {
        Application.runInBackground=true;Application.logMessageReceived+=Log;
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-social-output");string output=i>=0?args[i+1]:"SocialResults";Directory.CreateDirectory(output);
        PhotonNetwork.OfflineMode=true;PhotonNetwork.CreateRoom("Selection social isolated test",new RoomOptions{MaxPlayers=2});
        yield return null;yield return null;
        int result=0;
        try { Run();Check(errors.Count==0,"No runtime errors"); }catch(Exception e){checks.Add("FAILED: "+e);result=1;}
        File.WriteAllLines(output+"/validation.txt",checks);File.WriteAllLines(output+"/errors.txt",errors);
        Application.logMessageReceived-=Log;Application.Quit(result);
    }
    void Run()
    {
        var input=Field<InputField>(messages,"messageField");var send=Field<Button>(messages,"sendButton");
        var own=Field<Text>(messages,"myMessageText");var enemy=Field<Text>(messages,"enemyMessageText");
        var transfer=Field<Button>(host,"transferButton");var mineCrown=Field<GameObject>(host,"myCrown");var otherCrown=Field<GameObject>(host,"enemyCrown");
        Check(PhotonNetwork.InRoom&&PhotonNetwork.IsMasterClient,"Offline room is ready");
        Check(!transfer.interactable&&mineCrown.activeSelf&&!otherCrown.activeSelf,"Alone host keeps own crown but cannot transfer");
        var peer=new Peer();PhotonNetwork.CurrentRoom.AddPlayer(peer);host.OnPlayerEnteredRoom(peer);
        Check(transfer.interactable,"Host with opponent can transfer");
        input.text="  你好，準備好了！  ";send.onClick.Invoke();
        Check(own.text=="你好，準備好了！"&&input.text=="","Actual send button displays trimmed Unicode and clears field");
        Check(enemy.text=="","Send targets others without echoing into enemy label");
        input.text="  ";send.onClick.Invoke();Check(own.text=="你好，準備好了！","Whitespace does not replace own message");
        Receive(GamePhotonEventCodes.CharacterSelectionMessage,2,"對方訊息 <b>原樣顯示</b>");
        Check(enemy.text=="對方訊息 <b>原樣顯示</b>"&&!enemy.supportRichText&&!own.supportRichText,"Photon event callback updates enemy text literally");
        string kept=enemy.text;
        Receive(62,2,"wrong event");Receive(61,1,"self");Receive(61,99,"outsider");Receive(61,2,123);Receive(61,2,"  ");
        Check(enemy.text==kept,"Unrelated, local, non-member, malformed and empty events are ignored");
        Receive(61,2,new string('a',199)+"😀");Check(enemy.text.Length==199,"Incoming length cap preserves emoji surrogate pairs");
        input.text=new string('a',199)+"😀";send.onClick.Invoke();Check(own.text.Length==199,"Input limit does not send a split emoji");
        messages.enabled=false;Receive(61,2,"disabled");Check(enemy.text.Length==199,"Disabled scene component unsubscribes Photon events");
        messages.enabled=true;
        // Photon callback list changes are applied by the next dispatched event.
        Receive(61,2,"重新啟用");Check(enemy.text=="重新啟用","Re-enabled component receives events again");
        transfer.onClick.Invoke(); // PUN intentionally rejects master transfer while offline.
        Check(mineCrown.activeSelf&&!otherCrown.activeSelf&&transfer.interactable,"Rejected transfer leaves authority display unchanged and retryable");
        // OfflineMode hardcodes IsMasterClient=true. Temporarily exercise PUN's
        // online authority getters against this isolated room, without connecting.
        var offlineField=typeof(PhotonNetwork).GetField("offlineMode",BindingFlags.Static|BindingFlags.NonPublic);
        var savedState=PhotonNetwork.NetworkingClient.State;
        try {
        offlineField.SetValue(null,false);PhotonNetwork.NetworkingClient.State=ClientState.Joined;
        typeof(RoomInfo).GetField("masterClientId",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(PhotonNetwork.CurrentRoom,2);
        host.OnMasterClientSwitched(peer);
        Check(!mineCrown.activeSelf&&otherCrown.activeSelf&&!transfer.interactable,"Confirmed remote master hides own crown and enables enemy crown");
        host.TransferHost();Check(PhotonNetwork.CurrentRoom.MasterClientId==2,"Non-master cannot transfer authority");
        typeof(RoomInfo).GetField("masterClientId",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(PhotonNetwork.CurrentRoom,1);host.OnMasterClientSwitched(PhotonNetwork.LocalPlayer);
        Check(mineCrown.activeSelf&&!otherCrown.activeSelf&&transfer.interactable,"Authority returned to local player restores button and crowns");
        } finally { offlineField.SetValue(null,true);PhotonNetwork.NetworkingClient.State=savedState; }
        PhotonNetwork.CurrentRoom.Players.Remove(2);host.OnPlayerLeftRoom(peer);messages.OnPlayerLeftRoom(peer);
        Check(!transfer.interactable&&enemy.text=="","Opponent departure disables transfer and clears opponent message");
        PhotonNetwork.LeaveRoom();input.text="保留草稿";send.onClick.Invoke();
        Check(input.text=="保留草稿"&&own.text==""&&enemy.text=="","Leaving room clears labels and disconnected send preserves draft");
    }
}
#endif
