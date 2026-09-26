# Contact, ward and impact polish

Installed on Knight 0/1 and Oracle 0/1. Original animation clips, controllers, scenes and map arrays are unchanged.

## Animation timing contract

The user confirmed that all character clips except Hit are authored at 60 FPS and 1 second. Hit is 0.5 seconds. Unity readback verified every clip on all four skins. Frame 30 / normalized time 0.5 is the strong visual beat.

`KnightSlashShaderVFX` reads the actual Animator state, including transitions and current playback speed. The trail emits during normalized time 0.3–0.7 and reaches maximum width at 0.5. Hit stop prevents new trail samples. The effect never writes Animator speed, clip keys or combat timing.

The existing GameScene configuration is 120 BPM with 2 BPM beats per normal action, giving a 1-second action step. Existing movement has its own 0.5-second travel segment; that playback control remains intact. Damage resolution is still performed by the original combat resolver. The sword trail marks the authored action midpoint; confirmed hit effects are emitted only by actual hit/parry outcomes, not by a midpoint timer.

## Ground contact

`CharacterGroundPresentation` uses the body collider and two authored foot references. A downward physics query ignores triggers and character colliders. With no valid floor, the shadow is hidden. Height reduces shadow width and opacity; phase visibility also affects the shadow.

Grounded landings emit seven small particles. Movement cues can emit four particles at each of the existing travel segment's midpoint and end, provided enough actual distance was traversed. Cumulative distance makes this independent of frame rate. The system creates one shared-material shadow renderer and one particle system per character, with a 32-particle cap. It reuses them across actions and clears particles on disable.

Materials: `ContactShadow`, `ContactDust`. Shader: `Combat/Ground Contact`. The character component exposes shadow opacity (default 0.34); dust uses ordinary alpha blending and subdued earth colors.

## Ward readability

The Ward branch of `Combat/Energy Sprite` now reduces center opacity and concentrates additional light on the rim. `OracleWard` has center opacity 0.18 and rim strength 0.35. Other energy modes are unaffected.

`OracleVFXController.PlayWardSuccess(duration, attackerPosition)` uses the attacker's side for the impact ripple and pulses the active ward as well as the success effect. The existing overload remains available. `CombatEffectShaderDriver` maintains per-renderer UV mapping, mirrored sprites, and reset behavior when reused.

## Confirmed impact accents

`CombatImpactPresentation` retains the presented action and charge/release context supplied by `BattleStepPlayer`. `TurnPlanningManager` invokes it at existing hit and successful parry feedback sites. A start/charge/miss does not independently trigger a confirmed-hit effect.

Light hits use a small 0.095-second accent, released heavy/Rift/ultimate hits a larger 0.18-second accent, and successful parries a distinct 0.14-second star/ring. Hit flash and screen shake use correspondingly restrained strengths. Existing hit stop durations, damage rules and target shake remain unchanged. Each character owns one reusable accent renderer, updated with a MaterialPropertyBlock. No per-hit object instantiation is needed for these new accents.

## Setup and verification

- Install: `Tools > Combat Shaders > Install Contact and Impact Polish` (`CombatPolishSetup.Install`).
- Controlled render validation: `CombatPolishValidation.RunBatch` — 147 checks.
- Actual selection-to-battle routes: `CombatPolishValidation.RunIntegration` — 172 checks across seven maps, including three new-map visual showcases.
- Original combat shader regression: `CombatShaderValidation.RunBatch -combat-shader-output CodexLogs/CombatPolish-20260915/CombatRegression` — 149 checks.
- Evidence and source SHA audit: `CodexLogs/CombatPolish-20260915`.

Integration uses Photon OfflineMode with simulated peer readiness; showcase opponents are visual fixtures. This is not a two-machine network match. This batch did not repeat GPU performance measurement.
