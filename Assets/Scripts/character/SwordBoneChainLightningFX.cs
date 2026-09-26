using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SwordBoneChainLightningFX : MonoBehaviour
{
    [Header("Sword Start / End Points")]
    [SerializeField] private Transform swordStartPoint;
    [SerializeField] private Transform swordEndPoint;

    [Header("Jitter")]
    [SerializeField] private int pointCount = 6;
    [SerializeField] private float jitter = 0.05f;
    [SerializeField] private float forwardJitter = 0.015f;
    [SerializeField] private float redrawInterval = 0.03f;

    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Pin Endpoints")]
    [SerializeField] private bool keepFirstPointStable = true;
    [SerializeField] private bool keepLastPointStable = true;

    [Header("Visual Boost")]
    [SerializeField] private float alphaFlickerStrength = 0.22f;
    [SerializeField] private float widthPulseStrength = 0.18f;
    [SerializeField] private float pulseFrequency = 34f;

    private LineRenderer lr;
    private float timer;
    private float redrawTimer;
    private bool warnedMissingPoints;

    private Gradient originalGradient;
    private float originalStartWidth;
    private float originalEndWidth;

    public void SetSwordPoints(Transform startPoint, Transform endPoint)
    {
        swordStartPoint = startPoint;
        swordEndPoint = endPoint;
        warnedMissingPoints = false;
        GenerateLightning();
    }

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;

        originalGradient = lr.colorGradient;
        originalStartWidth = lr.startWidth;
        originalEndWidth = lr.endWidth;
    }

    private void OnEnable()
    {
        timer = 0f;
        redrawTimer = 0f;
        warnedMissingPoints = false;

        if (lr == null)
            lr = GetComponent<LineRenderer>();

        lr.useWorldSpace = true;
        GenerateLightning();
        SetVisual(1f, 1f);
    }

    private void Update()
    {
        float deltaTime = GetDeltaTime();
        timer += deltaTime;
        redrawTimer += deltaTime;

        if (redrawTimer >= redrawInterval)
        {
            redrawTimer = 0f;
            GenerateLightning();
        }

        float fadeStart = Mathf.Max(0f, lifeTime - fadeOutDuration);
        float alpha = 1f;
        float widthScale = 1f;

        if (timer >= fadeStart)
        {
            float t = Mathf.InverseLerp(fadeStart, lifeTime, timer);
            alpha = Mathf.Lerp(1f, 0f, t);
            widthScale = Mathf.Lerp(1f, 0.2f, t);
        }

        SetVisual(GetFlickeredAlpha(alpha), GetPulsedWidth(widthScale));

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void GenerateLightning()
    {
        if (lr == null)
            return;

        if (swordStartPoint == null || swordEndPoint == null)
        {
            lr.positionCount = 0;

            if (!warnedMissingPoints)
            {
                Debug.LogWarning("[SwordBoneChainLightningFX] swordStartPoint or swordEndPoint is not assigned: " + name, this);
                warnedMissingPoints = true;
            }

            return;
        }

        pointCount = Mathf.Max(2, pointCount);
        lr.positionCount = pointCount;

        Vector3 start = swordStartPoint.position;
        Vector3 end = swordEndPoint.position;
        Vector3 swordDir = (end - start).normalized;
        Vector3 perpendicular = new Vector3(-swordDir.y, swordDir.x, 0f);

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 pos = Vector3.Lerp(start, end, t);

            bool isFirst = i == 0;
            bool isLast = i == pointCount - 1;
            bool keepStable = (isFirst && keepFirstPointStable) || (isLast && keepLastPointStable);

            if (!keepStable)
            {
                float envelope = Mathf.Sin(t * Mathf.PI);
                pos += perpendicular * Random.Range(-jitter, jitter) * envelope;
                pos += swordDir * Random.Range(-forwardJitter, forwardJitter) * envelope;
            }

            lr.SetPosition(i, pos);
        }
    }

    private void SetVisual(float alpha, float widthScale)
    {
        if (lr == null)
            return;

        Gradient g = new Gradient();

        GradientColorKey[] colorKeys = originalGradient.colorKeys;
        GradientAlphaKey[] sourceAlphaKeys = originalGradient.alphaKeys;
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[sourceAlphaKeys.Length];

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i] = new GradientAlphaKey(
                sourceAlphaKeys[i].alpha * Mathf.Clamp01(alpha),
                sourceAlphaKeys[i].time
            );
        }

        g.SetKeys(colorKeys, alphaKeys);
        lr.colorGradient = g;

        lr.startWidth = originalStartWidth * widthScale;
        lr.endWidth = originalEndWidth * widthScale;
    }

    private float GetFlickeredAlpha(float baseAlpha)
    {
        if (alphaFlickerStrength <= 0f)
            return baseAlpha;

        return Mathf.Clamp01(baseAlpha * Random.Range(1f - alphaFlickerStrength, 1f + alphaFlickerStrength));
    }

    private float GetPulsedWidth(float baseWidthScale)
    {
        if (widthPulseStrength <= 0f)
            return baseWidthScale;

        float pulse = Mathf.Sin(Time.unscaledTime * pulseFrequency) * widthPulseStrength;
        return Mathf.Max(0.01f, baseWidthScale * (1f + pulse));
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
