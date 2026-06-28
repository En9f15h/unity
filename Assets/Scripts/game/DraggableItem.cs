using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool IsDroppedInSlot { get; set; }

    private ActionData actionData;
    private ActionType actionType = ActionType.None;
    private ActionSlot ownerSlot;
    private ActionSlot dragStartSlot;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;

    public void Setup(ActionData data, ActionType type)
    {
        actionData = data;
        actionType = type;
    }

    public ActionData GetActionData()
    {
        return actionData;
    }

    public ActionType GetActionType()
    {
        return actionType;
    }

    public void MarkDroppedInSlot(ActionSlot slot)
    {
        ownerSlot = slot;
        IsDroppedInSlot = true;
    }

    public ActionSlot GetOwnerSlot()
    {
        return ownerSlot;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ownerSlot == null)
            return;

        if (!ActionDragSource.GlobalDragEnabled)
            return;

        dragStartSlot = ownerSlot;
        ownerSlot.DetachPlacedItemForDrag(this);

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        ActionDragSource.CurrentDraggedClone = gameObject;
        IsDroppedInSlot = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragStartSlot == null || rectTransform == null || rootCanvas == null)
            return;

        RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            rectTransform.localPosition = localPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        ActionDragSource.CurrentDraggedClone = null;

        // 沒有成功放到其他 slot，就回原位
        if (!IsDroppedInSlot && dragStartSlot != null)
        {
            dragStartSlot.RestoreDraggedItem(this);
        }

        dragStartSlot = null;
    }
}