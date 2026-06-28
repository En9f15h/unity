using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LightningBoltFX : MonoBehaviour
{
    [Header("主雷柱")]
    [SerializeField] private int pointCount = 10;
    [SerializeField] private float horizontalJitter = 0.25f;
    [SerializeField] private float topExtraOffset = 2.0f;

    [Header("時間")]
    [Tooltip("從開始到劈到劍上花多久。若動畫事件設在 0.3 秒，這裡設 0.2，就會在 0.5 秒命中")]
    [SerializeField] private float strikeDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("刷新")]
    [SerializeField] private int redrawCount = 6;
    [SerializeField] private float redrawInterval = 0.03f;

    [Header("分岔")]
    [SerializeField] private int branchCount = 5;
    [SerializeField] private int branchPointCount = 4;
    [SerializeField] private float branchLengthMin = 0.6f;
    [SerializeField] private float branchLengthMax = 1.3f;
    [SerializeField] private float branchHorizontalJitter = 0.15f;
    [SerializeField] private float branchWidthMultiplier = 0.45f;
    [SerializeField] private float branchAlphaMultiplier = 0.75f;

    private LineRenderer mainLR;
    private readonly List<LineRenderer> branchLRs = new List<LineRenderer>();

    private float timer = 0f;
    private int redrawRemaining;

    private Gradient originalColorGradient;
    private float originalStartWidth;
    private float originalEndWidth;

    private Camera mainCam;
    private Vector3 bottomWorld;
    private Vector3 topWorld;
    private Vector3[] cachedMainPoints;

    private void Awake()
    {
        mainLR = GetComponent<LineRenderer>();
        mainCam = Camera.main;

        mainLR.useWorldSpace = true;

        originalColorGradient = mainLR.colorGradient;
        originalStartWidth = mainLR.startWidth;
        originalEndWidth = mainLR.endWidth;

        EnsureBranchRenderers();
        SetOverallVisual(0f, 1f);
        mainLR.positionCount = 0;
    }

    private void OnEnable()
    {
        timer = 0f;
        redrawRemaining = Mathf.Max(1, redrawCount);

        CacheWorldPoints();
        EnsureBranchRenderers();

        mainLR.positionCount = 0;
        for (int i = 0; i < branchLRs.Count; i++)
            branchLRs[i].positionCount = 0;

        SetOverallVisual(0f, 1f);
        CancelInvoke();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // 劈下階段
        if (timer <= strikeDuration)
        {
            float t = Mathf.InverseLerp(0f, strikeDuration, timer);
            DrawLightningProgress(t);

            if (!IsInvoking(nameof(RedrawLightning)))
                InvokeRepeating(nameof(RedrawLightning), redrawInterval, redrawInterval);

            SetOverallVisual(1f, 1f);
            return;
        }

        // 淡出階段
        if (IsInvoking(nameof(RedrawLightning)))
            CancelInvoke(nameof(RedrawLightning));

        float fadeT = Mathf.InverseLerp(strikeDuration, strikeDuration + fadeOutDuration, timer);
        float alpha = Mathf.Lerp(1f, 0f, fadeT);
        float widthScale = Mathf.Lerp(1f, 0.2f, fadeT);

        SetOverallVisual(alpha, widthScale);

        if (timer >= strikeDuration + fadeOutDuration)
            Destroy(gameObject);
    }

    private void CacheWorldPoints()
    {
        bottomWorld = transform.position;

        if (mainCam == null)
        {
            topWorld = bottomWorld + Vector3.up * 8f;
            return;
        }

        if (mainCam.orthographic)
        {
            float topY = mainCam.transform.position.y + mainCam.orthographicSize + topExtraOffset;
            topWorld = new Vector3(bottomWorld.x, topY, bottomWorld.z);
        }
        else
        {
            float distanceToCamera = Mathf.Abs(bottomWorld.z - mainCam.transform.position.z);
            Vector3 viewportTop = mainCam.ViewportToWorldPoint(new Vector3(0.5f, 1f, distanceToCamera));
            topWorld = new Vector3(bottomWorld.x, viewportTop.y + topExtraOffset, bottomWorld.z);
        }
    }

    private void EnsureBranchRenderers()
    {
        while (branchLRs.Count < branchCount)
        {
            GameObject go = new GameObject("LightningBranch_" + branchLRs.Count);
            go.transform.SetParent(transform, false);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            CopyMainLineStyleToBranch(lr);

            branchLRs.Add(lr);
        }

        for (int i = 0; i < branchLRs.Count; i++)
        {
            branchLRs[i].gameObject.SetActive(i < branchCount);
        }
    }

    private void CopyMainLineStyleToBranch(LineRenderer lr)
    {
        lr.useWorldSpace = true;
        lr.material = mainLR.material;
        lr.textureMode = mainLR.textureMode;
        lr.alignment = mainLR.alignment;
        lr.numCapVertices = mainLR.numCapVertices;
        lr.numCornerVertices = mainLR.numCornerVertices;
        lr.shadowCastingMode = mainLR.shadowCastingMode;
        lr.receiveShadows = mainLR.receiveShadows;
        lr.sortingLayerID = mainLR.sortingLayerID;
        lr.sortingOrder = mainLR.sortingOrder;

        lr.startWidth = originalStartWidth * branchWidthMultiplier;
        lr.endWidth = originalEndWidth * branchWidthMultiplier;
        lr.colorGradient = originalColorGradient;
    }

    private void RedrawLightning()
    {
        redrawRemaining--;
        DrawLightningProgress(1f);

        if (redrawRemaining <= 0)
            CancelInvoke(nameof(RedrawLightning));
    }

    private void DrawLightningProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        pointCount = Mathf.Max(2, pointCount);
        cachedMainPoints = new Vector3[pointCount];
        mainLR.positionCount = pointCount;

        Vector3 currentBottom = Vector3.Lerp(topWorld, bottomWorld, progress);

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 pos = Vector3.Lerp(topWorld, currentBottom, t);

            if (i != 0 && i != pointCount - 1)
                pos.x += Random.Range(-horizontalJitter, horizontalJitter);

            cachedMainPoints[i] = pos;
            mainLR.SetPosition(i, pos);
        }

        DrawBranches(progress);
    }

    private void DrawBranches(float progress)
    {
        if (cachedMainPoints == null || cachedMainPoints.Length < 3)
            return;

        int visibleMiddlePointMax = Mathf.Clamp(Mathf.FloorToInt((cachedMainPoints.Length - 1) * progress), 1, cachedMainPoints.Length - 2);

        for (int i = 0; i < branchLRs.Count; i++)
        {
            LineRenderer branch = branchLRs[i];
            if (branch == null || !branch.gameObject.activeSelf)
                continue;

            if (visibleMiddlePointMax <= 1)
            {
                branch.positionCount = 0;
                continue;
            }

            int startIndex = Random.Range(1, visibleMiddlePointMax);
            Vector3 start = cachedMainPoints[startIndex];

            float len = Random.Range(branchLengthMin, branchLengthMax);
            float dirX = Random.Range(-1f, 1f);
            float dirY = Random.Range(-1f, -0.2f);

            Vector3 end = start + new Vector3(dirX, dirY, 0f).normalized * len;

            branch.positionCount = Mathf.Max(2, branchPointCount);

            for (int p = 0; p < branch.positionCount; p++)
            {
                float t = p / (float)(branch.positionCount - 1);
                Vector3 pos = Vector3.Lerp(start, end, t);

                if (p != 0 && p != branch.positionCount - 1)
                    pos.x += Random.Range(-branchHorizontalJitter, branchHorizontalJitter);

                branch.SetPosition(p, pos);
            }
        }
    }

    private void SetOverallVisual(float alpha, float widthScale)
    {
        SetLineRendererAlpha(mainLR, alpha, widthScale, 1f);

        for (int i = 0; i < branchLRs.Count; i++)
        {
            SetLineRendererAlpha(branchLRs[i], alpha * branchAlphaMultiplier, widthScale, branchWidthMultiplier);
        }
    }

    private void SetLineRendererAlpha(LineRenderer lr, float alpha, float widthScale, float widthMultiplier)
    {
        if (lr == null)
            return;

        Gradient g = new Gradient();

        GradientColorKey[] colorKeys = originalColorGradient.colorKeys;
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[originalColorGradient.alphaKeys.Length];

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i] = new GradientAlphaKey(
                originalColorGradient.alphaKeys[i].alpha * alpha,
                originalColorGradient.alphaKeys[i].time
            );
        }

        g.SetKeys(colorKeys, alphaKeys);
        lr.colorGradient = g;

        lr.startWidth = originalStartWidth * widthScale * widthMultiplier;
        lr.endWidth = originalEndWidth * widthScale * widthMultiplier;
    }
}