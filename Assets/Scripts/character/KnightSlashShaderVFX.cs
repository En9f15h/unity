using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KnightSlashShaderVFX : MonoBehaviour
{
    [SerializeField] private Transform swordTip;
    [SerializeField] private Transform swordBase;
    [SerializeField] private Material slashMaterial;
    [SerializeField, Range(0.01f,0.3f)] private float trailWidth = 0.08f;
    private Coroutine trailRoutine;
    private GameObject trailObject;
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
        Animator animator = GetComponent<CharacterUnit>()?.GetAnimator();
        float started = Time.unscaledTime;
        bool entered = false;
        yield return null;
        while (Time.unscaledTime - started < seconds + 0.12f && animator != null)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName))
                state = animator.GetNextAnimatorStateInfo(0);
            bool matching = state.IsName(stateName);
            if (entered && !matching) break;
            entered |= matching;
            VisualPhase = matching ? state.normalizedTime : 0f;
            // Authored action midpoint is the strong beat. Follow its normalized phase,
            // including existing Animator speed and slow motion; never retime the clip.
            IsEmitting = matching && VisualPhase >= 0.3f && VisualPhase <= 0.7f && Time.timeScale > 0f;
            trail.emitting = IsEmitting;
            float envelope = 1f - Mathf.Clamp01(Mathf.Abs(VisualPhase - 0.5f) / 0.2f);
            trail.widthMultiplier = trailWidth * Mathf.Max(0.1f, Mathf.Abs(transform.lossyScale.x)) * Mathf.Lerp(0.35f, 1f, envelope);
            trailObject.transform.position = Vector3.Lerp(swordBase.position, swordTip.position, 0.85f);
            yield return null;
        }
        trail.emitting = false; IsEmitting = false;
        Destroy(trailObject, trail.time + 0.05f);
        trailObject = null; trailRoutine = null;
    }
    private void OnDisable()
    {
        if (trailRoutine != null) StopCoroutine(trailRoutine);
        if (trailObject != null) Destroy(trailObject);
        trailRoutine = null; trailObject = null; IsEmitting = false; VisualPhase = 0f;
    }
}
