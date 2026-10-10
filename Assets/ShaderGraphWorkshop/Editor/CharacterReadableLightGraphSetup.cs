using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using G = CombatShaderGraphWorkshop.Graph;

// Keep the existing graph and add broad, bounded directional light for black artwork.
public static class CharacterReadableLightGraphSetup
{
    public static void Install()
    {
        const string path = CharacterHD2DGraphSetup.GraphPath;
        if (File.ReadAllText(path).Contains("_HD2DLightWrap"))
            throw new InvalidOperationException("Readable light already installed; adjust the material controls.");
        var g = new G(path); var nodes = g.Nodes(); var edges = g.Edges();
        object From(object e) => G.Read(G.Read(e, "outputSlot"), "node");
        object To(object e) => G.Read(G.Read(e, "inputSlot"), "node");
        int Slot(object e) => (int)G.Read(G.Read(e, "outputSlot"), "slotId");
        object Property(string reference) => nodes.Single(n => n.GetType().Name == "PropertyNode" &&
            (string)G.Read(G.Read(n, "property"), "referenceName") == reference);
        var facing = nodes.Single(n => n.GetType().Name == "SmoothstepNode" &&
            G.Read(n,"group") != null && (string)G.Read(G.Read(n,"group"),"title") == "10  Directional character light - body contrast");
        var floatSlotType = AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.ShaderGraph.Vector1MaterialSlot")).First(t=>t!=null);
        foreach(var node in nodes.Where(n=>n.GetType().Name=="Vector1Node" && Equals(G.Read(n,"group"),G.Read(facing,"group"))))
        {
            var method=node.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                .First(m=>m.Name=="FindSlot" && m.IsGenericMethod && m.GetParameters().Length==1);
            var slot=method.MakeGenericMethod(floatSlotType).Invoke(node,new object[]{1});
            float value=(float)G.Read(slot,"value");
            // Deeper shadow side, similar peak brightness, clearer transition across the body.
            if(Mathf.Approximately(value,.55f))G.Write(slot,"value",.35f);
            else if(Mathf.Approximately(value,.85f))G.Write(slot,"value",1.1f);
            else if(Mathf.Approximately(value,3.5f))G.Write(slot,"value",5.5f);
        }
        var bodyBlock = nodes.Single(n => n.GetType().Name == "BlockNode" && (string)G.Read(n,"name") == "SurfaceDescription.BaseColor");
        var bodyInput = edges.Single(e => To(e) == bodyBlock);
        g.Group("13  Broad key light - readable at gameplay distance", 7700);
        var wrap = g.Float("HD2D broad light strength", "_HD2DLightWrap", .18f);
        // Squared facing leaves the unlit side black; no full-character gray overlay.
        var face = g.Math("Multiply", facing, 3, facing, 3);
        var light = g.Math("Multiply", face, 2, Property("_HD2DBodyLight"), 0);
        light = g.Math("Multiply", light, 2, wrap, 0);
        light = g.Math("Multiply", light, 2, Property("_HD2DStrength"), 0);
        var hit = g.Node("SaturateNode"); g.Edge(Property("_HitAmount"),0,hit,0);
        var visible = g.Node("OneMinusNode"); g.Edge(hit,1,visible,0);
        light = g.Math("Multiply", light,2,visible,1);
        var combined = g.Math("Add",From(bodyInput),Slot(bodyInput),light,2);
        g.Edge(combined,2,bodyBlock,0);
        g.Save(path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if(shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Readable light graph failed to compile");
        foreach (var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Resources/Combat/Materials"}))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material.shader != shader) continue;
            material.SetFloat("_HD2DContrast",1f);
            material.SetFloat("_HD2DReflection",4f);
            material.SetFloat("_HD2DRimWidth",1.25f);
            material.SetFloat("_HD2DLightWrap",material.name.EndsWith("_0",StringComparison.Ordinal)?.18f:.08f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }
}
