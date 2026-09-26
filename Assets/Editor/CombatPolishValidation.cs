using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CombatPolishValidation
{
    private const string Key="CombatPolishValidation.Running", Output=CombatPolishSetup.Output;
    private static readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();
    private static readonly List<string> results=new List<string>(), errors=new List<string>();
    private static Camera camera;
    private static GameObject floor;
    private static double deadline;
    static CombatPolishValidation() { EditorApplication.playModeStateChanged+=OnPlay; }
    public static void RunBatch()
    {
        CombatPolishSetup.Install(); CombatShaderSetup.Validate();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetString(Key,"polish"); EditorApplication.EnterPlaymode();
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
        if(mode=="integration") { ShaderSceneProbe.StartProbe(false,Output+"/Integration",true); return; }
        if(mode!="polish") return;
        results.Clear(); errors.Clear(); routines.Clear(); routines.Push(Run());
        deadline=EditorApplication.timeSinceStartup+180; Application.logMessageReceived+=Log; EditorApplication.update+=Advance;
    }
    private static void Log(string message,string stack,LogType type)
    { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message+"\n"+stack); }
    private static void Advance()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Polish validation timeout");
            while(routines.Count>0)
            {
                if(!routines.Peek().MoveNext()) { routines.Pop(); continue; }
                if(routines.Peek().Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Require(errors.Count==0,"No runtime errors in controlled presentation tests"); Finish(0);
        }
        catch(Exception e) { results.Add("FAILED: "+e); Debug.LogException(e); Finish(1); }
    }
    private static void Finish(int code)
    {
        EditorApplication.update-=Advance; Application.logMessageReceived-=Log; Time.timeScale=1;
        File.WriteAllLines(Output+"/validation.txt",results); File.WriteAllLines(Output+"/runtime-errors.txt",errors);
        Debug.Log("COMBAT_POLISH_VALIDATION "+(code==0?"PASSED":"FAILED")); EditorApplication.Exit(code);
    }
    private static void Require(bool condition,string message)
    { if(!condition) throw new InvalidOperationException(message); results.Add("PASS: "+message); }
    private static IEnumerator Frames(int count=4) { for(int i=0;i<count;i++) yield return null; }
    private static IEnumerator Seconds(float seconds)
    { float start=Time.unscaledTime; while(Time.unscaledTime-start<seconds) yield return null; }
    private static IEnumerator Run()
    {
        camera=new GameObject("Polish validation camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.orthographic=true; camera.orthographicSize=2.8f; camera.transform.position=new Vector3(0,2,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.2f,.23f,.28f); camera.allowHDR=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var light=new GameObject("Global 2D light").AddComponent<Light2D>(); light.lightType=Light2D.LightType.Global;
        floor=new GameObject("Test ground"); floor.transform.position=new Vector3(0,-.25f,0);
        var floorCollider=floor.AddComponent<BoxCollider2D>(); floorCollider.size=new Vector2(40,.5f);
        var floorSprite=floor.AddComponent<SpriteRenderer>(); floorSprite.sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
        floorSprite.drawMode=SpriteDrawMode.Sliced; floorSprite.size=new Vector2(40,.5f); floorSprite.color=new Color(.5f,.48f,.43f);
        yield return Frames(8);
        foreach(var path in CombatShaderSetup.CharacterPaths)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab!=null,path+" exists");
            var root=Object.Instantiate(prefab); var unit=root.GetComponentInChildren<CharacterUnit>(); unit.enabled=false;
            foreach(var rigidbody in root.GetComponentsInChildren<Rigidbody2D>()) { rigidbody.bodyType=RigidbodyType2D.Kinematic; rigidbody.linearVelocity=Vector2.zero; }
            var animator=unit.GetAnimator(); animator.Play("Idle",0,0); animator.Update(0); animator.speed=0;
            var collider=unit.GetComponent<Collider2D>();
            root.transform.localScale*=2.7f/Mathf.Max(.1f,collider.bounds.size.y);
            Physics2D.SyncTransforms(); root.transform.position-=new Vector3(collider.bounds.center.x,collider.bounds.min.y,0); Physics2D.SyncTransforms();
            var ground=unit.GetComponent<CharacterGroundPresentation>(); var accent=unit.GetComponent<CombatImpactPresentation>();
            Require(ground!=null && accent!=null,path+" has both presentation components");
            yield return Frames(8);
            Require(ground.IsGrounded && ground.ShadowRenderer.enabled,path+" contacts actual collider floor");
            Require(ground.DustBurstCount==0,path+" does not emit landing dust on initial spawn");
            var so=new SerializedObject(ground); Require(so.FindProperty("footAnchors").arraySize==2,path+" has two authored foot references");
            int rendererCount=root.GetComponentsInChildren<Renderer>(true).Length;
            Color32[] grounded=Capture(Path.GetFileNameWithoutExtension(path)+"-contact.png");
            var props=new MaterialPropertyBlock(); ground.ShadowRenderer.GetPropertyBlock(props); float groundedAlpha=props.GetColor("_Color").a;
            var initialPosition=root.transform.position;
            root.transform.position+=Vector3.up; Physics2D.SyncTransforms(); yield return Frames();
            ground.ShadowRenderer.GetPropertyBlock(props);
            Require(!ground.IsGrounded && ground.GroundDistance>.8f && props.GetColor("_Color").a<groundedAlpha,path+" airborne shadow softens and no landing is emitted");
            Require(ground.DustBurstCount==0,path+" airborne does not emit dust");
            root.transform.position=initialPosition; Physics2D.SyncTransforms(); yield return Frames();
            Require(ground.IsGrounded && ground.DustBurstCount==1,path+" landing emits one bounded dust burst");
            Capture(Path.GetFileNameWithoutExtension(path)+"-landing.png");
            yield return Seconds(.4f);
            var feedback=unit.GetComponent<CharacterShaderFeedback>(); feedback.SetPhaseVisibility(0); yield return Frames();
            Require(!ground.ShadowRenderer.enabled,path+" fully phased character hides contact shadow");
            feedback.SetPhaseVisibility(1); floor.SetActive(false); yield return Frames();
            Require(!ground.IsGrounded && !ground.ShadowRenderer.enabled,path+" missing floor does not invent a ground plane");
            floor.SetActive(true); Physics2D.SyncTransforms(); yield return Frames();
            int count=ground.DustBurstCount; ground.BeginStride(.8f); float started=Time.unscaledTime;
            while(Time.unscaledTime-started<.85f)
            { root.transform.position=initialPosition+Vector3.right*Mathf.Min(.8f,Time.unscaledTime-started); Physics2D.SyncTransforms(); yield return null; }
            Require(ground.DustBurstCount>count && ground.DustBurstCount<=count+2,path+" moving stride emits at most two contact bursts ("+(ground.DustBurstCount-count)+", grounded="+ground.IsGrounded+")");
            Require(rendererCount==root.GetComponentsInChildren<Renderer>(true).Length,path+" reuses shadow, particle and accent renderers");
            root.transform.position=initialPosition; Physics2D.SyncTransforms(); yield return Frames();
            var attackerObject=new GameObject("Attacker fixture"); attackerObject.transform.position=new Vector3(-3,1,0);
            var attacker=attackerObject.AddComponent<CharacterUnit>(); attacker.enabled=false;
            var source=attackerObject.AddComponent<CombatImpactPresentation>();
            int hp=unit.currentHP; float speed=animator.speed; Vector3 position=unit.transform.position;
            source.BeginAction(ActionType.LightAttack,false,1); yield return Frames();
            Require(!accent.AccentRenderer.enabled,path+" action start alone does not imply a hit");
            accent.PlayResolvedImpact(attacker); Require(accent.LastImpactTier==0 && accent.AccentRenderer.enabled,path+" resolved light hit selects short accent");
            float lightSize=accent.AccentRenderer.bounds.size.x; Capture(Path.GetFileNameWithoutExtension(path)+"-light.png");
            source.BeginAction(ActionType.HeavyAttack,true,1); accent.PlayResolvedImpact(attacker);
            Require(accent.LastImpactTier==1 && accent.AccentRenderer.bounds.size.x>lightSize,path+" released heavy hit has larger accent");
            Capture(Path.GetFileNameWithoutExtension(path)+"-heavy.png");
            accent.PlayResolvedImpact(attacker,true); Require(accent.LastImpactTier==2,path+" successful parry has distinct accent");
            Capture(Path.GetFileNameWithoutExtension(path)+"-parry.png");
            Time.timeScale=0; yield return Seconds(.25f);
            Require(!accent.AccentRenderer.enabled,path+" impact cleans up during hit stop"); Time.timeScale=1;
            Require(unit.currentHP==hp && animator.speed==speed && unit.transform.position==position,path+" accents do not mutate HP, animation speed or actor position");
            root.SetActive(false); yield return Frames(); root.SetActive(true); animator.speed=0; yield return Frames();
            Require(!accent.AccentRenderer.enabled,path+" re-enable has no stale impact");
            foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
            {
                bool hit=clip.name.IndexOf("hit",StringComparison.OrdinalIgnoreCase)>=0;
                Require(Mathf.Approximately(clip.frameRate,60) && Mathf.Abs(clip.length-(hit?.5f:1f))<.02f,path+" authored "+clip.name+" keeps 60 FPS / "+(hit?"0.5":"1")+" seconds");
            }
            var slash=unit.GetComponent<KnightSlashShaderVFX>();
            if(slash!=null)
            {
                animator.Play("LightAttack",0,.1f); animator.Update(0); slash.PlaySlash(ActionType.LightAttack,false,4); yield return Frames(2);
                Require(!slash.IsEmitting,path+" no sword trail in early anticipation");
                animator.Play("LightAttack",0,.5f); animator.Update(0); yield return Frames(2);
                Require(slash.IsEmitting && Mathf.Abs(slash.VisualPhase-.5f)<.02f,path+" sword trail active at frame 30 / midpoint");
                Time.timeScale=0; yield return Frames(2); Require(!slash.IsEmitting,path+" paused action stops new trail samples"); Time.timeScale=1;
                animator.Play("LightAttack",0,.9f); animator.Update(0); yield return Frames(2);
                Require(!slash.IsEmitting && animator.speed==0,path+" recovery clears sword trail without retiming animation");
            }
            Object.Destroy(attackerObject); Object.Destroy(root); yield return Frames(8);
        }
        yield return Ward();
        var step=new GameObject("Beat config fixture").AddComponent<BattleStepPlayer>(); step.SetBeatConfig(120,2);
        Require(Mathf.Approximately(step.GetStepDuration(),1f),"Existing 120 BPM / 2 beat action remains 1 second");
        Require(CombatImpactPresentation.ResolveTier(ActionType.HeavyAttack,false)==0,"Unreleased heavy charge cannot select heavy impact");
        Require(CombatImpactPresentation.ResolveTier(ActionType.Rift,true)==1,"Rift release selects strong impact");
        Object.Destroy(step.gameObject);
    }
    private static IEnumerator Ward()
    {
        floor.SetActive(false); camera.transform.position=new Vector3(0,0,-10); camera.orthographicSize=2.8f;
        var effect=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefab/Oracle/vfx/Oracle_WardVFX.prefab"));
        var sprites=effect.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.sharedMaterial!=null && s.sharedMaterial.HasProperty("_Mode") && Mathf.Approximately(s.sharedMaterial.GetFloat("_Mode"),1)).ToArray();
        Require(sprites.Length>0,"Ward prefab uses ward shader mode");
        var visual=sprites.OrderByDescending(s=>s.sortingOrder).First();
        effect.transform.localScale*=4f/Mathf.Max(visual.bounds.size.x,visual.bounds.size.y); effect.transform.position-=visual.bounds.center;
        var material=new Material(visual.sharedMaterial); foreach(var sprite in sprites) sprite.sharedMaterial=material;
        var driver=effect.GetComponent<CombatEffectShaderDriver>(); driver.Play(4); yield return Frames();
        material.SetFloat("_WardCenterOpacity",1); material.SetFloat("_WardRimStrength",0); yield return Frames();
        Color32[] opaque=Capture("ward-center-before.png");
        material.SetFloat("_WardCenterOpacity",.18f); material.SetFloat("_WardRimStrength",.35f); yield return Frames();
        Color32[] clear=Capture("ward-center-after.png");
        Require(Difference(opaque,clear)>.001f,"Ward transparency changes actual rendered pixels");
        Vector3 center=visual.bounds.center; driver.PulseImpact(center-Vector3.right*visual.bounds.extents.x*.65f);
        var props=new MaterialPropertyBlock(); visual.GetPropertyBlock(props); float left=props.GetVector("_ImpactUV").x;
        yield return Seconds(.12f); Capture("ward-impact-left.png");
        driver.PulseImpact(center+Vector3.right*visual.bounds.extents.x*.65f); visual.GetPropertyBlock(props); float right=props.GetVector("_ImpactUV").x;
        Require(Mathf.Abs(right-left)>.3f,"World impact location maps to distinct left/right ward UVs");
        yield return Seconds(.12f); Capture("ward-impact-right.png");
        driver.Play(1); visual.GetPropertyBlock(props); Require(props.GetFloat("_ImpactStart")<0,"Reused ward resets old impact ripple");
        Require(Mathf.Approximately(Resources.Load<Material>("Combat/Materials/OracleWard").GetFloat("_WardCenterOpacity"),.18f),"Shared ward preset remains intact after render comparisons");
        Object.Destroy(effect); Object.Destroy(material); yield return Frames();
    }
    private static Color32[] Capture(string name)
    {
        var target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32); var previous=RenderTexture.active;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var texture=new Texture2D(960,540,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply();
        var pixels=texture.GetPixels32(); if(name!=null) File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); Object.Destroy(texture); return pixels;
    }
    private static float Difference(Color32[] a,Color32[] b)
    { double sum=0; for(int i=0;i<a.Length;i++) sum+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b); return (float)(sum/(a.Length*765)); }
}
