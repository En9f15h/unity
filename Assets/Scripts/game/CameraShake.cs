using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    [Header("Shake Tuning")]
    [SerializeField] private float maxStrength = 0.35f;
    [SerializeField] private float frequency = 36f;
    [SerializeField] private float rotationMultiplier = 6f;

    private Coroutine shakeCoroutine;
    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeStrength;
    private float noiseSeed;

    private Vector3 appliedPositionOffset = Vector3.zero;
    private Quaternion appliedRotationOffset = Quaternion.identity;

    private void Awake()
    {
        Instance = this;
        noiseSeed = Random.Range(0f, 1000f);
    }

    public void Shake(float duration, float strength)
    {
        if (duration <= 0f || strength <= 0f)
            return;

        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeStrength = Mathf.Clamp(shakeStrength + strength, 0f, maxStrength);

        if (shakeCoroutine == null)
            shakeCoroutine = StartCoroutine(ShakeCoroutine());
    }

    private IEnumerator ShakeCoroutine()
    {
        while (shakeTimeRemaining > 0f)
        {
            shakeTimeRemaining -= Time.unscaledDeltaTime;

            float decay = Mathf.Clamp01(shakeTimeRemaining / Mathf.Max(0.0001f, shakeDuration));
            float currentStrength = shakeStrength * decay * decay;
            float noiseTime = Time.unscaledTime * frequency;

            float x = (Mathf.PerlinNoise(noiseSeed, noiseTime) - 0.5f) * 2f * currentStrength;
            float y = (Mathf.PerlinNoise(noiseSeed + 19.31f, noiseTime) - 0.5f) * 2f * currentStrength;
            float zRot = (Mathf.PerlinNoise(noiseSeed + 41.73f, noiseTime) - 0.5f) * 2f * currentStrength * rotationMultiplier;

            ApplyShakeOffset(new Vector3(x, y, 0f), Quaternion.Euler(0f, 0f, zRot));
            yield return null;
        }

        ClearShakeOffset();
        shakeStrength = 0f;
        shakeDuration = 0f;
        shakeCoroutine = null;
    }

    private void ApplyShakeOffset(Vector3 positionOffset, Quaternion rotationOffset)
    {
        ClearShakeOffset();

        appliedPositionOffset = positionOffset;
        appliedRotationOffset = rotationOffset;

        transform.localPosition += appliedPositionOffset;
        transform.localRotation *= appliedRotationOffset;
    }

    private void ClearShakeOffset()
    {
        transform.localPosition -= appliedPositionOffset;
        transform.localRotation *= Quaternion.Inverse(appliedRotationOffset);

        appliedPositionOffset = Vector3.zero;
        appliedRotationOffset = Quaternion.identity;
    }
}
