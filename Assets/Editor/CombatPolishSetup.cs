using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CombatPolishSetup
{
    public const string Output="CodexLogs/CombatPolish-20260915";
    [MenuItem("Tools/Combat Shaders/Install Contact and Impact Polish")]
    public static void Install()
    {
        Directory.CreateDirectory(Output);
        Material shadow=Material("ContactShadow","Combat/Ground Contact"); shadow.SetFloat("_Mode",0);
        Material dust=Material("ContactDust","Combat/Ground Contact"); dust.SetFloat("_Mode",1); dust.SetColor("_Color",Color.white);
        Material impact=Material("ImpactAccent","Combat/Impact Accent");
        var ward=Resources.Load<Material>("Combat/Materials/OracleWard");
        if(ward==null) throw new InvalidOperationException("Missing OracleWard");
        ward.SetFloat("_WardCenterOpacity",.18f); ward.SetFloat("_WardRimStrength",.35f); EditorUtility.SetDirty(ward);
        var report=new List<string>();
        foreach(string path in CombatShaderSetup.CharacterPaths)
        {
            GameObject root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var unit=root.GetComponentInChildren<CharacterUnit>(true);
                if(unit==null) throw new InvalidOperationException("Missing CharacterUnit: "+path);
                var ground=unit.GetComponent<CharacterGroundPresentation>()??unit.gameObject.AddComponent<CharacterGroundPresentation>();
                var so=new SerializedObject(ground);
                so.FindProperty("bodyCollider").objectReferenceValue=unit.GetComponent<Collider2D>();
                so.FindProperty("shadowMaterial").objectReferenceValue=shadow; so.FindProperty("dustMaterial").objectReferenceValue=dust;
                var animator=unit.GetAnimator();
                var transforms=animator.GetComponentsInChildren<Transform>(true);
                Transform[] feet=transforms.Where(t=>t.name.IndexOf("leg",StringComparison.OrdinalIgnoreCase)>=0 && t.name.EndsWith("_Target",StringComparison.Ordinal)).ToArray();
                if(feet.Length!=2)
                    feet=transforms.Where(t=>t.name.Equals("left_foot",StringComparison.OrdinalIgnoreCase)||t.name.Equals("right_foot",StringComparison.OrdinalIgnoreCase)).ToArray();
                if(feet.Length!=2 && animator.transform.Find("bone_1/bone_4/bone_5/GameObject")!=null)
                    feet=new[]{animator.transform.Find("bone_1/bone_4/bone_5/GameObject"),animator.transform.Find("bone_1/bone_6/bone_7/GameObject")};
                if(feet.Length!=2 && animator.transform.Find("root/right_thigh/right_calves/IK_rightleg")!=null)
                    feet=new[]{animator.transform.Find("root/right_thigh/right_calves/IK_rightleg"),animator.transform.Find("root/left_thigh/left_calves/IK_leftleg")};
                if(feet.Length!=2 && animator.transform.Find("bone_1/bone_3/bone_4/bone_5")!=null)
                    feet=new[]{animator.transform.Find("bone_1/bone_3/bone_4/bone_5"),animator.transform.Find("bone_1/bone_6/bone_7/bone_8")};
                feet=feet.Where(t=>t!=null).ToArray();
                var refs=so.FindProperty("footAnchors"); refs.arraySize=feet.Length;
                for(int i=0;i<feet.Length;i++) refs.GetArrayElementAtIndex(i).objectReferenceValue=feet[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                var accent=unit.GetComponent<CombatImpactPresentation>()??unit.gameObject.AddComponent<CombatImpactPresentation>();
                var accentData=new SerializedObject(accent); accentData.FindProperty("impactMaterial").objectReferenceValue=impact; accentData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
                report.Add(path+" foot anchors: "+string.Join(", ",feet.Select(t=>AnimationUtility.CalculateTransformPath(t,animator.transform))));
                foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                    report.Add("  Clip "+clip.name+" length="+clip.length+" frameRate="+clip.frameRate);
                report.Add("  Foot candidates: "+string.Join(", ",transforms.Where(t=>t.name.Contains("leg")||t.name.Contains("foot")||t.name.Contains("Target")).Select(t=>AnimationUtility.CalculateTransformPath(t,animator.transform))));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets(); File.WriteAllLines(Output+"/installation.txt",report);
        foreach(var material in new[]{shadow,dust,impact,ward})
            if(material.shader==null || !material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Shader unavailable: "+material.name);
        Debug.Log("COMBAT_POLISH_SETUP PASSED");
    }
    private static Material Material(string name,string shaderName)
    {
        var shader=Shader.Find(shaderName); if(shader==null) throw new InvalidOperationException("Missing shader: "+shaderName);
        string path=CombatShaderSetup.MaterialRoot+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
        material.shader=shader; EditorUtility.SetDirty(material); return material;
    }
    public static void InstallBatch()
    {
        try { Install(); EditorApplication.Exit(0); }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
