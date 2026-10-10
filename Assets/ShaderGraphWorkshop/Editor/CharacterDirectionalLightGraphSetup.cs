using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using G = CombatShaderGraphWorkshop.Graph;

// One-time native-node upgrade. Does not regenerate the graph or change its GUID.
public static class CharacterDirectionalLightGraphSetup
{
    public static void Install()
    {
        const string path = CharacterHD2DGraphSetup.GraphPath;
        if (File.ReadAllText(path).Contains("_HD2DContrast"))
            throw new InvalidOperationException("Directional lighting already installed; edit its exposed controls in Shader Graph.");
        var g = new G(path);
        var nodes = g.Nodes(); var edges = g.Edges();
        object From(object e) => G.Read(G.Read(e, "outputSlot"), "node");
        object To(object e) => G.Read(G.Read(e, "inputSlot"), "node");
        int Slot(object e, string end) => (int)G.Read(G.Read(e, end), "slotId");
        object Property(string reference) => nodes.Single(n => n.GetType().Name == "PropertyNode" &&
            (string)G.Read(G.Read(n, "property"), "referenceName") == reference);
        var strength = Property("_HD2DStrength");
        var previousGroup = G.Read(strength, "group");
        var destinations = edges.Where(e => Equals(G.Read(From(e), "group"), previousGroup) &&
            !Equals(G.Read(To(e), "group"), previousGroup)).ToArray();
        if (destinations.Length == 0) throw new InvalidOperationException("Missing existing HD2D output.");
        var artwork = To(edges.Single(e => From(e) == Property("_Color")));
        var rim = nodes.Single(n => n.GetType().Name == "SaturateNode" && edges.Any(e => To(e) == n &&
            From(e).GetType().Name == "SubtractNode" && edges.Any(s => To(s) == From(e) &&
            Slot(s, "inputSlot") == 0 && Slot(s, "outputSlot") == 7 && From(s).GetType().Name == "SampleTexture2DNode")));
        var rimSubtract = From(edges.Single(e => To(e) == rim));
        var sample = From(edges.Single(e => To(e) == rimSubtract && Slot(e, "inputSlot") == 0));
        g.Group("10  Directional character light - body contrast", 4700);
        object Unary(string type, object input, int slot)
        { var n = g.Node(type + "Node"); g.Edge(input, slot, n, 0); return n; }
        var contrast = g.Float("HD2D body contrast", "_HD2DContrast", .9f);
        var reflection = g.Float("HD2D directional rim intensity", "_HD2DReflection", 2.4f);
        var rimWidth = g.Float("HD2D rim width (screen pixels)", "_HD2DRimWidth", 1.25f);
        var anchor = Unary("Split", Property("_HD2DAnchor"), 0);
        var position = g.Node("PositionNode");
        G.Write(position, "m_Space", Enum.Parse(G.Read(position, "space").GetType(), "World"));
        var world = Unary("Split", position, 0);
        var horizontal = g.Math("Subtract", world, 1, anchor, 1);
        horizontal = g.Math("Divide", horizontal, 2, anchor, 3);
        horizontal = g.Math("Multiply", horizontal, 2, anchor, 4);
        horizontal = g.Math("Multiply", horizontal, 2, g.Constant(3.5f), 0);
        horizontal = g.Math("Add", horizontal, 2, g.Constant(.5f), 0);
        var facing = g.Node("SmoothstepNode");
        g.Edge(g.Constant(0), 0, facing, 0); g.Edge(g.Constant(1), 0, facing, 1); g.Edge(horizontal, 2, facing, 2);
        var height = g.Math("Subtract", world, 2, anchor, 2);
        height = g.Math("Divide", height, 2, anchor, 3);
        var top = Unary("Saturate", height, 2);
        var brightness = g.Math("Multiply", facing, 3, g.Constant(.85f), 0);
        brightness = g.Math("Add", brightness, 2, g.Constant(.55f), 0);
        var overhead = g.Math("Multiply", top, 1, g.Constant(.10f), 0);
        brightness = g.Math("Add", brightness, 2, overhead, 2);
        var body = g.Math("Multiply", Property("_HD2DBodyLight"), 0, brightness, 2);
        var amount = g.Math("Multiply", strength, 0, Unary("Saturate", contrast, 0), 1);
        var blend = g.Node("LerpNode");
        g.Edge(g.Constant(1), 0, blend, 0); g.Edge(body, 2, blend, 1); g.Edge(amount, 2, blend, 2);
        var lit = g.Math("Multiply", artwork, 2, blend, 3);

        g.Group("11  Screen alpha slope - bone-aware directional edge", 5500);
        // Sample beyond the current pixel: alpha derivatives alone vanish on tightly cut
        // opaque sprites. UV derivatives keep the width stable as bones deform or zoom.
        var uv = g.Node("UVNode");
        var rect = Unary("Split", Property("_SpriteUVRect"), 0);
        var lo = g.Node("CombineNode"); g.Edge(rect, 1, lo, 0); g.Edge(rect, 2, lo, 1);
        var hi = g.Node("CombineNode"); g.Edge(rect, 3, hi, 0); g.Edge(rect, 4, hi, 1);
        object Neighbor(string axis, string operation)
        {
            var offset = g.Math("Multiply", Unary(axis, uv, 0), 1, rimWidth, 0);
            var shifted = g.Math(operation, uv, 0, offset, 2);
            var clamped = g.Node("ClampNode");
            g.Edge(shifted, 2, clamped, 0); g.Edge(lo, 6, clamped, 1); g.Edge(hi, 6, clamped, 2);
            var neighbor = g.Node("SampleTexture2DNode");
            g.Edge(Property("_MainTex"), 0, neighbor, 1); g.Edge(clamped, 3, neighbor, 2);
            // Treat beyond this sprite's atlas rect as transparent, not adjacent artwork.
            var distance = g.Math("Distance", shifted, 2, clamped, 3);
            var outside = g.Math("Step", g.Constant(.000001f), 0, distance, 2);
            return g.Math("Multiply", neighbor, 7, Unary("OneMinus", outside, 2), 1);
        }
        var dx = g.Math("Subtract", Neighbor("DDX", "Add"), 2, Neighbor("DDX", "Subtract"), 2);
        var dy = g.Math("Subtract", Neighbor("DDY", "Add"), 2, Neighbor("DDY", "Subtract"), 2);
        var sx = Unary("Sign", Unary("DDX", world, 1), 1);
        var sy = Unary("Sign", Unary("DDY", world, 2), 1);
        var gx = g.Math("Multiply", dx, 2, sx, 1);
        var gy = g.Math("Multiply", dy, 2, sy, 1);
        var lightX = g.Math("Multiply", gx, 2, anchor, 4);
        var lightY = g.Math("Multiply", gy, 2, g.Constant(.7f), 0);
        var dot = g.Math("Add", lightX, 2, lightY, 2);
        dot = g.Math("Multiply", dot, 2, g.Constant(-1), 0);
        var magnitude = g.Math("Add", Unary("Absolute", gx, 2), 1, Unary("Absolute", gy, 2), 1);
        var safe = g.Math("Maximum", magnitude, 2, g.Constant(.0001f), 0);
        var direction = Unary("Saturate", g.Math("Divide", dot, 2, safe, 2), 2);
        // A one-texture-texel rim disappears when large sprites shrink on screen.
        // Use the already sampled alpha's screen slope for a visible pixel-width edge.
        var screenEdge = Unary("Saturate", g.Math("Multiply", magnitude, 2, g.Constant(1.5f), 0), 2);
        var edge = g.Math("Multiply", screenEdge, 1, direction, 1);
        edge = g.Math("Multiply", edge, 2, Property("_HD2DEdgeLight"), 0);
        edge = g.Math("Multiply", edge, 2, reflection, 0);
        edge = g.Math("Multiply", edge, 2, strength, 0);
        foreach (var destination in destinations)
            g.Edge(lit, 2, To(destination), Slot(destination, "inputSlot"));
        g.Group("12  Preserve sprite tint - illuminate black outlines", 7000);
        // URP normally multiplies the complete graph by SpriteRenderer.color. Knight_0
        // has a black tint, which erased every additive light. Apply the same tint here
        // to the original output, and add only the new environmental rim afterwards.
        foreach (var target in ((IEnumerable)G.Read(g.data, "activeTargets")))
            G.Write(target, "disableTint", true);
        var vertexColor = g.Node("VertexColorNode");
        var baseBlock = nodes.Single(n => n.GetType().Name == "BlockNode" && (string)G.Read(n, "name") == "SurfaceDescription.BaseColor");
        var alphaBlock = nodes.Single(n => n.GetType().Name == "BlockNode" && (string)G.Read(n, "name") == "SurfaceDescription.Alpha");
        var baseInput = edges.Single(e => To(e) == baseBlock);
        var alphaInput = edges.Single(e => To(e) == alphaBlock);
        var tinted = g.Math("Multiply", From(baseInput), Slot(baseInput,"outputSlot"), vertexColor, 0);
        edge = g.Math("Multiply", edge, 2, Unary("OneMinus", Unary("Saturate", Property("_HitAmount"), 0), 1), 1);
        var result = g.Math("Add", tinted, 2, edge, 2);
        g.Edge(result, 2, baseBlock, 0);
        var vertexChannels = Unary("Split", vertexColor, 0);
        var alpha = g.Math("Multiply", From(alphaInput), Slot(alphaInput,"outputSlot"), vertexChannels, 4);
        g.Edge(alpha, 2, alphaBlock, 0);
        // Old nodes stay available as a reference, but have no path to fragment output.
        g.Save(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Directional graph compilation failed");
        foreach (string materialPath in AssetDatabase.FindAssets("t:Material", new[]{"Assets/Resources/Combat/Materials"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material.shader != shader) continue;
            material.SetFloat("_HD2DContrast", .9f);
            material.SetFloat("_HD2DReflection", 2.4f);
            material.SetFloat("_HD2DRimWidth", 1.25f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }
}
