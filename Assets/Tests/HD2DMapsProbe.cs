#if UNITY_EDITOR || CODEX_HD2D_MAPS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public sealed class HD2DMapsProbe : MonoBehaviour
{
    public DynamicBattleCamera2D rig;
    public HD2DStagePresentation stage;
    public SpriteRenderer background;
    public GameObject[] characters;
    readonly List<string> checks=new List<string>(), errors=new List<string>();
    string output; Camera cam; GameObject left,right;
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add("PASS: "+text);}
    void Log(string text,string trace,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(text+"\n"+trace);}
    IEnumerator Delay(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    Texture2D Read(RenderTexture rt)
    {
        var active=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=active;return tex;
    }
    Color[] StagePixels(){stage.StageCamera.Render();var tex=Read(stage.StageTexture);var pixels=tex.GetPixels();Destroy(tex);return pixels;}
    double Difference(Color[] a,Color[] b){double d=0;for(int i=0;i<Math.Min(a.Length,b.Length);i+=17)d+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);return d/(Math.Min(a.Length,b.Length)/17.0*3);}
    void Capture(string name)
    {
        if(stage.IsStageActive)stage.StageCamera.Render();
        var rt=new RenderTexture(Mathf.RoundToInt(720*cam.aspect),720,24);var old=cam.targetTexture;cam.targetTexture=rt;cam.Render();
        var tex=Read(rt);File.WriteAllBytes(output+"/"+name+".png",tex.EncodeToPNG());cam.targetTexture=old;rt.Release();Destroy(rt);Destroy(tex);
    }
    Color[] CharacterPixels()
    {
        if(stage.IsStageActive)stage.StageCamera.Render();
        var rt=new RenderTexture(1280,720,24);var old=cam.targetTexture;cam.targetTexture=rt;cam.Render();
        var tex=Read(rt);var pixels=tex.GetPixels();cam.targetTexture=old;rt.Release();Destroy(rt);Destroy(tex);return pixels;
    }
    int NoticeablePixels(Color[] a, Color[] b, CharacterShaderFeedback feedback)
    {
        var bounds=feedback.BodyRenderers[0].bounds;
        foreach(var renderer in feedback.BodyRenderers)bounds.Encapsulate(renderer.bounds);
        var lo=cam.WorldToViewportPoint(bounds.min);var hi=cam.WorldToViewportPoint(bounds.max);
        int count=0;
        for(int y=Mathf.Clamp(Mathf.FloorToInt(lo.y*720),0,719);y<=Mathf.Clamp(Mathf.CeilToInt(hi.y*720),0,719);y++)
            for(int x=Mathf.Clamp(Mathf.FloorToInt(lo.x*1280),0,1279);x<=Mathf.Clamp(Mathf.CeilToInt(hi.x*1280),0,1279);x++)
            {
                int i=y*1280+x;var delta=a[i]-b[i];
                if(Mathf.Max(Mathf.Abs(delta.r),Mathf.Abs(delta.g),Mathf.Abs(delta.b))>.04f)count++;
            }
        return count;
    }
    void SetHit(CharacterShaderFeedback feedback,float amount)
    {
        var block=new MaterialPropertyBlock();
        foreach(var renderer in feedback.BodyRenderers)
        { renderer.GetPropertyBlock(block);block.SetFloat("_HitAmount",amount);block.SetColor("_HitColor",Color.white);renderer.SetPropertyBlock(block); }
    }
    GameObject Spawn(int index,float x)
    {
        var obj=Instantiate(characters[index],new Vector3(x,-.75f,0),Quaternion.identity);
        foreach(var script in obj.GetComponentsInChildren<MonoBehaviour>(true))if(script.GetType().Assembly==typeof(CharacterUnit).Assembly && !(script is CharacterGroundPresentation) && !(script is CharacterShaderFeedback) && !(script is CharacterProjectedShadow))script.enabled=false;
        foreach(var rb in obj.GetComponentsInChildren<Rigidbody2D>(true))rb.simulated=false;
        obj.GetComponentInChildren<Animator>(true).enabled=false;return obj;
    }
    IEnumerator Run()
    {
        cam=rig.GetComponent<Camera>();cam.aspect=16f/9f;
        stage.AnimateCharacterLight=false;
        var sync=UnityEngine.Object.FindFirstObjectByType<BackgroundSpriteSync>(FindObjectsInactive.Include);
        var apply=typeof(BackgroundSpriteSync).GetMethod("ApplyBackground",Flags);
        var floor=(SpriteRenderer)typeof(BackgroundSpriteSync).GetField("floorRenderer",Flags).GetValue(sync);
        var ground=floor.GetComponent<Collider2D>();var originalBounds=ground.bounds;
        apply.Invoke(sync,new object[]{0});
        left=Spawn(0,-3);right=Spawn(1,3);rig.SetTargets(left.transform,right.transform);
        yield return Delay(3);
        var coldStart=stage.CharacterLightSource.transform.position;
        stage.AnimateCharacterLight=true;yield return Delay(.25f);
        Check(Vector3.Distance(coldStart,stage.CharacterLightSource.transform.position)>.001f,"Overhead daylight drifts gently using the visual clock");
        stage.AnimateCharacterLight=false;stage.CharacterLightSource.transform.position=coldStart;yield return Delay(.1f);
        Check(stage.IsStageActive,"Arena index 0 enables the dedicated stage");
        Check(stage.StageCamera.GetUniversalAdditionalCameraData().scriptableRenderer is UniversalRenderer,"Stage uses a real 3D UniversalRenderer");
        Check(cam.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().Name=="Renderer2D","Gameplay camera retains Renderer2D");
        Check(stage.StageCamera.cullingMask==(1<<LayerMask.NameToLayer("HD2DStage")),"Stage camera excludes characters and UI");
        Check(stage.StageCamera.GetComponentsInParent<Transform>()[1].GetComponentsInChildren<Collider>().Length==0,"Stage geometry has no gameplay colliders");
        Check(stage.StageTexture.IsCreated() && stage.StageTexture.height<=900,"Stage render target created with bounded resolution");
        Check(stage.CharacterLightSource.transform.position.y-ground.bounds.max.y>=9f,"Daylight source stays above the characters");
        Check(!stage.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("Brazier") || t.name=="Warm ember" || t.name=="Warm local light"),"Added torch meshes and their warm lights are absent");
        var canopyPixels=StagePixels();
        typeof(HD2DStagePresentation).GetField("sunlightCanopy",Flags).SetValue(stage,0f);yield return Delay(.1f);Capture("canopy-off");
        Check(Difference(canopyPixels,StagePixels())>.0001,"Overhead sunlight canopy changes real background pixels");
        typeof(HD2DStagePresentation).GetField("sunlightCanopy",Flags).SetValue(stage,.24f);yield return Delay(.1f);
        var original=StagePixels();double brightness=original.Average(c=>(double)(c.r+c.g+c.b)/3);
        Check(brightness>.08 && brightness<.9,"Stage brightness remains readable: "+brightness);
        Capture("arena-on");
        var characterMaterials=left.GetComponentsInChildren<SpriteRenderer>(true).Select(r=>r.sharedMaterial).ToArray();
        stage.enabled=false;yield return Delay(.2f);Capture("arena-off");
        Check(stage.StageTexture==null,"Disabling stage releases its render target");
        stage.enabled=true;yield return Delay(.5f);
        Check(characterMaterials.SequenceEqual(left.GetComponentsInChildren<SpriteRenderer>(true).Select(r=>r.sharedMaterial)),"Stage toggle leaves character materials unchanged");
        original=StagePixels();stage.KeyLight.intensity=0;
        typeof(HD2DStagePresentation).GetField("keyIntensity",Flags).SetValue(stage,0f);
        yield return Delay(.2f);Capture("key-off");var unlit=StagePixels();
        Check(Difference(original,unlit)>.002,"Directional lighting changes actual rendered geometry: "+Difference(original,unlit));
        typeof(HD2DStagePresentation).GetField("keyIntensity",Flags).SetValue(stage,1.15f);yield return Delay(.2f);
        original=StagePixels();stage.KeyLight.shadows=LightShadows.None;yield return Delay(.2f);Capture("shadows-off");var noShadow=StagePixels();
        Check(Difference(original,noShadow)>.00001,"Geometry shadows change rendered pixels: "+Difference(original,noShadow));
        stage.KeyLight.shadows=LightShadows.Soft;stage.BackgroundSoftness=0;stage.MistDensity=0;yield return Delay(.2f);original=StagePixels();
        stage.BackgroundSoftness=.8f;stage.MistDensity=.035f;yield return Delay(.2f);
        Check(Difference(original,StagePixels())>.00005,"Background softness and haze are visible without editing image assets");
        original=StagePixels();typeof(HD2DStagePresentation).GetField("bloomIntensity",Flags).SetValue(stage,0f);yield return Delay(.15f);
        // There are no emissive braziers now; daylight must remain readable without bloom.
        var daylightWithoutBloom=StagePixels();
        Check(daylightWithoutBloom.Average(c=>(double)(c.r+c.g+c.b)/3)>.08 && Difference(original,daylightWithoutBloom)<.001,
            "Daylight exposure stays stable without optional bloom");
        typeof(HD2DStagePresentation).GetField("bloomIntensity",Flags).SetValue(stage,.12f);
        for(int skin=0;skin<4;skin++)
        {
            Destroy(left);Destroy(right);yield return null;left=Spawn(skin,-2);right=Spawn((skin+1)%4,2);rig.SetTargets(left.transform,right.transform);yield return Delay(2.5f);
            var animator=left.GetComponentInChildren<Animator>(true);var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
            var idle=clips.First(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);idle.SampleAnimation(animator.gameObject,0);yield return Delay(.1f);Capture("skin-"+skin);
            var projection=left.GetComponent<CharacterProjectedShadow>();var projectedIdle=projection.ProjectionBounds;
            Check(projection.VisiblePartCount>0,"Pose projection visible for skin "+skin);
            Check(projectedIdle.max.y<=left.GetComponent<CharacterGroundPresentation>().GroundPoint.y+.001f,"Projection stays on the ground for skin "+skin);
            var projectedPixels=CharacterPixels();projection.enabled=false;Capture("skin-"+skin+"-projection-off");
            Check(Difference(projectedPixels,CharacterPixels())>.000001,"Projection changes real floor pixels for skin "+skin);
            int meshCount=projection.MeshCount;projection.enabled=true;yield return Delay(.1f);
            Check(projection.MeshCount==meshCount,"Re-enabling reuses projection meshes for skin "+skin);
            var bodyCollider=left.GetComponent<Collider2D>();bodyCollider.enabled=false;yield return Delay(.1f);
            Check(projection.VisiblePartCount==0,"Disabled body collider leaves no stale projection for skin "+skin);
            bodyCollider.enabled=true;yield return Delay(.1f);
            var feedback=left.GetComponent<CharacterShaderFeedback>();
            var property=new MaterialPropertyBlock();feedback.BodyRenderers[0].GetPropertyBlock(property);
            Check(property.GetFloat("_HD2DStrength")==1,"Character lighting enabled for skin "+skin);
            var pose=animator.GetComponentsInChildren<Transform>().Select(t=>t.localPosition).ToArray();float speed=animator.speed;
            var litCharacter=CharacterPixels();stage.CharacterLightingEnabled=false;yield return Delay(.1f);Capture("skin-"+skin+"-light-off");
            Check(Difference(litCharacter,CharacterPixels())>.000001,"Character lighting changes rendered pixels for skin "+skin);
            Check(pose.SequenceEqual(animator.GetComponentsInChildren<Transform>().Select(t=>t.localPosition)) && animator.speed==speed,"Lighting preserves bones and animation speed for skin "+skin);
            stage.CharacterLightingEnabled=true;yield return Delay(.1f);
            // Immediate captures isolate the graph from clocks, shadows, and other effects.
            var directionalPixels=CharacterPixels();
            foreach(var renderer in feedback.BodyRenderers){renderer.GetPropertyBlock(property);property.SetFloat("_HD2DStrength",0);renderer.SetPropertyBlock(property);}
            int changed=NoticeablePixels(directionalPixels,CharacterPixels(),feedback);
            Check(changed>=12,"Directional graph visibly changes character pixels (>4% channel, >=12 pixels), skin "+skin+": "+changed);
            foreach(var renderer in feedback.BodyRenderers)
            {renderer.GetPropertyBlock(property);property.SetFloat("_HD2DStrength",1);var anchor=property.GetVector("_HD2DAnchor");anchor.w=-anchor.w;property.SetVector("_HD2DAnchor",anchor);renderer.SetPropertyBlock(property);}
            int reversed=NoticeablePixels(directionalPixels,CharacterPixels(),feedback);
            Check(reversed>=12,"Reversing light changes character shading, skin "+skin+": "+reversed);
            Capture("skin-"+skin+"-reversed-light");
            yield return Delay(.1f);
            HD2DStagePresentation.TryGetCharacterLighting(left,new Vector3(0,0,0),out var centralLight,out _,out _);
            HD2DStagePresentation.TryGetCharacterLighting(left,new Vector3(stage.CharacterLightSource.transform.position.x,0,0),out var nearLight,out _,out _);
            Check(nearLight.r+nearLight.g+nearLight.b>centralLight.r+centralLight.g+centralLight.b,"Point source preserves distance falloff for skin "+skin);
            var source=stage.CharacterLightSource;var lightPosition=source.transform.position;
            source.transform.position=new Vector3(-7,lightPosition.y,lightPosition.z);yield return Delay(.1f);
            Capture("skin-"+skin+"-cold-left");var coldPixels=CharacterPixels();var shadowLeft=projection.ProjectionBounds;
            source.transform.position=new Vector3(7,lightPosition.y,lightPosition.z);yield return Delay(.1f);
            Capture("skin-"+skin+"-cold-right");
            Check(NoticeablePixels(coldPixels,CharacterPixels(),feedback)>=12,"Moving actual cold Light changes character shading, skin "+skin);
            Check(Vector3.Distance(shadowLeft.center,projection.ProjectionBounds.center)>.05f,"Projected pose follows moving cold source, skin "+skin);
            source.transform.position=lightPosition;yield return Delay(.1f);
            SetHit(feedback,1);var hitLit=CharacterPixels();
            // Disable just the shader contribution without touching shadows or advancing animation.
            foreach(var renderer in feedback.BodyRenderers){renderer.GetPropertyBlock(property);property.SetFloat("_HD2DStrength",0);renderer.SetPropertyBlock(property);}
            Check(Difference(hitLit,CharacterPixels())<.000001,"Full hit flash overrides HD2D lighting for skin "+skin);
            feedback.SetPhaseVisibility(0);yield return Delay(.1f);Capture("skin-"+skin+"-hidden");
            feedback.BodyRenderers[0].GetPropertyBlock(property);
            Check(property.GetFloat("_Dissolve")==1 && !left.GetComponent<CharacterGroundPresentation>().ShadowRenderer.enabled && projection.VisiblePartCount==0,"Hidden character hides both shadows for skin "+skin);
            feedback.SetPhaseVisibility(1);yield return Delay(.1f);
            left.transform.position=new Vector3(-5.2f,-.75f,0);yield return Delay(.5f);Capture("skin-"+skin+"-warm");
            left.transform.position=new Vector3(-2,-.75f,0);yield return Delay(.5f);
            foreach(var clip in clips.Where(c=>c.name.IndexOf("Jump",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("HeavyAttack",StringComparison.OrdinalIgnoreCase)>=0 || c.name=="Bolt"))
            {clip.SampleAnimation(animator.gameObject,clip.length*.5f);yield return Delay(.1f);Capture("skin-"+skin+"-"+clip.name);
                Check(Vector3.Distance(projectedIdle.size,projection.ProjectionBounds.size)>.001f || Vector3.Distance(projectedIdle.center,projection.ProjectionBounds.center)>.001f,"Projection follows bone pose: "+skin+"/"+clip.name);}
            var ownerScale=left.transform.localScale;left.transform.localScale=new Vector3(-ownerScale.x,ownerScale.y,ownerScale.z);yield return Delay(.1f);Capture("skin-"+skin+"-mirrored");
            Check(projection.VisiblePartCount>0 && projection.ProjectionBounds.size.x>0,"Mirrored owner retains valid projection for skin "+skin);
            left.transform.localScale=ownerScale;
            Check(left.GetComponent<CharacterGroundPresentation>().ShadowRenderer!=null,"Existing contact shadow retained for skin "+skin);
        }
        left.transform.position=new Vector3(-7.5f,-.75f,0);right.transform.position=new Vector3(7.5f,-.75f,0);yield return Delay(2);Capture("wide-arena");
        Check(cam.WorldToViewportPoint(left.transform.position).x>.02f && cam.WorldToViewportPoint(right.transform.position).x<.98f,"Wide duel stays in frame");
        left.transform.position=new Vector3(-.6f,-.75f,0);right.transform.position=new Vector3(.6f,-.75f,0);
        foreach(var character in new[]{left,right})
        {
            var animator=character.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController.animationClips.First(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0).SampleAnimation(animator.gameObject,0);
        }
        yield return Delay(3);Capture("close-duel");
        foreach(float aspect in new[]{4f/3,16f/9,21f/9})
        {
            cam.aspect=aspect;yield return Delay(.3f);
            Check(Mathf.Abs((float)stage.StageTexture.width/stage.StageTexture.height-aspect)<.003,"Render target follows aspect "+aspect);
            Check(Vector3.Distance(cam.transform.position,stage.StageCamera.transform.position)<.0001f,"Stage camera follows final gameplay pose at aspect "+aspect);
            Capture("aspect-"+aspect.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
        }
        cam.aspect=16f/9;
        for(int map=1;map<7;map++)
        {
            apply.Invoke(sync,new object[]{map});yield return Delay(.15f);
            Check(!stage.IsStageActive && !stage.StageCamera.enabled || !stage.IsStageActive && !stage.StageCamera.gameObject.activeInHierarchy,"Non-sample map disables stage camera: "+map);
            Check(ground.bounds==originalBounds,"Map switching preserves ground collision: "+map);
            Check(stage.IsDaylightCanopyActive,"Map enables its overhead daylight canopy: "+map);
            var withCanopy=CharacterPixels();
            Capture("map-"+map+"-canopy-on");
            typeof(HD2DStagePresentation).GetField("sunlightCanopy",Flags).SetValue(stage,0f);yield return Delay(.1f);
            Capture("map-"+map+"-canopy-off");
            Check(!stage.IsDaylightCanopyActive && Difference(withCanopy,CharacterPixels())>.00001,
                "Map canopy visibly changes rendered pixels and can be disabled: "+map);
            typeof(HD2DStagePresentation).GetField("sunlightCanopy",Flags).SetValue(stage,.24f);yield return Delay(.1f);
            var block=new MaterialPropertyBlock();left.GetComponent<CharacterShaderFeedback>().BodyRenderers[0].GetPropertyBlock(block);
            Check(block.GetFloat("_HD2DStrength")==1,"Map enables its own character environment: "+map);
            Check(left.GetComponent<CharacterProjectedShadow>().VisiblePartCount>0,"Map keeps character pose projection: "+map);
            Check(HD2DStagePresentation.TryGetCharacterEnvironment(left,out var environment) && environment.background==background.sprite && !environment.arenaBraziers,"Map resolves by Sprite identity without arena firelight: "+map);
            HD2DStagePresentation.TryGetCharacterLighting(left,Vector3.zero,out var middleLight,out _,out _);
            HD2DStagePresentation.TryGetCharacterLighting(left,new Vector3(5.2f,0,0),out var sideLight,out _,out _);
            Check(Mathf.Max(middleLight.r,middleLight.g,middleLight.b)-Mathf.Min(middleLight.r,middleLight.g,middleLight.b)<.08f && middleLight!=sideLight,
                "Map light preserves near-neutral body color and distance falloff: "+map);
            var mapSource=stage.CharacterLightSource;
            Check(mapSource.color==environment.daylightColor && Mathf.Approximately(mapSource.intensity,environment.daylightIntensity) &&
                Mathf.Abs(mapSource.transform.position.x-background.bounds.center.x-environment.daylightOffset.x)<.001f,
                "Changing map applies its own light color, intensity and direction: "+map);
            for(int skin=0;skin<4;skin++)
            {
                Destroy(left);Destroy(right);yield return null;
                left=Spawn(skin,-2);right=Spawn((skin+1)%4,2);rig.SetTargets(left.transform,right.transform);yield return Delay(.5f);
                var animator=left.GetComponentInChildren<Animator>(true);
                var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
                clips.First(c=>c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0).SampleAnimation(animator.gameObject,0);yield return Delay(.1f);
                Capture("map-"+map+"-skin-"+skin);
                left.GetComponent<CharacterShaderFeedback>().BodyRenderers[0].GetPropertyBlock(block);
                Check(block.GetFloat("_HD2DStrength")==1 && left.GetComponent<CharacterProjectedShadow>().VisiblePartCount>0,"Map/skin has light and shadow: "+map+"/"+skin);
                var lit=CharacterPixels();stage.CharacterLightingEnabled=false;yield return Delay(.1f);
                Capture("map-"+map+"-skin-"+skin+"-off");
                Check(Difference(lit,CharacterPixels())>.000001,"Map/skin visual contribution verified: "+map+"/"+skin);
                Check(left.GetComponent<CharacterProjectedShadow>().VisiblePartCount==0,"Disabling environment hides map/skin projection: "+map+"/"+skin);
                stage.CharacterLightingEnabled=true;yield return Delay(.1f);
                foreach(var clip in clips.Where(c=>c.name.IndexOf("Jump",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("HeavyAttack",StringComparison.OrdinalIgnoreCase)>=0 || c.name=="Bolt"))
                {
                    clip.SampleAnimation(animator.gameObject,clip.length*.5f);yield return Delay(.05f);
                    Check(left.GetComponent<CharacterProjectedShadow>().VisiblePartCount>0,"Map/skin action projection: "+map+"/"+skin+"/"+clip.name);
                }
                Capture("map-"+map+"-skin-"+skin+"-action");
                var feedback=left.GetComponent<CharacterShaderFeedback>();feedback.SetPhaseVisibility(0);yield return Delay(.05f);
                Check(left.GetComponent<CharacterProjectedShadow>().VisiblePartCount==0,"Map/skin hidden projection cleared: "+map+"/"+skin);
                feedback.SetPhaseVisibility(1);yield return Delay(.05f);
            }
        }
        var validBackground=background.sprite;background.sprite=null;yield return Delay(.1f);
        var fallbackBlock=new MaterialPropertyBlock();left.GetComponent<CharacterShaderFeedback>().BodyRenderers[0].GetPropertyBlock(fallbackBlock);
        Check(fallbackBlock.GetFloat("_HD2DStrength")==0 && left.GetComponent<CharacterProjectedShadow>().VisiblePartCount==0 && !stage.IsDaylightCanopyActive,"Unknown background safely clears character lighting and daylight canopy");
        background.sprite=validBackground;
        stage.CharacterLightSource.enabled=false;yield return Delay(.1f);
        left.GetComponent<CharacterShaderFeedback>().BodyRenderers[0].GetPropertyBlock(fallbackBlock);
        Check(fallbackBlock.GetFloat("_HD2DStrength")==0 && left.GetComponent<CharacterProjectedShadow>().VisiblePartCount==0,"Disabling the source clears its character light and projection");
        stage.CharacterLightSource.enabled=true;
        apply.Invoke(sync,new object[]{0});yield return Delay(.3f);
        Check(stage.IsStageActive,"Returning to arena restores stage");
        stage.enabled=false;yield return Delay(.2f);
        Check(!UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c=>c.name=="Stage camera"),"Disabling destroys auxiliary camera");
        stage.enabled=true;yield return Delay(.4f);
        Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.name=="Stage camera")==1,"Re-enabling creates exactly one auxiliary camera");
        Check(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t=>t.name=="HD2D stage target")==1,"Re-enabling retains exactly one stage render texture");
        Check(ground.bounds==originalBounds,"Stage never moves gameplay ground");
        Check(errors.Count==0,"No runtime or shader errors");
        File.WriteAllText(output+"/resources.txt","RT="+stage.StageTexture.width+"x"+stage.StageTexture.height+" ARGBHalf, 24-bit depth\nStage cameras=1\nLights=2 (one shadowed overhead directional, one unshadowed overhead character source); no added torches\nThis hidden-window test is not a GPU performance benchmark.");
        var scene=gameObject.scene;
        var shadowMeshes=Resources.FindObjectsOfTypeAll<Mesh>().Where(m=>m.name=="HD2D projected pose").ToArray();
        File.AppendAllText(output+"/resources.txt","\nLive pose shadow meshes="+shadowMeshes.Length+"; vertices="+shadowMeshes.Sum(m=>m.vertexCount)+"\nNo additional cameras, render textures, skeletons or per-frame materials for pose shadows.");
        DontDestroyOnLoad(gameObject);
        var empty=UnityEngine.SceneManagement.SceneManager.CreateScene("HD2D unload verification");
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(empty);
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        yield return Delay(.2f);
        Check(!Resources.FindObjectsOfTypeAll<Camera>().Any(c=>c.name=="Stage camera"),"Scene unload releases the stage camera");
        Check(!Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t=>t.name=="HD2D stage target"),"Scene unload releases the stage render texture");
        Check(!Resources.FindObjectsOfTypeAll<Material>().Any(m=>m.name.StartsWith("HD2D instance ") || m.name=="HD2D existing floor detail"),"Scene unload releases owned stage materials");
        Check(errors.Count==0,"Scene unload has no runtime errors");
        Check(!Resources.FindObjectsOfTypeAll<Mesh>().Any(m=>m.name=="HD2D projected pose"),"Scene unload releases projection meshes");
        Check(!Resources.FindObjectsOfTypeAll<Light>().Any(l=>l.name=="Overhead daylight source"),"Scene unload releases the owned daylight source");
        Check(!Resources.FindObjectsOfTypeAll<Mesh>().Any(m=>m.name=="Overhead daylight canopy"),"Scene unload releases the 2D daylight canopy mesh");
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;Application.targetFrameRate=120;Application.logMessageReceived+=Log;
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-hd2d-output");output=i>=0?args[i+1]:"HD2DResults";Directory.CreateDirectory(output);
        var stack=new Stack<IEnumerator>();stack.Push(Run());int code=0;
        while(stack.Count>0)
        {
            object next;
            try{if(!stack.Peek().MoveNext()){stack.Pop();continue;}next=stack.Peek().Current;if(next is IEnumerator nested){stack.Push(nested);continue;}}
            catch(Exception e){checks.Add("FAILED: "+e);code=1;break;}
            yield return next;
        }
        if(errors.Count>0)code=1;File.WriteAllLines(output+"/validation.txt",checks);File.WriteAllLines(output+"/errors.txt",errors);Application.logMessageReceived-=Log;Application.Quit(code);
    }
}
#endif
