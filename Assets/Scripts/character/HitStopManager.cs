using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance;

    [Header("節拍設定")]
    [SerializeField] private float bpm = 120f;

    [Tooltip("停幾拍。120 BPM 時，1 拍 = 0.5 秒")]
    [SerializeField] private float stopBeats = 1f;

    [Header("一般 Hit Stop 設定")]
    [SerializeField] private float stopTimeScale = 0f;

    [Header("跳舞慢動作設定")]
    [SerializeField] private float danceSlowTimeScale = 0.2f;

    private bool isTimeEffectPlaying = false;
    private float originalFixedDeltaTime;
    private Coroutine currentTimeEffectCoroutine;

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
        float duration = GetBeatStopDuration();
        yield return StartCoroutine(TimeEffectCoroutine(stopTimeScale, duration));
    }

    /// <summary>
    /// 播放跳舞慢動作，並且讓整體動作「額外增加 extraDuration 秒」
    /// 例如 extraDuration = 0.5f，就會讓原本 1 秒的動作變成 1.5 秒。
    /// </summary>
    public void PlayDanceSlowMotionWithExtraTime(float extraDuration)
    {
        if (danceSlowTimeScale >= 1f)
        {
            Debug.LogWarning("danceSlowTimeScale 必須小於 1，否則不會變慢");
            return;
        }

        float realDuration = extraDuration / (1f - danceSlowTimeScale);

        if (currentTimeEffectCoroutine != null)
            StopCoroutine(currentTimeEffectCoroutine);

        currentTimeEffectCoroutine = StartCoroutine(TimeEffectCoroutine(danceSlowTimeScale, realDuration));
    }

    private IEnumerator TimeEffectCoroutine(float targetTimeScale, float realDuration)
    {
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

        Time.timeScale = originalTimeScale <= 0f ? 1f : originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        isTimeEffectPlaying = false;
        currentTimeEffectCoroutine = null;
    }
}