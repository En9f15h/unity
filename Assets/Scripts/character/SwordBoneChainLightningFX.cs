using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SwordBoneChainLightningFX : MonoBehaviour
{
    [Header("劍身起點 / 終點")]
    [SerializeField] private Transform swordStartPoint;
    [SerializeField] private Transform swordEndPoint;

    [Header("抖動")]
    [SerializeField] private int pointCount = 6;
    [SerializeField] private float jitter = 0.05f;
    [SerializeField] private float redrawInterval = 0.03f;

    [Header("生命時間")]
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("是否固定首尾")]
    [SerializeField] private bool keepFirstPointStable = true;
    [SerializeField] private bool keepLastPointStable = true;

    private LineRenderer lr;
    private float timer;
    private float redrawTimer;

    private Gradient originalGradient;
    private float originalStartWidth;
    private float originalEndWidth;

    public void SetSwordPoints(Transform startPoint, Transform endPoint)
    {
        swordStartPoint = startPoint;
        swordEndPoint = endPoint;
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

        if (lr == null)
            lr = GetComponent<LineRenderer>();

        lr.useWorldSpace = true;
        GenerateLightning();
        SetVisual(1f, 1f);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        redrawTimer += Time.deltaTime;

        if (redrawTimer >= redrawInterval)
        {
            redrawTimer = 0f;
            GenerateLightning();
        }

        float fadeStart = Mathf.Max(0f, lifeTime - fadeOutDuration);

        if (timer >= fadeStart)
        {
            float t = Mathf.InverseLerp(fadeStart, lifeTime, timer);
            float alpha = Mathf.Lerp(1f, 0f, t);
            float widthScale = Mathf.Lerp(1f, 0.2f, t);
            SetVisual(alpha, widthScale);
        }

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
            Debug.LogWarning("[SwordBoneChainLightningFX] swordStartPoint 或 swordEndPoint 沒指定: " + name, this);
            return;
        }

        pointCount = Mathf.Max(2, pointCount);
        lr.positionCount = pointCount;

        Vector3 start = swordStartPoint.position;
        Vector3 end = swordEndPoint.position;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 pos = Vector3.Lerp(start, end, t);

            bool isFirst = i == 0;
            bool isLast = i == pointCount - 1;
            bool keepStable = (isFirst && keepFirstPointStable) || (isLast && keepLastPointStable);

            if (!keepStable)
            {
                pos.x += Random.Range(-jitter, jitter);
                pos.y += Random.Range(-jitter, jitter);
            }

            lr.SetPosition(i, pos);
        }
    }

    private void SetVisual(float alpha, float widthScale)
    {
        Gradient g = new Gradient();

        GradientColorKey[] colorKeys = originalGradient.colorKeys;
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[originalGradient.alphaKeys.Length];

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i] = new GradientAlphaKey(
                originalGradient.alphaKeys[i].alpha * alpha,
                originalGradient.alphaKeys[i].time
            );
        }

        g.SetKeys(colorKeys, alphaKeys);
        lr.colorGradient = g;

        lr.startWidth = originalStartWidth * widthScale;
        lr.endWidth = originalEndWidth * widthScale;
    }
}