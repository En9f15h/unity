using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit authoring command. The resulting asset contains editable native graph nodes.
public static class CharacterGraphSetup
{
    const string Path = "Assets/ShaderGraphWorkshop/CharacterPresentation.shadergraph";
    public static void Install()
    {
        if (File.Exists(Path)) throw new Exception("Character graph already exists; edit it in Shader Graph.");
        BuildGraph();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Path);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Character graph import failed.");
        foreach (string role in new[] { "knight", "Oracle" })
            for (int skin = 0; skin < 2; skin++) Bind(role, skin, shader);
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_GRAPH_INSTALLED: native graph, four character materials and prefabs");
    }
    public static void InstallSkillUpgrade()
    {
        const string path = "Assets/ShaderGraphWorkshop/CharacterSkillPresentation.shadergraph";
        if (File.Exists(path)) throw new Exception("Skill graph already exists; edit it directly.");
        BuildGraph(true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Skill graph import failed.");
        foreach (string role in new[] { "knight", "Oracle" }) for (int skin = 0; skin < 2; skin++)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Combat/Materials/CharacterGraph_" + role + "_" + skin + ".mat");
            if (material == null) throw new Exception("Character material missing.");
            material.shader = shader;
            material.SetFloat("_SkillPattern", role == "Oracle" ? 1 : 0);
            material.SetFloat("_SkillStrength", skin == 0 ? .20f : .28f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_SKILL_GRAPH_INSTALLED");
    }
    public static void InstallHealthUpgrade()
    {
        const string path = "Assets/ShaderGraphWorkshop/CharacterSkillPresentation.shadergraph";
        // This explicit upgrade is only for the unmodified graph delivered in the previous batch.
        // Keep its GUID and existing material references when regenerating native nodes.
        if (!File.Exists(path)) throw new Exception("Install the skill graph first.");
        string source = File.ReadAllText(path);
        if (source.Contains("_LowHealthAmount")) throw new Exception("Health effect already exists; edit the graph directly.");
        using (var hash = System.Security.Cryptography.SHA256.Create())
        {
            string digest = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            if (digest != "9CF011418858FE1BA2856AD03B59EFB2341C23630DBAED1A8355AC481102ADA7")
                throw new Exception("Skill graph has been edited; merge the health nodes manually to preserve those edits.");
        }
        BuildGraph(true, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Health graph import failed.");
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_HEALTH_GRAPH_INSTALLED");
    }
    public static void InstallEntranceUpgrade()
    {
        const string path = "Assets/ShaderGraphWorkshop/CharacterSkillPresentation.shadergraph";
        using (var hash = System.Security.Cryptography.SHA256.Create())
        {
            string digest = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            if (digest != "098BAA8079291D92A123A748C068C7C2CE1B164AB120177FDB25FCE7321EE792")
                throw new Exception("Graph differs from the delivered health graph; preserve custom edits by merging nodes manually.");
        }
        BuildGraph(true, true, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Entrance graph import failed.");
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_ENTRANCE_GRAPH_INSTALLED");
    }
    static void BuildGraph(bool skills = false, bool health = false, bool entrance = false)
    {
        var g = new CombatShaderGraphWorkshop.Graph();
        g.Group("01  Original sprite / atlas-safe sampling", -2400);
        var tex = g.Property("Texture2D", "Sprite Texture", "_MainTex", null);
        var sample = g.Node("SampleTexture2DNode"); g.Edge(tex, 0, sample, 1);
        var uv = g.Node("UVNode");
        var rect = g.Property("Vector4", "Sprite UV Rect (runtime)", "_SpriteUVRect", new Vector4(0, 0, 1, 1));
        var split = g.Node("SplitNode"); g.Edge(rect, 0, split, 0);
        var lo = g.Node("CombineNode"); g.Edge(split, 1, lo, 0); g.Edge(split, 2, lo, 1);
        var hi = g.Node("CombineNode"); g.Edge(split, 3, hi, 0); g.Edge(split, 4, hi, 1);
        var size = g.Node("Texture2DPropertiesNode"); g.Edge(tex, 0, size, 1);
        var x = g.Node("CombineNode"); g.Edge(size, 3, x, 0);
        var y = g.Node("CombineNode"); g.Edge(size, 4, y, 1);
        object minimum = null; int minSlot = 7;
        foreach (var offset in new[] { x, y }) foreach (string op in new[] { "Add", "Subtract" })
        {
            var shifted = g.Math(op, uv, 0, offset, 6);
            var clamp = g.Node("ClampNode"); g.Edge(shifted, 2, clamp, 0); g.Edge(lo, 6, clamp, 1); g.Edge(hi, 6, clamp, 2);
            var neighbor = g.Node("SampleTexture2DNode"); g.Edge(tex, 0, neighbor, 1); g.Edge(clamp, 3, neighbor, 2);
            if (minimum == null) minimum = neighbor;
            else { minimum = g.Math("Minimum", minimum, minSlot, neighbor, 7); minSlot = 2; }
        }
        var difference = g.Math("Subtract", sample, 7, minimum, minSlot);
        var rim = g.Node("SaturateNode"); g.Edge(difference, 2, rim, 0);
        g.Group("02  Class edge light / action midpoint / guard", -1400);
        var edgeColor = g.Property("Color", "Edge Colour", "_RimColor", new Color(.8f, .6f, .25f, 1));
        var strength = g.Float("Resting Edge Strength", "_RimStrength", .08f);
        var pulse = g.Float("Action Midpoint (runtime)", "_ActionPulse", 0);
        var pulseStrength = g.Float("Action Edge Strength", "_PulseStrength", .55f);
        var action = g.Math("Multiply", pulse, 0, pulseStrength, 0);
        var guard = g.Float("Guard Flash (runtime)", "_GuardFlash", 0);
        var total = g.Math("Add", strength, 0, action, 2);
        total = g.Math("Add", total, 2, guard, 0);
        var edge = g.Math("Multiply", rim, 1, total, 2);
        var edgeRGB = g.Math("Multiply", edge, 2, edgeColor, 0);
        if (health)
        {
            var severity = g.Float("Low Health Pulse (runtime)", "_LowHealthAmount", 0);
            var warningStrength = g.Float("Low Health Edge Strength", "_LowHealthStrength", .65f);
            var warningColor = g.Property("Color", "Low Health Colour", "_LowHealthColor", new Color(1.2f, .13f, .08f, 1));
            var warning = g.Math("Multiply", severity, 0, warningStrength, 0);
            warning = g.Math("Multiply", warning, 2, rim, 1);
            var warningRGB = g.Math("Multiply", warning, 2, warningColor, 0);
            edgeRGB = g.Math("Add", edgeRGB, 2, warningRGB, 2);
        }
        var bodyTint = g.Property("Color", "Artwork Tint", "_Color", Color.white);
        var artwork = g.Math("Multiply", sample, 0, bodyTint, 0);
        var rgb = g.Math("Add", artwork, 2, edgeRGB, 2);
        // A faint body sheen makes the midpoint readable on thin line-art sprites too.
        var sheenStrength = g.Float("Body Sheen Strength", "_SheenStrength", .055f);
        var sheen = g.Math("Multiply", action, 2, sheenStrength, 0);
        var sheenRGB = g.Math("Multiply", sheen, 2, edgeColor, 0);
        rgb = g.Math("Add", rgb, 2, sheenRGB, 2);
        g.Group("03  Confirmed hit feedback", -400);
        var hit = g.Float("Hit Amount (runtime)", "_HitAmount", 0);
        var hitColor = g.Property("Color", "Hit Colour", "_HitColor", new Color(1.5f, 1.1f, .8f, 1));
        var hitRGB = g.Node("LerpNode"); g.Edge(rgb, 2, hitRGB, 0); g.Edge(hitColor, 0, hitRGB, 1); g.Edge(hit, 0, hitRGB, 2);
        g.Group("04  Phase visibility / exact visible and hidden endpoints", 400);
        var extent = g.Math("Subtract", hi, 6, lo, 6);
        var relative = g.Math("Subtract", uv, 0, lo, 6);
        var localUV = g.Math("Divide", relative, 2, extent, 2);
        object skillRGB = null;
        object classPattern = null;
        if (skills)
        {
            g.Group("05  Animated charge sweep / spell lattice", 1200);
            var channels = g.Node("SplitNode"); g.Edge(localUV, 2, channels, 0);
            var phaseTime = g.Float("Skill Phase (animation)", "_SkillPhase", 0);
            var amount = g.Float("Skill Amount (animation)", "_SkillAmount", 0);
            var intensity = g.Float("Skill Brightness", "_SkillStrength", .2f);
            var pattern = g.Float("Pattern: 0 sweep / 1 spell lattice", "_SkillPattern", 0);
            classPattern = pattern;
            // The sweep traverses the whole sprite and is centered at the strong beat.
            var spanSkill = g.Constant(2); var margin = g.Constant(.5f);
            var sweepPosition = g.Math("Multiply", phaseTime, 0, spanSkill, 0);
            sweepPosition = g.Math("Subtract", sweepPosition, 2, margin, 0);
            var delta = g.Math("Subtract", channels, 2, sweepPosition, 2);
            var absolute = g.Node("AbsoluteNode"); g.Edge(delta, 2, absolute, 0);
            var bandWidth = g.Constant(.18f); var zero = g.Constant(0);
            var soft = g.Node("SmoothstepNode"); g.Edge(zero, 0, soft, 0); g.Edge(bandWidth, 0, soft, 1); g.Edge(absolute, 1, soft, 2);
            var band = g.Node("OneMinusNode"); g.Edge(soft, 3, band, 0);
            // Crossed diagonal lines form a restrained geometric spell texture.
            var diagonalA = g.Math("Add", channels, 1, channels, 2);
            var diagonalB = g.Math("Subtract", channels, 1, channels, 2);
            var density = g.Float("Spell Line Density", "_SkillDensity", 32);
            var scroll = g.Math("Multiply", phaseTime, 0, g.Constant(6.283185f), 0);
            object lines = null;
            foreach (var diagonal in new[] { diagonalA, diagonalB })
            {
                var coord = g.Math("Multiply", diagonal, 2, density, 0);
                coord = g.Math("Add", coord, 2, scroll, 2);
                var sine = g.Node("SineNode"); g.Edge(coord, 2, sine, 0);
                var abs = g.Node("AbsoluteNode"); g.Edge(sine, 1, abs, 0);
                var sharp = g.Math("Power", abs, 1, g.Constant(18), 0);
                lines = lines == null ? sharp : g.Math("Maximum", lines, 2, sharp, 2);
            }
            var lattice = g.Math("Multiply", lines, 2, band, 1);
            var select = g.Node("LerpNode"); g.Edge(band, 1, select, 0); g.Edge(lattice, 2, select, 1); g.Edge(pattern, 0, select, 2);
            var gain = g.Math("Multiply", select, 3, amount, 0);
            gain = g.Math("Multiply", gain, 2, intensity, 0);
            skillRGB = g.Math("Multiply", gain, 2, edgeColor, 0);
            g.Group("06  Phase output", 2100);
        }
        var noise = g.Node("NoiseNode"); g.Edge(localUV, 2, noise, 0);
        var noiseScale = g.Constant(24); g.Edge(noiseScale, 0, noise, 1);
        var phase = g.Float("Dissolve (runtime)", "_Dissolve", 0);
        var span = g.Constant(1.75f); var half = g.Constant(.5f);
        var scaled = g.Math("Multiply", phase, 0, span, 0);
        var threshold = g.Math("Subtract", scaled, 2, half, 0);
        var width = g.Float("Phase Edge Width", "_PhaseWidth", .045f);
        var end = g.Math("Add", threshold, 2, width, 0);
        var cover = g.Node("SmoothstepNode"); g.Edge(threshold, 2, cover, 0); g.Edge(end, 2, cover, 1); g.Edge(noise, 2, cover, 2);
        var outerEnd = g.Math("Add", end, 2, width, 0);
        var inner = g.Node("SmoothstepNode"); g.Edge(end, 2, inner, 0); g.Edge(outerEnd, 2, inner, 1); g.Edge(noise, 2, inner, 2);
        var phaseEdge = g.Math("Subtract", cover, 3, inner, 3);
        var phaseColor = g.Property("Color", "Phase Colour", "_PhaseColor", new Color(.4f, 1.4f, 2, 1));
        var phaseRGB = g.Math("Multiply", phaseEdge, 2, phaseColor, 0);
        var finalRGB = g.Math("Add", hitRGB, 3, phaseRGB, 2);
        if (skills)
        {
            // Hit flash takes priority; visibility is applied to the entire result below.
            var noHit = g.Node("OneMinusNode"); g.Edge(hit, 0, noHit, 0);
            var skillVisible = g.Math("Multiply", skillRGB, 2, noHit, 1);
            finalRGB = g.Math("Add", finalRGB, 2, skillVisible, 2);
        }
        if (entrance)
        {
            g.Group("07  Shared-beat entrance / additive only", 3000);
            var introPhase = g.Float("Entrance Phase (runtime)", "_EntrancePhase", 0);
            var introAmount = g.Float("Entrance Amount (runtime)", "_EntranceAmount", 0);
            var introStrength = g.Float("Entrance Brightness", "_EntranceStrength", .35f);
            var position = g.Math("Multiply", introPhase, 0, g.Constant(2), 0);
            position = g.Math("Subtract", position, 2, g.Constant(.5f), 0);
            var axis = g.Node("SplitNode"); g.Edge(localUV, 2, axis, 0);
            var deltaIntro = g.Math("Subtract", axis, 2, position, 2);
            var distance = g.Node("AbsoluteNode"); g.Edge(deltaIntro, 2, distance, 0);
            var fade = g.Node("SmoothstepNode"); g.Edge(g.Constant(0), 0, fade, 0); g.Edge(g.Constant(.25f), 0, fade, 1); g.Edge(distance, 1, fade, 2);
            var introBand = g.Node("OneMinusNode"); g.Edge(fade, 3, introBand, 0);
            // Reuse the phase noise already sampled by the graph: no extra texture or noise sample.
            var stars = g.Node("SmoothstepNode"); g.Edge(g.Constant(.40f), 0, stars, 0); g.Edge(g.Constant(.6f), 0, stars, 1); g.Edge(noise, 2, stars, 2);
            var sparkle = g.Math("Multiply", stars, 3, introBand, 1);
            var style = g.Node("LerpNode"); g.Edge(introBand, 1, style, 0); g.Edge(sparkle, 2, style, 1); g.Edge(classPattern, 0, style, 2);
            var brightness = g.Math("Multiply", style, 3, introAmount, 0);
            brightness = g.Math("Multiply", brightness, 2, introStrength, 0);
            var introRGB = g.Math("Multiply", brightness, 2, edgeColor, 0);
            var freeOfHit = g.Node("OneMinusNode"); g.Edge(hit, 0, freeOfHit, 0);
            introRGB = g.Math("Multiply", introRGB, 2, freeOfHit, 1);
            finalRGB = g.Math("Add", finalRGB, 2, introRGB, 2);
        }
        var alpha = g.Math("Multiply", sample, 7, cover, 3);
        g.Output(finalRGB, 2, alpha, 2, skills ? "CharacterSkillPresentation" : "CharacterPresentation");
    }
    static void Bind(string role, int skin, Shader shader)
    {
        string name = role + "_" + skin;
        string path = "Assets/Resources/Prefab/" + role + "/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var feedback = root.GetComponent<CharacterShaderFeedback>();
            if (feedback == null) throw new Exception("Missing feedback on " + path);
            var material = new Material(shader) { name = "CharacterGraph_" + name };
            bool oracle = role == "Oracle";
            material.SetColor("_RimColor", oracle ? new Color(.4f, .6f, 1.1f, 1) : new Color(1.1f, .77f, .3f, 1));
            material.SetFloat("_RimStrength", skin == 0 ? .06f : .10f);
            material.SetFloat("_PulseStrength", skin == 0 ? .45f : .65f);
            AssetDatabase.CreateAsset(material, "Assets/Resources/Combat/Materials/" + material.name + ".mat");
            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("characterMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var renderer in feedback.BodyRenderers) if (renderer != null) renderer.sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
