#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class RangePresentationProbe
{
    public static IEnumerator Run(int map, CharacterUnit unit, Action<string> capture, Action<bool,string> check)
    {
        var manager = UnityEngine.Object.FindFirstObjectByType<AttackRangePreviewManager>();
        check(manager != null,"Range manager available on map " + map);
        bool mine = manager.ShowMyRange, enemy = manager.ShowEnemyRange;
        var pulseField = typeof(AttackRangePreviewManager).GetField("pulseBorder",BindingFlags.Instance|BindingFlags.NonPublic);
        bool pulse = (bool)pulseField.GetValue(manager);
        manager.ClearAll(); manager.SetVisibleEnabled(true);
        yield return null;
        var reference = unit.GetComponentInChildren<CharacterShaderFeedback>().BodyRenderers.First(r=>r!=null);
        Vector3 origin = unit.transform.position, target = origin + Vector3.right*5;
        var material = Resources.Load<Material>("Combat/Materials/RangeTelegraph");
        check(material != null && material.shader.isSupported,"Range shader supported in actual scene " + map);
        try
        {
            manager.ShowHoverRange(origin,target,1,3,reference,AttackRangeDisplayType.My);
            yield return null;
            var fills = Renderers(manager).Where(r=>r.name=="Fill").OrderBy(r=>r.transform.position.x).ToArray();
            check(fills.Length==3,"Hover preserves inclusive min/max tile count " + map);
            float width = (float)typeof(AttackRangePreviewManager).GetField("tileWidth",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager);
            check(fills.Select((r,i)=>Mathf.Abs(r.transform.position.x-origin.x-(i+1)*width)<.001f).All(x=>x),"Hover retains exact logical tile centers " + map);
            check(Renderers(manager).All(r=>r.sharedMaterial==material),"All cells reuse one shared material " + map);
            check(Renderers(manager).All(r=>Property(r,"_Progress")==-1),"Hover has no attack countdown " + map);
            check(Renderers(manager).All(r=>Property(r,"_EnemyStyle")==0),"Ally uses solid pattern " + map);
            if(map<3) capture("Range-"+map+"-ally-hover.png");
            manager.ShowHoverRange(origin,target,1,3,reference,AttackRangeDisplayType.Enemy);
            yield return null;
            check(Renderers(manager).All(r=>Property(r,"_EnemyStyle")==1),"Enemy uses hatch pattern " + map);
            if(map<3) capture("Range-"+map+"-enemy-hover.png");
            manager.ClearAll(); yield return null;
            manager.ShowRange(origin,target,1,3,.5f,.12f,reference,AttackRangeDisplayType.My);
            manager.ShowRange(origin,target,2,4,.5f,.12f,reference,AttackRangeDisplayType.Enemy);
            yield return null;
            check(Renderers(manager).Length==12,"Two overlapping sides retain six cells and twelve renderers " + map);
            check(Renderers(manager).Where(r=>r.name=="GlowBorder").All(r=>Property(r,"_Progress")>=0),"Timed warning drives convergence " + map);
            if(map<3) capture("Range-"+map+"-overlap.png");
            float end=Time.realtimeSinceStartup+.8f;
            while(Time.realtimeSinceStartup<end) yield return null;
            yield return null;
            check(Renderers(manager).Length==0,"Timed cells fade and release after existing deadline " + map);
            pulseField.SetValue(manager,false);
            manager.ShowRange(origin,target,1,1,1,.12f,reference,AttackRangeDisplayType.Enemy);
            yield return null;
            check(Renderers(manager).Where(r=>r.name=="GlowBorder").All(r=>Property(r,"_Progress")>=0),"Countdown works when scale pulse is disabled " + map);
            manager.SetEnemyRangeEnabled(false); yield return null;
            check(Renderers(manager).Length==0,"Enemy visibility toggle removes enemy warning " + map);
            manager.ShowRange(origin,target,1,2,1,.12f,reference,AttackRangeDisplayType.Enemy);
            check(Renderers(manager).Length==0,"Hidden enemy ranges remain hidden " + map);
            manager.ShowRange(origin,target,1,2,1,.12f,reference,AttackRangeDisplayType.My);
            yield return null;
            check(Renderers(manager).Length==4,"Ally toggle remains independent " + map);
            manager.ClearAll(); yield return null;
            manager.ShowRange(origin,target,3,1,1,.12f,reference,AttackRangeDisplayType.My);
            check(Renderers(manager).Length==0,"Invalid range creates no cells " + map);
        }
        finally
        {
            manager.ClearAll(); pulseField.SetValue(manager,pulse);
            manager.SetMyRangeEnabled(mine); manager.SetEnemyRangeEnabled(enemy);
        }
    }
    private static SpriteRenderer[] Renderers(AttackRangePreviewManager m) => m.GetComponentsInChildren<SpriteRenderer>();
    private static float Property(SpriteRenderer r,string name)
    {var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);return block.GetFloat(name);}
}
#endif
