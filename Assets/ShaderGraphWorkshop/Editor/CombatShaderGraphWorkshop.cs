using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Uses the installed Shader Graph editor model to serialize real native nodes and edges.
// Reflection is editor-only: these internal authoring APIs are version-specific (URP/SG 17.3).
public static class CombatShaderGraphWorkshop
{
    public const string Root = "Assets/ShaderGraphWorkshop";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static string Output => Path.GetFullPath("../CodexLogs/ShaderGraphs-20260924");
    static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    static object New(string name) => Activator.CreateInstance(T(name), true);
    static object Get(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            var p = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly); if (p != null) return p.GetValue(obj);
            var f = t.GetField(name, Flags | BindingFlags.DeclaredOnly); if (f != null) return f.GetValue(obj);
        }
        throw new MissingMemberException(obj.GetType().Name, name);
    }
    static void Set(object obj, string name, object value)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            var p = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly); if (p != null) { p.SetValue(obj, value); return; }
            var f = t.GetField(name, Flags | BindingFlags.DeclaredOnly); if (f != null) { f.SetValue(obj, value); return; }
        }
        throw new MissingMemberException(obj.GetType().Name, name);
    }
    static object Call(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethods(Flags).First(x => x.Name == method && !x.IsGenericMethod && x.GetParameters().Length == args.Length);
        return m.Invoke(obj, args);
    }
    static Array TypedArray(Type type, params object[] items)
    {
        var a = Array.CreateInstance(type, items.Length); for (int i = 0; i < items.Length; i++) a.SetValue(items[i], i); return a;
    }
    sealed class Graph
    {
        public readonly object data;
        readonly object category;
        object group;
        float row;
        public Graph()
        {
            data = New("UnityEditor.ShaderGraph.GraphData"); Call(data, "AddContexts"); Set(data, "path", "Combat/Graphs");
            var target = New("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
            Call(target, "TrySetActiveSubTarget", T("UnityEditor.Rendering.Universal.ShaderGraph.UniversalSpriteUnlitSubTarget"));
            var descriptors = new List<object>();
            foreach (string name in new[] { "VertexDescription.Position", "VertexDescription.Normal", "VertexDescription.Tangent", "SurfaceDescription.BaseColor", "SurfaceDescription.Alpha" })
            {
                var parts = name.Split('.');
                descriptors.Add(T("UnityEditor.ShaderGraph.BlockFields+" + parts[0]).GetField(parts[1], Flags).GetValue(null));
            }
            Call(data, "InitializeOutputs", TypedArray(T("UnityEditor.ShaderGraph.Target"), target), TypedArray(descriptors[0].GetType(), descriptors.ToArray()));
            category = New("UnityEditor.ShaderGraph.CategoryData"); Set(category, "name", "Effect Controls"); Call(data, "AddCategory", category);
            Set(Get(data, "vertexContext"), "position", new Vector2(2100, -250));
            Set(Get(data, "fragmentContext"), "position", new Vector2(2100, 100));
        }
        public void Group(string title, float x)
        {
            group = New("UnityEditor.ShaderGraph.GroupData"); Set(group, "title", title); Set(group, "position", new Vector2(x, 0));
            Call(data, "CreateGroup", group); row = 0;
        }
        public object Node(string type)
        {
            var n = New("UnityEditor.ShaderGraph." + type);
            Call(data, "AddNode", n); Set(n, "group", group);
            var draw = Get(n, "drawState"); Set(draw, "expanded", true);
            Vector2 origin = (Vector2)Get(group, "position");
            Set(draw, "position", new Rect(origin.x + ((int)row % 2) * 255, origin.y + (int)(row / 2) * 220, 220, 160));
            Set(n, "drawState", draw); row++; return n;
        }
        public object Property(string type, string name, string reference, object value, bool exposed = true)
        {
            var p = New("UnityEditor.ShaderGraph.Internal." + type + "ShaderProperty");
            Set(p, "displayName", name); Set(p, "overrideReferenceName", reference); Set(p, "generatePropertyBlock", exposed);
            if (value != null) Set(p, "value", value);
            if (type == "Texture2D") Set(p, "isMainTexture", true);
            if (type == "Color") Set(p, "colorMode", Enum.Parse(Get(p, "colorMode").GetType(), "HDR"));
            Call(data, "AddGraphInput", p, -1); Call(category, "InsertItemIntoCategory", p, -1);
            var n = Node("PropertyNode"); Set(n, "property", p); return n;
        }
        public object Float(string name, string reference, float value)
        {
            var n = Property("Vector1", name, reference, value); var p = Get(n, "property");
            Vector2 range = reference == "_Opacity" || reference == "_EffectProgress" ? new Vector2(0, 1) :
                reference == "_Softness" || reference == "_EdgeWidth" ? new Vector2(.01f, .2f) :
                reference == "_NoiseScale" ? new Vector2(.1f, 30) : reference == "_FlowSpeed" ? new Vector2(-3, 3) : new Vector2(0, 5);
            Set(p, "floatType", Enum.Parse(Get(p, "floatType").GetType(), "Slider")); Set(p, "rangeValues", range); return n;
        }
        public object Constant(float value)
        {
            var n = Node("Vector1Node");
            var method = n.GetType().GetMethods(Flags).First(m => m.Name == "FindSlot" && m.IsGenericMethod && m.GetParameters().Length == 1);
            var slot = method.MakeGenericMethod(T("UnityEditor.ShaderGraph.Vector1MaterialSlot")).Invoke(n, new object[] { 1 });
            Set(slot, "value", value); return n;
        }
        public void Edge(object from, int output, object to, int input)
        {
            var type = T("UnityEditor.Graphing.SlotReference");
            var a = Activator.CreateInstance(type, Flags, null, new object[] { from, output }, null);
            var b = Activator.CreateInstance(type, Flags, null, new object[] { to, input }, null);
            if (Call(data, "Connect", a, b) == null) throw new Exception("Graph edge rejected");
        }
        public object Math(string type, object a, int aSlot, object b, int bSlot)
        {
            var n = Node(type + "Node"); Edge(a, aSlot, n, 0); Edge(b, bSlot, n, 1); return n;
        }
        public void Output(object rgb, int rgbSlot, object alpha, int alphaSlot, string file)
        {
            var getNodes = data.GetType().GetMethods(Flags).First(m => m.Name == "GetNodes" && m.IsGenericMethod);
            var blocks = ((IEnumerable)getNodes.MakeGenericMethod(T("UnityEditor.ShaderGraph.BlockNode")).Invoke(data, null)).Cast<object>().ToArray();
            Edge(rgb, rgbSlot, blocks.First(n => (string)Get(n, "name") == "SurfaceDescription.BaseColor"), 0);
            Edge(alpha, alphaSlot, blocks.First(n => (string)Get(n, "name") == "SurfaceDescription.Alpha"), 0);
            Call(data, "ValidateGraph");
            string json = (string)T("UnityEditor.ShaderGraph.Serialization.MultiJson").GetMethod("Serialize", Flags).Invoke(null, new[] { data });
            File.WriteAllText(Root + "/" + file + ".shadergraph", json);
        }
    }

    static void CreateFlow()
    {
        var g = new Graph(); g.Group("01  Sprite and HDR colour", -1500);
        var tex = g.Property("Texture2D", "Sprite Texture", "_MainTex", null);
        var sample = g.Node("SampleTexture2DNode"); g.Edge(tex, 0, sample, 1);
        var tint = g.Property("Color", "Energy Tint (HDR)", "_Tint", new Color(1.4f, .8f, .22f, 1));
        var intensity = g.Float("Intensity", "_Intensity", 1.2f);
        var opacity = g.Float("Opacity", "_Opacity", .9f);
        g.Group("02  Shared visual clock and flowing noise", -700);
        var uv = g.Node("UVNode");
        var time = g.Property("Vector1", "Combat Visual Time (global)", "_CombatVisualTime", 0f, false);
        var speed = g.Float("Flow Speed", "_FlowSpeed", .25f);
        var offset = g.Math("Multiply", time, 0, speed, 0);
        var flowUV = g.Math("Add", uv, 0, offset, 2);
        var noise = g.Node("NoiseNode"); g.Edge(flowUV, 2, noise, 0);
        var noiseScale = g.Float("Noise Scale", "_NoiseScale", 7f); g.Edge(noiseScale, 0, noise, 1);
        var strength = g.Float("Flow Strength", "_FlowStrength", .45f);
        var noiseAmount = g.Math("Multiply", noise, 2, strength, 0);
        g.Group("03  Preserve artwork, add energy", 100);
        var baseline = g.Constant(.8f);
        var modulation = g.Math("Add", baseline, 0, noiseAmount, 2);
        var tinted = g.Math("Multiply", sample, 0, tint, 0);
        var bright = g.Math("Multiply", tinted, 2, intensity, 0);
        var rgb = g.Math("Multiply", bright, 2, modulation, 2);
        var alpha = g.Math("Multiply", sample, 7, opacity, 0);
        g.Output(rgb, 2, alpha, 2, "EnergyFlow");
    }
    static void CreateDissolve()
    {
        var g = new Graph(); g.Group("01  Sprite and colours", -2000);
        var tex = g.Property("Texture2D", "Sprite Texture", "_MainTex", null);
        var sample = g.Node("SampleTexture2DNode"); g.Edge(tex, 0, sample, 1);
        var tint = g.Property("Color", "Tint (HDR)", "_Tint", new Color(.65f, .4f, 1.2f, 1));
        var edgeColour = g.Property("Color", "Edge Colour (HDR)", "_EdgeColor", new Color(.35f, .8f, 2f, 1));
        var opacity = g.Float("Opacity", "_Opacity", 1);
        g.Group("02  Progress: 0 visible, 1 dissolved", -1200);
        var progress = g.Float("Dissolve Progress 0-1", "_EffectProgress", 0);
        var saturated = g.Node("SaturateNode"); g.Edge(progress, 0, saturated, 0);
        // Simple Noise has a lower centre than 0.5; keep a readable partial silhouette at mid-progress.
        var span = g.Constant(1.75f); var half = g.Constant(.5f);
        var scaled = g.Math("Multiply", saturated, 1, span, 0);
        var threshold = g.Math("Subtract", scaled, 2, half, 0);
        var noise = g.Node("NoiseNode");
        var noiseScale = g.Float("Noise Scale", "_NoiseScale", 6); g.Edge(noiseScale, 0, noise, 1);
        g.Group("03  Soft coverage and bright edge", -400);
        var softness = g.Float("Softness (0.01-0.2)", "_Softness", .06f);
        var width = g.Float("Edge Width (0.01-0.2)", "_EdgeWidth", .1f);
        var softEnd = g.Math("Add", threshold, 2, softness, 0);
        var edgeEnd = g.Math("Add", softEnd, 2, width, 0);
        var cover = g.Node("SmoothstepNode"); g.Edge(threshold, 2, cover, 0); g.Edge(softEnd, 2, cover, 1); g.Edge(noise, 2, cover, 2);
        var inner = g.Node("SmoothstepNode"); g.Edge(softEnd, 2, inner, 0); g.Edge(edgeEnd, 2, inner, 1); g.Edge(noise, 2, inner, 2);
        var rim = g.Math("Subtract", cover, 3, inner, 3);
        g.Group("04  Colour and alpha output", 500);
        var edgeStrength = g.Float("Edge Intensity", "_EdgeIntensity", 2);
        var edge = g.Math("Multiply", rim, 2, edgeColour, 0);
        var glow = g.Math("Multiply", edge, 2, edgeStrength, 0);
        var baseColour = g.Math("Multiply", sample, 0, tint, 0);
        var rgb = g.Math("Add", baseColour, 2, glow, 2);
        var masked = g.Math("Multiply", sample, 7, cover, 3);
        var alpha = g.Math("Multiply", masked, 2, opacity, 0);
        g.Output(rgb, 2, alpha, 2, "EdgeDissolve");
    }

    public static void CreateBatch()
    {
        Directory.CreateDirectory(Output);
        try
        {
            Directory.CreateDirectory(Root + "/Materials");
            // Deliberately refuse to overwrite manually edited graphs or a previous workshop.
            if (File.Exists(Root + "/EnergyFlow.shadergraph") || File.Exists(Root + "/EdgeDissolve.shadergraph"))
                throw new InvalidOperationException("Workshop graphs already exist; edit them directly instead of regenerating.");
            CreateFlow(); CreateDissolve(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var flow = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/EnergyFlow.shadergraph");
            var dissolve = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/EdgeDissolve.shadergraph");
            CheckShader(flow); CheckShader(dissolve);
            Material gold = Material(flow, "Energy_Gold", new Color(1.4f, .85f, .23f, 1));
            Material violet = Material(flow, "Energy_Violet", new Color(.62f, .3f, 1.6f, 1));
            Material phase = Material(dissolve, "Dissolve_Violet", new Color(.65f, .4f, 1.2f, 1));
            CreateScene(gold, violet, phase); AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "/creation.txt", "PASS: two native Sprite Unlit graphs imported without shader errors; three materials; demo scene created.\n");
            BuildProbe();
        }
        catch (Exception e) { File.WriteAllText(Output + "/creation-error.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void CheckShader(Shader shader)
    {
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader Graph failed to import/compile: " + shader);
    }
    public static void BuildProbe() => Build(true);
    public static void RefreshDemoAndBuildProbe()
    {
        CreateScene(AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Energy_Gold.mat"),
            AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Energy_Violet.mat"),
            AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Dissolve_Violet.mat"));
        AssetDatabase.SaveAssets(); BuildProbe();
    }
    public static void BuildDemo() => Build(false);
    static void Build(bool probe)
    {
        string folder = Output + (probe ? "/Player" : "/Demo"); Directory.CreateDirectory(folder);
        var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions {
            scenes = new[] { Root + "/ShaderGraphWorkshop.unity" },
            locationPathName = folder + "/ShaderGraphWorkshop.exe", target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None, extraScriptingDefines = probe ? new[] { "CODEX_SHADER_GRAPH_WORKSHOP" } : Array.Empty<string>()
        });
        File.WriteAllText(Output + (probe ? "/probe-build.txt" : "/demo-build.txt"), report.summary.result + " " + report.summary.totalTime);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
    static Material Material(Shader shader, string name, Color tint)
    {
        var m = new Material(shader) { name = name }; m.SetColor("_Tint", tint);
        AssetDatabase.CreateAsset(m, Root + "/Materials/" + name + ".mat"); return m;
    }
    static void CreateScene(Material gold, Material violet, Material dissolve)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Workshop Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 4.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .055f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Character/Oracle/Animations/Shift Teleport Rune.png");
        if (sprite == null) throw new Exception("Existing Oracle rune sprite was not found");
        Label("SHADER GRAPH WORKSHOP", new Vector2(0, 3.7f), .065f);
        Label("Native nodes / URP Sprite Unlit", new Vector2(0, 3.1f), .035f);
        Sprite("Gold Flow", sprite, gold, new Vector2(-3.5f, 1.2f), 0);
        Sprite("Violet Flow", sprite, violet, new Vector2(3.5f, 1.2f), 0);
        Label("GOLD ENERGY", new Vector2(-3.5f, 2.5f), .04f); Label("VIOLET ENERGY", new Vector2(3.5f, 2.5f), .04f);
        var full = Sprite("Dissolve 0", sprite, dissolve, new Vector2(-4, -1.9f), 0);
        var middle = Sprite("Dissolve 50", sprite, dissolve, new Vector2(0, -1.9f), .5f);
        var gone = Sprite("Dissolve 100", sprite, dissolve, new Vector2(4, -1.9f), 1);
        Label("VISIBLE   0.0", new Vector2(-4, -.5f), .035f); Label("EDGE   0.5", new Vector2(0, -.5f), .035f); Label("GONE   1.0", new Vector2(4, -.5f), .035f);
        Label("Progress is externally controlled. Existing battle timing is unchanged.", new Vector2(0, -3.75f), .03f);
        var controls = new GameObject("Preview Controls").AddComponent<ShaderGraphWorkshopPreview>();
        controls.dissolveSprites = new[] { full, middle, gone };
        EditorSceneManager.SaveScene(SceneManagerScene(), Root + "/ShaderGraphWorkshop.unity");
    }
    static UnityEngine.SceneManagement.Scene SceneManagerScene() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    static SpriteRenderer Sprite(string name, Sprite sprite, Material material, Vector2 position, float progress)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sharedMaterial = material;
        r.transform.position = position; r.transform.localScale = Vector3.one * (2.3f / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        return r;
    }
    static void Label(string text, Vector2 position, float size)
    {
        var t = new GameObject(text).AddComponent<TextMesh>(); t.text = text; t.fontSize = 48; t.characterSize = size;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
        t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = new Color(.82f, .86f, .94f);
        t.transform.position = new Vector3(position.x, position.y, -1);
    }
}
