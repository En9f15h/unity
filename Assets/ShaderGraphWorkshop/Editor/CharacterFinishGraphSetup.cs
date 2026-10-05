using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CharacterFinishGraphSetup
{
    public static void Install()
    {
        foreach (string name in new[] { "CharacterGhost", "WeaponRibbon" })
            if (File.Exists("Assets/ShaderGraphWorkshop/" + name + ".shadergraph")) throw new Exception("Graph already exists: " + name);
        Ghost(); Ribbon(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Upgrade("OracleGhost", "CharacterGhost"); Upgrade("KnightSlash", "WeaponRibbon");
        AssetDatabase.SaveAssets(); Debug.Log("CHARACTER_FINISH_GRAPHS_INSTALLED");
    }
    static void Upgrade(string materialName, string graph)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ShaderGraphWorkshop/" + graph + ".shadergraph");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader import failed: " + graph);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Combat/Materials/" + materialName + ".mat");
        if (material == null) throw new Exception("Material missing: " + materialName);
        material.shader = shader; EditorUtility.SetDirty(material);
    }
    static void Ghost()
    {
        var g = new CombatShaderGraphWorkshop.Graph(false);
        g.Group("01  Baked pose / runtime colour", -1500);
        var tex = g.Property("Texture2D", "Sprite", "_MainTex", null);
        var sample = g.Node("SampleTexture2DNode"); g.Edge(tex, 0, sample, 1);
        var tint = g.Property("Color", "Snapshot Tint", "_Color", Color.white);
        var tintChannels = g.Node("SplitNode"); g.Edge(tint, 0, tintChannels, 0);
        var vertex = g.Node("VertexColorNode"); var channels = g.Node("SplitNode"); g.Edge(vertex, 0, channels, 0);
        g.Group("02  Lifetime / dissolving energy", -650);
        var progress = g.Float("Snapshot Progress", "_EffectProgress", 0);
        var threshold = g.Math("Multiply", progress, 0, g.Constant(1.75f), 0);
        threshold = g.Math("Subtract", threshold, 2, g.Constant(.5f), 0);
        var noise = g.Node("NoiseNode"); g.Edge(g.Constant(30), 0, noise, 1);
        var end = g.Math("Add", threshold, 2, g.Constant(.08f), 0);
        var coverage = g.Node("SmoothstepNode"); g.Edge(threshold, 2, coverage, 0); g.Edge(end, 2, coverage, 1); g.Edge(noise, 2, coverage, 2);
        var glow = g.Property("Color", "Spectral Light", "_GlowColor", new Color(.3f, .7f, 1.2f, 1));
        var glowStrength = g.Float("Spectral Brightness", "_GlowStrength", .32f);
        var light = g.Math("Multiply", glow, 0, glowStrength, 0);
        var artwork = g.Math("Multiply", sample, 0, tint, 0);
        var rgb = g.Math("Add", artwork, 2, light, 2);
        rgb = g.Math("Multiply", rgb, 2, vertex, 0);
        var intensity = g.Float("Intensity", "_Intensity", .85f); rgb = g.Math("Multiply", rgb, 2, intensity, 0);
        g.Group("03  Lifetime alpha / no live skeleton", 200);
        var alpha = g.Math("Multiply", sample, 7, tintChannels, 4);
        alpha = g.Math("Multiply", alpha, 2, channels, 4);
        alpha = g.Math("Multiply", alpha, 2, coverage, 3);
        g.Output(rgb, 2, alpha, 2, "CharacterGhost");
    }
    static void Ribbon()
    {
        var g = new CombatShaderGraphWorkshop.Graph(false, true);
        g.Group("01  Blade ribbon shape", -1500);
        var uv = g.Node("UVNode"); var split = g.Node("SplitNode"); g.Edge(uv, 0, split, 0);
        var y = g.Math("Multiply", split, 2, g.Constant(2), 0); y = g.Math("Subtract", y, 2, g.Constant(1), 0);
        var abs = g.Node("AbsoluteNode"); g.Edge(y, 2, abs, 0);
        var inv = g.Node("OneMinusNode"); g.Edge(abs, 1, inv, 0);
        var shape = g.Node("SaturateNode"); g.Edge(inv, 1, shape, 0);
        var core = g.Math("Power", shape, 1, g.Constant(7), 0);
        var coverage = g.Math("Power", shape, 1, g.Constant(1.4f), 0);
        var edgeColor = g.Property("Color", "Edge", "_EdgeColor", new Color(1, .5f, .15f, 1));
        var coreColor = g.Property("Color", "Core", "_CoreColor", new Color(1.8f, 1.5f, .9f, 1));
        var color = g.Node("LerpNode"); g.Edge(edgeColor, 0, color, 0); g.Edge(coreColor, 0, color, 1); g.Edge(core, 2, color, 2);
        g.Group("02  Actual animation phase / flowing highlights", -650);
        var time = g.Float("Animation Phase (runtime)", "_RibbonPhase", 0);
        var speed = g.Float("Flow Speed", "_FlowSpeed", 2.5f);
        var moving = g.Math("Multiply", time, 0, speed, 0); moving = g.Math("Multiply", moving, 2, g.Constant(8), 0);
        var coord = g.Math("Multiply", split, 1, g.Constant(32), 0); coord = g.Math("Subtract", coord, 2, moving, 2);
        var sine = g.Node("SineNode"); g.Edge(coord, 2, sine, 0);
        var flow = g.Math("Multiply", sine, 1, g.Constant(.08f), 0); flow = g.Math("Add", flow, 2, g.Constant(.92f), 0);
        var rgb = g.Math("Multiply", color, 3, flow, 2);
        var intensity = g.Float("Intensity", "_Intensity", 1.1f); rgb = g.Math("Multiply", rgb, 2, intensity, 0);
        g.Group("03  Trail vertex colour and fade", 200);
        var vertex = g.Node("VertexColorNode"); var v = g.Node("SplitNode"); g.Edge(vertex, 0, v, 0);
        var tint = g.Property("Color", "Tint", "_Color", Color.white); var t = g.Node("SplitNode"); g.Edge(tint, 0, t, 0);
        rgb = g.Math("Multiply", rgb, 2, vertex, 0); rgb = g.Math("Multiply", rgb, 2, tint, 0);
        var alpha = g.Math("Multiply", coverage, 2, v, 4); alpha = g.Math("Multiply", alpha, 2, t, 4);
        g.Output(rgb, 2, alpha, 2, "WeaponRibbon");
    }
}
