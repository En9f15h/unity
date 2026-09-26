#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CombatShaderGraphIntegrationProbe
{
    const string Path = "Prefab/Oracle/vfx/";
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static readonly string[] Flow = { "Oracle_RiftWarningVFX", "Oracle_ShiftVFX", "OracleShiftEnterFX", "Oracle_SightVFX" };
    static readonly string[] Dissolve = { "Oracle_FadeVFX", "OracleShiftExitFX" };
    public static IEnumerator Run(int map, CharacterUnit unit, Action<string> capture, Action<bool, string> check)
    {
        foreach (string name in Flow.Concat(Dissolve))
        {
            bool dissolve = Dissolve.Contains(name);
            string shader = dissolve ? CombatEffectShaderDriver.DissolveGraphShader : CombatEffectShaderDriver.FlowGraphShader;
            var prefab = Resources.Load<GameObject>(Path + name);
            check(prefab != null && prefab.GetComponentsInChildren<SpriteRenderer>(true).All(r => r.sharedMaterial.shader.name == shader && r.sharedMaterial.shader.isSupported), "Production Graph binding: " + name);
            var instance = Object.Instantiate(prefab);
            try
            {
                var driver = instance.GetComponent<CombatEffectShaderDriver>(); driver.Play(2);
                check(Mathf.Approximately(Field<float>(driver, "duration"), 2), "Effect uses caller duration: " + name);
                if (!dissolve) continue;
                var renderer = instance.GetComponentsInChildren<SpriteRenderer>(true).First();
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                check(Mathf.Approximately(block.GetFloat("_EffectProgress"), 0), "Dissolve starts visible: " + name);
                block.SetFloat("_Opacity", .47f); renderer.SetPropertyBlock(block);
                driver.ApplyProperties(.25f); renderer.GetPropertyBlock(block);
                check(Mathf.Approximately(block.GetFloat("_EffectProgress"), name == "Oracle_FadeVFX" ? .5f : .25f), "Dissolve follows authored phase fraction: " + name);
                check(Mathf.Approximately(block.GetFloat("_Opacity"), .47f), "Dissolve preserves other renderer overrides: " + name);
                driver.ApplyProperties(name == "Oracle_FadeVFX" ? .5f : 1); renderer.GetPropertyBlock(block);
                check(Mathf.Approximately(block.GetFloat("_EffectProgress"), 1), "Dissolve completes at its scheduled phase: " + name);
                driver.Play(.8f); renderer.GetPropertyBlock(block);
                check(Mathf.Approximately(block.GetFloat("_EffectProgress"), 0), "Replay resets dissolution: " + name);
                check(Mathf.Approximately(renderer.sharedMaterial.GetFloat("_EffectProgress"), 0), "Shared Graph material stays unchanged: " + name);
            }
            finally { Object.Destroy(instance); }
        }
        foreach (var name in new[] { "Oracle_WardVFX", "Oracle_WardSuccessVFX" })
            check(Resources.Load<GameObject>(Path + name).GetComponentsInChildren<SpriteRenderer>(true).All(r => r.sharedMaterial.shader.name == "Combat/Energy Sprite"), "Ward impact shader preserved: " + name);
        check(unit.GetComponent<CharacterShaderFeedback>().BodyRenderers.All(r => r.sharedMaterial.shader.name == "Combat/Character Lit"), "Character hit feedback preserved on selected skin");
        yield return null;

        var oracle = unit.GetComponentInChildren<OracleVFXController>();
        if (oracle == null) yield break;
        oracle.PlayRiftWarning(unit, 1);
        yield return null;
        var warning = Field<GameObject>(oracle, "activeRiftWarning");
        check(warning != null && Uses(warning, CombatEffectShaderDriver.FlowGraphShader), "Actual Rift API spawns EnergyFlow on map " + map);
        capture("Graph-Rift-" + map + ".png"); oracle.StopRiftWarning(false);

        oracle.PlaySightActivation(1);
        yield return null;
        check(Find("Oracle_SightVFX") != null && Uses(Find("Oracle_SightVFX"), CombatEffectShaderDriver.FlowGraphShader), "Actual Sight API spawns EnergyFlow");
        yield return new WaitForSecondsRealtime(1.2f);

        oracle.PlayFade(1);
        yield return new WaitForSecondsRealtime(.12f);
        var fade = Find("Oracle_FadeVFX");
        check(fade != null && Uses(fade, CombatEffectShaderDriver.DissolveGraphShader), "Actual Fade API spawns EdgeDissolve");
        var fadeDriver = fade.GetComponent<CombatEffectShaderDriver>();
        check(Mathf.Approximately(Field<float>(fadeDriver, "duration"), 1), "Fade receives actual action duration");
        capture("Graph-Fade-" + map + ".png");
        yield return new WaitForSecondsRealtime(.43f);
        var progress = new MaterialPropertyBlock(); fade.GetComponentInChildren<SpriteRenderer>().GetPropertyBlock(progress);
        check(Mathf.Approximately(progress.GetFloat("_EffectProgress"), 1), "Fade is fully dissolved by the 50 percent return beat");
        yield return new WaitForSecondsRealtime(.65f);

        oracle.PlayShiftSwap(1);
        yield return null;
        var exit = Find("OracleShiftExitFX");
        check(exit != null && Uses(exit, CombatEffectShaderDriver.DissolveGraphShader), "Actual Shift departure uses EdgeDissolve");
        check(Mathf.Approximately(Field<float>(exit.GetComponent<CombatEffectShaderDriver>(), "duration"), .5f), "Shift departure respects existing half-action duration");
        yield return new WaitForSecondsRealtime(.65f);
        var enter = Find("OracleShiftEnterFX");
        check(enter != null && Uses(enter, CombatEffectShaderDriver.FlowGraphShader), "Actual Shift arrival uses EnergyFlow");
        capture("Graph-Shift-" + map + ".png");
        yield return new WaitForSecondsRealtime(.6f);
        oracle.CancelShift();
    }
    // Runtime APIs also attach legacy glow/ring sprites. Those deliberately keep their
    // additive materials; prefab checks above verify every authored Graph binding.
    static bool Uses(GameObject obj, string shader) => obj.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == shader);
    static GameObject Find(string name) => GameObject.Find(name + "(Clone)");
}
#endif
