#if UNITY_EDITOR || CODEX_SHIFT_MOVEMENT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class ShiftMovementProbe : MonoBehaviour
{
    public CharacterClassConfig knightConfig, oracleConfig;
    public int sceneMinCell, sceneMaxCell;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    TurnPlanningManager manager;
    CharacterUnit mine, enemy, oracle, target;
    sealed class Peer : Player { public Peer() : base("Shift test peer", 2, false) { } }
    readonly Peer peer = new Peer();
    bool measuring;
    float originalX, started, changedAt;
    void Update() { if(measuring && changedAt < 0 && Mathf.Abs(oracle.transform.position.x-originalX)>.01f) changedAt=Time.realtimeSinceStartup-started; }
    void Set(string n, object v) => typeof(TurnPlanningManager).GetField(n,Flags).SetValue(manager,v);
    void Check(bool v,string s) { if(!v) throw new Exception(s); checks.Add("PASS: "+s); }
    void Log(string s,string trace,LogType t) { if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert) errors.Add(s+"\n"+trace); }
    IEnumerator Step(ActionType a,ActionType b) => (IEnumerator)typeof(TurnPlanningManager).GetMethod("ResolveSingleStepCoroutine",Flags).Invoke(manager,new object[]{0,a,b,new[]{(int)b}});
    CharacterUnit Unit(string label)
    {
        var u=new GameObject(label,typeof(PhotonView)).AddComponent<CharacterUnit>(); u.enabled=false;
        u.maxHP=u.currentHP=100; return u;
    }
    void Setup(bool local,float x,float otherX)
    {
        oracle=local?mine:enemy; target=local?enemy:mine;
        oracle.className=oracleConfig.className; target.className=knightConfig.className;
        PhotonNetwork.LocalPlayer.CustomProperties["classIndex"]=local?1:0; peer.CustomProperties["classIndex"]=local?0:1;
        oracle.transform.position=new Vector3(x,-.75f,0); target.transform.position=new Vector3(otherX,-.75f,0);
        mine.currentHP=enemy.currentHP=100;
        Set("boardMinCell",sceneMinCell); Set("boardMaxCell",sceneMaxCell); Set("boardOriginX",0f); Set("moveStep",1f);
        Set("blockedBoardCells",new int[0]); Set("constrainOracleSpecialMovementToWall",true);
        Set("oracleMovementWallMinX",-8.5f); Set("oracleMovementWallMaxX",8.5f);
    }
    IEnumerator Shift(bool local,ActionType other=ActionType.None) => Step(local?ActionType.Shift:other,local?other:ActionType.Shift);
    bool At(float a,float b) => Mathf.Abs(oracle.transform.position.x-a)<.001f && Mathf.Abs(target.transform.position.x-b)<.001f;
    IEnumerator Run()
    {
        manager=new GameObject("Production Shift resolver").AddComponent<TurnPlanningManager>(); manager.enabled=false;
        mine=Unit("Mine"); enemy=Unit("Enemy"); typeof(PhotonView).GetProperty("Owner").SetValue(enemy.photonView,peer);
        Set("myUnit",mine); Set("enemyUnit",enemy); Set("classConfigs",new[]{knightConfig,oracleConfig});
        Set("battleStepPlayer",manager.gameObject.AddComponent<BattleStepPlayer>()); Set("bpm",120f); Set("normalStepBeats",1f);
        yield return null;
        Check(!OracleShiftResolver.Resolve(5,7,-4,4,new int[0],3).valid,"Reproduced old board-range rejection inside the arena");
        foreach(bool local in new[]{true,false}) foreach(int sign in new[]{-1,1})
        {
            string label=(local?"local":"remote")+" / side "+sign;
            Setup(local,5*sign,7*sign); measuring=true; originalX=oracle.transform.position.x; started=Time.realtimeSinceStartup; changedAt=-1;
            yield return Shift(local); measuring=false;
            Check(At(8*sign,7*sign),"Outer arena Shift reaches behind target: "+label);
            Check(changedAt>=.20f && changedAt<.4f,"Shift still repositions on animation midpoint: "+label);
            Check(Time.realtimeSinceStartup-started>=.48f && Time.realtimeSinceStartup-started<.85f,"Shift preserves one-beat duration: "+label);
            Check(Mathf.Abs(oracle.transform.position.y+.75f)<.001f && Mathf.Abs(target.transform.position.y+.75f)<.001f,"Shift preserves both ground heights: "+label);
            yield return Step(ActionType.None,ActionType.None);
            Check(At(8*sign,7*sign),"Following idle step does not undo Shift: "+label);
            Setup(local,6*sign,8*sign); yield return Shift(local);
            Check(At(8*sign,6*sign),"Wall edge uses existing swap fallback: "+label);
            Setup(local,5*sign,6*sign); yield return Shift(local,ActionType.LightAttack);
            // The hit's existing visual recoil outlives the action coroutine.
            yield return new WaitForSecondsRealtime(.2f);
            Check(At(5*sign,6*sign) && oracle.currentHP<100,"Close LightAttack still interrupts Shift: "+label);
        }
        foreach(int sign in new[]{-1,1})
        {
            Setup(true,6*sign,8*sign); target.className=oracleConfig.className; peer.CustomProperties["classIndex"]=1;
            yield return Step(ActionType.Shift,ActionType.Shift);
            Check(At(8*sign,6*sign),"Simultaneous Shift exchanges outer arena positions: "+sign);
        }
        Setup(true,4,6); Set("oracleMovementWallMinX",-6.5f); Set("oracleMovementWallMaxX",6.5f); yield return Shift(true);
        Check(At(6,4),"Narrower physical walls select fallback before position assignment");
        Setup(true,5,7); Set("blockedBoardCells",new[]{8}); yield return Shift(true);
        Check(At(7,5),"Blocked behind-target cell preserves swap fallback");
        Setup(true,5,7); Set("blockedBoardCells",new[]{5,7,8}); yield return Shift(true);
        Check(At(5,7),"No valid destination leaves positions unchanged");
        Setup(true,5,7); Set("constrainOracleSpecialMovementToWall",false); Set("boardMinCell",-4); Set("boardMaxCell",4); yield return Shift(true);
        Check(At(5,7),"Explicit custom board limits remain supported");
        Setup(true,2.5f,3.5f); Set("moveStep",.5f); yield return Shift(true);
        Check(At(4,3.5f),"Grid conversion supports non-unit tile spacing");
        Check(Time.timeScale==1,"Shift does not change global time scale");
        Check(errors.Count==0,"No runtime errors");
    }
    IEnumerator Start()
    {
        Application.runInBackground=true; Application.targetFrameRate=120; Application.logMessageReceived+=Log;
        var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-shift-output"); string output=i>=0?args[i+1]:"ShiftResults"; Directory.CreateDirectory(output);
        var stack=new Stack<IEnumerator>(); stack.Push(Run()); int code=0;
        while(stack.Count>0)
        {
            object next;
            try { if(!stack.Peek().MoveNext()){stack.Pop();continue;} next=stack.Peek().Current; if(next is IEnumerator nested){stack.Push(nested);continue;} }
            catch(Exception e){checks.Add("FAILED: "+e);code=1;break;}
            yield return next;
        }
        if(errors.Count>0) code=1;
        File.WriteAllLines(output+"/validation.txt",checks); File.WriteAllLines(output+"/errors.txt",errors); Application.logMessageReceived-=Log; Application.Quit(code);
    }
}
#endif
