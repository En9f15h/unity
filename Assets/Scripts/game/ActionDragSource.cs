using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ActionDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject dragPrefab;

    [Header("行動資料")]
    [SerializeField] private ActionData actionData;
    [SerializeField] private ActionType actionType = ActionType.None;

    public static bool GlobalDragEnabled = true;
    public static GameObject CurrentDraggedClone;

    private GameObject draggingObject;
    private RectTransform draggingRect;
    private CanvasGroup draggingCanvasGroup;
    private CanvasGroup sourceCanvasGroup;
    private Graphic[] graphics;
    private bool dragBlockedThisTime = false;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        sourceCanvasGroup = GetComponent<CanvasGroup>();
        if (sourceCanvasGroup == null)
            sourceCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        graphics = GetComponentsInChildren<Graphic>(true);
    }

    public void Init(ActionData data)
    {
        actionData = data;

        // 不信任 prefab 原本序列化的 actionType，強制依 data 決定
        if (data is UltimateActionData)
            actionType = ActionType.Ultimate;
        else
            actionType = data != null ? data.actionType : ActionType.None;

        // 同步自己身上的 ActionDragData
        ActionDragData selfDragData = GetComponent<ActionDragData>();
        if (selfDragData == null)
            selfDragData = GetComponentInChildren<ActionDragData>(true);

        if (selfDragData != null)
            selfDragData.actionType = actionType;

        Debug.Log($"ActionDragSource.Init -> {gameObject.name}, final actionType={actionType}, dataType={(data != null ? data.GetType().Name : "NULL")}", this);
    }

    public void SetCanvas(Canvas c)
    {
        canvas = c;
    }

    public ActionData GetActionData()
    {
        return actionData;
    }

    public ActionType GetActionType()
    {
        return actionType;
    }

    private bool IsUltimateAction()
    {
        if (actionType == ActionType.Ultimate)
            return true;

        if (actionData is UltimateActionData)
            return true;

        if (actionData != null && actionData.actionType == ActionType.Ultimate)
            return true;

        ActionDragData dragData = GetComponent<ActionDragData>();
        if (dragData == null)
            dragData = GetComponentInChildren<ActionDragData>(true);

        if (dragData != null && dragData.actionType == ActionType.Ultimate)
            return true;

        return false;
    }
    private bool CanStartNow()
    {
        if (!GlobalDragEnabled)
            return false;

        if (actionData == null)
            return false;

        // 非大招直接可用
        if (!IsUltimateAction())
            return true;

        if (TurnPlanningManager.Instance == null)
            return false;

        // 這裡不要再呼叫 CanDragOrPlaceAction，避免每幀刷 log
        if (!TurnPlanningManager.Instance.CanUseUltimate())
            return false;

        if (TurnPlanningManager.Instance.HasQueuedUltimate())
            return false;

        return true;
    }
    private void Update()
    {
        if (sourceCanvasGroup == null)
            return;

        bool canUse = true;

        if (IsUltimateAction())
        {
            canUse = CanStartNow();
            sourceCanvasGroup.alpha = canUse ? 1f : 0.4f;
        }
        else
        {
            sourceCanvasGroup.alpha = 1f;
        }

        sourceCanvasGroup.blocksRaycasts = canUse;
        sourceCanvasGroup.interactable = canUse;

        if (graphics != null)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = canUse;
            }
        }
    }

    private GameObject CreateDraggedObject()
    {
        GameObject prefabToClone = dragPrefab != null ? dragPrefab : gameObject;

        GameObject obj = Instantiate(prefabToClone, canvas.transform);
        obj.transform.SetAsLastSibling();

        ActionDragSource cloneDragSource = obj.GetComponent<ActionDragSource>();
        if (cloneDragSource != null)
            cloneDragSource.enabled = false;

        ActionDragData[] dragDatas = obj.GetComponentsInChildren<ActionDragData>(true);
        for (int i = 0; i < dragDatas.Length; i++)
        {
            if (dragDatas[i] != null)
                dragDatas[i].actionType = actionType;
        }

        DraggableItem item = obj.GetComponent<DraggableItem>();
        if (item == null)
            item = obj.AddComponent<DraggableItem>();

        item.Setup(actionData, actionType);
        item.IsDroppedInSlot = false;
        return obj;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!CanStartNow())
            return;

        if (canvas == null)
            return;

        GameObject obj = CreateDraggedObject();
        if (obj == null)
            return;

        DraggableItem item = obj.GetComponent<DraggableItem>();
        if (item == null)
        {
            Destroy(obj);
            return;
        }

        ActionSlot[] slots = FindObjectsByType<ActionSlot>(FindObjectsSortMode.None);
        Array.Sort(slots, (a, b) => a.slotIndex.CompareTo(b.slotIndex));

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (slots[i].TryAutoPlaceNewObject(obj, actionData, actionType))
                return;
        }

        Destroy(obj);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragBlockedThisTime = false;

        if (!CanStartNow())
        {
            dragBlockedThisTime = true;
            return;
        }

        if (canvas == null)
            return;

        draggingObject = CreateDraggedObject();
        if (draggingObject == null)
            return;

        draggingRect = draggingObject.GetComponent<RectTransform>();
        draggingCanvasGroup = draggingObject.GetComponent<CanvasGroup>();

        if (draggingCanvasGroup == null)
            draggingCanvasGroup = draggingObject.AddComponent<CanvasGroup>();

        CurrentDraggedClone = draggingObject;

        draggingCanvasGroup.alpha = 0.85f;
        draggingCanvasGroup.blocksRaycasts = false;

        RectTransform sourceRect = GetComponent<RectTransform>();
        if (draggingRect != null)
        {
            draggingRect.localScale = Vector3.one;
            draggingRect.localRotation = Quaternion.identity;

            float width = 100f;
            float height = 100f;

            if (sourceRect != null)
            {
                if (sourceRect.rect.width > 0) width = sourceRect.rect.width;
                if (sourceRect.rect.height > 0) height = sourceRect.rect.height;
            }

            draggingRect.anchorMin = new Vector2(0.5f, 0.5f);
            draggingRect.anchorMax = new Vector2(0.5f, 0.5f);
            draggingRect.pivot = new Vector2(0.5f, 0.5f);

            draggingRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            draggingRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        UpdateDraggedPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragBlockedThisTime)
            return;

        if (draggingRect != null)
            UpdateDraggedPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragBlockedThisTime)
        {
            dragBlockedThisTime = false;
            return;
        }

        if (draggingObject == null)
        {
            CurrentDraggedClone = null;
            return;
        }

        if (draggingCanvasGroup != null)
        {
            draggingCanvasGroup.alpha = 1f;
            draggingCanvasGroup.blocksRaycasts = true;
        }

        DraggableItem item = draggingObject.GetComponent<DraggableItem>();
        if (item != null && !item.IsDroppedInSlot)
            Destroy(draggingObject);

        draggingObject = null;
        draggingRect = null;
        draggingCanvasGroup = null;
        CurrentDraggedClone = null;
    }

    private void UpdateDraggedPosition(PointerEventData eventData)
    {
        if (canvas == null || draggingRect == null)
            return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            draggingRect.localPosition = localPoint;
        }
    }
}