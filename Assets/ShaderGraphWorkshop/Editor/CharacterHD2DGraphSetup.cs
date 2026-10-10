using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using G = CombatShaderGraphWorkshop.Graph;

// Append native nodes to the existing graph; retain every existing node, GUID and effect.
public static class CharacterHD2DGraphSetup
{
    public const string GraphPath="Assets/ShaderGraphWorkshop/CharacterSkillPresentation.shadergraph";
    public static void Install()
    {
        if(File.ReadAllText(GraphPath).Contains("_HD2DStrength")) throw new InvalidOperationException("HD2D nodes already installed; edit the graph directly.");
        var g=new G(GraphPath);var nodes=g.Nodes();var edges=g.Edges();
        object From(object e)=>G.Read(G.Read(e,"outputSlot"),"node");
        object To(object e)=>G.Read(G.Read(e,"inputSlot"),"node");
        int Slot(object e,string end)=>(int)G.Read(G.Read(e,end),"slotId");
        var tint=nodes.Single(n=>n.GetType().Name=="PropertyNode" && (string)G.Read(G.Read(n,"property"),"referenceName")=="_Color");
        var artwork=To(edges.Single(e=>From(e)==tint));
        var destinations=edges.Where(e=>From(e)==artwork).ToArray();
        var rim=nodes.Single(n=>n.GetType().Name=="SaturateNode" && edges.Any(e=>To(e)==n && From(e).GetType().Name=="SubtractNode" &&
            edges.Any(s=>To(s)==From(e) && Slot(s,"inputSlot")==0 && Slot(s,"outputSlot")==7 && From(s).GetType().Name=="SampleTexture2DNode")));
        g.Group("09  HD2D world-space artwork light / preserves hit and phase",4000);
        var strength=g.Float("HD2D enabled (runtime)","_HD2DStrength",0);
        var body=g.Property("Color","HD2D body light (runtime)","_HD2DBodyLight",Color.white);
        var edge=g.Property("Color","HD2D reflected edge (runtime)","_HD2DEdgeLight",Color.clear);
        var anchor=g.Property("Vector4","HD2D owner anchor (runtime)","_HD2DAnchor",new Vector4(0,0,1,0));
        var split=g.Node("SplitNode");g.Edge(anchor,0,split,0);
        var position=g.Node("PositionNode");var space=G.Read(position,"space");G.Write(position,"m_Space",Enum.Parse(space.GetType(),"World"));
        var world=g.Node("SplitNode");g.Edge(position,0,world,0);
        var height=g.Math("Subtract",world,2,split,2);
        height=g.Math("Divide",height,2,split,3);
        var ramp=g.Node("SaturateNode");g.Edge(height,2,ramp,0);
        var brightness=g.Math("Multiply",ramp,1,g.Constant(.12f),0);
        brightness=g.Math("Add",brightness,2,g.Constant(.96f),0);
        var side=g.Math("Subtract",world,1,split,1);
        side=g.Math("Divide",side,2,split,3);
        side=g.Math("Multiply",side,2,split,4);
        var sideClamp=g.Node("ClampNode");g.Edge(side,2,sideClamp,0);g.Edge(g.Constant(-.5f),0,sideClamp,1);g.Edge(g.Constant(.5f),0,sideClamp,2);
        var sideLight=g.Math("Multiply",sideClamp,3,g.Constant(.12f),0);
        brightness=g.Math("Add",brightness,2,sideLight,2);
        var bodyLit=g.Math("Multiply",body,0,brightness,2);
        var mix=g.Node("LerpNode");g.Edge(g.Constant(1),0,mix,0);g.Edge(bodyLit,2,mix,1);g.Edge(strength,0,mix,2);
        var lit=g.Math("Multiply",artwork,2,mix,3);
        var edgeHeight=g.Math("Multiply",ramp,1,g.Constant(.65f),0);
        edgeHeight=g.Math("Add",edgeHeight,2,g.Constant(.35f),0);
        edgeHeight=g.Math("Add",edgeHeight,2,sideLight,2);
        var reflected=g.Math("Multiply",rim,1,edge,0);
        reflected=g.Math("Multiply",reflected,2,edgeHeight,2);
        reflected=g.Math("Multiply",reflected,2,strength,0);
        lit=g.Math("Add",lit,2,reflected,2);
        foreach(var destination in destinations)g.Edge(lit,2,To(destination),Slot(destination,"inputSlot"));
        g.Save(GraphPath);AssetDatabase.ImportAsset(GraphPath,ImportAssetOptions.ForceSynchronousImport);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
        if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("HD2D character graph import failed");
        AssetDatabase.SaveAssets();
    }
}
