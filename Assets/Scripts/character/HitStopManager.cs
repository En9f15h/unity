using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance;

    [Header("Beat Settings")]
    [SerializeField] private float bpm = 120f;

    [Tooltip("Number of beats to pause. At 120 BPM, 1 beat = 0.5 seconds.")]
    [SerializeField] private float stopBeats = 1f;

    [Header("General Hit Stop Settings")]
    [SerializeField] private float stopTimeScale = 0f;

    [Header("Dance Slow Motion Settings")]
    [SerializeField] private float danceSlowTimeScale = 0.2f;

    private bool isTimeEffectPlaying = false;
    private float originalFixedDeltaTime;
    private Coroutine currentTimeEffectCoroutine;
    private int timeEffectVersion = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        originalFixedDeltaTime = Time.fixedDeltaTime;
    }

    public float GetBeatStopDuration()
    {
        return 60f / bpm * stopBeats;
    }

    public IEnumerator HitStopByBeat()
    {
        // Compatibility entry point: confirmed hits must not pause the global clock.
        // Impact flashes and camera shake supply feedback without delaying the beat.
        yield break;
    }

    /// <summary>
    /// Plays dance slow motion and extends the total action duration by extraDuration seconds.
    /// Example: extraDuration = 0.5f makes a 1-second action last 1.5 seconds.
    /// </summary>
    public void PlayDanceSlowMotionWithExtraTime(float extraDuration)
    {
        if (danceSlowTimeScale >= 1f)
        {
            Debug.LogWarning("danceSlowTimeScale must be lower than 1 or the action will not slow down.");
            return;
        }

        float realDuration = extraDuration / (1f - danceSlowTimeScale);

        if (currentTimeEffectCoroutine != null)
            StopCoroutine(currentTimeEffectCoroutine);

        currentTimeEffectCoroutine = StartCoroutine(TimeEffectCoroutine(danceSlowTimeScale, realDuration));
    }

    public void PlaySlowMotion(float targetTimeScale, float realDuration)
    {
        if (realDuration <= 0f)
            return;

        targetTimeScale = Mathf.Clamp(targetTimeScale, 0.01f, 0.99f);

        if (currentTimeEffectCoroutine != null)
            StopCoroutine(currentTimeEffectCoroutine);

        currentTimeEffectCoroutine = StartCoroutine(TimeEffectCoroutine(targetTimeScale, realDuration));
    }

    private IEnumerator TimeEffectCoroutine(float targetTimeScale, float realDuration)
    {
        int version = ++timeEffectVersion;

        if (isTimeEffectPlaying)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            isTimeEffectPlaying = false;
        }

        isTimeEffectPlaying = true;

        float originalTimeScale = Time.timeScale;

        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * Time.timeScale;

        yield return new WaitForSecondsRealtime(realDuration);

        if (version != timeEffectVersion)
            yield break;

        Time.timeScale = originalTimeScale <= 0f ? 1f : originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        isTimeEffectPlaying = false;
        currentTimeEffectCoroutine = null;
    }
}
