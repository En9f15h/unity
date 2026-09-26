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
    private ClaimSlot ownerClaimSlot;
    private ClaimSlot dragStartClaimSlot;

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
        ownerClaimSlot = null;
        IsDroppedInSlot = true;
    }

    public ActionSlot GetOwnerSlot()
    {
        return ownerSlot;
    }

    public void MarkDroppedInClaimSlot(ClaimSlot slot)
    {
        ownerSlot = null;
        ownerClaimSlot = slot;
        IsDroppedInSlot = true;
    }

    public ClaimSlot GetOwnerClaimSlot()
    {
        return ownerClaimSlot;
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
        if (ownerSlot == null && ownerClaimSlot == null)
            return;

        if (!ActionDragSource.GlobalDragEnabled)
            return;

        dragStartSlot = ownerSlot;
        dragStartClaimSlot = ownerClaimSlot;

        if (ownerSlot != null)
            ownerSlot.DetachPlacedItemForDrag(this);
        else if (ownerClaimSlot != null)
            ownerClaimSlot.DetachPlacedItemForDrag(this);

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();

        ApplyDragPresentationSize();

        canvasGroup.blocksRaycasts = false;
        ActionDragSource.CurrentDraggedClone = gameObject;
        IsDroppedInSlot = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragStartSlot == null && dragStartClaimSlot == null)
            return;

        if (rectTransform == null || rootCanvas == null)
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


        if (!IsDroppedInSlot && dragStartSlot != null)
        {
            dragStartSlot.RestoreDraggedItem(this);
        }
        else if (!IsDroppedInSlot && dragStartClaimSlot != null)
        {
            dragStartClaimSlot.RestoreDraggedItem(this);
        }

        dragStartSlot = null;
        dragStartClaimSlot = null;
    }

    private void ApplyDragPresentationSize()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (rectTransform == null)
            return;

        Vector2 dragSize = ActionDragData.ResolveDragSize(gameObject);

        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dragSize.x);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dragSize.y);
    }
}
