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
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PresentationShaderValidation
{
    private const string Key="PresentationShaderValidation.Running";
    private const string Output="CodexLogs/PresentationShaders-20260915";
    private static readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();
    private static Camera camera;
    private static Canvas canvas;
    private static readonly List<string> results=new List<string>();
    static PresentationShaderValidation()=>EditorApplication.playModeStateChanged+=OnPlayMode;
    public static void RunBatch()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
        results.Clear(); routines.Clear(); routines.Push(Run()); EditorApplication.update+=Advance;
    }
    private static void Advance()
    {
        try
        {
            while(routines.Count>0)
            {
                if(!routines.Peek().MoveNext()) { routines.Pop(); continue; }
                if(routines.Peek().Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish(0);
        }
        catch(Exception e) { Debug.LogException(e); results.Add("FAILED: "+e); Finish(1); }
    }
    private static void Finish(int code)
    {
        EditorApplication.update-=Advance; SessionState.SetBool(Key,false); Time.timeScale=1;
        File.WriteAllLines(Output+"/validation.txt",results);
        Debug.Log("PRESENTATION_SHADER_VALIDATION "+(code==0?"PASSED":"FAILED")); EditorApplication.Exit(code);
    }
    private static void Require(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException(message);
        results.Add("PASS: "+message);
    }
    private static IEnumerator Run()
    {
        camera=new GameObject("Presentation validation camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.orthographic=true; camera.orthographicSize=2.8f; camera.transform.position=new Vector3(0,0,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.12f,0.14f,0.18f); camera.allowHDR=true;
        var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=false;
        var light=new GameObject("Global 2D light").AddComponent<Light2D>(); light.lightType=Light2D.LightType.Global; light.intensity=1;
        var canvasObject=new GameObject("Presentation UI canvas",typeof(RectTransform),typeof(Canvas));
        canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvasObject.transform.localScale=Vector3.one*0.01f;
        for(int f=0;f<10;f++) yield return null;
        Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset,"URP active");
        Sprite ready=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Character/ReadyButton_獨立去背.png");
        foreach(string style in new[]{"UI_Gold","UI_Cyan","UI_Rune"})
        {
            Image image=ImageObject(style,canvas.transform,ready,new Vector2(400,230));
            var feedback=UIShaderFeedback.Ensure(image,style); feedback.ResetVisuals();
            yield return Settle();
            Color32[] idle=Capture(style+"-idle.png");
            Require(Pixels(idle)>200,style+" visible image");
            feedback.Pulse(3f); Time.timeScale=0;
            float pulseStart=Time.unscaledTime;
            double timeout=EditorApplication.timeSinceStartup+10;
            while(Time.unscaledTime-pulseStart<1.5f && EditorApplication.timeSinceStartup<timeout) yield return null;
            // Use the same player clock as the component, then flush its current parameters.
            float progress=image.materialForRendering.GetFloat("_PulseProgress");
            Require(progress>0.1f && progress<0.95f,style+" pulse advances on the unscaled player clock");
            Color32[] pulse=Capture(style+"-pulse.png");
            Require(Difference(idle,pulse)>0.0001f,style+" pulse animates during HitStop");
            Time.timeScale=1;
            Material first=image.materialForRendering;
            Require(first==image.materialForRendering,style+" reuses material instance across refreshes");
            Image second=ImageObject("Independent image",canvas.transform,ready,new Vector2(400,230));
            var other=UIShaderFeedback.Ensure(second,style); other.ResetVisuals();
            Require(other.GetModifiedMaterial(second.material)!=first,style+" per-image state isolation");
            Object.Destroy(second.gameObject); yield return null;
            var group=image.gameObject.AddComponent<CanvasGroup>(); group.alpha=0; yield return Settle();
            Require(Pixels(Capture(null))<10,style+" CanvasGroup alpha zero hides all highlights");
            group.alpha=1;
            feedback.enabled=false; yield return null;
            Require(first==null,style+" releases material on disable");
            feedback.enabled=true; feedback.ResetVisuals(); yield return Settle();
            Require(Mathf.Abs(image.materialForRendering.GetFloat("_Persistent"))<0.001f,style+" re-enabled image has no stale status");
            Object.Destroy(image.gameObject); yield return null;
        }
        var maskRoot=new GameObject("UI clipping",typeof(RectTransform)); maskRoot.transform.SetParent(canvas.transform,false);
        ((RectTransform)maskRoot.transform).sizeDelta=new Vector2(150,150);
        var mask=maskRoot.AddComponent<RectMask2D>(); mask.enabled=false;
        Image clipped=ImageObject("Clipped energy",maskRoot.transform,ready,new Vector2(400,230));
        UIShaderFeedback.Ensure(clipped,"UI_Cyan").SetState(1,true); yield return Settle();
        int full=Pixels(Capture(null)); mask.enabled=true; yield return Settle();
        Require(Pixels(Capture("UI-RectMask.png"))<full*0.9f,"RectMask2D clips state shader");
        mask.enabled=false;
        var stencilImage=maskRoot.AddComponent<Image>(); stencilImage.color=Color.white;
        var stencil=maskRoot.AddComponent<Mask>(); stencil.showMaskGraphic=false;
        yield return Settle();
        Require(Pixels(Capture("UI-Stencil.png"))<full*0.9f,"Stencil Mask clips state shader");
        Require(clipped.materialForRendering.GetFloat("_Stencil")>0,"Modified UI material preserves inherited stencil reference");
        Object.Destroy(maskRoot); yield return null;

        GameObject energyRoot=SpawnUI("Assets/Resources/Prefab/knight/energyBar.prefab");
        var energy=energyRoot.GetComponent<EnergyBarUI>(); energy.Init(10,10); yield return Settle();
        var energyData=new SerializedObject(energy);
        var fill=(Image)energyData.FindProperty("fillImage").objectReferenceValue;
        foreach(Text text in energyRoot.GetComponentsInChildren<Text>()) text.enabled=false;
        NormalizeRect(fill.rectTransform,new Vector2(380,100));
        yield return Settle(); int fullEnergyPixels=Pixels(Capture("Energy-full.png"));
        energy.SetEnergy(5,10); yield return Settle(); int halfEnergyPixels=Pixels(Capture("Energy-half.png"));
        Require(energy.CurrentEnergy==5 && Mathf.Approximately(fill.fillAmount,0.5f),"Actual energy bar keeps fill amount and value");
        Require(halfEnergyPixels<fullEnergyPixels*0.75f && halfEnergyPixels>fullEnergyPixels*0.25f,"Half energy renders approximately half the bar");
        energy.SetEnergy(0,10); yield return Settle(); Require(Pixels(Capture(null))<10,"Empty energy bar has no misleading highlight");
        Object.Destroy(energyRoot); yield return null;
        GameObject oracleRoot=SpawnUI("Assets/Resources/Prefab/Oracle/UI/OracleEnergyBar.prefab");
        var oracle=oracleRoot.GetComponent<OracleSightEnergyUI>(); oracle.Init(10,0);
        var oracleData=new SerializedObject(oracle); var eye=(Image)oracleData.FindProperty("eyeImage").objectReferenceValue;
        NormalizeRect(eye.rectTransform,new Vector2(280,280));
        var emptyEye=eye.sprite; oracle.SetEnergy(10,10); yield return Settle();
        Require(eye.sprite!=emptyEye,"Oracle energy changes eye stage"); Capture("Oracle-eye-full.png");
        oracle.SetSightActive(true); yield return Settle();
        Require(eye.sprite==oracleData.FindProperty("activatedEyeSprite").objectReferenceValue,"Sight selects activated eye sprite");
        Require(eye.materialForRendering.GetFloat("_Persistent")==1,"Sight enables persistent rune highlight"); Capture("Oracle-eye-active.png");
        oracle.SetSightActive(false); oracle.SetEnergy(0,10); yield return Settle();
        Require(eye.materialForRendering.GetFloat("_Persistent")==0,"Sight reset removes active highlight");
        Object.Destroy(oracleRoot); yield return null;

        Image readyButton=ImageObject("Ready state",canvas.transform,ready,new Vector2(400,230));
        var button=readyButton.gameObject.AddComponent<Button>();
        var panel=readyButton.gameObject.AddComponent<CharacterSelectionPlayerPanel>();
        var panelData=new SerializedObject(panel); panelData.FindProperty("confirmButton").objectReferenceValue=button; panelData.ApplyModifiedPropertiesWithoutUndo();
        panel.SetReadyState(true); yield return Settle();
        Require(readyButton.materialForRendering.GetFloat("_Persistent")==1,"Ready flow activates gold state highlight");
        panel.SetReadyState(false); yield return Settle();
        Require(readyButton.materialForRendering.GetFloat("_Persistent")==0,"Unready flow clears gold state highlight");
        var vs=readyButton.gameObject.AddComponent<CharacterSelectionAnimationController>();
        vs.PlayTrigger("VSFlash"); yield return null;
        Require(readyButton.materialForRendering.GetFloat("_PulseProgress")<1,"VSFlash trigger starts UI scan");
        Object.Destroy(readyButton.gameObject); yield return null;

        Image action=ImageObject("Reveal action",canvas.transform,ready,new Vector2(300,180));
        var reveal=new GameObject("Reveal controller").AddComponent<EnemyActionRevealController>(); reveal.Bind(new[]{action});
        reveal.ResetForPlanning(ready); reveal.RevealSlot(2,0,ready,null,false,true);
        yield return Settle();
        Require(reveal.IsLogicalSlotRevealed(2) && action.GetComponent<UIShaderFeedback>()!=null,"Actual reveal flow starts shader without changing reveal state");
        var highlight=action.transform.Find("CurrentRevealHighlight");
        Require(highlight!=null && highlight.GetComponentsInChildren<Image>().All(i=>i.materialForRendering.GetFloat("_Persistent")==1),"Active reveal border restores its rune state after being hidden");
        reveal.ResetForPlanning(ready); yield return Settle();
        Require(!reveal.IsLogicalSlotRevealed(2) && action.materialForRendering.GetFloat("_PulseProgress")==1,"Planning reset clears reveal pulse");
        Object.Destroy(action.gameObject); Object.Destroy(reveal.gameObject); yield return null;
        canvasObject.SetActive(false);

        GameObject staging=new GameObject("Inactive blood staging"); staging.SetActive(false);
        GameObject decal=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/blood/bloodDecalPrefab.prefab"),staging.transform);
        var fade=decal.GetComponent<BloodDecalFade>(); fade.enabled=false;
        decal.transform.SetParent(null); Object.Destroy(staging); decal.SetActive(true);
        var renderer=decal.GetComponent<SpriteRenderer>(); renderer.maskInteraction=SpriteMaskInteraction.None;
        Frame(renderer.bounds); fade.ApplyShaderState(0,0); yield return null;
        Color32[] wet=Capture("Blood-wet.png"); int wetPixels=Pixels(wet);
        Require(wetPixels>100,"Original blood sprite renders with transparent material");
        Sprite square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(0.5f,0.5f),Texture2D.whiteTexture.width);
        var bloodMask=new GameObject("Blood ground mask").AddComponent<SpriteMask>(); bloodMask.sprite=square;
        bloodMask.isCustomRangeActive=true;
        bloodMask.backSortingLayerID=bloodMask.frontSortingLayerID=renderer.sortingLayerID;
        bloodMask.backSortingOrder=-1; bloodMask.frontSortingOrder=1;
        bloodMask.transform.position=renderer.bounds.center;
        bloodMask.transform.localScale=renderer.bounds.size*0.45f;
        renderer.maskInteraction=SpriteMaskInteraction.VisibleInsideMask; yield return Settle();
        int maskedBlood=Pixels(Capture("Blood-ground-mask.png"));
        Require(maskedBlood>20 && maskedBlood<wetPixels*0.6f,"Blood decal respects original inside-ground SpriteMask");
        renderer.maskInteraction=SpriteMaskInteraction.None; Object.Destroy(bloodMask.gameObject); yield return null;
        fade.ApplyShaderState(1,0); Color32[] dry=Capture("Blood-dry.png");
        Require(Difference(wet,dry)>0.001f,"Blood drying darkens existing pigment");
        fade.ApplyShaderState(1,0.55f); Require(Pixels(Capture("Blood-eroding.png"))<wetPixels*0.95f,"Blood erosion removes coverage");
        fade.ApplyShaderState(1,1); Require(Pixels(Capture(null))<10,"Blood erosion endpoint is fully transparent");
        var fadeData=new SerializedObject(fade);
        fadeData.FindProperty("holdTime").floatValue=0.05f; fadeData.FindProperty("fadeDuration").floatValue=0.1f; fadeData.FindProperty("settleDuration").floatValue=0;
        fadeData.ApplyModifiedPropertiesWithoutUndo(); fade.enabled=true; yield return null;
        MaterialPropertyBlock bloodBlock=new MaterialPropertyBlock(); renderer.GetPropertyBlock(bloodBlock);
        Require(bloodBlock.GetFloat("_Erosion")==0,"Restarted blood clears previous erosion");
        double expiry=EditorApplication.timeSinceStartup+0.4;
        while(EditorApplication.timeSinceStartup<expiry) yield return null;
        Require(decal==null,"Blood lifetime still destroys completed decal");

        GameObject spray=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/blood/bloodSprayPrefab.prefab"));
        spray.transform.position=Vector3.zero; spray.transform.rotation=Quaternion.identity;
        var ps=spray.GetComponent<ParticleSystem>();
        var sprayRenderer=ps.GetComponent<ParticleSystemRenderer>(); sprayRenderer.maskInteraction=SpriteMaskInteraction.None;
        ps.Simulate(0.12f,true,true); camera.orthographicSize=2; camera.transform.position=new Vector3(0,0,-10);
        Require(sprayRenderer.sharedMaterial.shader.name=="Combat/Blood","Blood spray uses non-additive shader");
        Require(Pixels(Capture("Blood-spray.png"))>100,"Blood spray particles produce visible transparent droplets");
        var sprayMask=new GameObject("Spray ground mask").AddComponent<SpriteMask>(); sprayMask.sprite=square;
        sprayMask.isCustomRangeActive=true;
        sprayMask.backSortingLayerID=sprayMask.frontSortingLayerID=sprayRenderer.sortingLayerID;
        sprayMask.backSortingOrder=-1; sprayMask.frontSortingOrder=1; sprayMask.transform.localScale=Vector3.one*30;
        sprayRenderer.maskInteraction=SpriteMaskInteraction.VisibleOutsideMask; yield return null;
        Require(Pixels(Capture(null))<10,"Blood spray respects original outside-ground SpriteMask");
        Object.Destroy(sprayMask.gameObject); Object.Destroy(square); Object.Destroy(spray); yield return null;

        var palette=Resources.Load<StageAtmospherePalette>("Combat/StageAtmospherePalette");
        Require(palette!=null && palette.entries.Length==3,"Three new backgrounds have atmosphere presets");
        foreach(var entry in palette.entries)
        {
            var background=new GameObject("Atmosphere background").AddComponent<SpriteRenderer>(); background.sprite=entry.background;
            background.sortingLayerName="backGround";
            var unlit=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); background.sharedMaterial=unlit;
            camera.orthographicSize=background.bounds.extents.y; camera.transform.position=new Vector3(background.bounds.center.x,background.bounds.center.y,-10);
            string name=entry.background.name.StartsWith("castle")?"Castle":entry.background.name.StartsWith("arena")?"Arena":"Dark";
            Color32[] before=Capture(name+"-before.png",1280,720);
            var atmosphere=background.gameObject.AddComponent<StageAtmosphereController>();
            var atmosphereData=new SerializedObject(atmosphere); atmosphereData.FindProperty("backgroundRenderer").objectReferenceValue=background; atmosphereData.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.Refresh(); yield return null;
            Color32[] after=Capture(name+"-atmosphere.png",1280,720);
            float change=Difference(before,after);
            Require(change>0.00001f && change<0.03f,name+" adds a restrained independent atmosphere layer");
            var mist=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).First(r=>r.name=="Stage Atmosphere");
            Require(mist.sortingLayerID==background.sortingLayerID && mist.sortingOrder>background.sortingOrder,name+" mist stays in background sorting layer");
            background.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleSceneImage/background.png"); atmosphere.Refresh();
            Require(!mist.gameObject.activeSelf,name+" switching to doodle map disables atmosphere");
            background.sprite=entry.background; atmosphere.Refresh(); Require(mist.gameObject.activeSelf,name+" switching back restores atmosphere");
            Object.Destroy(background.gameObject); Object.Destroy(unlit); yield return null;
        }
        Require(!Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Any(r=>r.name=="Stage Atmosphere"),"Atmosphere mesh cleans up with background owner");
        canvasObject.SetActive(true); camera.orthographicSize=2.8f; camera.transform.position=new Vector3(0,0,-10);
        Image preview=ImageObject("Map preview",canvas.transform,palette.entries[2].background,new Vector2(420,350));
        var selector=preview.gameObject.AddComponent<MapSelectorController>();
        var selectorData=new SerializedObject(selector); selectorData.FindProperty("mapPreviewImage").objectReferenceValue=preview; selectorData.ApplyModifiedPropertiesWithoutUndo();
        var newMap=AssetDatabase.LoadAssetAtPath<MapSelectionDefinition>("Assets/Generated/CharacterSelection/Definitions/Map_DarkStoneArena.asset");
        var oldMap=AssetDatabase.LoadAssetAtPath<MapSelectionDefinition>("Assets/Generated/CharacterSelection/Definitions/Map_Paint.asset");
        selector.SetMaps(new[]{newMap,oldMap}); yield return Settle();
        var overlay=preview.transform.Find("Stage Atmosphere");
        Require(overlay!=null && overlay.gameObject.activeSelf,"Map UI preview creates atmosphere overlay");
        Require(((RectTransform)overlay).rect.height<preview.rectTransform.rect.height,"Map preview fog respects letterboxed aspect ratio");
        Require(!overlay.GetComponent<Image>().raycastTarget,"Map atmosphere does not intercept clicks"); Capture("Map-preview.png");
        selector.SetSelectionWithoutNotify(oldMap.mapId); yield return Settle();
        Require(!overlay.gameObject.activeSelf,"Map UI preview disables fog for legacy map");
        Require(selector.GetCurrentMapId()==oldMap.mapId && selector.GetCurrentMapIndex()==1,"Atmosphere leaves selector map ID and index unchanged");
        Object.Destroy(preview.gameObject); yield return null;
        foreach(string shaderName in new[]{"Combat/Presentation UI","Combat/Blood","Combat/Stage Atmosphere"})
        {
            var errors=ShaderUtil.GetShaderMessages(Shader.Find(shaderName)).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            Require(errors.Length==0,shaderName+" GPU variants compile: "+string.Join("; ",errors.Select(m=>m.message)));
        }
    }
    private static IEnumerator Settle()
    {
        Canvas.ForceUpdateCanvases(); for(int f=0;f<5;f++) yield return null;
    }
    private static Image ImageObject(string name,Transform parent,Sprite sprite,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.sprite=sprite; image.preserveAspect=true; image.raycastTarget=false;
        NormalizeRect(image.rectTransform,size); return image;
    }
    private static void NormalizeRect(RectTransform rect,Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(0.5f,0.5f); rect.pivot=new Vector2(0.5f,0.5f);
        rect.anchoredPosition=Vector2.zero; rect.localRotation=Quaternion.identity; rect.localScale=Vector3.one; rect.sizeDelta=size;
    }
    private static GameObject SpawnUI(string path)
    {
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),canvas.transform);
        NormalizeRect((RectTransform)root.transform,new Vector2(300,200)); return root;
    }
    private static void Frame(Bounds bounds)
    {
        camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-10); camera.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.y)*1.1f;
    }
    private static Color32[] Capture(string name,int width=640,int height=480)
    {
        Canvas.ForceUpdateCanvases();
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active; RenderTexture.active=rt;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
        if(name!=null) File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());
        var pixels=image.GetPixels32(); Object.Destroy(image); RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); return pixels;
    }
    private static int Pixels(Color32[] data)
    {
        Color32 clear=data[0]; return data.Count(p=>Math.Abs(p.r-clear.r)>7 || Math.Abs(p.g-clear.g)>7 || Math.Abs(p.b-clear.b)>7);
    }
    private static float Difference(Color32[] a,Color32[] b)
    {
        double sum=0; for(int i=0;i<a.Length;i++) sum+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
        return (float)(sum/(a.Length*765));
    }
}
