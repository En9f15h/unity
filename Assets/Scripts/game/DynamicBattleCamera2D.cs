using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DynamicBattleCamera2D : MonoBehaviour
{
    [Header("自動抓角色")]
    [SerializeField] private bool autoFindPlayersOnSyncedStart = true;
    [SerializeField] private string playerTag = "PLAYER";
    [SerializeField] private float retryFindInterval = 0.5f;
    [Header("子物件跟著縮放")]
    [SerializeField] private bool scaleChildrenWithZoom = true;

    [Tooltip("不指定就用 Camera 自己當根節點")]
    [SerializeField] private Transform childrenScaleRoot;

    [Tooltip("true = 只縮放直屬子物件；false = 縮放所有後代")]
    [SerializeField] private bool onlyDirectChildren = true;

    [SerializeField] private bool includeInactiveChildren = true;

    [Tooltip("如果你覺得縮放方向反了，可以打開這個")]
    [SerializeField] private bool invertChildScale = false;
    [Header("追蹤目標")]
    [SerializeField] private Transform targetA;
    [SerializeField] private Transform targetB;

    [Header("跟隨設定")]
    [SerializeField] private float followSmoothTime = 0.18f;
    [SerializeField] private Vector3 baseOffset = new Vector3(0f, 0f, -10f);

    [Header("前視設定")]
    [SerializeField] private bool enableLookAhead = true;
    [SerializeField] private float lookAheadStrength = 0.35f;
    [SerializeField] private float maxLookAheadX = 1.25f;
    [SerializeField] private float velocitySampleSmoothing = 12f;

    [Header("縮放設定")]
    [SerializeField] private bool autoZoom = true;
    [SerializeField] private float distanceToSizeMultiplier = 0.45f;
    [SerializeField] private float zoomSmoothSpeed = 5f;

    [Tooltip("最小鏡頭大小。數字越小，拉得越近。")]
    [SerializeField] private float minOrthoSize = 3.5f;

    [Tooltip("最大鏡頭大小。若 <= 0，會在 Start 時自動使用鏡頭原始大小。")]
    [SerializeField] private float maxOrthoSize = 0f;

    [Header("安全邊距")]
    [SerializeField] private float horizontalPadding = 2f;
    [SerializeField] private float verticalPadding = 1f;
    [Header("背景邊界限制")]
    [SerializeField] private bool useBackgroundBounds = true;
    [SerializeField] private SpriteRenderer backgroundSpriteRenderer;
    [SerializeField] private Collider2D backgroundCollider2D;

    [Tooltip("背景內縮邊距，避免鏡頭貼太邊")]
    [SerializeField] private float backgroundPadding = 0f;
    [Header("場地邊界（可選）")]
    [SerializeField] private bool useCameraBounds = false;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 10f;
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 5f;
    private Bounds cachedBackgroundBounds;
    private bool hasBackgroundBounds = false;
    private Camera cam;
    private Vector3 positionVelocity;

    private Vector3 lastAPosition;
    private Vector3 lastBPosition;
    private Vector3 smoothedVelocityA;
    private Vector3 smoothedVelocityB;
    private float referenceOrthoSize;
    private Transform[] zoomScaleChildren;
    private Vector3[] zoomScaleOriginalScales;
    private int cachedChildCount = -1;
    private Coroutine autoFindCoroutine;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {


        if (!cam.orthographic)
        {
            Debug.LogWarning("DynamicBattleCamera2D 建議搭配 Orthographic Camera 使用。");
        }

        if (maxOrthoSize <= 0f)
            maxOrthoSize = cam.orthographicSize;
        TryCacheBackgroundBounds();
        referenceOrthoSize = cam.orthographicSize;

        if (childrenScaleRoot == null)
            childrenScaleRoot = transform;

        CacheZoomScaleChildren();

        if (autoFindPlayersOnSyncedStart)
            autoFindCoroutine = StartCoroutine(WaitForSyncedStartAndFindPlayers());
        else
            CacheCurrentTargetPositions();
    }

    private void LateUpdate()
    {
        if (autoFindPlayersOnSyncedStart && (!IsTargetValid(targetA) || !IsTargetValid(targetB)))
        {
            if (autoFindCoroutine == null)
                autoFindCoroutine = StartCoroutine(WaitForSyncedStartAndFindPlayers());

            return;
        }

        if (!IsTargetValid(targetA) || !IsTargetValid(targetB))
            return;

        UpdateTargetVelocities();

        Vector3 desiredCenter = GetTargetsCenter();
        desiredCenter += GetLookAheadOffset();
        desiredCenter += new Vector3(baseOffset.x, baseOffset.y, 0f);

        float desiredSize = cam.orthographicSize;
        if (autoZoom)
            desiredSize = GetDesiredOrthoSize();

        Vector3 desiredCameraPos = new Vector3(
            desiredCenter.x,
            desiredCenter.y,
            transform.position.z
        );

        if (useCameraBounds || hasBackgroundBounds)
            desiredCameraPos = ClampCameraPositionToBounds(desiredCameraPos, desiredSize);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredCameraPos,
            ref positionVelocity,
            followSmoothTime
        );

        if (autoZoom)
        {
            cam.orthographicSize = Mathf.Lerp(
                cam.orthographicSize,
                desiredSize,
                Time.deltaTime * zoomSmoothSpeed
            );
        }
        if (scaleChildrenWithZoom)
        {
            if (childrenScaleRoot != null && childrenScaleRoot.childCount != cachedChildCount)
                CacheZoomScaleChildren();

            ApplyChildScaleWithZoom();
        }
    }

    private float GetMaxOrthoSizeFromBackground()
    {
        if (!hasBackgroundBounds)
            return maxOrthoSize;

        float bgWidth = cachedBackgroundBounds.size.x - backgroundPadding * 2f;
        float bgHeight = cachedBackgroundBounds.size.y - backgroundPadding * 2f;

        bgWidth = Mathf.Max(0.01f, bgWidth);
        bgHeight = Mathf.Max(0.01f, bgHeight);

        float maxSizeFromWidth = (bgWidth * 0.5f) / cam.aspect;
        float maxSizeFromHeight = bgHeight * 0.5f;

        return Mathf.Min(maxSizeFromWidth, maxSizeFromHeight);
    }
    private void TryCacheBackgroundBounds()
    {
        hasBackgroundBounds = false;

        if (!useBackgroundBounds)
            return;

        if (backgroundSpriteRenderer != null)
        {
            cachedBackgroundBounds = backgroundSpriteRenderer.bounds;
            hasBackgroundBounds = true;
            return;
        }

        if (backgroundCollider2D != null)
        {
            cachedBackgroundBounds = backgroundCollider2D.bounds;
            hasBackgroundBounds = true;
            return;
        }
    }
    private void CacheZoomScaleChildren()
    {
        if (childrenScaleRoot == null)
            return;

        if (onlyDirectChildren)
        {
            int count = childrenScaleRoot.childCount;
            zoomScaleChildren = new Transform[count];
            zoomScaleOriginalScales = new Vector3[count];
            cachedChildCount = count;

            for (int i = 0; i < count; i++)
            {
                Transform child = childrenScaleRoot.GetChild(i);
                zoomScaleChildren[i] = child;
                zoomScaleOriginalScales[i] = child.localScale;
            }
        }
        else
        {
            Transform[] allChildren = childrenScaleRoot.GetComponentsInChildren<Transform>(includeInactiveChildren);

            System.Collections.Generic.List<Transform> validChildren = new System.Collections.Generic.List<Transform>();
            System.Collections.Generic.List<Vector3> validScales = new System.Collections.Generic.List<Vector3>();

            for (int i = 0; i < allChildren.Length; i++)
            {
                Transform t = allChildren[i];
                if (t == null || t == childrenScaleRoot)
                    continue;

                validChildren.Add(t);
                validScales.Add(t.localScale);
            }

            zoomScaleChildren = validChildren.ToArray();
            zoomScaleOriginalScales = validScales.ToArray();
            cachedChildCount = childrenScaleRoot.childCount;
        }
    }

    private void ApplyChildScaleWithZoom()
    {
        if (zoomScaleChildren == null || zoomScaleOriginalScales == null)
            return;

        if (referenceOrthoSize <= 0.0001f)
            return;

        float scaleRatio = cam.orthographicSize / referenceOrthoSize;

        if (invertChildScale)
            scaleRatio = referenceOrthoSize / Mathf.Max(cam.orthographicSize, 0.0001f);

        for (int i = 0; i < zoomScaleChildren.Length; i++)
        {
            if (zoomScaleChildren[i] == null)
                continue;

            zoomScaleChildren[i].localScale = zoomScaleOriginalScales[i] * scaleRatio;
        }
    }
    private IEnumerator WaitForSyncedStartAndFindPlayers()
    {
        while (GameSceneStartSync.Instance == null || !GameSceneStartSync.Instance.HasGameStarted())
            yield return null;

        while (!GameSceneStartSync.Instance.HasBeatStarted())
            yield return null;

        while (!TryFindPlayerTargets())
            yield return new WaitForSeconds(retryFindInterval);

        autoFindCoroutine = null;
    }

    private bool TryFindPlayerTargets()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

        if (players == null || players.Length < 2)
            return false;

        // 取前兩個即可；如果你之後場上超過兩個 PLAYER，再改篩選規則
        targetA = players[0] != null ? players[0].transform : null;
        targetB = players[1] != null ? players[1].transform : null;

        if (!IsTargetValid(targetA) || !IsTargetValid(targetB))
            return false;

        CacheCurrentTargetPositions();

        Debug.Log($"DynamicBattleCamera2D 已自動抓到 PLAYER 目標: {targetA.name}, {targetB.name}");
        return true;
    }

    private void CacheCurrentTargetPositions()
    {
        if (targetA != null)
            lastAPosition = targetA.position;

        if (targetB != null)
            lastBPosition = targetB.position;
    }

    private bool IsTargetValid(Transform t)
    {
        return t != null && t.gameObject != null;
    }

    private void UpdateTargetVelocities()
    {
        if (targetA != null)
        {
            Vector3 rawVelocityA = (targetA.position - lastAPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            smoothedVelocityA = Vector3.Lerp(
                smoothedVelocityA,
                rawVelocityA,
                Time.deltaTime * velocitySampleSmoothing
            );
            lastAPosition = targetA.position;
        }

        if (targetB != null)
        {
            Vector3 rawVelocityB = (targetB.position - lastBPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            smoothedVelocityB = Vector3.Lerp(
                smoothedVelocityB,
                rawVelocityB,
                Time.deltaTime * velocitySampleSmoothing
            );
            lastBPosition = targetB.position;
        }
    }

    private Vector3 GetTargetsCenter()
    {
        return (targetA.position + targetB.position) * 0.5f;
    }

    private Vector3 GetLookAheadOffset()
    {
        if (!enableLookAhead)
            return Vector3.zero;

        Vector3 averageVelocity = (smoothedVelocityA + smoothedVelocityB) * 0.5f;

        float lookX = averageVelocity.x * lookAheadStrength * 0.01f;
        lookX = Mathf.Clamp(lookX, -maxLookAheadX, maxLookAheadX);

        return new Vector3(lookX, 0f, 0f);
    }

    private float GetDesiredOrthoSize()
    {
        float distanceX = Mathf.Abs(targetA.position.x - targetB.position.x);
        float distanceY = Mathf.Abs(targetA.position.y - targetB.position.y);

        float sizeFromWidth = ((distanceX + horizontalPadding) * 0.5f) / cam.aspect;
        float sizeFromHeight = (distanceY + verticalPadding) * 0.5f;

        float distanceBasedSize = Mathf.Max(sizeFromWidth, sizeFromHeight);
        distanceBasedSize = Mathf.Max(distanceBasedSize, minOrthoSize);

        float extraSize = distanceX * distanceToSizeMultiplier * 0.1f;
        float finalSize = distanceBasedSize + extraSize;

        // 原本設定的最大值
        float maxSizeLimit = maxOrthoSize;

        // 如果有背景 bounds，再進一步壓到背景允許範圍內
        if (hasBackgroundBounds)
            maxSizeLimit = Mathf.Min(maxSizeLimit, GetMaxOrthoSizeFromBackground());

        finalSize = Mathf.Min(finalSize, maxSizeLimit);
        finalSize = Mathf.Max(finalSize, minOrthoSize);

        return finalSize;
    }

    private Vector3 ClampCameraPositionToBounds(Vector3 desiredPosition, float orthoSize)
    {
        float halfHeight = orthoSize;
        float halfWidth = orthoSize * cam.aspect;

        // 優先用背景 bounds
        if (hasBackgroundBounds)
        {
            float left = cachedBackgroundBounds.min.x + backgroundPadding;
            float right = cachedBackgroundBounds.max.x - backgroundPadding;
            float bottom = cachedBackgroundBounds.min.y + backgroundPadding;
            float top = cachedBackgroundBounds.max.y - backgroundPadding;

            float minCamX = left + halfWidth;
            float maxCamX = right - halfWidth;
            float minCamY = bottom + halfHeight;
            float maxCamY = top - halfHeight;

            // 如果背景比鏡頭還小，就直接鎖中間
            float clampedX;
            if (minCamX > maxCamX)
                clampedX = (left + right) * 0.5f;
            else
                clampedX = Mathf.Clamp(desiredPosition.x, minCamX, maxCamX);

            float clampedY;
            if (minCamY > maxCamY)
                clampedY = (bottom + top) * 0.5f;
            else
                clampedY = Mathf.Clamp(desiredPosition.y, minCamY, maxCamY);

            return new Vector3(clampedX, clampedY, desiredPosition.z);
        }

        // 沒背景 bounds 時，退回手動 bounds
        if (useCameraBounds)
        {
            float clampedX = Mathf.Clamp(desiredPosition.x, minX + halfWidth, maxX - halfWidth);
            float clampedY = Mathf.Clamp(desiredPosition.y, minY + halfHeight, maxY - halfHeight);
            return new Vector3(clampedX, clampedY, desiredPosition.z);
        }

        return desiredPosition;
    }

    public void SetTargets(Transform a, Transform b)
    {
        targetA = a;
        targetB = b;
        CacheCurrentTargetPositions();
    }

    public void SetTargetA(Transform a)
    {
        targetA = a;
        if (targetA != null)
            lastAPosition = targetA.position;
    }

    public void SetTargetB(Transform b)
    {
        targetB = b;
        if (targetB != null)
            lastBPosition = targetB.position;
    }

    public Transform GetTargetA()
    {
        return targetA;
    }

    public Transform GetTargetB()
    {
        return targetB;
    }
}