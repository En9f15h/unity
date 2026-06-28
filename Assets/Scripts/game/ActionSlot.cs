using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ActionSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("第幾格")]
    public int slotIndex;

    [Header("顯示序號文字")]
    [SerializeField] private Text indexText;

    [Header("技能內容容器")]
    [SerializeField] private RectTransform contentRoot;

    [Header("HeavyAttack 佔用下一格")]
    [SerializeField] private ActionSlot nextSlot;

    [Header("半透明鎖定遮罩")]
    [SerializeField] private GameObject lockOverlay;

    [Header("目前放入的行動資料")]
    [SerializeField] private ActionData currentActionData;

    private ActionType currentActionType = ActionType.None;
    private GameObject currentPlacedObject;

    public bool IsOccupiedByHeavyExtension { get; private set; } = false;

    private void Start()
    {
        RefreshIndexText();
        RefreshLockOverlay();
        CacheCurrentPlacedObject();
    }

    public ActionData GetCurrentActionData()
    {
        return currentActionData;
    }

    public ActionType GetCurrentActionType()
    {
        return currentActionType;
    }

    public void ClearCurrentActionData()
    {
        currentActionData = null;
        currentActionType = ActionType.None;
    }

    public void SetIndex(int index)
    {
        slotIndex = index;
        RefreshIndexText();
    }

    public void SetNextSlot(ActionSlot slot)
    {
        nextSlot = slot;
    }

    private RectTransform GetTargetRect()
    {
        return contentRoot != null ? contentRoot : GetComponent<RectTransform>();
    }

    private void RefreshIndexText()
    {
        if (indexText != null)
            indexText.text = (slotIndex + 1).ToString();
    }

    private void RefreshLockOverlay()
    {
        if (lockOverlay != null)
            lockOverlay.SetActive(IsOccupiedByHeavyExtension);
    }

    public void SetHeavyExtensionOccupied(bool value)
    {
        IsOccupiedByHeavyExtension = value;
        RefreshLockOverlay();
    }

    public bool HasPlacedItem()
    {
        CacheCurrentPlacedObject();
        return currentPlacedObject != null;
    }

    private void CacheCurrentPlacedObject()
    {
        if (currentPlacedObject != null)
            return;

        RectTransform root = GetTargetRect();
        DraggableItem placed = root.GetComponentInChildren<DraggableItem>(true);
        if (placed != null)
        {
            currentPlacedObject = placed.gameObject;

            if (currentActionData == null)
                currentActionData = placed.GetActionData();

            if (currentActionType == ActionType.None)
                currentActionType = placed.GetActionType();
        }
    }

    private bool RequiresTwoSlots(ActionType type)
    {
        return type == ActionType.HeavyAttack;
    }

    private bool CanPlaceHeavyHere(ActionSlot ignoreSlot = null)
    {
        if (nextSlot == null)
            return false;

        if (nextSlot == ignoreSlot)
            return true;

        if (nextSlot.IsOccupiedByHeavyExtension)
            return false;

        if (nextSlot.HasPlacedItem())
            return false;

        return true;
    }

    public bool CanAcceptAction(ActionData actionData, ActionType actionType, ActionSlot fromSlot = null, bool forAutoFill = false)
    {
        if (actionData == null)
            return false;

        if (IsOccupiedByHeavyExtension && fromSlot != this)
            return false;

        if (TurnPlanningManager.Instance != null &&
            !TurnPlanningManager.Instance.CanDragOrPlaceAction(actionData, this))
            return false;

        if (forAutoFill && HasPlacedItem())
            return false;

        if (RequiresTwoSlots(actionType))
        {
            ActionSlot ignore = null;
            if (fromSlot != null && fromSlot.nextSlot == nextSlot)
                ignore = fromSlot;

            if (!CanPlaceHeavyHere(ignore))
                return false;
        }

        return true;
    }

    private void ApplyPlacement(GameObject obj, ActionData actionData, ActionType actionType)
    {
        RectTransform targetRect = GetTargetRect();
        RectTransform droppedRect = obj.GetComponent<RectTransform>();

        if (droppedRect == null || targetRect == null)
            return;

        droppedRect.SetParent(targetRect, false);
        droppedRect.anchoredPosition = Vector2.zero;
        droppedRect.localScale = Vector3.one;
        droppedRect.localRotation = Quaternion.identity;

        droppedRect.anchorMin = new Vector2(0.5f, 0.5f);
        droppedRect.anchorMax = new Vector2(0.5f, 0.5f);
        droppedRect.pivot = new Vector2(0.5f, 0.5f);

        droppedRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetRect.rect.width);
        droppedRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetRect.rect.height);

        currentPlacedObject = obj;
        currentActionData = actionData;
        currentActionType = actionType;

        DraggableItem item = obj.GetComponent<DraggableItem>();
        if (item != null)
            item.MarkDroppedInSlot(this);

        if (RequiresTwoSlots(actionType) && nextSlot != null)
            nextSlot.SetHeavyExtensionOccupied(true);
    }

    public void DetachPlacedItemForDrag(DraggableItem item)
    {
        if (item == null)
            return;

        if (currentPlacedObject == item.gameObject)
        {
            if (RequiresTwoSlots(currentActionType) && nextSlot != null)
                nextSlot.SetHeavyExtensionOccupied(false);

            currentPlacedObject = null;
            currentActionData = null;
            currentActionType = ActionType.None;
        }
    }

    public void RestoreDraggedItem(DraggableItem item)
    {
        if (item == null)
            return;

        ApplyPlacement(item.gameObject, item.GetActionData(), item.GetActionType());
    }

    public bool TryAutoPlaceNewObject(GameObject obj, ActionData actionData, ActionType actionType)
    {
        if (!CanAcceptAction(actionData, actionType, null, true))
            return false;

        ApplyPlacement(obj, actionData, actionType);
        return true;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // 來源拖曳時要優先吃 clone，不要先吃 pointerDrag
        GameObject dropped = ActionDragSource.CurrentDraggedClone;

        // 如果沒有 clone，才代表可能是 slot 裡的物件互相拖曳
        if (dropped == null)
            dropped = eventData.pointerDrag;

        if (dropped == null)
        {
            Debug.Log("OnDrop 失敗：找不到拖曳物件");
            return;
        }

        if (IsOccupiedByHeavyExtension)
        {
            Debug.Log("這格被 HeavyAttack 佔用，不能放置");
            return;
        }

        DraggableItem incomingItem = dropped.GetComponent<DraggableItem>();
        if (incomingItem == null)
        {
            Debug.Log("OnDrop 失敗：拖曳物沒有 DraggableItem");
            return;
        }

        ActionData incomingData = incomingItem.GetActionData();
        ActionType incomingType = incomingItem.GetActionType();
        ActionSlot fromSlot = incomingItem.GetOwnerSlot();

        if (incomingData == null)
        {
            Debug.Log("OnDrop 失敗：incomingData 為空");
            return;
        }

        if (!CanAcceptAction(incomingData, incomingType, fromSlot, false))
        {
            Debug.Log("這個行動現在不能放入");
            return;
        }

        // 拖回原位
        if (fromSlot == this)
        {
            RestoreDraggedItem(incomingItem);
            return;
        }

        // 目標為空：直接放
        if (!HasPlacedItem())
        {
            if (fromSlot != null)
                fromSlot.DetachPlacedItemForDrag(incomingItem);

            ApplyPlacement(dropped, incomingData, incomingType);
            return;
        }

        // 目標有東西：做交換
        DraggableItem targetItem = GetTargetRect().GetComponentInChildren<DraggableItem>(true);
        if (targetItem == null)
        {
            if (fromSlot != null)
                fromSlot.DetachPlacedItemForDrag(incomingItem);

            ApplyPlacement(dropped, incomingData, incomingType);
            return;
        }

        ActionData targetData = targetItem.GetActionData();
        ActionType targetType = targetItem.GetActionType();

        // 來源是技能欄拖進來：直接覆蓋
        if (fromSlot == null)
        {
            ClearPlacedItemOnly();
            ApplyPlacement(dropped, incomingData, incomingType);
            return;
        }

        // 交換前檢查來源格能不能接受對方
        if (!fromSlot.CanAcceptAction(targetData, targetType, this, false))
        {
            Debug.Log("交換失敗：來源格不能接受目標行動");
            fromSlot.RestoreDraggedItem(incomingItem);
            return;
        }

        this.DetachPlacedItemForDrag(targetItem);
        fromSlot.DetachPlacedItemForDrag(incomingItem);

        ApplyPlacement(dropped, incomingData, incomingType);
        fromSlot.ApplyPlacement(targetItem.gameObject, targetData, targetType);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (HasPlacedItem())
            ClearPlacedItemOnly();
    }

    public void ClearPlacedItemOnly()
    {
        CacheCurrentPlacedObject();

        if (currentPlacedObject != null)
        {
            if (RequiresTwoSlots(currentActionType) && nextSlot != null)
                nextSlot.SetHeavyExtensionOccupied(false);

            Destroy(currentPlacedObject);
        }

        currentPlacedObject = null;
        currentActionData = null;
        currentActionType = ActionType.None;
    }
}