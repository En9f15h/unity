using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KnightSlashShaderVFX : MonoBehaviour
{
    [SerializeField] private Transform swordTip;
    [SerializeField] private Transform swordBase;
    [SerializeField] private Material slashMaterial;
    [SerializeField, Range(0.01f,0.6f)] private float trailWidth = 0.08f;
    private Coroutine trailRoutine;
    private GameObject trailObject;
    private LineRenderer chargeLine;
    private Animator chargeAnimator;
    private MaterialPropertyBlock chargeBlock;
    private static readonly int RibbonPhaseId = Shader.PropertyToID("_RibbonPhase");
    public LineRenderer ChargeRenderer => chargeLine;
    public float VisualPhase { get; private set; }
    public bool IsEmitting { get; private set; }
    public void PlaySlash(ActionType action, bool heavyRelease, float stepDuration)
    {
        if (!isActiveAndEnabled || swordTip == null || swordBase == null) return;
        if (action != ActionType.LightAttack && action != ActionType.LowAttack && !(action == ActionType.HeavyAttack && heavyRelease)) return;
        if (trailRoutine != null) StopCoroutine(trailRoutine);
        if (trailObject != null) Destroy(trailObject);
        string state = action == ActionType.LightAttack ? "LightAttack" : action == ActionType.LowAttack ? "LowAttack" : "HeavyAttack";
        trailRoutine = StartCoroutine(TrackSword(Mathf.Max(0.05f, stepDuration), state));
    }
    private IEnumerator TrackSword(float seconds, string stateName)
    {
        trailObject = new GameObject("Knight Sword Energy Trail");
        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = slashMaterial != null ? slashMaterial : CombatShaderMaterials.KnightSlash;
        trail.time = 0.11f; trail.minVertexDistance = 0.025f;
        trail.widthMultiplier = trailWidth * Mathf.Max(0.1f, Mathf.Abs(transform.lossyScale.x));
        trail.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        trail.startColor = new Color(1f,0.86f,0.6f,0.65f); trail.endColor = new Color(1f,0.65f,0.18f,0f);
        trail.sortingLayerID = SortingLayer.NameToID("Effect"); trail.sortingOrder = 4;
        trail.textureMode = LineTextureMode.Stretch; trail.emitting = false;
        trailObject.transform.position = Vector3.Lerp(swordBase.position, swordTip.position, 0.85f);
        var trailBlock = new MaterialPropertyBlock();
        Animator animator = GetComponent<CharacterUnit>()?.GetAnimator();
        float entryWait = 0f;
        bool entered = false;
        bool interrupted = false;
        yield return null;
        while (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null && swordBase != null && swordTip != null)
        {
            AnimatorStateInfo state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            bool matching = state.IsName(stateName);
            if (entered && !matching) { interrupted = true; break; }
            entered |= matching;
            // Bound only the wait for an action to start. Once entered, its real
            // animation phase owns the lifetime, including pauses and slow motion.
            if (!entered)
            {
                entryWait += Time.deltaTime;
                if (entryWait >= seconds + 0.12f) break;
            }
            VisualPhase = matching ? state.normalizedTime : 0f;
            if (matching && VisualPhase > 0.7f) break;
            trailBlock.SetFloat(RibbonPhaseId, VisualPhase); trail.SetPropertyBlock(trailBlock);
            // Authored action midpoint is the strong beat. Follow its normalized phase,
            // including existing Animator speed and slow motion; never retime the clip.
            IsEmitting = matching && VisualPhase >= 0.3f && VisualPhase <= 0.7f && Time.timeScale > 0f && animator.speed > 0f;
            trail.emitting = IsEmitting;
            float envelope = 1f - Mathf.Clamp01(Mathf.Abs(VisualPhase - 0.5f) / 0.2f);
            trail.widthMultiplier = trailWidth * Mathf.Max(0.1f, Mathf.Abs(transform.lossyScale.x)) * Mathf.Lerp(0.35f, 1f, envelope);
            trailObject.transform.position = Vector3.Lerp(swordBase.position, swordTip.position, 0.85f);
            yield return null;
        }
        trail.emitting = false; IsEmitting = false;
        if (interrupted || animator == null || !animator.isActiveAndEnabled) trail.Clear();
        Destroy(trailObject, trail.time + 0.05f);
        trailObject = null; trailRoutine = null;
    }
    private void LateUpdate()
    {
        if (chargeLine != null) chargeLine.enabled = false;
        if (swordBase == null || swordTip == null) return;
        if (chargeAnimator == null) chargeAnimator = GetComponent<CharacterUnit>()?.GetAnimator();
        if (chargeAnimator == null || !chargeAnimator.isActiveAndEnabled || chargeAnimator.runtimeAnimatorController == null) return;
        var state = chargeAnimator.IsInTransition(0) ? chargeAnimator.GetNextAnimatorStateInfo(0) : chargeAnimator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("HeavyCharge")) return;
        float phase = state.loop ? Mathf.Repeat(state.normalizedTime, 1) : state.normalizedTime;
        if (phase < 0 || phase >= 1) return;
        Material material = slashMaterial != null ? slashMaterial : CombatShaderMaterials.KnightSlash;
        if (material == null) return;
        if (chargeLine == null)
        {
            var go = new GameObject("Knight Blade Charge"); go.transform.SetParent(transform, false);
            chargeLine = go.AddComponent<LineRenderer>(); chargeLine.sharedMaterial = material;
            chargeLine.useWorldSpace = true; chargeLine.positionCount = 2;
            chargeLine.sortingLayerID = SortingLayer.NameToID("Effect"); chargeLine.sortingOrder = 4;
            chargeLine.textureMode = LineTextureMode.Stretch;
            chargeBlock = new MaterialPropertyBlock();
        }
        float envelope = Mathf.Sin(phase * Mathf.PI);
        chargeLine.enabled = envelope > .01f;
        chargeLine.widthMultiplier = trailWidth * Mathf.Max(.1f, Mathf.Abs(transform.lossyScale.x)) * .7f;
        chargeLine.startColor = new Color(1, .85f, .55f, envelope * .65f);
        chargeLine.endColor = new Color(1, .95f, .7f, envelope * .9f);
        chargeLine.SetPosition(0, swordBase.position); chargeLine.SetPosition(1, swordTip.position);
        chargeBlock.SetFloat(RibbonPhaseId, phase); chargeLine.SetPropertyBlock(chargeBlock);
    }
    private void OnDisable()
    {
        if (trailRoutine != null) StopCoroutine(trailRoutine);
        if (trailObject != null) Destroy(trailObject);
        trailRoutine = null; trailObject = null; IsEmitting = false; VisualPhase = 0f;
        if (chargeLine != null) chargeLine.enabled = false;
    }
}
