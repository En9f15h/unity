#if UNITY_EDITOR || CODEX_MATCH_FEATURES
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D.Animation;
using UnityEngine.Localization.Settings;

[DefaultExecutionOrder(20)]
public sealed class MatchFeaturesProbe : MonoBehaviour
{
    public LobbyManager lobby;
    public GameObject[] prefabs;
    readonly List<string> checks=new List<string>(), errors=new List<string>();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Output="C:/Users/1/Downloads/unity/CodexLogs/HitIdle-20261013/Results";
    Action lateSample;
    void LateUpdate(){lateSample?.Invoke();}
    void Check(bool ok,string label){ checks.Add((ok?"PASS: ":"FAILED: ")+label); }
    void Log(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+trace);}
    object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    void Storage()
    {
        Check(LocalMatchHistory.RecordResult("win",1,1),"Win saved");
        Check(LocalMatchHistory.RecordResult("loss",2,1),"Loss saved");
        Check(LocalMatchHistory.RecordResult("draw",0,1),"Draw saved separately");
        LocalMatchHistory.RecordResult("win",1,1);
        var r=LocalMatchHistory.Read();
        Check(r.wins==1&&r.losses==1&&r.draws==1,"Duplicate result is idempotent; counts reload from disk");
        Check(!LocalMatchHistory.RecordResult("invalid",1,0),"Invalid local actor is not counted");
        File.WriteAllText(LocalMatchHistory.FilePath,"{}");
        r=LocalMatchHistory.Read();Check(r.wins==1&&r.losses==1&&r.draws==0,"Malformed primary recovers last valid backup");
        Check(LocalMatchHistory.RecordResult("recovered-draw",0,1),"Recovered history saves without replacing good backup with corruption");
        using(var gate=new FileStream(LocalMatchHistory.FilePath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            Check(!LocalMatchHistory.RecordResult("retry",1,1),"Another local process cannot overwrite a locked history");
        Check(LocalMatchHistory.RecordResult("retry",1,1),"A blocked save can retry after lock release");
        r=LocalMatchHistory.Read();Check(r.wins==2&&r.losses==1&&r.draws==1,"Recovered and retried history retains correct totals");
        Check(File.Exists(LocalMatchHistory.FilePath),"Match records exist as a JSON file");
    }
    void UI()
    {
        Check(LobbyManager.FormatPing(42,true)=="PING: 42 ms","Connected ping uses millisecond value");
        Check(LobbyManager.FormatPing(0,true)=="PING: 0 ms","Zero millisecond ping is a valid connected sample");
        Check(LobbyManager.FormatPing(42,false)=="PING: --","Disconnected ping does not display a stale value");
        Call(lobby,"SetupRegionDropdown");Call(lobby,"UpdateRoomCountText",false);Call(lobby,"RefreshMatchRecord");
        var count=(Text)typeof(LobbyManager).GetField("RegionRoomCountText",Private).GetValue(lobby);
        var history=(Text)typeof(LobbyManager).GetField("matchRecordText",Private).GetValue(lobby);
        Check(count.text.Contains("PING: --"),"Real Lobby scene appends ping alongside its room count");
        Check(history.text.Contains("2")&&history.text.Contains("1"),"Real Lobby scene displays persisted wins and losses");
        Check(!history.raycastTarget,"Record label cannot block lobby input");
        Canvas.ForceUpdateCanvases();
        Check(count.preferredWidth<=count.rectTransform.rect.width,"Room count and ping fit their text rectangle");
        Check(history.preferredWidth<=history.rectTransform.rect.width,"Match record fits its text rectangle");
        Check(history.supportRichText&&history.text=="<color=#20B74B>W</color>:2 / <color=#E04444>L</color>:1 =>0.67","Record uses coloured W/L labels and requested win-rate format");
        Check(LobbyManager.FormatMatchRecord(1,3).EndsWith("=>0.25"),"Win rate divides wins by total matches with two decimals");
        Check(LobbyManager.FormatMatchRecord(0,0).EndsWith("=>0.00"),"No matches displays zero win rate");
        Check(LobbyManager.FormatMatchRecord(2,0).EndsWith("=>1.00"),"Undefeated record displays full win rate");
        var culture=System.Globalization.CultureInfo.CurrentCulture;
        try {System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("de-DE");Check(LobbyManager.FormatMatchRecord(3,2).EndsWith("=>0.60"),"Win-rate format does not change with locale");}
        finally {System.Globalization.CultureInfo.CurrentCulture=culture;}
        CaptureLobby();
    }
    void CaptureLobby()
    {
        var camera=Camera.main;
        if(camera==null){var go=new GameObject("Capture camera");camera=go.AddComponent<Camera>();camera.orthographic=true;}
        var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas).ToArray();
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;}
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();
        var old=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        File.WriteAllBytes(Output+"/lobby.png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;target.Release();Destroy(target);Destroy(image);
    }
    IEnumerator Poses()
    {
        foreach(int damage in new[]{0,1,2,3,5,10,20,30})Check(CharacterPoseHitStop.FramesForDamage(damage)==(int)(damage*.6),"Damage "+damage+" uses requested integer frame formula");
        Check(Math.Abs(CharacterPoseHitStop.SecondsForDamage(10)-.1)<.000001,"Six frames at 60 FPS equal 0.1 seconds");
        foreach(var prefab in prefabs)
        {
            var go=Instantiate(prefab,new Vector3(500,0,0),Quaternion.identity);
            // The UI test scene has no arena floor; isolate root motion from gravity in this fixture.
            foreach(var body in go.GetComponentsInChildren<Rigidbody2D>(true))body.simulated=false;
            var unit=go.GetComponent<CharacterUnit>();unit.currentHP=unit.maxHP=30;
            var animator=unit.GetAnimator();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var skin in go.GetComponentsInChildren<SpriteSkin>(true))skin.alwaysUpdate=true;
            yield return null;yield return null;
            animator.Play(animator.HasState(0,Animator.StringToHash("Idle"))?"Idle":"idle",0,.2f);animator.Update(0);
            var bones=go.GetComponentsInChildren<SpriteSkin>(true).SelectMany(s=>s.boneTransforms).Where(b=>b!=null&&b!=go.transform).Distinct().ToArray();
            var initial=bones.Select(b=>b.localPosition).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
            double started=Time.unscaledTimeAsDouble;float before=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Vector3 root=go.transform.position;
            unit.TakeDamage(10);
            var hold=go.GetComponent<CharacterPoseHitStop>();
            Check(hold!=null&&hold.BoneCount>0,prefab.name+": damage starts a real skeleton pose hold");
            int samples=0,meshSamples=0;float poseError=0,advanced=0,meshError=0;
            var firstSkin=go.GetComponentsInChildren<SpriteSkin>(true).First();Vector3[] frozenVertices=null;
            lateSample=()=>
            {
                if(!hold.IsHolding)return;
                samples++;
                for(int i=0;i<bones.Length;i++)poseError=Mathf.Max(poseError,Vector3.Distance(bones[i].localPosition,initial[i]),Quaternion.Angle(bones[i].localRotation,rotations[i]));
                advanced=Mathf.Max(advanced,animator.GetCurrentAnimatorStateInfo(0).normalizedTime-before);
                if(firstSkin.HasCurrentDeformedVertices())
                {
                    var vertices=firstSkin.GetDeformedVertexPositionData().ToArray();meshSamples++;
                    if(frozenVertices==null)frozenVertices=vertices;
                    else for(int i=0;i<Mathf.Min(vertices.Length,frozenVertices.Length);i++)meshError=Mathf.Max(meshError,Vector3.Distance(vertices[i],frozenVertices[i]));
                }
            };
            while(Time.unscaledTimeAsDouble-started<.16)yield return null;
            lateSample=null;
            Check(samples>0&&poseError<.001f,prefab.name+": bone pose stays fixed across rendered-frame samples");
            Check(meshSamples>0&&meshError<.001f,prefab.name+": SpriteSkin deformation also remains frozen");
            Check(advanced>0,prefab.name+": Animator clock continues during the visible hold");
            Check(!hold.IsHolding&&animator.GetCurrentAnimatorStateInfo(0).normalizedTime-before>.1f,prefab.name+": release is already at current animation phase");
            Check(animator.speed==1f&&Time.timeScale==1f&&go.transform.position==root,prefab.name+": hold does not change speed, global time or gameplay root");
            hold.HoldForDamage(20);
            animator.Play("Dance",0,0f);animator.Update(0f);
            yield return new WaitForSecondsRealtime(.25f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Dance")&&animator.GetCurrentAnimatorStateInfo(0).normalizedTime>.15f,prefab.name+": next action continues during hold and is not overwritten on release");
            hold.HoldForDamage(10);yield return null;hold.enabled=false;
            Check(!hold.IsHolding&&animator.speed==1,prefab.name+": disabling hold restores presentation without freezing Animator");
            HitIdleTransition(animator,prefab.name);
            Destroy(go);yield return null;
        }
    }
    void HitIdleTransition(Animator animator,string label)
    {
        string idle=animator.HasState(0,Animator.StringToHash("Idle"))?"Idle":"idle";
        foreach(var p in animator.parameters)if(p.type==AnimatorControllerParameterType.Trigger)animator.ResetTrigger(p.nameHash);
        animator.speed=1;animator.Play(idle,0,0);animator.Update(0);
        animator.SetTrigger("Hit");animator.Update(.001f);
        var state=animator.GetCurrentAnimatorStateInfo(0);
        Check(state.IsName("Hit")||state.IsName("hit"),label+": production Hit trigger enters hit state");
        // Advance the real controller deterministically, without waiting for any display hold.
        for(int i=0;i<300;i++)
        {
            animator.Update(.005f);state=animator.GetCurrentAnimatorStateInfo(0);
            if(state.IsName(idle)&&!animator.IsInTransition(0))break;
        }
        Check(state.IsName(idle),label+": Hit returns naturally to Idle");
        Check(state.normalizedTime>=.499f&&state.normalizedTime<.52f,label+": Idle resumes at its halfway point");
        float untilNextCycle=(1f-state.normalizedTime)*state.length;
        animator.Update(Mathf.Max(0,untilNextCycle)+.001f);state=animator.GetCurrentAnimatorStateInfo(0);
        Check(state.IsName(idle)&&state.normalizedTime>=.999f&&state.normalizedTime<1.02f&&untilNextCycle<.51f,label+": remaining half Idle reaches next full cycle without an extra half beat");
        animator.SetTrigger("Dance");animator.Update(.001f);state=animator.GetCurrentAnimatorStateInfo(0);
        Check(state.IsName("Dance")&&state.normalizedTime<.02f,label+": next action begins immediately after aligned Idle");
    }
    IEnumerator Start()
    {
        Directory.CreateDirectory(Output);Application.logMessageReceived+=Log;
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-verify-persistence")>=0)
        {
            LocalMatchHistory.SetTestPath(File.ReadAllText(Output+"/stats-path.txt"));
            var r=LocalMatchHistory.Read();Check(r.wins==2&&r.losses==1&&r.draws==1,"A fresh player process reloads wins, losses and draws");
            File.WriteAllLines(Output+"/persistence.txt",checks);Application.Quit(checks.Exists(c=>c.StartsWith("FAILED"))?1:0);yield break;
        }
        string path=Output+"/stats-"+Guid.NewGuid().ToString("N")+"/match-history.json";
        LocalMatchHistory.SetTestPath(path);File.WriteAllText(Output+"/stats-path.txt",path);
        yield return LocalizationSettings.InitializationOperation;
        try{Storage();UI();}catch(Exception e){errors.Add(e.ToString());}
        var poses=Poses();
        while(true){bool more;try{more=poses.MoveNext();}catch(Exception e){errors.Add(e.ToString());break;}if(!more)break;yield return poses.Current;}
        File.WriteAllLines(Output+"/validation.txt",checks);File.WriteAllLines(Output+"/errors.txt",errors);
        Application.logMessageReceived-=Log;Application.Quit(errors.Count==0&&!checks.Exists(c=>c.StartsWith("FAILED"))?0:1);
    }
}
#endif
