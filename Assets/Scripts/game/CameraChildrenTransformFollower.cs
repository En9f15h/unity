using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraChildrenTransformFollower : MonoBehaviour
{
    [Header("Child Root To Adjust")]
    [SerializeField] private Transform childrenRoot;

    [Header("Search Settings")]
    [SerializeField] private bool onlyDirectChildren = true;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Camera Change Adjustment")]
    [SerializeField] private bool adjustPositionWhenCameraMoves = true;
    [SerializeField] private bool adjustPositionWhenCameraZooms = true;
    [SerializeField] private bool adjustScaleWhenCameraZooms = true;

    [Header("Position Adjustment Multipliers")]
    [SerializeField] private float movePositionFactorX = 0.15f;
    [SerializeField] private float movePositionFactorY = 0.15f;
    [SerializeField] private float zoomPositionFactor = 1f;

    [Header("Scale Settings")]
    [SerializeField] private bool invertScaleWithZoom = false;

    [Header("Smoothing")]
    [SerializeField] private bool smoothTransform = true;
    [SerializeField] private float positionSmoothSpeed = 12f;
    [SerializeField] private float scaleSmoothSpeed = 12f;

    private Camera cam;
    private Vector3 referenceCameraPosition;
    private float referenceOrthoSize;

    private readonly List<Transform> trackedChildren = new List<Transform>();
    private readonly List<Vector3> originalLocalPositions = new List<Vector3>();
    private readonly List<Vector3> originalLocalScales = new List<Vector3>();

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (childrenRoot == null)
            childrenRoot = transform;
    }

    private void Start()
    {
        referenceCameraPosition = transform.position;
        referenceOrthoSize = cam.orthographicSize;

        CacheChildren();
    }

    private void LateUpdate()
    {
        if (childrenRoot == null)
            return;

        if (ChildrenCountChanged())
            CacheChildren();

        UpdateChildrenTransforms();
    }

    [ContextMenu("Refresh Child Cache")]
    public void CacheChildren()
    {
        trackedChildren.Clear();
        originalLocalPositions.Clear();
        originalLocalScales.Clear();

        if (childrenRoot == null)
            return;

        if (onlyDirectChildren)
        {
            for (int i = 0; i < childrenRoot.childCount; i++)
            {
                Transform child = childrenRoot.GetChild(i);
                if (child == null)
                    continue;

                trackedChildren.Add(child);
                originalLocalPositions.Add(child.localPosition);
                originalLocalScales.Add(child.localScale);
            }
        }
        else
        {
            Transform[] all = childrenRoot.GetComponentsInChildren<Transform>(includeInactiveChildren);

            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];
                if (child == null || child == childrenRoot)
                    continue;

                trackedChildren.Add(child);
                originalLocalPositions.Add(child.localPosition);
                originalLocalScales.Add(child.localScale);
            }
        }
    }

    private bool ChildrenCountChanged()
    {
        if (childrenRoot == null)
            return false;

        if (onlyDirectChildren)
            return trackedChildren.Count != childrenRoot.childCount;

        int expected = childrenRoot.GetComponentsInChildren<Transform>(includeInactiveChildren).Length - 1;
        return trackedChildren.Count != expected;
    }

    private void UpdateChildrenTransforms()
    {
        Vector3 cameraDelta = transform.position - referenceCameraPosition;

        float zoomRatio = 1f;
        if (referenceOrthoSize > 0.0001f)
            zoomRatio = cam.orthographicSize / referenceOrthoSize;

        float scaleRatio = invertScaleWithZoom
            ? (referenceOrthoSize / Mathf.Max(cam.orthographicSize, 0.0001f))
            : zoomRatio;

        for (int i = 0; i < trackedChildren.Count; i++)
        {
            Transform child = trackedChildren[i];
            if (child == null)
                continue;

            Vector3 targetLocalPos = originalLocalPositions[i];
            Vector3 targetLocalScale = originalLocalScales[i];

            // Adjust position while the camera moves.
            if (adjustPositionWhenCameraMoves)
            {
                targetLocalPos += new Vector3(
                    cameraDelta.x * movePositionFactorX,
                    cameraDelta.y * movePositionFactorY,
                    0f
                );
            }

            // Adjust position while the camera zooms.
            if (adjustPositionWhenCameraZooms)
            {
                targetLocalPos = new Vector3(
                    targetLocalPos.x * zoomRatio * zoomPositionFactor,
                    targetLocalPos.y * zoomRatio * zoomPositionFactor,
                    targetLocalPos.z
                );
            }

            // Adjust scale while the camera zooms.
            if (adjustScaleWhenCameraZooms)
            {
                targetLocalScale = originalLocalScales[i] * scaleRatio;
            }

            if (smoothTransform)
            {
                child.localPosition = Vector3.Lerp(
                    child.localPosition,
                    targetLocalPos,
                    Time.deltaTime * positionSmoothSpeed
                );

                child.localScale = Vector3.Lerp(
                    child.localScale,
                    targetLocalScale,
                    Time.deltaTime * scaleSmoothSpeed
                );
            }
            else
            {
                child.localPosition = targetLocalPos;
                child.localScale = targetLocalScale;
            }
        }
    }

    [ContextMenu("Set Current State As New Baseline")]
    public void RebuildReference()
    {
        referenceCameraPosition = transform.position;
        referenceOrthoSize = cam.orthographicSize;
        CacheChildren();
    }
}
