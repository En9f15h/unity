#if UNITY_EDITOR || CODEX_BATTLE_CAMERA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(1200)]
public sealed class BattleCameraProbe : MonoBehaviour
{
    public DynamicBattleCamera2D rig;
    public SpriteRenderer background;
    public Sprite[] maps;
    public GameObject[] characters;
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    string output; Camera cam; CameraShake shake; GameObject left, right;
    Transform measuredHud;
    float renderedOffset;
    Vector3 renderedHudViewport;
    bool measureMotion;
    Vector3 measuredPose;
    float measuredSize, maxMotion, maxZoom;
    void BeginMotionMeasurement()
    { measuredPose=cam.transform.position; measuredSize=cam.orthographicSize; maxMotion=maxZoom=0; measureMotion=true; }
    private void LateUpdate()
    {
        if (cam == null || rig == null) return;
        renderedOffset = Vector3.Distance(cam.transform.position, rig.StablePosition);
        if (measuredHud != null) renderedHudViewport = cam.WorldToViewportPoint(measuredHud.position);
        if (measureMotion)
        { maxMotion=Mathf.Max(maxMotion,Vector3.Distance(cam.transform.position,measuredPose)); maxZoom=Mathf.Max(maxZoom,Mathf.Abs(cam.orthographicSize-measuredSize)); }
    }
    void Check(bool b, string s) { if (!b) throw new Exception(s); checks.Add("PASS: " + s); }
    void Log(string s, string trace, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(s + "\n" + trace); }
    IEnumerator Delay(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
    void Capture(string name)
    {
        var rt = new RenderTexture(1280, 720, 24); var prev = cam.targetTexture; var active = RenderTexture.active;
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0,0,1280,720),0,0); tex.Apply();
        File.WriteAllBytes(output + "/" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = prev; RenderTexture.active = active; rt.Release(); Destroy(rt); Destroy(tex);
    }
    GameObject Spawn(int index, float x)
    {
        var go = Instantiate(characters[index], new Vector3(x, -.75f, 0), Quaternion.identity);
        foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (script.GetType().Assembly == typeof(CharacterUnit).Assembly) script.enabled = false;
        foreach (var body in go.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
        go.GetComponentInChildren<Animator>(true).enabled = false;
        return go;
    }
    IEnumerator Run()
    {
        cam = rig.GetComponent<Camera>(); shake = rig.GetComponent<CameraShake>(); cam.aspect = 16f/9f;
        var stage = UnityEngine.Object.FindFirstObjectByType<BackgroundSpriteSync>(FindObjectsInactive.Include);
        var stageFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var floor = (SpriteRenderer)typeof(BackgroundSpriteSync).GetField("floorRenderer",stageFlags).GetValue(stage);
        var floors = (Sprite[])typeof(BackgroundSpriteSync).GetField("floorSprites",stageFlags).GetValue(stage);
        var ground = floor.GetComponent<Collider2D>();
        Bounds groundBefore = ground.bounds;
        Check(floor.sprite == floors[Array.IndexOf(maps,background.sprite)], "Initial stage already has its matching floor before room synchronization");
        for (int skin = 0; skin < characters.Length; skin++)
        {
            if (left != null) { Destroy(left); Destroy(right); yield return null; }
            left = Spawn(skin, -3); right = Spawn(skin, 3); rig.SetTargets(left.transform, right.transform);
            Check(rig.GetTargetA().CompareTag("Root") && rig.GetTargetB().CompareTag("Root"), "Tagged Root targets retained for skin " + skin);
            yield return Delay(2.5f);
            var a = left.GetComponentInChildren<Animator>(true); var b = right.GetComponentInChildren<Animator>(true);
            Vector3 pose = cam.transform.position; float size = cam.orthographicSize;
            var clips = a.runtimeAnimatorController.animationClips.Distinct().ToArray();
            var idle = clips.First(c => c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0);
            BeginMotionMeasurement();
            for (int i = 0; i < 120; i++)
            {
                idle.SampleAnimation(a.gameObject, (i % 60)/60f*idle.length);
                idle.SampleAnimation(b.gameObject, ((i+17) % 60)/60f*idle.length);
                yield return null;
            }
            Check(maxMotion < .003f && maxZoom < .003f, "Idle peak camera motion/zoom for skin " + skin + ": " + maxMotion + " / " + maxZoom);
            pose = cam.transform.position; size = cam.orthographicSize;
            foreach (var clip in clips)
            {
                BeginMotionMeasurement();
                for (int i=0;i<16;i++)
                {
                    clip.SampleAnimation(a.gameObject,i/15f*clip.length); yield return null;
                    if (i==8 && (clip.name.IndexOf("Jump",StringComparison.OrdinalIgnoreCase)>=0 || clip.name.IndexOf("HeavyAttack",StringComparison.OrdinalIgnoreCase)>=0))
                        Capture("skin-"+skin+"-"+clip.name);
                }
                Check(maxMotion<.003f && maxZoom<.003f, "Animation peak motion stays stable: skin " + skin + " / " + clip.name);
            }
            measureMotion=false;
            idle.SampleAnimation(a.gameObject,0); idle.SampleAnimation(b.gameObject,0); yield return null;
            Capture("skin-" + skin);
        }
        Vector3 before = rig.StablePosition;
        left.transform.position += Vector3.right * 2; right.transform.position += Vector3.right * 2;
        yield return Delay(1.5f);
        Check(rig.StablePosition.x > before.x + 1, "Camera follows real gameplay translation");
        yield return Delay(1);
        Vector3 recoilBaseline = rig.StablePosition;
        var manager = UnityEngine.Object.FindFirstObjectByType<TurnPlanningManager>(FindObjectsInactive.Include);
        for (int i=0;i<5;i++)
            yield return (IEnumerator)typeof(TurnPlanningManager).GetMethod("PlayHitShakeCoroutine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{left.transform});
        yield return Delay(.5f);
        Check(Vector3.Distance(recoilBaseline,rig.StablePosition)<.003f && left.GetComponent<CharacterUnit>().PresentationOffset.sqrMagnitude<.000001f, "Real character hit recoil never feeds back into camera tracking");
        left.transform.position = new Vector3(-7.5f,-.75f,0); right.transform.position = new Vector3(7.5f,-.75f,0);
        yield return Delay(1.5f); float wideSize = cam.orthographicSize;
        Check(cam.WorldToViewportPoint(left.transform.position).x > .03f && cam.WorldToViewportPoint(right.transform.position).x < .97f, "Wide duel keeps both players in frame");
        Capture("wide-arena");
        left.transform.position = new Vector3(-.6f,-.75f,0); right.transform.position = new Vector3(.6f,-.75f,0);
        yield return Delay(2);
        Check(cam.orthographicSize < wideSize-.3f, "Close duel smoothly tightens composition");
        Check(Mathf.Abs(cam.WorldToViewportPoint(new Vector3(0,ground.bounds.max.y,0)).y-.34f)<.01f, "Close composition keeps ground above bottom planning HUD");
        // Ground composition now follows the eased zoom. Finish that transition
        // before measuring whether an impact leaves any persistent offset.
        yield return Delay(1.5f);
        Vector3 stable = rig.StablePosition; float baseSize = cam.orthographicSize;
        var hud = cam.transform.GetChild(0); Vector3 hudViewport = cam.WorldToViewportPoint(hud.position);
        measuredHud = hud;
        float maxOffset=0, maxHudDelta=0;
        for(int hit=0;hit<6;hit++)
        {
            shake.Impact(ActionType.Ultimate,true,Vector2.right);
            float until=Time.realtimeSinceStartup+.3f;
            while(Time.realtimeSinceStartup<until)
            { yield return null; maxOffset=Mathf.Max(maxOffset,renderedOffset); maxHudDelta=Mathf.Max(maxHudDelta,Vector2.Distance(renderedHudViewport,hudViewport)); }
        }
        yield return Delay(.3f);
        Check(maxOffset>.015f && maxOffset<.15f, "Impact is visible and bounded: " + maxOffset);
        Check(Vector3.Distance(cam.transform.position,stable)<.003f, "Repeated impacts return exactly to stable camera pose");
        Check(maxHudDelta<.002f, "World-space HP bar stays fixed during shake: " + maxHudDelta);
        Time.timeScale = 0; shake.Impact(ActionType.HeavyAttack,true,Vector2.right); yield return Delay(.5f);
        Check(Vector3.Distance(cam.transform.position,rig.StablePosition)<.003f, "Impact recovers even when timeScale is zero"); Time.timeScale=1;
        shake.Impact(ActionType.Ultimate,true,Vector2.right); yield return null; shake.enabled=false;
        Check(Vector3.Distance(cam.transform.position,rig.StablePosition)<.003f, "Disabling shake removes its offset"); shake.enabled=true;
        foreach(float aspect in new[]{4f/3f,16f/9f,21f/9f})
        {
            cam.aspect=aspect;
            for(int mapIndex=0;mapIndex<maps.Length;mapIndex++)
            {
                var map=maps[mapIndex];
                typeof(BackgroundSpriteSync).GetMethod("ApplyBackground",stageFlags).Invoke(stage,new object[]{mapIndex});
                yield return Delay(.15f);
                Check(floor.sprite==floors[mapIndex], "Stage and floor stay paired: " + map.name + " / " + aspect);
                Check(ground.bounds==groundBefore, "Stage selection preserves gameplay floor bounds: " + map.name + " / " + aspect);
                Bounds bounds=background.bounds;
                Vector3 lo=cam.ViewportToWorldPoint(new Vector3(0,0,10)), hi=cam.ViewportToWorldPoint(new Vector3(1,1,10));
                Check(lo.x>=bounds.min.x-.002f && hi.x<=bounds.max.x+.002f && lo.y>=bounds.min.y-.002f && hi.y<=bounds.max.y+.002f, "Background contains viewport after map/aspect change: " + map.name + " / " + aspect);
            }
        }
        cam.aspect=16f/9f; typeof(BackgroundSpriteSync).GetMethod("ApplyBackground",stageFlags).Invoke(stage,new object[]{0}); yield return Delay(1); Capture("close-duel");
        Check(errors.Count==0,"No runtime errors");
    }
    IEnumerator Start()
    {
        Application.runInBackground=true; Application.targetFrameRate=120; Application.logMessageReceived+=Log;
        var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-camera-output"); output=i>=0?args[i+1]:"CameraResults"; Directory.CreateDirectory(output);
        var stack=new Stack<IEnumerator>(); stack.Push(Run()); int result=0;
        while(stack.Count>0)
        {
            object next;
            try { if(!stack.Peek().MoveNext()){stack.Pop();continue;} next=stack.Peek().Current; if(next is IEnumerator nested){stack.Push(nested);continue;} }
            catch(Exception e){checks.Add("FAILED: "+e);result=1;break;}
            yield return next;
        }
        Time.timeScale=1; File.WriteAllLines(output+"/validation.txt",checks); File.WriteAllLines(output+"/errors.txt",errors); Application.logMessageReceived-=Log; Application.Quit(result);
    }
}
#endif
