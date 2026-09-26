using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ActionDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject dragPrefab;

    [Header("Action Data")]
    [SerializeField] private ActionData actionData;
    [SerializeField] private ActionType actionType = ActionType.None;

    public static bool GlobalDragEnabled = true;
    public static GameObject CurrentDraggedClone;

    private GameObject draggingObject;
    private RectTransform draggingRect;
    private CanvasGroup draggingCanvasGroup;
    private CanvasGroup sourceCanvasGroup;
    private Graphic[] graphics;
    private ActionButtonView buttonView;
    private bool dragBlockedThisTime = false;
    private bool showingHoverRange;
    private bool pointerInside;
    private PaletteInteractionFeedback interactionFeedback;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        sourceCanvasGroup = GetComponent<CanvasGroup>();
        if (sourceCanvasGroup == null)
            sourceCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        graphics = GetComponentsInChildren<Graphic>(true);
        buttonView = GetComponent<ActionButtonView>();
        if (buttonView == null)
            buttonView = GetComponentInChildren<ActionButtonView>(true);

        if (buttonView == null)
            buttonView = gameObject.AddComponent<ActionButtonView>();
        interactionFeedback = PaletteInteractionFeedback.Ensure(this);
    }

    public void Init(ActionData data)
    {
        actionData = data;

        // Force the action type from data instead of trusting prefab serialization.
        actionType = data != null ? data.actionType : ActionType.None;
        if (actionType == ActionType.None && data is UltimateActionData)
            actionType = ActionType.Ultimate;

        if (data != null && data.sourcePrefab != null)
            dragPrefab = data.sourcePrefab;

        // Sync the ActionDragData on this object.
        ActionDragData selfDragData = GetComponent<ActionDragData>();
        if (selfDragData == null)
            selfDragData = GetComponentInChildren<ActionDragData>(true);

        if (selfDragData != null)
            selfDragData.actionType = actionType;

        if (buttonView == null)
            buttonView = GetComponentInChildren<ActionButtonView>(true);

        if (buttonView != null)
            buttonView.Bind(data);

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

    private void OnDisable()
    {
        pointerInside = false;
        ClearHoveredAttackRange();
    }

    private void OnDestroy()
    {
        pointerInside = false;
        ClearHoveredAttackRange();
    }

    private bool IsUltimateAction()
    {
        if (actionType == ActionType.Ultimate)
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
    private bool CanUseAsActualAction()
    {
        if (!GlobalDragEnabled)
            return false;

        if (actionData == null)
            return false;

        if (TurnPlanningManager.Instance == null)
            return !IsUltimateAction();

        return TurnPlanningManager.Instance.CanDragOrPlaceAction(actionData);
    }

    private bool CanStartDragNow()
    {
        if (!GlobalDragEnabled)
            return false;

        if (actionData == null)
            return false;

        if (TurnPlanningManager.Instance == null)
            return !IsUltimateAction();

        return TurnPlanningManager.Instance.CanStartActionPaletteDrag(actionData);
    }
    private void Update()
    {
        if (sourceCanvasGroup == null)
            return;

        bool canUse = CanUseAsActualAction();
        bool canDrag = CanStartDragNow();
        interactionFeedback.SetAvailability(canUse, canDrag);
        bool cooldown = TurnPlanningManager.Instance != null && TurnPlanningManager.Instance.IsActionOnCooldown(actionData);
        bool used = TurnPlanningManager.Instance != null && TurnPlanningManager.Instance.IsActionUsed(actionData);

        if (showingHoverRange && !canDrag)
            ClearHoveredAttackRange();
        else if (pointerInside && canDrag && !showingHoverRange)
            TryShowHoveredAttackRange();

        sourceCanvasGroup.alpha = canUse ? 1f : 0.4f;

        sourceCanvasGroup.blocksRaycasts = canDrag;
        sourceCanvasGroup.interactable = canDrag;

        if (buttonView != null)
            buttonView.SetAvailability(canUse, cooldown, used);

        if (graphics != null)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = canDrag;
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

        if (!CanUseAsActualAction())
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        TryShowHoveredAttackRange();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        ClearHoveredAttackRange();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragBlockedThisTime = false;

        if (!CanStartDragNow())
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

        if (draggingRect != null)
        {
            draggingRect.localScale = Vector3.one;
            draggingRect.localRotation = Quaternion.identity;

            Vector2 dragSize = ActionDragData.ResolveDragSize(draggingObject);

            draggingRect.anchorMin = new Vector2(0.5f, 0.5f);
            draggingRect.anchorMax = new Vector2(0.5f, 0.5f);
            draggingRect.pivot = new Vector2(0.5f, 0.5f);

            draggingRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dragSize.x);
            draggingRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dragSize.y);
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
        interactionFeedback.ResetVisuals();
        pointerInside = false;
        ClearHoveredAttackRange();

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

    private void TryShowHoveredAttackRange()
    {
        if (!CanStartDragNow())
        {
            ClearHoveredAttackRange();
            return;
        }

        TurnPlanningManager manager = TurnPlanningManager.Instance;
        if (manager == null)
            return;

        showingHoverRange = manager.ShowHoveredAttackRange(actionData);
    }

    private void ClearHoveredAttackRange()
    {
        if (!showingHoverRange)
            return;

        TurnPlanningManager manager = TurnPlanningManager.Instance;
        if (manager != null)
            manager.ClearHoveredAttackRange(actionData);

        showingHoverRange = false;
    }

}
