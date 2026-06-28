using System.Collections;
using UnityEngine;

public class LightningStrikeVFXRoot : MonoBehaviour
{
    [Header("命中時播放的粒子")]
    [SerializeField] private ParticleSystem branchGlow;
    [SerializeField] private ParticleSystem impactBurst;

    [Header("時間同步")]
    [Tooltip("從特效生成後，到雷真正劈到劍上的時間。若動畫事件在 0.3 秒觸發，主雷 0.2 秒落下，這裡就填 0.2")]
    [SerializeField] private float impactDelay = 0.2f;

    [Tooltip("整個 root 多久後刪掉")]
    [SerializeField] private float destroyDelay = 1.0f;

    private Coroutine playCoroutine;

    private void Awake()
    {
        StopAndClear(branchGlow);
        StopAndClear(impactBurst);
    }

    private void OnEnable()
    {
        if (playCoroutine != null)
            StopCoroutine(playCoroutine);

        StopAndClear(branchGlow);
        StopAndClear(impactBurst);

        playCoroutine = StartCoroutine(PlayTimeline());
    }

    private IEnumerator PlayTimeline()
    {
        if (impactDelay > 0f)
            yield return new WaitForSeconds(impactDelay);

        PlayNow(branchGlow);
        PlayNow(impactBurst);

        if (destroyDelay > 0f)
            Destroy(gameObject, destroyDelay);
    }

    private void StopAndClear(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void PlayNow(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.gameObject.SetActive(true);
        ps.Play(true);
    }
}