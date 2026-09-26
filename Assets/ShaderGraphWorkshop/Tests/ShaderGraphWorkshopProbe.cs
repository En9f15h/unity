#if UNITY_EDITOR || CODEX_SHADER_GRAPH_WORKSHOP
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ShaderGraphWorkshopProbe : MonoBehaviour
{
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    Camera cameraRef;
    string output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartProbe()
    {
        var args = Environment.GetCommandLineArgs(); if (!args.Contains("-graph-probe")) return;
        int i = Array.IndexOf(args, "-graph-output");
        var p = new GameObject("Shader Graph render validation").AddComponent<ShaderGraphWorkshopProbe>();
        p.output = i >= 0 ? args[i + 1] : "ShaderGraphResults";
        Directory.CreateDirectory(p.output); Application.logMessageReceived += p.Log;
        Application.runInBackground = true;
    }
    void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    IEnumerator Start()
    {
        yield return null; yield return null;
        try { Validate(); Check(errors.Count == 0, "No captured runtime errors"); Finish(0); }
        catch (Exception e) { checks.Add("FAILED: " + e); Finish(1); }
    }
    void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks.Add("PASS: " + message); }
    void Validate()
    {
        cameraRef = Camera.main;
        var controls = FindFirstObjectByType<ShaderGraphWorkshopPreview>(); controls.enabled = false;
        var sprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        var gold = sprites.First(r => r.name == "Gold Flow");
        var violet = sprites.First(r => r.name == "Violet Flow");
        var dissolve = sprites.First(r => r.name == "Dissolve 0");
        Check(sprites.Length == 5 && sprites.All(r => r.sharedMaterial.shader.isSupported), "Five sample sprites use supported Shader Graph shaders");
        Check(gold.sharedMaterial.shader.name.Contains("EnergyFlow") && dissolve.sharedMaterial.shader.name.Contains("EdgeDissolve"), "Actual scene uses both generated graphs");
        Check(FindObjectsByType<CombatShaderClock>(FindObjectsSortMode.None).Length == 1, "Existing shared visual clock is available");
        Shader.SetGlobalFloat("_CombatVisualTime", 0); Capture("workshop.png", 1600, 900);
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) renderer.enabled = false;
        cameraRef.backgroundColor = Color.black; cameraRef.orthographicSize = 1.5f; cameraRef.transform.position = new Vector3(0, 0, -10);
        gold.enabled = true; gold.transform.position = Vector3.zero;
        Shader.SetGlobalFloat("_CombatVisualTime", 0); var a = Capture("gold-flow-0.png");
        Shader.SetGlobalFloat("_CombatVisualTime", 1.37f); var b = Capture("gold-flow-1.png");
        Check(Lit(a) > 1000, "Energy flow visibly renders source artwork");
        Check(Difference(a, b) > .05, "Shared clock changes flowing energy pixels");
        var block = new MaterialPropertyBlock(); block.SetFloat("_Opacity", 0); gold.SetPropertyBlock(block);
        Check(Lit(Capture(null)) < 10, "Flow opacity zero is transparent");
        gold.SetPropertyBlock(null); gold.color = new Color(1, 1, 1, 0);
        Check(Lit(Capture(null)) < 10, "SpriteRenderer alpha controls graph transparency");
        gold.color = Color.white; gold.enabled = false;
        violet.enabled = true; violet.transform.position = Vector3.zero;
        Check(Lit(Capture("violet-flow.png")) > 1000, "Second colour preset visibly renders");
        violet.enabled = false; dissolve.enabled = true; dissolve.transform.position = Vector3.zero;
        block.Clear(); block.SetFloat("_EffectProgress", 0); dissolve.SetPropertyBlock(block);
        var full = Capture("dissolve-0.png"); int fullCount = Lit(full);
        block.SetFloat("_EffectProgress", .5f); dissolve.SetPropertyBlock(block);
        var half = Capture("dissolve-50.png"); int halfCount = Lit(half);
        Check(fullCount > 1000 && halfCount > fullCount * .02 && halfCount < fullCount * .95, "Midpoint dissolves only part of the artwork");
        block.SetFloat("_EdgeIntensity", 0); dissolve.SetPropertyBlock(block);
        Check(Difference(half, Capture(null)) > .01, "Edge intensity changes the visible dissolve rim");
        block.Clear(); block.SetFloat("_EffectProgress", 1); dissolve.SetPropertyBlock(block);
        Check(Lit(Capture("dissolve-100.png")) < 10, "Dissolve endpoint is fully transparent");
        block.SetFloat("_EffectProgress", 2); dissolve.SetPropertyBlock(block);
        Check(Lit(Capture(null)) < 10, "Progress above one clamps to fully dissolved");
        block.SetFloat("_EffectProgress", -1); dissolve.SetPropertyBlock(block);
        Check(Math.Abs(Lit(Capture(null)) - fullCount) < 10, "Progress below zero clamps to fully visible");
        dissolve.flipX = true;
        Check(Math.Abs(Lit(Capture(null)) - fullCount) < fullCount * .03, "Sprite flip preserves coverage");
        Check(Mathf.Approximately(dissolve.sharedMaterial.GetFloat("_EffectProgress"), 0), "Per-sprite progress leaves shared material unchanged");
        File.WriteAllText(output + "/render-metrics.json", JsonUtility.ToJson(new Metrics { fullPixels = fullCount, midpointPixels = halfCount, graphicsApi = SystemInfo.graphicsDeviceVersion }, true));
    }
    [Serializable] class Metrics { public int fullPixels, midpointPixels; public string graphicsApi; }
    static int Lit(Color32[] pixels) => pixels.Count(c => c.r > 6 || c.g > 6 || c.b > 6);
    static double Difference(Color32[] a, Color32[] b)
    {
        double sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
        return sum / (a.Length * 3);
    }
    Color32[] Capture(string name, int width = 512, int height = 512)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active; var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            cameraRef.aspect = width / (float)height;
            RenderPipeline.SubmitRenderRequest(cameraRef, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            if (name != null) File.WriteAllBytes(output + "/" + name, tex.EncodeToPNG());
            return tex.GetPixels32();
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Destroy(tex); }
    }
    void Finish(int code)
    {
        File.WriteAllLines(output + "/validation.txt", checks); File.WriteAllLines(output + "/runtime-errors.txt", errors);
        Application.logMessageReceived -= Log; Application.Quit(code);
    }
}
#endif
