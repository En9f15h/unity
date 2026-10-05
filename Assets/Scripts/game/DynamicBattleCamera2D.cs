using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DynamicBattleCamera2D : MonoBehaviour
{
    [Header("Auto-Find Characters")]
    [SerializeField] private bool autoFindPlayersOnSyncedStart = true;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Track the tagged Root inside each character, rather than its outer player object.")]
    [SerializeField] private string rootTag = "Root";
    [SerializeField] private float retryFindInterval = 0.5f;
    [Header("Child Scaling With Zoom")]
    [SerializeField] private bool scaleChildrenWithZoom = true;

    [Tooltip("Uses the Camera transform as the root when not assigned.")]
    [SerializeField] private Transform childrenScaleRoot;

    [Tooltip("true = scale only direct children; false = scale all descendants.")]
    [SerializeField] private bool onlyDirectChildren = true;

    [SerializeField] private bool includeInactiveChildren = true;

    [Tooltip("Enable this if child scaling moves in the opposite direction.")]
    [SerializeField] private bool invertChildScale = false;
    [Header("Tracking Targets")]
    [SerializeField] private Transform targetA;
    [SerializeField] private Transform targetB;

    [Header("Follow Settings")]
    [SerializeField] private float followSmoothTime = 0.18f;
    [SerializeField] private Vector3 baseOffset = new Vector3(0f, 0f, -10f);

    [Header("Look-Ahead Settings")]
    [SerializeField] private bool enableLookAhead = true;
    [SerializeField] private float lookAheadStrength = 0.35f;
    [SerializeField] private float maxLookAheadX = 1.25f;
    [SerializeField] private float velocitySampleSmoothing = 12f;

    [Header("Zoom Settings")]
    [SerializeField] private bool autoZoom = true;
    [SerializeField] private float distanceToSizeMultiplier = 0.45f;
    [SerializeField] private float zoomSmoothSpeed = 5f;

    [Tooltip("Minimum camera size. Smaller values zoom closer.")]
    [SerializeField] private float minOrthoSize = 3.5f;

    [Tooltip("Maximum camera size. If <= 0, Start uses the initial camera size.")]
    [SerializeField] private float maxOrthoSize = 0f;

    [Header("Safe Padding")]
    [SerializeField] private float horizontalPadding = 2f;
    [SerializeField] private float verticalPadding = 1f;
    [Header("Background Bounds Limit")]
    [SerializeField] private bool useBackgroundBounds = true;
    [SerializeField] private SpriteRenderer backgroundSpriteRenderer;
    [SerializeField] private Collider2D backgroundCollider2D;

    [Tooltip("Inset from the background bounds so the camera does not hug the edge.")]
    [SerializeField] private float backgroundPadding = 0f;
    [Header("Optional Arena Bounds")]
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
            Debug.LogWarning("DynamicBattleCamera2D is intended for an Orthographic Camera.");
        }

        if (maxOrthoSize <= 0f)
            maxOrthoSize = cam.orthographicSize;
        TryCacheBackgroundBounds();
        referenceOrthoSize = cam.orthographicSize;

        if (childrenScaleRoot == null)
            childrenScaleRoot = transform;

        CacheZoomScaleChildren();

        SetTargets(targetA, targetB);

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

        Transform first = null;
        foreach (GameObject player in players)
        {
            if (player.scene != gameObject.scene) continue;
            Transform root = FindTrackingRoot(player.transform);
            if (!IsTargetValid(root)) continue;
            if (first == null) first = root;
            else if (root != first)
            {
                SetTargets(first, root);
                Debug.Log($"DynamicBattleCamera2D found character Root targets: {targetA.name}, {targetB.name}");
                return true;
            }
        }
        return false;
    }

    private Transform FindTrackingRoot(Transform character)
    {
        if (character == null) return null;
        foreach (Transform child in character.GetComponentsInChildren<Transform>(true))
            if (child.CompareTag(rootTag)) return child;
        return null;
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
        return t != null && t.gameObject.activeInHierarchy;
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

        // Existing configured maximum value.
        float maxSizeLimit = maxOrthoSize;

        // Clamp further when background bounds are available.
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

        // Prefer background bounds when available.
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

            // If the background is smaller than the camera, lock to center.
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

        // Fall back to manual bounds when background bounds are unavailable.
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
        targetA = FindTrackingRoot(a);
        targetB = FindTrackingRoot(b);
        smoothedVelocityA = smoothedVelocityB = Vector3.zero;
        CacheCurrentTargetPositions();
    }

    public void SetTargetA(Transform a)
    {
        targetA = FindTrackingRoot(a);
        smoothedVelocityA = Vector3.zero;
        if (targetA != null)
            lastAPosition = targetA.position;
    }

    public void SetTargetB(Transform b)
    {
        targetB = FindTrackingRoot(b);
        smoothedVelocityB = Vector3.zero;
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
