using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HealthHudValidation
{
    private const string Key="HealthHudValidation.Mode", Output=HealthHudSetup.Output;
    private static readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();
    private static readonly List<string> results=new List<string>(),errors=new List<string>();
    private static Camera camera;
    private static Sprite sprite;
    private static double deadline;
    static HealthHudValidation() { EditorApplication.playModeStateChanged+=OnPlay; }
    public static void RunBatch()
    {
        HealthHudSetup.Install(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetString(Key,"controlled"); EditorApplication.EnterPlaymode();
    }
    public static void RunIntegration()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetString(Key,"integration"); EditorApplication.EnterPlaymode();
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode) return;
        string mode=SessionState.GetString(Key,""); SessionState.SetString(Key,"");
        if(mode=="integration") { ShaderSceneProbe.StartProbe(false,Output+"/Integration",false,true); return; }
        if(mode!="controlled") return;
        results.Clear(); errors.Clear(); routines.Clear(); routines.Push(Run()); deadline=EditorApplication.timeSinceStartup+180;
        Application.logMessageReceived+=Log; EditorApplication.update+=Advance;
    }
    private static void Log(string message,string stack,LogType type)
    { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message+"\n"+stack); }
    private static void Advance()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("HUD validation timeout");
            while(routines.Count>0)
            {
                if(!routines.Peek().MoveNext()) { routines.Pop(); continue; }
                if(routines.Peek().Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Require(errors.Count==0,"No runtime errors during HUD tests"); Finish(0);
        }
        catch(Exception e) { results.Add("FAILED: "+e); Debug.LogException(e); Finish(1); }
    }
    private static void Finish(int code)
    {
        EditorApplication.update-=Advance; Application.logMessageReceived-=Log; Time.timeScale=1;
        File.WriteAllLines(Output+"/validation.txt",results); File.WriteAllLines(Output+"/runtime-errors.txt",errors);
        Debug.Log("HEALTH_HUD_VALIDATION "+(code==0?"PASSED":"FAILED")); EditorApplication.Exit(code);
    }
    private static void Require(bool condition,string message)
    { if(!condition) throw new InvalidOperationException(message); results.Add("PASS: "+message); }
    private static IEnumerator Frames(int count=4) { for(int i=0;i<count;i++) yield return null; }
    private static IEnumerator Seconds(float seconds) { float start=Time.unscaledTime; while(Time.unscaledTime-start<seconds) yield return null; }
    private static IEnumerator Run()
    {
        camera=new GameObject("Health HUD camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.orthographic=true; camera.orthographicSize=2; camera.aspect=16f/9; camera.transform.position=new Vector3(0,0,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.1f,.13f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f),Texture2D.whiteTexture.width);
        foreach(bool reverse in new[]{false,true}) foreach(bool mirror in new[]{false,true})
        {
            string label="reverse="+reverse+", mirror="+mirror;
            var root=new GameObject("Health fixture"); root.SetActive(false);
            root.transform.rotation=Quaternion.Euler(0,mirror?180:0,0); root.transform.localScale=new Vector3(1,1,0);
            var fill=new GameObject("fill").AddComponent<SpriteRenderer>(); fill.transform.SetParent(root.transform,false);
            fill.sprite=sprite; fill.color=Color.green; fill.transform.localScale=new Vector3(5.5f,.6f,1);
            var bar=root.AddComponent<DirectionalHealthBarUI>(); var so=new SerializedObject(bar);
            so.FindProperty("fill").objectReferenceValue=fill.transform; so.FindProperty("shrinkToLeft").boolValue=reverse; so.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true); bar.SetPerspectiveLabel("YOU",true); bar.Init(100,100); yield return Frames();
            var presentation=root.GetComponent<HealthBarPresentation>();
            Require(presentation!=null && presentation.GaugeRenderer.enabled && !fill.enabled,label+" uses one full-width gauge");
            var owner=(TextMesh)new SerializedObject(bar).FindProperty("perspectiveLabel").objectReferenceValue;
            Require(owner.text=="YOU  100 / 100",label+" exact health label");
            Require(Quaternion.Angle(owner.transform.rotation,camera.transform.rotation)<.01f,label+" text faces camera even with mirrored parent");
            owner.GetComponent<MeshRenderer>().enabled=false;
            Color32[] full=Capture("full-"+reverse+"-"+mirror+".png");
            bar.Init(0,100); yield return Frames(); var empty=Capture(null);
            bar.Init(25,100); yield return Frames(); var quarter=Capture("quarter-"+reverse+"-"+mirror+".png");
            int total=Changed(full,empty), partial=Changed(quarter,empty);
            Require(total>1000 && partial>total*.23f && partial<total*.27f,label+" rendered 25% health occupies one quarter of gauge");
            float centroid=ChangedCentroid(quarter,empty);
            bool right=reverse!=mirror;
            Require(right?centroid>480:centroid<480,label+" shader preserves original physical shrink direction");
            Require(Mathf.Approximately(fill.transform.localScale.x,5.5f*.25f),label+" original fill transform reports actual HP immediately");
            fill.flipX=true; yield return Frames(); var flipped=Capture(null);
            Require(Mathf.Abs(ChangedCentroid(flipped,empty)-centroid)<2,label+" sprite flip preserves shrink direction"); fill.flipX=false;
            bar.Init(100,100); bar.SetHP(60,100);
            Require(presentation.CurrentRatio==.6f && presentation.DelayedRatio==1,label+" damage updates HP immediately and holds previous amount");
            Require(owner.text=="YOU  60 / 100",label+" damage label is immediate");
            yield return Frames(1); Capture("damage-"+reverse+"-"+mirror+".png");
            Time.timeScale=0; float started=Time.unscaledTime;
            while(Time.unscaledTime-started<.32f) { bar.SetHP(60,100); yield return null; }
            Require(presentation.DelayedRatio<.99f && presentation.DelayedRatio>.6f,label+" duplicate HP updates do not restart trail during hit stop");
            yield return Seconds(.3f); Require(Mathf.Approximately(presentation.DelayedRatio,.6f),label+" damage trail finishes on unscaled clock"); Time.timeScale=1;
            bar.Init(100,100); bar.SetHP(80,100); bar.SetHP(35,100);
            Require(presentation.CurrentRatio==.35f && presentation.DelayedRatio==1,label+" consecutive damage retains accumulated loss");
            bar.SetHP(70,100); Require(presentation.CurrentRatio==.7f && presentation.DelayedRatio==.7f,label+" healing immediately removes misleading damage trail");
            bar.SetHP(80,200); Require(presentation.CurrentRatio==.4f && presentation.DelayedRatio==.4f,label+" maximum-health changes reset trail");
            bar.SetHP(400,200); Require(presentation.CurrentRatio==1 && owner.text=="YOU  200 / 200",label+" over-heal is clamped");
            bar.SetHP(-10,200); Require(presentation.CurrentRatio==0 && owner.text=="YOU  0 / 200",label+" death has zero current health");
            yield return Seconds(.6f); Require(presentation.DelayedRatio==0,label+" death leaves no persistent chip segment");
            bar.Init(50,100); bar.SetHP(10,100); bar.Init(40,100);
            Require(presentation.CurrentRatio==.4f && presentation.DelayedRatio==.4f,label+" round initialization clears previous damage");
            bar.SetPerspectiveLabel("ENEMY",false); yield return Frames();
            var block=new MaterialPropertyBlock(); presentation.GaugeRenderer.GetPropertyBlock(block);
            Require(block.GetColor("_HealthColor").r>block.GetColor("_HealthColor").g && owner.text=="ENEMY  40 / 100",label+" ownership change updates label and health palette");
            fill.color=new Color(0,1,0,0); yield return Frames();
            Require(presentation.GaugeRenderer.color.a==0,label+" preserves source alpha"); fill.color=Color.green;
            var renderer=presentation.GaugeRenderer; presentation.enabled=false; yield return Frames();
            Require(fill.enabled && !renderer.enabled,label+" disabling style restores original fill");
            presentation.enabled=true; yield return Frames();
            Require(!fill.enabled && renderer.enabled && presentation.GaugeRenderer==renderer,label+" re-enabling reuses renderer");
            root.SetActive(false); yield return Frames(); root.SetActive(true); yield return Frames();
            Require(presentation.DelayedRatio==presentation.CurrentRatio,label+" pooled HUD has no stale damage");
            Object.Destroy(root); yield return Frames(); Require(renderer==null && owner==null,label+" destroys generated gauge and label with owner");
        }
        Require(!ShaderUtil.ShaderHasError(Shader.Find("Combat/Health Gauge")),"Health Gauge compiled after actual render");
    }
    private static Color32[] Capture(string name)
    {
        var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32); var old=RenderTexture.active;
        camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var texture=new Texture2D(960,540,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply();
        var pixels=texture.GetPixels32(); if(name!=null) File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); Object.Destroy(texture); return pixels;
    }
    private static bool Different(Color32 a,Color32 b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>20;
    private static int Changed(Color32[] a,Color32[] b) { int count=0; for(int i=0;i<a.Length;i++) if(Different(a[i],b[i])) count++; return count; }
    private static float ChangedCentroid(Color32[] a,Color32[] b)
    { float sum=0; int count=0; for(int i=0;i<a.Length;i++) if(Different(a[i],b[i])) { sum+=i%960; count++; } return count==0?480:sum/count; }
}
