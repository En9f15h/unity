using UnityEngine;

// Clear the previous presentation offset before tracking, then apply the new
// impulse after framing. Neither the tracking anchor nor game time is modified.
[DefaultExecutionOrder(1000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;
    [Header("Shake Tuning")]
    [SerializeField] private float maxStrength = .14f;
    [SerializeField] private float frequency = 24f;
    [SerializeField] private float rotationMultiplier = 1.5f;
    private float remaining, duration, strength, seed;
    private Vector2 impactDirection;
    private Vector3 appliedPosition;
    private Quaternion appliedRotation = Quaternion.identity;
    public Vector3 UnshakenPosition { get; private set; }
    public Quaternion UnshakenRotation { get; private set; }
    private void Awake() { Instance = this; seed = Random.Range(0f, 1000f); CapturePose(); }
    private void CapturePose() { UnshakenPosition = transform.position; UnshakenRotation = transform.rotation; }
    private void Update() => ClearOffset();
    public void Shake(float seconds, float amount)
    {
        if (!isActiveAndEnabled || seconds <= 0 || amount <= 0) return;
        remaining = Mathf.Max(remaining, Mathf.Min(seconds, .3f));
        duration = Mathf.Max(duration, remaining);
        strength = Mathf.Max(strength, Mathf.Clamp(amount, 0f, maxStrength));
    }
    public void Impact(ActionType action, bool released, Vector2 direction, bool parry = false)
    {
        impactDirection = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
        bool heavy = released && (action == ActionType.HeavyAttack || action == ActionType.Rift);
        float amount = parry ? .065f : action == ActionType.Ultimate ? .12f : heavy ? .09f : .045f;
        float seconds = parry ? .12f : action == ActionType.Ultimate ? .2f : heavy ? .16f : .1f;
        Shake(seconds, amount);
    }
    private void LateUpdate()
    {
        CapturePose();
        if (remaining <= 0f) return;
        float envelope = Mathf.Clamp01(remaining / Mathf.Max(.001f, duration));
        envelope *= envelope;
        float t = (duration - remaining) * frequency;
        float x = (Mathf.PerlinNoise(seed, t) - .5f) * 2f;
        float y = (Mathf.PerlinNoise(seed + 19.31f, t) - .5f) * 2f;
        float kick = Mathf.Cos(t * 2f) * .65f;
        Vector3 offset = new Vector3(x * .4f + impactDirection.x * kick, y * .3f + impactDirection.y * kick, 0) * strength * envelope;
        appliedPosition = offset;
        appliedRotation = Quaternion.Euler(0, 0, x * strength * envelope * rotationMultiplier);
        transform.position += offset;
        transform.rotation *= appliedRotation;
        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
        if (remaining <= 0f) { strength = duration = 0f; impactDirection = Vector2.zero; }
    }
    private void ClearOffset()
    {
        transform.position -= appliedPosition;
        transform.rotation *= Quaternion.Inverse(appliedRotation);
        appliedPosition = Vector3.zero; appliedRotation = Quaternion.identity;
    }
    private void OnDisable() { ClearOffset(); remaining = duration = strength = 0f; CapturePose(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
