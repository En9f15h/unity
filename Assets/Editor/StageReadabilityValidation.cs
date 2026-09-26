using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StageReadabilityValidation
{
    private const string Key = "StageReadabilityValidation.Mode", Output = StageReadabilitySetup.Output;
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static Camera camera;
    private static double deadline;
    static StageReadabilityValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void RunBatch()
    {
        StageReadabilitySetup.Install();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetString(Key, "controlled"); EditorApplication.EnterPlaymode();
    }
    public static void RunIntegration()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetString(Key, "integration"); EditorApplication.EnterPlaymode();
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        string mode = SessionState.GetString(Key, ""); SessionState.SetString(Key, "");
        if (mode == "integration") { ShaderSceneProbe.StartProbe(false, Output + "/Integration", false, false, true); return; }
        if (mode != "controlled") return;
        checks.Clear(); errors.Clear(); routines.Clear(); routines.Push(Run()); deadline = EditorApplication.timeSinceStartup + 180;
        Application.logMessageReceived += Log; EditorApplication.update += Advance;
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Advance()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Stage validation timeout");
            while (routines.Count > 0)
            {
                if (!routines.Peek().MoveNext()) { routines.Pop(); continue; }
                if (routines.Peek().Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Require(errors.Count == 0, "No runtime errors during controlled rendering"); Finish(0);
        }
        catch (Exception e) { checks.Add("FAILED: " + e); Debug.LogException(e); Finish(1); }
    }
    private static void Finish(int code)
    {
        EditorApplication.update -= Advance; Application.logMessageReceived -= Log;
        File.WriteAllLines(Output + "/validation.txt", checks); File.WriteAllLines(Output + "/runtime-errors.txt", errors);
        Debug.Log("STAGE_READABILITY_VALIDATION " + (code == 0 ? "PASSED" : "FAILED")); EditorApplication.Exit(code);
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); checks.Add("PASS: " + message); }
    private static IEnumerator Frames(int count = 4) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator Run()
    {
        camera = new GameObject("Stage rendering camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 3; camera.aspect = 16f / 9;
        camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.03f,.04f,.05f); camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var light = new GameObject("Global 2D light").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global;
        var background = new GameObject("Background fixture").AddComponent<SpriteRenderer>();
        var original = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        background.sharedMaterial = original;
        var block = new MaterialPropertyBlock(); block.SetFloat("_UnrelatedValue", 17); background.SetPropertyBlock(block);
        var controller = background.gameObject.AddComponent<StageReadabilityController>();
        var palette = Resources.Load<StageReadabilityPalette>("Combat/StageReadabilityPalette");
        Require(palette.entries.Length == 3, "Only the three new map sprites are authored");
        var otherScene = SceneManager.CreateScene("Unrelated preview scene");
        var other = new GameObject("Other scene character"); SceneManager.MoveGameObjectToScene(other, otherScene);
        foreach (var entry in palette.entries)
        {
            string name = entry.background.name;
            controller.enabled = false; background.sprite = entry.background;
            background.transform.localScale = Vector3.one * (10 / entry.background.bounds.size.x);
            yield return Frames(); var before = Capture(name + "-before.png");
            controller.enabled = true; yield return Frames(); var after = Capture(name + "-after.png");
            Require(controller.HasStyle && background.sharedMaterial.shader.name == "Combat/Stage Background", name + " swaps only background material");
            Require(Difference(before, after) > .2f, name + " grade affects real rendered pixels");
            Require(Average(after) > Average(before) * .65f && Average(after) < Average(before) * 1.06f, name + " retains luminance within restrained bounds");
            Require(DarkPixels(after) <= DarkPixels(before) + 300, name + " does not crush more shadow pixels");
            Require(StageReadabilityController.TryGetCharacterStyle(background.gameObject, out var style) && style.background == entry.background, name + " character style follows actual sprite");
            Require(!StageReadabilityController.TryGetCharacterStyle(other, out _), name + " style does not leak into other scenes");
            // An identity grade must reproduce the original URP lit material.
            background.GetPropertyBlock(block); block.SetVector("_StageGrade", new Vector4(1,1,1,0)); background.SetPropertyBlock(block);
            var identity = Capture(null);
            Require(Difference(before, identity) < .25f, name + " identity grade matches original 2D lighting");
            controller.Refresh();
            light.intensity = .5f; yield return Frames(); var dim = Capture(null);
            Require(Average(dim) < Average(after) * .9f, name + " background still responds to 2D lights");
            light.intensity = 1;
            background.color = new Color(1,1,1,0); yield return Frames(); var invisible = Capture(null);
            background.enabled = false; yield return Frames();
            Require(Difference(invisible, Capture(null)) < .01f, name + " zero source alpha remains fully transparent");
            background.color = Color.white; background.enabled = true;
            controller.enabled = false; yield return Frames();
            Require(background.sharedMaterial == original && Difference(before, Capture(null)) < .01f, name + " disabling restores original appearance");
            background.GetPropertyBlock(block);
            Require(block.GetFloat("_UnrelatedValue") == 17, name + " preserves unrelated property block values");
            controller.enabled = true;
        }
        var legacy = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleSceneImage/background.png");
        Require(legacy != null, "Legacy background fixture exists");
        background.sprite = legacy; yield return Frames();
        Require(!controller.HasStyle && background.sharedMaterial == original, "Unmatched sprite restores original background material");
        Require(!StageReadabilityController.TryGetCharacterStyle(background.gameObject, out _), "Unmatched sprite removes character override");
        foreach (string path in CombatShaderSetup.CharacterPaths)
        {
            var staging = new GameObject("Inactive skin staging"); staging.SetActive(false);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), staging.transform);
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(component is CharacterShaderFeedback) && !(component is UnityEngine.U2D.Animation.SpriteSkin)) component.enabled = false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            root.transform.SetParent(null); root.SetActive(true); Object.Destroy(staging);
            var feedback = root.GetComponentInChildren<CharacterShaderFeedback>();
            foreach (var entry in palette.entries)
            {
                background.sprite = entry.background; yield return Frames();
                Require(feedback.BodyRenderers.All(r => { r.GetPropertyBlock(block); return Mathf.Approximately(block.GetFloat("_RimStrength"),r.sharedMaterial.GetFloat("_RimStrength") * entry.edgeMultiplier); }), root.name + " keeps authored skin strength on " + entry.background.name);
                feedback.SetPhaseVisibility(0);
                Require(feedback.BodyRenderers.All(r => { r.GetPropertyBlock(block); return block.GetFloat("_Dissolve") == 1; }), root.name + " grade coexists with exact phase endpoint");
                feedback.ResetPresentation();
            }
            controller.enabled = false; yield return Frames();
            Require(feedback.BodyRenderers.All(r => { r.GetPropertyBlock(block); return block.GetFloat("_RimStrength") == r.sharedMaterial.GetFloat("_RimStrength"); }), root.name + " disabling stage restores authored edges");
            controller.enabled = true; yield return Frames(); feedback.enabled = false;
            Require(feedback.BodyRenderers.All(r => { r.GetPropertyBlock(block); return block.GetFloat("_RimStrength") == r.sharedMaterial.GetFloat("_RimStrength"); }), root.name + " disabling feedback clears map edges");
            Object.Destroy(root); yield return Frames();
        }
        Object.Destroy(background.gameObject); Object.Destroy(other); yield return Frames();
        var checkObject = new GameObject("After teardown");
        Require(!StageReadabilityController.TryGetCharacterStyle(checkObject, out _), "Destroying background removes scene registration"); Object.Destroy(checkObject);
        foreach (bool rectangular in new[] { false, true })
        {
            var canvas = new GameObject("UI mask fixture", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var mask = new GameObject("Mask", typeof(RectTransform)); mask.transform.SetParent(canvas.transform, false);
            ((RectTransform)mask.transform).sizeDelta = new Vector2(200,160);
            if (rectangular) mask.AddComponent<RectMask2D>();
            else { mask.AddComponent<Image>(); mask.AddComponent<Mask>().showMaskGraphic = false; }
            var preview = new GameObject("Preview", typeof(RectTransform)).AddComponent<Image>(); preview.transform.SetParent(mask.transform, false);
            preview.rectTransform.sizeDelta = new Vector2(400,200); preview.raycastTarget = false;
            foreach (var entry in palette.entries)
            {
                preview.sprite = entry.background; StageReadabilityController.ForImage(preview); yield return Frames();
                var pixels = Capture("UI-" + rectangular + "-" + entry.background.name + ".png");
                var ui = preview.GetComponent<StageReadabilityController>();
                Require(ui.HasStyle && preview.materialForRendering.shader.name == "Combat/Stage Background UI", "Preview uses palette under " + (rectangular ? "RectMask2D" : "Mask"));
                Require(Delta(pixels[270*960+630], pixels[270*960+730]) < 2, "UI grade respects mask outside its bounds");
                Require(Delta(pixels[270*960+480], pixels[270*960+730]) > 15, "UI mask retains visible interior");
                if (!rectangular) Require(preview.materialForRendering.GetFloat("_Stencil") != 0, "UI material preserves Mask stencil state");
                var instance = preview.materialForRendering; Canvas.ForceUpdateCanvases();
                Require(preview.materialForRendering == instance, "UI reuses material while source is unchanged");
                var group = preview.gameObject.GetComponent<CanvasGroup>();
                if (group == null) group = preview.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0; yield return Frames();
                var hidden = Capture(null); Require(Delta(hidden[270*960+480], hidden[270*960+730]) < 2, "CanvasGroup fade preserves zero alpha"); group.alpha = 1;
                ui.enabled = false; yield return Frames();
                Require(preview.materialForRendering.shader.name == "UI/Default", "Disabled preview restores default UI shader");
                ui.enabled = true;
                preview.sprite = null; yield return Frames(); Require(!ui.HasStyle && preview.materialForRendering.shader.name == "UI/Default", "Null preview removes grade");
            }
            Object.Destroy(canvas.gameObject); yield return Frames();
        }
        foreach (string shader in new[] { "Combat/Stage Background", "Combat/Stage Background UI" })
            Require(!ShaderUtil.ShaderHasError(Shader.Find(shader)), shader + " compiles after actual renders");
    }
    private static Color32[] Capture(string name)
    {
        var rt = RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); var old = RenderTexture.active;
        Canvas.ForceUpdateCanvases(); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(960,540,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply();
        var pixels = texture.GetPixels32(); if (name != null) File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.Destroy(texture); return pixels;
    }
    private static int Delta(Color32 a, Color32 b) => Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
    private static float Difference(Color32[] a, Color32[] b) { long sum=0; for(int i=0;i<a.Length;i++) sum+=Delta(a[i],b[i]); return sum/(float)a.Length/3; }
    private static float Average(Color32[] pixels) => pixels.Average(p => (p.r+p.g+p.b)/3f);
    private static int DarkPixels(Color32[] pixels) => pixels.Count(p => p.r+p.g+p.b<15);
}
