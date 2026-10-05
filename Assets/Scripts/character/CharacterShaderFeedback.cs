using System.Collections;
using UnityEngine;
using UnityEngine.Sprites;

[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public sealed class CharacterShaderFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] bodyRenderers;
    [SerializeField] private Material characterMaterial;
    [SerializeField, Range(0.05f, 0.4f)] private float hitDuration = 0.13f;
    [SerializeField, ColorUsage(false, true)] private Color hitColor = new Color(1.5f, 1.1f, 0.8f);
    private static readonly int HitId = Shader.PropertyToID("_HitAmount"), HitColorId = Shader.PropertyToID("_HitColor"),
        PhaseId = Shader.PropertyToID("_Dissolve"), SpaceId = Shader.PropertyToID("_CharacterWorldToLocal"),
        HeightId = Shader.PropertyToID("_CharacterHeight"), UVId = Shader.PropertyToID("_SpriteUVRect"),
        RimId = Shader.PropertyToID("_RimStrength"), RimColorId = Shader.PropertyToID("_RimColor");
    private MaterialPropertyBlock block;
    private Coroutine hitRoutine;
    private float hitAmount, characterHeight = 1f, visibility = 1f;
    private Color activeHitColor;
    private Animator actionAnimator;
    private CharacterUnit characterUnit;
    private float guardUntil;
    private bool entrancePlayed, entranceActive;
    private float entranceStart, entranceDuration;
    private static readonly int EntranceId = Shader.PropertyToID("_EntranceAmount"), EntrancePhaseId = Shader.PropertyToID("_EntrancePhase");
    private static readonly int PulseId = Shader.PropertyToID("_ActionPulse"), GuardId = Shader.PropertyToID("_GuardFlash");
    private static readonly int SkillId = Shader.PropertyToID("_SkillAmount"), SkillPhaseId = Shader.PropertyToID("_SkillPhase");
    private static readonly int LowHealthId = Shader.PropertyToID("_LowHealthAmount");
    private static readonly string[] ActionStates = {
        "LightAttack", "HeavyCharge", "HeavyAttack", "LowAttack", "Parry", "ParryCounter", "Defense",
        "MoveForward", "MoveBackward", "Jump", "Dance", "Ultimate", "Bolt", "RiftCharge", "Rift",
        "RiftRelease", "Shift", "Ward", "Fade", "Sight"
    };
    public float ActionPulse { get; private set; }
    public float SkillAmount { get; private set; }
    public float SkillPhase { get; private set; }
    public float LowHealthSeverity { get; private set; }
    public float EntranceAmount { get; private set; }
    public float EntrancePhase { get; private set; }
    public float Visibility => visibility;
    public SpriteRenderer[] BodyRenderers => bodyRenderers;
    private void Awake()
    {
        characterUnit = GetComponent<CharacterUnit>();
        actionAnimator = characterUnit != null ? characterUnit.GetAnimator() : null;
        InitializeRenderers();
    }
    private void OnEnable() => ResetPresentation();
    private void OnDisable()
    {
        if (hitRoutine != null) StopCoroutine(hitRoutine);
        hitRoutine = null;
        ResetPresentation();
    }
    public void InitializeRenderers()
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (bodyRenderers == null) return;
        bool hasBounds = false;
        Bounds combined = default;
        foreach (SpriteRenderer renderer in bodyRenderers)
        {
            if (renderer == null) continue;
            if (characterMaterial != null) renderer.sharedMaterial = characterMaterial;
            if (!hasBounds) { combined = renderer.bounds; hasBounds = true; }
            else combined.Encapsulate(renderer.bounds);
        }
        if (hasBounds) characterHeight = Mathf.Max(0.1f, combined.size.y / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y)));
        ApplyProperties();
    }
    public void FlashHit()
    {
        FlashHit(hitDuration, 0.9f, hitColor);
    }
    public void FlashHit(float seconds, float strength, Color color)
    {
        if (!isActiveAndEnabled) return;
        CancelEntrance();
        if (hitRoutine != null) StopCoroutine(hitRoutine);
        activeHitColor = color;
        hitRoutine = StartCoroutine(HitFlash(Mathf.Max(0.01f, seconds), Mathf.Clamp01(strength)));
    }
    private IEnumerator HitFlash(float seconds, float strength)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            hitAmount = strength * (1f - Mathf.Clamp01(elapsed / seconds));
            ApplyProperties();
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        hitAmount = 0f; hitRoutine = null;
        ApplyProperties();
    }
    public void SetPhaseVisibility(float value) { visibility = Mathf.Clamp01(value); ApplyProperties(); }
    public void FlashGuard() { if (isActiveAndEnabled) guardUntil = Time.unscaledTime + .18f; }
    public void BeginEntrance(float beatSeconds, float elapsedSeconds)
    {
        if (!isActiveAndEnabled || entrancePlayed) return;
        entrancePlayed = true;
        entranceDuration = Mathf.Max(.01f, beatSeconds);
        if (elapsedSeconds >= entranceDuration) return;
        entranceStart = Time.unscaledTime - Mathf.Max(0f, elapsedSeconds);
        entranceActive = true;
    }
    private void CancelEntrance() { entranceActive = false; EntranceAmount = EntrancePhase = 0f; }
    public void ResetPresentation() { hitAmount = 0f; visibility = 1f; ActionPulse = SkillAmount = SkillPhase = LowHealthSeverity = 0f; guardUntil = 0f; CancelEntrance(); ApplyProperties(); }
    private void LateUpdate()
    {
        ActionPulse = 0f;
        SkillAmount = SkillPhase = 0f;
        if (entranceActive)
        {
            EntrancePhase = Mathf.Clamp01((Time.unscaledTime - entranceStart) / entranceDuration);
            EntranceAmount = Mathf.Sin(EntrancePhase * Mathf.PI);
            if (EntrancePhase >= 1f || (characterUnit != null && characterUnit.maxHP > 0 && characterUnit.currentHP <= 0)) CancelEntrance();
        }
        // Read the existing synchronized HP; presentation never changes gameplay or persistence.
        LowHealthSeverity = characterUnit != null && characterUnit.maxHP > 0 && characterUnit.currentHP > 0 &&
            (long)characterUnit.currentHP * 10 < (long)characterUnit.maxHP * 3
            ? Mathf.Clamp01(1f - characterUnit.currentHP / (characterUnit.maxHP * .3f)) : 0f;
        if (actionAnimator != null && actionAnimator.isActiveAndEnabled && actionAnimator.runtimeAnimatorController != null)
        {
            var state = actionAnimator.IsInTransition(0) ? actionAnimator.GetNextAnimatorStateInfo(0) : actionAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Hit") || state.IsName("hit") || state.IsName("getParry")) CancelEntrance();
            foreach (string name in ActionStates)
            {
                if (!state.IsName(name)) continue;
                CancelEntrance();
                // Follow the actual animation clock (including beat speed and hit stop).
                // Hit and Idle are intentionally excluded. Never change clip length or Animator speed.
                float phase = state.loop ? Mathf.Repeat(state.normalizedTime, 1f) : state.normalizedTime;
                float peak = Mathf.Clamp01(1f - Mathf.Abs(phase - .5f) / .3f);
                ActionPulse = peak * peak * (3f - 2f * peak);
                if (name == "HeavyCharge" || name == "HeavyAttack" || name == "Bolt" || name == "RiftCharge" ||
                    name == "RiftRelease" || name == "Shift" || name == "Ward" || name == "Fade" || name == "Sight")
                {
                    SkillPhase = Mathf.Clamp01(phase);
                    SkillAmount = ActionPulse;
                }
                break;
            }
        }
        ApplyProperties();
    }
    private void ApplyProperties()
    {
        if (bodyRenderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        Matrix4x4 space = transform.worldToLocalMatrix;
        bool stageStyled = StageReadabilityController.TryGetCharacterStyle(gameObject, out var stage) && isActiveAndEnabled;
        // A slow warning pulse freezes with game time and stays separate from action beats.
        float warning = .35f + .65f * (.5f + .5f * Mathf.Sin(Time.time * (2f * Mathf.PI / 1.4f)));
        foreach (SpriteRenderer renderer in bodyRenderers)
        {
            if (renderer == null || renderer.sprite == null) continue;
            // Keep other property-block values, including SpriteSkin data.
            renderer.GetPropertyBlock(block);
            block.SetFloat(PulseId, ActionPulse);
            block.SetFloat(EntranceId, EntranceAmount);
            block.SetFloat(EntrancePhaseId, EntrancePhase);
            block.SetFloat(SkillId, SkillAmount);
            block.SetFloat(SkillPhaseId, SkillPhase);
            block.SetFloat(LowHealthId, LowHealthSeverity * warning);
            block.SetFloat(GuardId, 1.6f * Mathf.Clamp01((guardUntil - Time.unscaledTime) / .18f));
            block.SetFloat(HitId, hitAmount); block.SetColor(HitColorId, activeHitColor);
            block.SetFloat(PhaseId, 1f - visibility); block.SetFloat(HeightId, characterHeight);
            block.SetMatrix(SpaceId, space);
            block.SetVector(UVId, DataUtility.GetOuterUV(renderer.sprite));
            var material = renderer.sharedMaterial;
            if (material != null && material.HasProperty(RimId))
            {
                // Scale each skin's authored edge strength; sketch skins retain their restrained value.
                float strength = material.GetFloat(RimId);
                Color edgeColor = material.GetColor(RimColorId);
                block.SetFloat(RimId, stageStyled ? strength * stage.edgeMultiplier : strength);
                block.SetColor(RimColorId, stageStyled ? Color.Lerp(edgeColor, stage.edgeColor, stage.edgeColorMix) : edgeColor);
            }
            renderer.SetPropertyBlock(block);
        }
    }
}
