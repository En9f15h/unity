#if UNITY_EDITOR || CODEX_CHARACTER_GRAPH
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Sprites;

public sealed class CharacterGraphProbe : MonoBehaviour
{
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    Camera cam; SpriteRenderer sprite; string output;
    IEnumerator Start()
    {
        output = Path.GetFullPath("../Results");
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-character-output");
        if (index >= 0) output = args[index + 1];
        Directory.CreateDirectory(output); Application.runInBackground = true;
        Application.logMessageReceived += Log;
        yield return null; yield return null;
        int exitCode = 0;
        try { Run(); Check(errors.Count == 0, "No runtime errors"); }
        catch (Exception e) { checks.Add("FAILED: " + e); exitCode = 1; }
        File.WriteAllLines(output + "/validation.txt", checks);
        File.WriteAllLines(output + "/errors.txt", errors);
        Application.logMessageReceived -= Log; Application.Quit(exitCode);
    }
    void Log(string message, string stack, LogType kind)
    { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack); }
    void Check(bool okay, string message) { if (!okay) throw new Exception(message); checks.Add("PASS: " + message); }
    void Run()
    {
        cam = Camera.main;
        sprite = new GameObject("Validation Sprite").AddComponent<SpriteRenderer>();
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            texture.SetPixel(x, y, (new Vector2(x - 31.5f, y - 31.5f)).magnitude < 26 ? new Color(.32f, .4f, .5f, 1) : Color.clear);
        texture.Apply(); sprite.sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 32);
        cam.orthographicSize = 1.2f;
        var material = Resources.Load<Material>("Combat/Materials/CharacterGraph_knight_0"); sprite.sharedMaterial = material;
        Check(material != null && material.shader.isSupported, "Character graph supported by actual player graphics API");
        var block = new MaterialPropertyBlock(); block.SetFloat("_RimStrength", 0); block.SetFloat("_PulseStrength", 0);
        sprite.SetPropertyBlock(block); var neutral = Capture("neutral.png");
        Check(Lit(neutral) > 10000 && neutral[0].r < 3, "Artwork renders and transparent background stays clear");
        Check(Math.Abs(neutral[128 * 256 + 128].r / 255f - .32f) < .08f, "Neutral graph preserves source brightness");
        block.SetFloat("_RimStrength", .8f); sprite.SetPropertyBlock(block);
        Check(Difference(neutral, Capture("edge.png")) > .01, "Native graph edge light changes rendered pixels");
        block.SetFloat("_ActionPulse", 1); block.SetFloat("_PulseStrength", .65f); sprite.SetPropertyBlock(block);
        Check(Difference(neutral, Capture("pulse.png")) > .1, "Midpoint pulse produces visible edge and body sheen");
        block.Clear(); block.SetFloat("_HitAmount", .8f); block.SetColor("_HitColor", Color.white); sprite.SetPropertyBlock(block);
        Check(Difference(neutral, Capture("hit.png")) > 5, "Hit feedback changes pixels");
        block.Clear(); block.SetFloat("_GuardFlash", 1); sprite.SetPropertyBlock(block);
        Check(Difference(neutral, Capture("guard.png")) > .01, "Successful guard lights the silhouette");
        block.Clear(); sprite.SetPropertyBlock(block); var healthy = Capture(null);
        block.SetFloat("_LowHealthAmount", .4f); sprite.SetPropertyBlock(block); var low = Capture("health-low.png");
        block.SetFloat("_LowHealthAmount", 1); sprite.SetPropertyBlock(block); var critical = Capture("health-critical.png");
        Check(Difference(healthy, low) > .01 && Difference(healthy, critical) > Difference(healthy, low), "Low health gives progressively stronger visible rim");
        Check(healthy[128 * 256 + 128].Equals(critical[128 * 256 + 128]), "Health warning preserves interior artwork colour");
        block.SetFloat("_HitAmount", 1); block.SetColor("_HitColor", Color.white); sprite.SetPropertyBlock(block);
        var healthHit = Capture(null); block.SetFloat("_LowHealthAmount", 0); sprite.SetPropertyBlock(block);
        Check(Difference(healthHit, Capture(null)) < .001, "Confirmed hit flash takes priority over health warning");
        block.Clear(); sprite.SetPropertyBlock(block); var beforeEntry = Capture(null);
        block.SetFloat("_EntranceAmount", 1); block.SetFloat("_EntrancePhase", .5f); sprite.SetPropertyBlock(block);
        var knightEntry = Capture("entrance-knight.png");
        Check(Difference(beforeEntry, knightEntry) > .1 && Lit(beforeEntry) == Lit(knightEntry), "Knight entrance adds light without hiding the sprite");
        block.SetFloat("_SkillPattern", 1); sprite.SetPropertyBlock(block);
        var oracleEntry = Capture("entrance-oracle.png");
        Check(Difference(beforeEntry, oracleEntry) > .01 && Difference(knightEntry, oracleEntry) > .1, "Oracle entrance uses distinct star pattern");
        block.SetFloat("_EntranceAmount", 0); sprite.SetPropertyBlock(block);
        Check(Difference(beforeEntry, Capture(null)) < .001, "Entrance off returns exactly to original presentation");
        block.Clear(); block.SetFloat("_SkillAmount", 1); block.SetFloat("_SkillPhase", .35f); sprite.SetPropertyBlock(block);
        var sweep = Capture("skill-sweep-35.png");
        Check(Difference(neutral, sweep) > .1, "Charge sweep visibly adds light");
        block.SetFloat("_SkillPhase", .65f); sprite.SetPropertyBlock(block);
        Check(Difference(sweep, Capture("skill-sweep-65.png")) > .1, "Animation phase moves the sweep");
        block.SetFloat("_SkillPhase", .5f); block.SetFloat("_SkillPattern", 0); sprite.SetPropertyBlock(block);
        var solid = Capture("skill-sweep-50.png");
        block.SetFloat("_SkillPattern", 1); sprite.SetPropertyBlock(block);
        Check(Difference(solid, Capture("skill-lattice-50.png")) > .1, "Oracle lattice differs from Knight sweep");
        block.SetFloat("_HitAmount", 1); block.SetColor("_HitColor", Color.white); sprite.SetPropertyBlock(block);
        var priority = Capture(null); block.SetFloat("_SkillAmount", 0); sprite.SetPropertyBlock(block);
        Check(Difference(priority, Capture(null)) < .001, "Full hit flash suppresses spell overlay");
        block.Clear(); block.SetFloat("_Dissolve", .5f); sprite.SetPropertyBlock(block);
        int partial = Lit(Capture("phase-half.png"));
        Check(partial > 10 && partial < Lit(neutral) * .95f, "Phase midpoint is partially visible");
        block.SetFloat("_Dissolve", 1); block.SetFloat("_SkillAmount", 1); block.SetFloat("_SkillPhase", .5f); block.SetFloat("_LowHealthAmount", 1); block.SetFloat("_EntranceAmount", 1); block.SetFloat("_EntrancePhase", .5f); sprite.SetPropertyBlock(block);
        Check(Lit(Capture("phase-hidden.png")) == 0, "Phase endpoint is completely invisible");
        sprite.SetPropertyBlock(null); sprite.color = new Color(1, 1, 1, 0);
        Check(Lit(Capture(null)) == 0, "SpriteRenderer alpha remains supported"); sprite.color = Color.white;
        sprite.flipX = true; Check(Math.Abs(Lit(Capture(null)) - Lit(neutral)) < 20, "Horizontal flip preserves sprite coverage"); sprite.flipX = false;
        var ghostParent = new GameObject("Ghost validation");
        CombatGhostSnapshot.Create(sprite, ghostParent.transform, Vector3.zero, new Color(.45f, .85f, 1, .6f), 1, 1);
        var snapshot = ghostParent.GetComponentInChildren<CombatGhostSnapshot>();
        var ghostRenderer = ghostParent.GetComponentInChildren<MeshRenderer>();
        Check(snapshot != null && ghostRenderer.sharedMaterial.shader.name.Contains("CharacterGhost"), "Actual baked pose snapshot uses ghost graph");
        sprite.enabled = false; var ghostFull = Capture("ghost-full.png");
        Check(Lit(ghostFull) > 1000, "Ghost mesh visibly renders in the 2D renderer");
        var ghostFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(CombatGhostSnapshot).GetField("startTime", ghostFlags).SetValue(snapshot, Time.unscaledTime - .5f);
        typeof(CombatGhostSnapshot).GetMethod("LateUpdate", ghostFlags).Invoke(snapshot, null);
        int ghostHalf = Lit(Capture("ghost-half.png"));
        Check(ghostHalf > 10 && ghostHalf < Lit(ghostFull) * .95f, "Snapshot lifetime dissolves real ghost mesh");
        typeof(CombatGhostSnapshot).GetField("startTime", ghostFlags).SetValue(snapshot, Time.unscaledTime - 1f);
        typeof(CombatGhostSnapshot).GetMethod("LateUpdate", ghostFlags).Invoke(snapshot, null);
        Check(Lit(Capture(null)) == 0, "Ghost lifetime endpoint renders fully transparent"); ghostParent.SetActive(false);
        var line = new GameObject("Ribbon validation").AddComponent<LineRenderer>();
        line.sharedMaterial = CombatShaderMaterials.KnightSlash; line.useWorldSpace = true; line.positionCount = 2; line.widthMultiplier = .3f;
        line.SetPosition(0, new Vector3(-.9f, 0, 0)); line.SetPosition(1, new Vector3(.9f, 0, 0));
        line.startColor = line.endColor = new Color(1, .85f, .6f, .65f);
        Check(line.sharedMaterial.shader.name.Contains("WeaponRibbon"), "Blade renderer uses native weapon graph");
        block.Clear(); block.SetFloat("_RibbonPhase", .2f); line.SetPropertyBlock(block); var ribbon = Capture("blade-phase-20.png");
        Check(Lit(ribbon) > 500, "Weapon ribbon visibly renders in the 2D renderer");
        block.SetFloat("_RibbonPhase", .6f); line.SetPropertyBlock(block);
        Check(Difference(ribbon, Capture("blade-phase-60.png")) > .01, "Animation phase advances weapon flow");
        line.startColor = line.endColor = Color.clear;
        Check(Lit(Capture(null)) == 0, "Ribbon respects vertex alpha for trail fading");
        line.gameObject.SetActive(false); sprite.enabled = true;
        foreach (string role in new[] { "knight", "Oracle" }) for (int skin = 0; skin < 2; skin++)
        {
            string name = role + "_" + skin;
            var prefab = Resources.Load<GameObject>("Prefab/" + role + "/" + name);
            var feedback = prefab.GetComponent<CharacterShaderFeedback>();
            Check(feedback.BodyRenderers.All(r => r != null && r.sharedMaterial.shader == material.shader), name + " body renderers use graph");
            var body = feedback.BodyRenderers.First(r => r.sprite != null);
            sprite.sprite = body.sprite; sprite.sharedMaterial = body.sharedMaterial;
            sprite.transform.localScale = Vector3.one * (1.8f / Mathf.Max(body.sprite.bounds.size.x, body.sprite.bounds.size.y));
            sprite.transform.position = -Vector3.Scale(body.sprite.bounds.center, sprite.transform.localScale);
            block.Clear(); block.SetVector("_SpriteUVRect", DataUtility.GetOuterUV(sprite.sprite)); sprite.SetPropertyBlock(block);
            Check(Lit(Capture(name + "-rest.png")) > 100, name + " real sprite renders");
            block.SetFloat("_ActionPulse", 1); sprite.SetPropertyBlock(block); Capture(name + "-pulse.png");
            block.SetFloat("_SkillAmount", 1); block.SetFloat("_SkillPhase", .5f); sprite.SetPropertyBlock(block); Capture(name + "-skill.png");
            block.SetFloat("_ActionPulse", 0); block.SetFloat("_SkillAmount", 0); block.SetFloat("_LowHealthAmount", 1); sprite.SetPropertyBlock(block); Capture(name + "-critical.png");
            block.SetFloat("_LowHealthAmount", 0); block.SetFloat("_EntranceAmount", 1); block.SetFloat("_EntrancePhase", .5f); sprite.SetPropertyBlock(block); Capture(name + "-entrance.png");
        }
        // Use the production Knight controller: the feedback must follow normalized animation time.
        var source = Resources.Load<GameObject>("Prefab/knight/knight_0").GetComponent<CharacterUnit>().GetAnimator();
        var animator = new GameObject("Animation clock validation").AddComponent<Animator>();
        animator.runtimeAnimatorController = source.runtimeAnimatorController; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var driver = animator.gameObject.AddComponent<CharacterShaderFeedback>();
        typeof(CharacterShaderFeedback).GetField("actionAnimator", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(driver, animator);
        var late = typeof(CharacterShaderFeedback).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (float phase in new[] { 0f, .5f, .99f })
        {
            animator.Play("LightAttack", 0, phase); animator.Update(0); late.Invoke(driver, null);
            Check(phase == .5f ? driver.ActionPulse > .99f : driver.ActionPulse < .01f, "Actual animation phase " + phase + " gives expected pulse");
        }
        animator.Play("hit", 0, .5f); animator.Update(0); late.Invoke(driver, null);
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("hit"), "Knight hit validation enters its actual lowercase state");
        Check(driver.ActionPulse == 0 && animator.speed == 1, "Hit is excluded and animation speed is unchanged");
        foreach (string role in new[] { "knight", "Oracle" })
        {
            var unit = Resources.Load<GameObject>("Prefab/" + role + "/" + role + "_0").GetComponent<CharacterUnit>();
            animator.runtimeAnimatorController = unit.GetAnimator().runtimeAnimatorController;
            string skill = role == "knight" ? "HeavyCharge" : "Bolt";
            foreach (float phase in new[] { 0f, .5f, .99f })
            {
                animator.Play(skill, 0, phase); animator.Update(0); late.Invoke(driver, null);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(skill), role + " entered " + skill + " at " + phase);
                Check(phase == .5f ? driver.SkillAmount > .99f : driver.SkillAmount < .01f, role + " skill envelope at " + phase);
            }
            animator.Play(skill, 0, .5f); animator.Update(0); late.Invoke(driver, null);
            float previous = driver.SkillPhase; animator.speed = 0; animator.Update(.2f); late.Invoke(driver, null);
            Check(Mathf.Approximately(previous, driver.SkillPhase), role + " skill clock freezes with animator"); animator.speed = 1;
            string hitState = role == "knight" ? "hit" : "Hit";
            animator.Play(hitState, 0, .5f); animator.Update(0); late.Invoke(driver, null);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName(hitState), role + " entered actual hit state");
            Check(driver.SkillAmount == 0, role + " hit clears skill effect");
        }
        driver.ResetPresentation(); Check(driver.SkillAmount == 0 && driver.SkillPhase == 0, "Reset clears skill state");
        var healthUnit = animator.gameObject.AddComponent<CharacterUnit>(); healthUnit.enabled = false;
        typeof(CharacterShaderFeedback).GetField("characterUnit", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(driver, healthUnit);
        typeof(CharacterShaderFeedback).GetField("bodyRenderers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(driver, new[] { sprite });
        block.Clear(); block.SetFloat("_ProbePreserved", 17); sprite.SetPropertyBlock(block);
        foreach (int maxHP in new[] { 30, 25 })
        {
            healthUnit.maxHP = healthUnit.currentHP = maxHP; late.Invoke(driver, null);
            Check(driver.LowHealthSeverity == 0, maxHP + " HP character starts without warning");
            if (maxHP == 30) { healthUnit.currentHP = 9; late.Invoke(driver, null); Check(driver.LowHealthSeverity == 0, "Exactly 30 percent is not low health"); healthUnit.currentHP = maxHP; }
            healthUnit.TakeDamage(maxHP == 30 ? 22 : 18); late.Invoke(driver, null);
            float lowSeverity = driver.LowHealthSeverity;
            Check(lowSeverity > 0, maxHP + " HP damage crosses warning threshold");
            sprite.GetPropertyBlock(block); Check(block.GetFloat("_LowHealthAmount") > 0 && block.GetFloat("_ProbePreserved") == 17, "HP warning reaches renderer and preserves unrelated properties");
            healthUnit.currentHP = 1; late.Invoke(driver, null); Check(driver.LowHealthSeverity > lowSeverity, "Critical HP increases warning severity");
            healthUnit.Heal(maxHP); late.Invoke(driver, null); Check(driver.LowHealthSeverity == 0, "Healing removes warning");
            healthUnit.TakeDamage(maxHP); late.Invoke(driver, null); Check(driver.LowHealthSeverity == 0, "Defeated character does not keep warning pulse");
        }
        healthUnit.maxHP = 0; healthUnit.currentHP = 1; late.Invoke(driver, null);
        Check(driver.LowHealthSeverity == 0 && healthUnit.currentHP == 1, "Uninitialized HP is ignored without changing gameplay HP");
        driver.ResetPresentation(); sprite.GetPropertyBlock(block);
        Check(driver.LowHealthSeverity == 0 && block.GetFloat("_LowHealthAmount") == 0, "Presentation reset clears health shader value");
        var sync = new GameObject("Entrance sync validation").AddComponent<GameSceneStartSync>(); sync.enabled = false;
        var entrantA = new GameObject("First entrant").AddComponent<CharacterShaderFeedback>();
        var entrantB = new GameObject("Second entrant").AddComponent<CharacterShaderFeedback>();
        sync.RegisterMyCharacter(entrantA.gameObject); sync.RegisterEnemyCharacter(entrantB.gameObject);
        Check(!entrantA.gameObject.activeSelf && !entrantB.gameObject.activeSelf, "Registration still hides characters before synchronized start");
        var fields = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(GameSceneStartSync).GetField("beatStartServerTimestamp", fields).SetValue(sync, unchecked(Photon.Pun.PhotonNetwork.ServerTimestamp - 250));
        typeof(GameSceneStartSync).GetField("gameStartedOnBeat", fields).SetValue(sync, true);
        typeof(GameSceneStartSync).GetMethod("ShowAfterStart", fields).Invoke(sync, null);
        late.Invoke(entrantA, null); late.Invoke(entrantB, null);
        Check(entrantA.gameObject.activeSelf && entrantB.gameObject.activeSelf, "Synchronized start shows both characters");
        Check(entrantA.EntranceAmount > .9f && entrantB.EntranceAmount > .9f && Mathf.Abs(entrantA.EntrancePhase - entrantB.EntrancePhase) < .1f, "Both entrances catch up to the same beat anchor");
        float firstPhase = entrantA.EntrancePhase; sync.RegisterMyCharacter(entrantA.gameObject); late.Invoke(entrantA, null);
        Check(Mathf.Approximately(firstPhase, entrantA.EntrancePhase), "Duplicate character registration does not restart entrance");
        entrantA.FlashHit(); late.Invoke(entrantA, null); Check(entrantA.EntranceAmount == 0, "Confirmed hit cancels entrance immediately");
        entrantB.enabled = false; Check(entrantB.EntranceAmount == 0, "Disabling feedback clears entrance");
        var lateEntrant = new GameObject("Late entrant").AddComponent<CharacterShaderFeedback>();
        typeof(GameSceneStartSync).GetField("beatStartServerTimestamp", fields).SetValue(sync, unchecked(Photon.Pun.PhotonNetwork.ServerTimestamp - 1000));
        sync.RegisterMyCharacter(lateEntrant.gameObject); late.Invoke(lateEntrant, null);
        Check(lateEntrant.gameObject.activeSelf && lateEntrant.EntranceAmount == 0, "Late spawn stays visible and skips expired entrance");
        var actionEntrant = new GameObject("Action entrant").AddComponent<CharacterShaderFeedback>();
        typeof(CharacterShaderFeedback).GetField("actionAnimator", fields).SetValue(actionEntrant, animator);
        animator.runtimeAnimatorController = source.runtimeAnimatorController;
        animator.Play("LightAttack", 0, .5f); animator.Update(0); actionEntrant.BeginEntrance(.5f, .25f); late.Invoke(actionEntrant, null);
        Check(actionEntrant.ActionPulse > .99f && actionEntrant.EntranceAmount == 0, "First action cancels entrance while preserving midpoint pulse");
        var finished = new GameObject("Completed entrance").AddComponent<CharacterShaderFeedback>();
        finished.BeginEntrance(.5f, .25f); late.Invoke(finished, null); Check(finished.EntranceAmount > .99f, "Entrance peak occurs halfway through its beat");
        typeof(CharacterShaderFeedback).GetField("entranceStart", fields).SetValue(finished, Time.unscaledTime - .6f); late.Invoke(finished, null);
        Check(finished.EntranceAmount == 0 && finished.Visibility == 1, "Entrance completes without changing visibility");
        var swordBase = new GameObject("Sword base").transform; swordBase.position = new Vector3(-.5f, -.25f, 0);
        var swordTip = new GameObject("Sword tip").transform; swordTip.position = new Vector3(.5f, .5f, 0);
        var blade = animator.gameObject.AddComponent<KnightSlashShaderVFX>();
        typeof(KnightSlashShaderVFX).GetField("swordBase", fields).SetValue(blade, swordBase);
        typeof(KnightSlashShaderVFX).GetField("swordTip", fields).SetValue(blade, swordTip);
        var bladeUpdate = typeof(KnightSlashShaderVFX).GetMethod("LateUpdate", fields);
        animator.Play("HeavyCharge", 0, .5f); animator.Update(0); bladeUpdate.Invoke(blade, null);
        Check(blade.ChargeRenderer != null && blade.ChargeRenderer.enabled, "Actual HeavyCharge enables blade glow at midpoint");
        Check(blade.ChargeRenderer.GetPosition(0) == swordBase.position && blade.ChargeRenderer.GetPosition(1) == swordTip.position, "Blade glow follows authored sword anchors");
        blade.ChargeRenderer.GetPropertyBlock(block); Check(Mathf.Abs(block.GetFloat("_RibbonPhase") - .5f) < .001f, "Blade graph receives actual animation phase");
        var sharedBlade = blade.ChargeRenderer.sharedMaterial;
        animator.Play("hit", 0, .5f); animator.Update(0); bladeUpdate.Invoke(blade, null);
        Check(!blade.ChargeRenderer.enabled, "Hit ends blade charge glow");
        animator.Play("HeavyCharge", 0, .5f); animator.Update(0); bladeUpdate.Invoke(blade, null);
        Check(blade.GetComponentsInChildren<LineRenderer>(true).Length == 1 && blade.ChargeRenderer.sharedMaterial == sharedBlade, "Repeated charge reuses one renderer and material");
        var track = typeof(KnightSlashShaderVFX).GetMethod("TrackSword", fields);
        animator.Play("LightAttack", 0, .5f); animator.Update(0);
        var slash = (System.Collections.IEnumerator)track.Invoke(blade, new object[] { .05f, "LightAttack" });
        slash.MoveNext();
        Check(slash.MoveNext() && blade.IsEmitting, "Actual attack midpoint emits sword trail");
        animator.speed = 0;
        Check(slash.MoveNext() && !blade.IsEmitting && Mathf.Abs(blade.VisualPhase - .5f) < .001f, "Paused Animator preserves slash phase and stops new trail emission");
        animator.speed = 1;
        Check(slash.MoveNext() && blade.IsEmitting, "Resumed Animator restores midpoint trail emission");
        animator.CrossFade("hit", .2f, 0, 0); animator.Update(.01f);
        Check(animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("hit"), "Interruption test enters real attack-to-hit crossfade");
        Check(!slash.MoveNext() && !blade.IsEmitting, "Hit transition immediately ends attack trail before blend finishes");
        animator.Play("LightAttack", 0, .5f); animator.Update(0);
        slash = (System.Collections.IEnumerator)track.Invoke(blade, new object[] { .05f, "LightAttack" }); slash.MoveNext(); slash.MoveNext();
        animator.Play("LightAttack", 0, .8f); animator.Update(0);
        Check(!slash.MoveNext() && !blade.IsEmitting, "Passing authored slash window finishes trail without wall-clock timeout");
        blade.enabled = false; Check(!blade.ChargeRenderer.enabled, "Disabling character clears blade glow");
        var stopObject = new GameObject("Hit clock regression");
        var stop = stopObject.AddComponent<HitStopManager>();
        float previousScale = Time.timeScale, previousFixed = Time.fixedDeltaTime;
        try
        {
            Time.timeScale = .65f;
            for (int i = 0; i < 3; i++) Check(!stop.HitStopByBeat().MoveNext(), "Hit presentation adds no coroutine delay " + i);
            Check(Time.timeScale == .65f && Time.fixedDeltaTime == previousFixed, "Repeated hits preserve global and physics clocks");
        }
        finally { Time.timeScale = previousScale; Time.fixedDeltaTime = previousFixed; DestroyImmediate(stopObject); }
        var manager = new GameObject("Parry dispatch regression").AddComponent<TurnPlanningManager>(); manager.enabled = false;
        var opponent = new GameObject("Parry opponent").AddComponent<Animator>();
        opponent.runtimeAnimatorController = source.runtimeAnimatorController; opponent.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var mine = animator.GetComponent<CharacterUnit>(); mine.className = "knight";
        var enemy = opponent.gameObject.AddComponent<CharacterUnit>(); enemy.className = "knight";
        typeof(TurnPlanningManager).GetField("myUnit", fields).SetValue(manager, mine);
        typeof(TurnPlanningManager).GetField("enemyUnit", fields).SetValue(manager, enemy);
        var parry = typeof(TurnPlanningManager).GetMethod("PlayParrySuccessEffect", fields);
        for (int skin = 0; skin < 2; skin++)
        {
            animator.runtimeAnimatorController = Resources.Load<GameObject>("Prefab/knight/knight_" + skin).GetComponent<CharacterUnit>().GetAnimator().runtimeAnimatorController;
            animator.Play("HeavyCharge", 0, .5f); animator.Update(0);
            opponent.Play("Parry", 0, .5f); opponent.Update(0);
            parry.Invoke(manager, new object[] { false });
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("getParry"), "Enemy successful parry forcibly interrupts knight skin " + skin);
            Check(opponent.GetCurrentAnimatorStateInfo(0).IsName("Parry"), "Defender is not assigned attacker reaction " + skin);
            var clip = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
            Check(Mathf.Abs(clip.length / animator.speed - .5f) < .001f, "GetParry fits one beat at 120 BPM " + skin);
        }
        parry.Invoke(manager, new object[] { true });
        Check(opponent.GetCurrentAnimatorStateInfo(0).IsName("getParry"), "Local successful parry interrupts remote knight");
        enemy.className = "Oracle"; opponent.Play("idle", 0, 0); opponent.Update(0);
        parry.Invoke(manager, new object[] { true });
        Check(opponent.GetCurrentAnimatorStateInfo(0).IsName("idle"), "Knight getParry is not applied to Oracle");
        Check(BattleStepPlayer.TryPlayForcedReaction(animator, "MissingReaction", .5f, "hit") && animator.GetCurrentAnimatorStateInfo(0).IsName("hit"), "Missing future reaction safely falls back to hit");
        var counter = typeof(TurnPlanningManager).GetMethod("PlayParryCounterAnimationAndWait", fields);
        var presentCounter = (IEnumerator)counter.Invoke(manager, new object[] { animator });
        Check(presentCounter.MoveNext() && presentCounter.Current is WaitForSecondsRealtime, "Counter starts immediately without transition-entry delay");
        float counterWait = ((WaitForSecondsRealtime)presentCounter.Current).waitTime;
        var missingCounter = (IEnumerator)counter.Invoke(manager, new object[] { null });
        Check(missingCounter.MoveNext() && missingCounter.Current is WaitForSecondsRealtime && Mathf.Approximately(((WaitForSecondsRealtime)missingCounter.Current).waitTime, counterWait), "Missing Animator does not shorten the gameplay counter step");
        presentCounter.MoveNext(); missingCounter.MoveNext();
        manager.StopAllCoroutines(); DestroyImmediate(manager.gameObject); DestroyImmediate(opponent.gameObject);
        Check(material.GetFloat("_ActionPulse") == 0, "Per-character values do not modify shared material");
        File.WriteAllText(output + "/graphics.txt", SystemInfo.graphicsDeviceVersion);
    }
    static int Lit(Color32[] data) => data.Count(c => c.r > 6 || c.g > 6 || c.b > 6);
    static double Difference(Color32[] a, Color32[] b)
    { double sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b); return sum / (a.Length * 3); }
    Color32[] Capture(string name)
    {
        var rt = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active; var tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
        try
        {
            cam.aspect = 1;
            RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); tex.Apply();
            if (name != null) File.WriteAllBytes(output + "/" + name, tex.EncodeToPNG());
            return tex.GetPixels32();
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Destroy(tex); }
    }
}
#endif
