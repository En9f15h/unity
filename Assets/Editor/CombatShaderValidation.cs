using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

// Runs in a temporary, unsaved scene. No Photon room or gameplay scene is started.
[InitializeOnLoad]
public static class CombatShaderValidation
{
    private const string Running = "CombatShaderValidation.Running";
    private static string Output
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-combat-shader-output");
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "CodexLogs/CombatShaders-20260914";
        }
    }
    private static IEnumerator routine;
    private static Camera camera;
    private static readonly List<string> results = new List<string>();
    static CombatShaderValidation() { EditorApplication.playModeStateChanged += OnPlayMode; }
    public static void RunBatch()
    {
        CombatShaderSetup.Validate();
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
        routine = Run(); results.Clear();
        EditorApplication.update += Advance;
    }
    private static void Advance()
    {
        try { if (routine.MoveNext()) return; Finish(0); }
        catch (Exception e) { Debug.LogException(e); results.Add("FAILED: " + e); Finish(1); }
    }
    private static void Finish(int code)
    {
        EditorApplication.update -= Advance;
        SessionState.SetBool(Running, false);
        File.WriteAllLines(Output + "/validation.txt", results);
        Debug.Log("COMBAT_SHADER_VALIDATION " + (code == 0 ? "PASSED" : "FAILED"));
        EditorApplication.Exit(code);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }
    private static IEnumerator Run()
    {
        camera = new GameObject("Shader validation camera").AddComponent<Camera>();
        camera.tag="MainCamera";
        camera.transform.position = new Vector3(0,0,-10); camera.orthographic = true;
        camera.orthographicSize = 3; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f,0.2f,0.24f); camera.allowHDR = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        Light2D light = new GameObject("Global 2D light").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global; light.intensity = 1;
        for (int frame=0;frame<8;frame++) yield return null;
        Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "Project uses URP during Play Mode");

        foreach (string path in CombatShaderSetup.CharacterPaths)
        {
            GameObject root = SpawnVisual(path);
            CharacterShaderFeedback feedback = root.GetComponentInChildren<CharacterShaderFeedback>(true);
            feedback.InitializeRenderers(); feedback.ResetPresentation();
            Bounds bounds = GetBounds(feedback.BodyRenderers);
            Frame(bounds);
            for (int f=0;f<8;f++) yield return null;
            Frame(GetBounds(feedback.BodyRenderers));
            string name = Path.GetFileNameWithoutExtension(path);
            Color32[] normal = Capture(name + "-normal.png");
            int normalPixels = LitPixels(normal);
            Require(normalPixels > 200, name + " renders visible body including skinning");
            foreach (SpriteSkin skin in root.GetComponentsInChildren<SpriteSkin>())
                Require(skin.rootBone != null && skin.boneTransforms != null && skin.boneTransforms.All(b=>b!=null), name + " valid bone binding: " + skin.name);
            Color[] sourceColors = feedback.BodyRenderers.Select(r=>r.color).ToArray();
            feedback.SetPhaseVisibility(0.5f);
            Color32[] phase = Capture(name + "-phase.png");
            Require(LitPixels(phase) < normalPixels * 0.97f, name + " phase removes pixels");
            Require(feedback.BodyRenderers.Select((r,i)=>r.color == sourceColors[i]).All(x=>x), name + " phase preserves SpriteRenderer animation colors");
            feedback.SetPhaseVisibility(0);
            Require(LitPixels(Capture(null)) < Math.Max(10, normalPixels / 100), name + " phase endpoint is invisible");
            feedback.enabled = false; feedback.enabled = true;
            Require(feedback.Visibility == 1 && LitPixels(Capture(null)) > normalPixels * 0.95f, name + " disabling/re-enabling restores visibility");
            CharacterUnit unit = root.GetComponentInChildren<CharacterUnit>(true);
            unit.maxHP = 50; unit.currentHP = 50; unit.TakeDamage(1);
            Require(unit.currentHP == 49, name + " damage still subtracts exactly one HP");
            Require(Difference(normal,Capture(name + "-hit.png")) > 0.001f, name + " hit flash changes rendered color");
            double deadline = EditorApplication.timeSinceStartup + 0.3;
            while (EditorApplication.timeSinceStartup < deadline) yield return null;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            feedback.BodyRenderers[0].GetPropertyBlock(block);
            Require(block.GetFloat("_HitAmount") == 0, name + " hit flash completes");
            GameObject ghosts=new GameObject("Ghost pose validation");
            foreach(SpriteRenderer body in feedback.BodyRenderers)
                CombatGhostSnapshot.Create(body,ghosts.transform,Vector3.zero,new Color(0.5f,0.85f,1,0.7f),5,0);
            foreach(SpriteRenderer body in feedback.BodyRenderers) body.enabled=false;
            Require(LitPixels(Capture(name+"-ghost.png")) > normalPixels*0.5f, name+" baked pose ghost renders with original UVs");
            Object.Destroy(ghosts);
            foreach(SpriteRenderer body in feedback.BodyRenderers) body.enabled=true;
            yield return null;
            KnightSlashShaderVFX slash=root.GetComponentInChildren<KnightSlashShaderVFX>(true);
            if(slash!=null)
            {
                slash.enabled=true;
                slash.PlaySlash(ActionType.HeavyAttack,false,0.12f);
                Require(GameObject.Find("Knight Sword Energy Trail")==null,name+" charging does not emit slash");
                slash.PlaySlash(ActionType.LightAttack,false,0.12f);
                GameObject trail=GameObject.Find("Knight Sword Energy Trail");
                Require(trail!=null && trail.GetComponent<TrailRenderer>().sharedMaterial==CombatShaderMaterials.KnightSlash,name+" attack creates shader trail");
                slash.enabled=false; yield return null;
                Require(GameObject.Find("Knight Sword Energy Trail")==null,name+" interrupted slash cleans up");
            }
            root.transform.localScale = Vector3.Scale(root.transform.localScale, new Vector3(-1,1,1));
            for (int f=0;f<5;f++) yield return null;
            Frame(GetBounds(feedback.BodyRenderers));
            Require(LitPixels(Capture(name + "-flipped.png")) > 200, name + " mirrored character renders");
            Object.Destroy(root);
            yield return null;
        }

        string[] effects = Directory.GetFiles("Assets/Resources/Prefab/Oracle/vfx", "*.prefab")
            .Concat(new[]{"Assets/Resources/Prefab/knight/ParrySuccessEffect.prefab", "Assets/Resources/Prefab/knight/LightningStrikePrefab.prefab",
                "Assets/Resources/Prefab/knight/SwordResidualLightningPrefab.prefab", "Assets/Resources/Prefab/knight/Ultimate.prefab"}).ToArray();
        foreach (string path in effects)
        {
            GameObject root = SpawnVisual(path.Replace('\\','/'));
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                Require(r.sharedMaterial != null && r.sharedMaterial.shader.name.StartsWith("Combat/"), Path.GetFileName(path) + " material: " + r.name);
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                AnimationClip clip = animator.runtimeAnimatorController?.animationClips.FirstOrDefault();
                animator.enabled = false;
                if (clip != null) clip.SampleAnimation(animator.gameObject, Mathf.Min(clip.length * 0.4f,0.18f));
            }
            CombatEffectShaderDriver driver = root.GetComponent<CombatEffectShaderDriver>();
            if (driver != null) { driver.Play(1); driver.ApplyProperties(0.35f); }
            SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled && r.sprite != null).ToArray();
            if (sprites.Length > 0)
            {
                Frame(GetBounds(sprites));
                for (int f=0;f<3;f++) yield return null;
                Require(LitPixels(Capture(Path.GetFileNameWithoutExtension(path) + ".png")) > 10, Path.GetFileName(path) + " sprite frame renders");
            }
            OracleParticleEffect particle = root.GetComponent<OracleParticleEffect>();
            if (particle != null)
            {
                particle.PlayConfigured(0.6f,Vector3.right,CombatShaderMaterials.OracleParticles,SortingLayer.NameToID("Effect"),0);
                Require(root.GetComponentsInChildren<ParticleSystemRenderer>().All(r=>r.sharedMaterial==CombatShaderMaterials.OracleParticles), Path.GetFileName(path) + " runtime particle materials");
            }
            Object.Destroy(root); yield return null;
        }
        // Exercise ribbon/particle GPU paths even when a prefab generates its geometry at runtime.
        GameObject lineObject=new GameObject("Ribbon render validation");
        var line=lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(CombatShaderSetup.MaterialRoot+"KnightLightning.mat");
        line.positionCount=5; line.SetPositions(new[]{new Vector3(-2,1,0),new Vector3(-1,0.1f,0),Vector3.zero,new Vector3(1,-0.8f,0),new Vector3(2,-1,0)});
        line.startWidth=0.12f; line.endWidth=0.08f; line.startColor=Color.white; line.endColor=Color.white;
        camera.transform.position=new Vector3(0,0,-10); camera.orthographicSize=2.5f;
        Require(LitPixels(Capture("Lightning-Ribbon.png"))>100,"Ribbon shader draws geometry with soft edges");
        line.startColor=Color.clear; line.endColor=Color.clear;
        Require(LitPixels(Capture(null))<10,"Ribbon honors zero vertex alpha");
        Object.Destroy(lineObject); yield return null;
        GameObject particleObject=new GameObject("Particle render validation");
        var ps=particleObject.AddComponent<ParticleSystem>();
        var main=ps.main; main.startSize=0.3f; main.startSpeed=0; main.startLifetime=5;
        var emission=ps.emission; emission.enabled=false;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=CombatShaderMaterials.OracleParticles;
        ps.Emit(new ParticleSystem.EmitParams{position=Vector3.zero,startColor=new Color(0.3f,0.7f,1f),startSize=0.8f},1);
        for(int f=0;f<3;f++) yield return null;
        Require(LitPixels(Capture("Energy-Particle.png"))>50,"Runtime particle shader renders radial coverage");
        Object.Destroy(particleObject); yield return null;
        GameObject expirySource=new GameObject("Ghost lifetime source");
        var expirySprite=expirySource.AddComponent<SpriteRenderer>(); expirySprite.sprite=RuntimeMagicSpriteLibrary.SoftCircle;
        GameObject expiryParent=new GameObject("Independent ghost lifetime");
        CombatGhostSnapshot.Create(expirySprite,expiryParent.transform,Vector3.zero,Color.white,0.05f,0);
        Object.Destroy(expirySource);
        double ghostDeadline=EditorApplication.timeSinceStartup+0.2;
        while(EditorApplication.timeSinceStartup<ghostDeadline) yield return null;
        Require(expiryParent.GetComponentsInChildren<CombatGhostSnapshot>().Length==0,"Ghost releases its mesh even when the source is destroyed");
        Object.Destroy(expiryParent); yield return null;

        GameObject wardTest=new GameObject("Ward timing validation");
        var wardSprite=wardTest.AddComponent<SpriteRenderer>(); wardSprite.sprite=RuntimeMagicSpriteLibrary.SoftCircle;
        wardSprite.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(CombatShaderSetup.MaterialRoot+"OracleWard.mat");
        var wardDriver=wardTest.AddComponent<CombatEffectShaderDriver>(); wardDriver.Play(1);
        Frame(wardSprite.bounds);
        Shader.SetGlobalFloat("_CombatVisualTime",Time.unscaledTime);
        Color32[] idleWard=Capture("Ward-idle.png");
        wardDriver.PulseImpact(Vector3.zero);
        Shader.SetGlobalFloat("_CombatVisualTime",Time.unscaledTime+0.15f);
        Require(Difference(idleWard,Capture("Ward-impact.png"))>0.001f,"Ward impact produces visible expanding ripple");
        wardDriver.enabled=false; wardDriver.enabled=true;
        MaterialPropertyBlock wardBlock=new MaterialPropertyBlock(); wardSprite.GetPropertyBlock(wardBlock);
        Require(wardBlock.GetFloat("_ImpactStart")<0,"Re-enabled effect resets previous impact pulse");
        wardSprite.sharedMaterial=CombatShaderMaterials.OracleEnergy;
        Shader.SetGlobalFloat("_CombatVisualTime",1);
        Color32[] flowStart=Capture(null);
        Shader.SetGlobalFloat("_CombatVisualTime",3);
        Require(Difference(flowStart,Capture(null))>0.0001f,"Energy noise changes over presentation time");
        Object.Destroy(wardTest); yield return null;
        camera.transform.position=new Vector3(0,0,-10); camera.orthographicSize=2.5f;

        GameObject canvasObject=new GameObject("Preview UI validation",typeof(RectTransform),typeof(Canvas));
        Canvas canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
        canvasObject.transform.localScale=Vector3.one*0.01f;
        ((RectTransform)canvasObject.transform).sizeDelta=new Vector2(512,512);
        var maskObject=new GameObject("Portrait clip",typeof(RectTransform),typeof(UnityEngine.UI.RectMask2D));
        maskObject.transform.SetParent(canvasObject.transform,false);
        ((RectTransform)maskObject.transform).sizeDelta=new Vector2(160,160);
        foreach(string path in Directory.GetFiles("Assets/Generated/CharacterSelection/Prefabs/UICharacters","*.prefab"))
        {
            GameObject portrait=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),maskObject.transform);
            var portraitRect=(RectTransform)portrait.transform; portraitRect.sizeDelta=new Vector2(300,300); portraitRect.anchoredPosition=Vector2.zero;
            var mask=maskObject.GetComponent<UnityEngine.UI.RectMask2D>(); mask.enabled=false;
            Canvas.ForceUpdateCanvases(); for(int f=0;f<3;f++) yield return null;
            int full=LitPixels(Capture(Path.GetFileNameWithoutExtension(path)+".png"));
            Require(full>100,Path.GetFileName(path)+" UI shader renders portrait");
            var portraitImage=portrait.GetComponent<UnityEngine.UI.Image>();
            Material selectedMaterial=portraitImage.material;
            portraitImage.material=null; Canvas.ForceUpdateCanvases();
            Capture(Path.GetFileNameWithoutExtension(path)+"-original.png");
            portraitImage.material=selectedMaterial; Canvas.ForceUpdateCanvases();
            mask.enabled=true; Canvas.ForceUpdateCanvases(); for(int f=0;f<3;f++) yield return null;
            Require(LitPixels(Capture(Path.GetFileNameWithoutExtension(path)+"-clipped.png"))<full*0.95f,Path.GetFileName(path)+" RectMask2D clips portrait");
            Object.Destroy(portrait); yield return null;
        }
        Object.Destroy(canvasObject); yield return null;
        var bloomVolume = new GameObject("Battle bloom").AddComponent<Volume>();
        bloomVolume.isGlobal=true; bloomVolume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/CombatBloom.asset");
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        foreach (string map in new[]{"castle_1920x1088.png", "ChatGPT Image 2026年9月13日 下午05_10_44.png"})
        {
            GameObject bg = new GameObject("Backdrop");
            SpriteRenderer background = bg.AddComponent<SpriteRenderer>();
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleSceneImage/VersionNew/" + map);
            Require(background.sprite != null, "Preview background " + map);
            background.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            background.sortingOrder = -1000;
            camera.orthographicSize = 5.4f; camera.transform.position = new Vector3(0,0,-10);
            var knight = SpawnVisual(CombatShaderSetup.CharacterPaths[1]);
            var oracle = SpawnVisual(CombatShaderSetup.CharacterPaths[3]);
            for(int f=0;f<8;f++) yield return null;
            PlaceCharacter(knight,-3,-3.8f,3.8f); PlaceCharacter(oracle,3,-3.8f,3.8f);
            var ward = SpawnVisual("Assets/Resources/Prefab/Oracle/vfx/Oracle_WardVFX.prefab");
            PlaceEffect(ward,new Vector3(2.7f,-1.8f,0),3.4f);
            ward.GetComponent<CombatEffectShaderDriver>()?.PulseImpact(new Vector3(0.8f,-0.5f,0));
            var rune = SpawnVisual("Assets/Resources/Prefab/Oracle/vfx/Oracle_RiftWarningVFX.prefab");
            PlaceEffect(rune,new Vector3(-0.2f,-3,0),2.4f);
            for (int f=0;f<12;f++) yield return null;
            Capture(map.StartsWith("castle") ? "Bright-Castle.png" : "Dark-Arena.png",1280,720);
            foreach (GameObject go in new[]{bg,knight,oracle,ward,rune}) Object.Destroy(go);
            yield return null;
        }
        foreach(string shaderName in new[]{"Combat/Character Lit","Combat/Energy Sprite","Combat/Energy Ribbon and Particle","Combat/Character UI"})
        {
            Shader shader = Shader.Find(shaderName);
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            Require(errors.Length == 0, shaderName + " compiled after rendering: " + string.Join("; ",errors.Select(e=>e.message)));
        }
    }
    private static GameObject SpawnVisual(string path)
    {
        GameObject holder = new GameObject("Inactive staging"); holder.SetActive(false);
        GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),holder.transform);
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is SpriteSkin) && !(behaviour is CharacterShaderFeedback) && !(behaviour is CombatEffectShaderDriver)) behaviour.enabled = false;
        foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
        foreach (Light2D light in root.GetComponentsInChildren<Light2D>(true)) light.enabled = false;
        root.transform.SetParent(null); root.SetActive(true); Object.Destroy(holder);
        foreach(Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            var clips=animator.runtimeAnimatorController?.animationClips;
            var clip=clips?.FirstOrDefault(c=>c.name.Contains("Idle")) ?? clips?.FirstOrDefault();
            animator.enabled=false;
            if(clip!=null) clip.SampleAnimation(animator.gameObject,Mathf.Min(0.1f,clip.length*0.4f));
        }
        return root;
    }
    private static Bounds GetBounds(IEnumerable<SpriteRenderer> renderers)
    {
        var list = renderers.Where(r=>r!=null && r.sprite!=null).ToArray();
        Bounds bounds = list[0].bounds; foreach(var r in list.Skip(1)) bounds.Encapsulate(r.bounds); return bounds;
    }
    private static void Frame(Bounds bounds)
    {
        camera.transform.position = new Vector3(bounds.center.x,bounds.center.y,-10);
        camera.orthographicSize = Mathf.Max(0.1f,Mathf.Max(bounds.extents.y,bounds.extents.x)*1.15f);
    }
    private static void PlaceCharacter(GameObject root,float x,float y,float height)
    {
        var feedback = root.GetComponentInChildren<CharacterShaderFeedback>();
        Bounds bounds = GetBounds(feedback.BodyRenderers);
        root.transform.localScale *= height / bounds.size.y;
        bounds = GetBounds(feedback.BodyRenderers);
        root.transform.position += new Vector3(x-bounds.center.x,y-bounds.min.y,0);
        feedback.InitializeRenderers();
    }
    private static void PlaceEffect(GameObject root,Vector3 position,float size)
    {
        foreach(Animator animator in root.GetComponentsInChildren<Animator>())
        {
            var clip = animator.runtimeAnimatorController?.animationClips.FirstOrDefault(); animator.enabled=false;
            if(clip!=null) clip.SampleAnimation(animator.gameObject,Mathf.Min(0.15f,clip.length*0.4f));
        }
        var renderers = root.GetComponentsInChildren<SpriteRenderer>();
        Bounds bounds = GetBounds(renderers);
        root.transform.localScale *= size / Mathf.Max(bounds.size.x,bounds.size.y);
        bounds = GetBounds(renderers); root.transform.position += position-bounds.center;
        foreach(var r in renderers) { r.sortingLayerName="Effect"; r.sortingOrder=5; }
    }
    private static Color32[] Capture(string name,int width=512,int height=512)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        RenderTexture previous = RenderTexture.active; RenderTexture.active=rt;
        Texture2D image = new Texture2D(width,height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
        if(name!=null) File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());
        Color32[] pixels=image.GetPixels32(); Object.Destroy(image);
        RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); return pixels;
    }
    private static int LitPixels(Color32[] pixels)
    {
        Color32 background=pixels[0];
        return pixels.Count(p=>Math.Abs(p.r-background.r)>8 || Math.Abs(p.g-background.g)>8 || Math.Abs(p.b-background.b)>8);
    }
    private static float Difference(Color32[] a,Color32[] b)
    {
        double sum=0; for(int i=0;i<a.Length;i++) sum+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
        return (float)(sum/(a.Length*765));
    }
}
