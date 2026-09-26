using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class ClaimController : MonoBehaviour
{
    [Header("Local Claim Layout")]
    [SerializeField] private bool autoAlignLocalBubbleToActionSlots;
    [SerializeField] private bool autoAlignEnemyBubbleToPreviewRoot;
    [SerializeField] private bool autoPositionClaimVisibilityToggle;
    [SerializeField] private bool matchLocalClaimSlotSizeToActionSlots = true;
    [SerializeField] private Vector2 localClaimSlotSize = new Vector2(100f, 100f);
    [SerializeField] private Vector2 enemyClaimSlotSize = new Vector2(100f, 100f);
    [SerializeField] private float enemyClaimBubbleLeftInset = 28f;
    [SerializeField] private float claimRowVerticalSpacing = 12f;
    [SerializeField] private Vector2 claimVisibilityToggleSize = new Vector2(90f, 32f);
    [SerializeField] private Vector3 claimVisibilityToggleScale = new Vector3(2f, 2f, 2f);
    [SerializeField] private float claimVisibilityToggleVerticalSpacing = 10f;
    [SerializeField] private float claimVisibilityToggleLeftPadding = 28f;
    [SerializeField] private float claimVisibilityToggleRightPadding = 28f;
    [SerializeField] private bool autoPositionRangeVisibilityToggle = true;
    [SerializeField] private float rangeVisibilityToggleVerticalSpacing = 6f;
    [SerializeField] private RectTransform rangeVisibilityAnchor;
    [SerializeField] private Toggle claimVisibilityToggle;
    [SerializeField] private AttackRangePreviewManager attackRangePreviewManager;
    [SerializeField] private RectTransform hostSlotRoot;
    [SerializeField] private RectTransform clientSlotRoot;

    [SerializeField] private ClaimSpeechBubbleUI localBubble;
    [SerializeField] private ClaimSpeechBubbleUI enemyBubble;

    private TurnPlanningManager owner;
    private ClaimSlot[] localSlots;
    private ClaimSlot[] enemySlots;
    private ActionSlot[] localPlanningSlots;
    private RectTransform localAnchor;
    private RectTransform enemyAnchor;
    private Sprite emptySprite;
    private bool mirrorEnemyOrder;
    private int slotCount;
    private int planningRound;
    private int localRevision;
    private int remoteRevision;
    private bool initialized;
    private bool suppressLocalPublish;
    private bool localClaimVisible;
    private bool suppressToggleCallback;
    private bool warnedMissingStaticUi;
    private bool enemyClaimHasAnyClaim;
    private bool claimActionDisplayLayoutActive;

    public bool CanEditLocalClaims { get; private set; }

    // Logical order includes the action represented by every continuation slot.
    public int[] CaptureLocalClaimActions() => BuildActionPayload(localSlots);

    public void Initialize(
        TurnPlanningManager manager,
        ActionSlot[] planningSlots,
        RectTransform enemyPreviewRoot,
        Sprite emptyPreviewSprite,
        bool mirrorEnemyPreviewOrder)
    {
        RectTransform newLocalAnchor = GetLocalAnchor(planningSlots);
        int newSlotCount = planningSlots != null ? planningSlots.Length : 0;
        bool requiresSlotRebuild =
            !initialized ||
            localSlots == null ||
            enemySlots == null ||
            slotCount != newSlotCount ||
            localAnchor != newLocalAnchor ||
            enemyAnchor != enemyPreviewRoot ||
            mirrorEnemyOrder != mirrorEnemyPreviewOrder;

        owner = manager;
        emptySprite = emptyPreviewSprite;
        mirrorEnemyOrder = mirrorEnemyPreviewOrder;
        localPlanningSlots = planningSlots;
        localAnchor = newLocalAnchor;
        enemyAnchor = enemyPreviewRoot;
        slotCount = newSlotCount;

        if (slotCount <= 0)
            return;

        ResolveStaticUiReferences();

        if (localBubble != null)
        {
            localBubble.SetTitle("BLUFF");
            RefreshLocalClaimLayout();
        }

        if (enemyBubble != null)
        {
            enemyBubble.SetTitle("ENEMY CLAIM");
            if (autoAlignEnemyBubbleToPreviewRoot)
                enemyBubble.ConfigureNear(enemyAnchor != null ? enemyAnchor : localAnchor, true, slotCount);
            else
                enemyBubble.ResolveReferences();

            RefreshEnemyClaimLayout();
            enemyBubble.SetVisible(false, false);
        }

        if (requiresSlotRebuild)
        {
            RebuildSlots(localBubble, ref localSlots, true);
            RebuildSlots(enemyBubble, ref enemySlots, false);
            RefreshLocalClaimLayout();
            RefreshEnemyClaimLayout();
        }

        EnsureClaimVisibilityToggle();
        SetClaimToggleValueWithoutNotify(localClaimVisible);
        SetLocalClaimVisible(localClaimVisible, false);
        SetClaimToggleInteractable(CanEditLocalClaims);

        initialized = true;
    }

    public void EnterActionDisplayLayout(RectTransform enemyPreviewRoot)
    {
        if (!initialized)
            return;

        claimActionDisplayLayoutActive = true;

        if (enemyPreviewRoot != null)
            enemyAnchor = enemyPreviewRoot;

        if (localBubble != null)
            localBubble.SetVisible(false, true);

        RefreshEnemyClaimLayout();

        if (enemyBubble != null)
            enemyBubble.SetVisible(enemyClaimHasAnyClaim, false);
    }

    public void ExitActionDisplayLayout()
    {
        if (!claimActionDisplayLayoutActive)
            return;

        claimActionDisplayLayoutActive = false;

        RefreshLocalClaimLayout();
        RefreshEnemyClaimLayout();

        if (localBubble != null)
            localBubble.SetVisible(localClaimVisible, false);

        if (enemyBubble != null)
            enemyBubble.SetVisible(enemyClaimHasAnyClaim, false);
    }

    public void ResetForPlanning(int newPlanningRound)
    {
        planningRound = newPlanningRound;
        localRevision = 0;
        remoteRevision = 0;
        enemyClaimHasAnyClaim = false;
        claimActionDisplayLayoutActive = false;

        suppressLocalPublish = true;
        ClearSlots(localSlots);
        ClearSlots(enemySlots);
        suppressLocalPublish = false;

        SetLocalClaimVisible(false, false);
        SetClaimToggleValueWithoutNotify(false);

        if (enemyBubble != null)
            enemyBubble.SetVisible(false, true);
    }

    public void SetLocalInteractable(bool value)
    {
        CanEditLocalClaims = value;
        SetClaimToggleInteractable(value);

        if (localSlots == null)
            return;

        for (int i = 0; i < localSlots.Length; i++)
        {
            if (localSlots[i] != null)
                localSlots[i].SetInteractable(value);
        }
    }

    public void SetLocalClaimVisible(bool visible)
    {
        SetLocalClaimVisible(visible, true);
        SetClaimToggleValueWithoutNotify(visible);
    }

    private void SetLocalClaimVisible(bool visible, bool animate)
    {
        localClaimVisible = visible;

        if (localBubble != null)
            localBubble.SetVisible(visible, animate);
        
    }

    private void RefreshLocalClaimLayout()
    {
        if (localBubble == null)
            return;

        if (localAnchor != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(localAnchor);
            localBubble.ConfigureAlignedBelow(localAnchor, localPlanningSlots, claimRowVerticalSpacing);
        }
        else
        {
            localBubble.ResolveReferences();
        }

        PositionVisibilityToggles();
    }

    private void RefreshEnemyClaimLayout()
    {
        if (enemyBubble == null)
            return;

        bool alignedToEnemySlots = ConfigureEnemyClaimBubbleFromEnemySlots(claimActionDisplayLayoutActive);
        if (!alignedToEnemySlots && autoAlignEnemyBubbleToPreviewRoot)
        {
            enemyBubble.ConfigureNear(enemyAnchor != null ? enemyAnchor : localAnchor, true, slotCount);
        }
        else if (!alignedToEnemySlots)
        {
            enemyBubble.ResolveReferences();
            if (localBubble != null)
                enemyBubble.CopySlotRootLayoutFrom(localBubble);
        }
    }

    private void EnsureClaimVisibilityToggle()
    {
        ResolveStaticUiReferences();

        if (claimVisibilityToggle == null)
            return;

        claimVisibilityToggle.onValueChanged.RemoveListener(OnClaimVisibilityToggleChanged);
        claimVisibilityToggle.onValueChanged.AddListener(OnClaimVisibilityToggleChanged);
        PositionVisibilityToggles();
    }

    private void PositionVisibilityToggles()
    {
        RectTransform claimToggleRect = PositionClaimVisibilityToggle();
        PositionRangeVisibilityToggle(claimToggleRect);
    }

    private RectTransform PositionClaimVisibilityToggle()
    {
        if (claimVisibilityToggle == null)
            return null;

        RectTransform rect = claimVisibilityToggle.transform as RectTransform;
        if (rect == null)
            return null;

        RectTransform bubbleRect = null;
        if (localBubble != null)
        {
            localBubble.ResolveReferences();
            bubbleRect = localBubble.RectTransform;
        }

        if (bubbleRect != null)
        {
            RectTransform parent = bubbleRect.parent as RectTransform;
            if (parent != null && rect.parent != parent)
                rect.SetParent(parent, false);

            bool placeOnClientSide = IsClientSideUi();
            float bubbleWidth = bubbleRect.rect.width > 0f ? bubbleRect.rect.width : bubbleRect.sizeDelta.x;
            float bubbleHeight = bubbleRect.rect.height > 0f ? bubbleRect.rect.height : bubbleRect.sizeDelta.y;
            float leftEdge = bubbleRect.anchoredPosition.x - bubbleWidth * bubbleRect.pivot.x;
            float rightEdge = bubbleRect.anchoredPosition.x + bubbleWidth * (1f - bubbleRect.pivot.x);
            float centerY = bubbleRect.anchoredPosition.y + bubbleHeight * (0.5f - bubbleRect.pivot.y);
            float sideSpacing = Mathf.Max(0f, placeOnClientSide ? claimVisibilityToggleRightPadding : claimVisibilityToggleLeftPadding);

            rect.anchorMin = bubbleRect.anchorMin;
            rect.anchorMax = bubbleRect.anchorMax;
            rect.pivot = new Vector2(placeOnClientSide ? 0f : 1f, 0.5f);
            rect.sizeDelta = claimVisibilityToggleSize;
            rect.anchoredPosition = new Vector2(
                placeOnClientSide ? rightEdge + sideSpacing : leftEdge - sideSpacing,
                centerY
            );
            rect.localScale = claimVisibilityToggleScale;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        if (localAnchor == null)
            return null;

        Vector2 slotSize = GetLocalActionSlotSize(0);
        bool clientSide = IsClientSideUi();
        Vector2 anchor = new Vector2(clientSide ? 1f : 0f, localAnchor.anchorMin.y);

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(clientSide ? 1f : 0f, 1f);
        rect.sizeDelta = claimVisibilityToggleSize;
        Vector2 anchoredPosition = new Vector2(
            clientSide
                ? -Mathf.Max(0f, claimVisibilityToggleRightPadding)
                : Mathf.Max(0f, claimVisibilityToggleLeftPadding),
            localAnchor.anchoredPosition.y - (slotSize.y * 1.5f + claimRowVerticalSpacing + claimVisibilityToggleVerticalSpacing)
        );

        rect.anchoredPosition = anchoredPosition;
        rect.localScale = claimVisibilityToggleScale;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    private void PositionRangeVisibilityToggle(RectTransform claimToggleRect)
    {
        if (!autoPositionRangeVisibilityToggle)
            return;

        AttackRangePreviewManager manager = ResolveAttackRangePreviewManager();
        RectTransform anchor = ResolveRangeVisibilityAnchor(claimToggleRect);
        if (manager != null && anchor != null)
            manager.PositionToggleBelow(anchor, rangeVisibilityToggleVerticalSpacing);
    }

    private AttackRangePreviewManager ResolveAttackRangePreviewManager()
    {
        if (attackRangePreviewManager != null)
            return attackRangePreviewManager;

        attackRangePreviewManager = FindFirstObjectByType<AttackRangePreviewManager>();
        return attackRangePreviewManager;
    }

    private RectTransform ResolveRangeVisibilityAnchor(RectTransform fallback)
    {
        if (rangeVisibilityAnchor != null)
            return rangeVisibilityAnchor;

        RectTransform searchRoot = null;
        if (fallback != null)
            searchRoot = fallback.parent as RectTransform;
        else if (localAnchor != null)
            searchRoot = localAnchor.parent as RectTransform;

        if (searchRoot != null)
        {
            Transform found = searchRoot.Find("countDown");
            if (found == null)
                found = searchRoot.Find("countDownText");

            rangeVisibilityAnchor = found as RectTransform;
        }

        return rangeVisibilityAnchor != null ? rangeVisibilityAnchor : fallback;
    }

    private bool IsClientSideUi()
    {
        if (PhotonNetwork.InRoom)
            return !PhotonNetwork.IsMasterClient;

        return localAnchor != null && localAnchor.anchorMin.x > 0.5f;
    }

    private RectTransform GetEnemyClaimAnchor()
    {
        return IsClientSideUi()
            ? ResolveSlotRoot(ref hostSlotRoot, "hostSlotRoot", "masterSlotRoot")
            : ResolveSlotRoot(ref clientSlotRoot, "clientSlotRoot", "enemySlotRoot", "opponentSlotRoot");
    }

    private bool ConfigureEnemyClaimBubbleFromEnemySlots(bool forceFullWidthBelowPreview)
    {
        if (enemyBubble == null || enemyAnchor == null)
            return false;

        enemyBubble.ResolveReferences();

        RectTransform enemyRow = enemyAnchor;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(enemyRow);

        RectTransform bubbleRect = enemyBubble.RectTransform;
        if (bubbleRect == null)
            return false;

        RectTransform parent = enemyRow.parent as RectTransform;
        if (parent != null && bubbleRect.parent != parent)
            bubbleRect.SetParent(parent, false);

        Vector2 rowSize = GetEnemyPreviewRowSize();
        Vector2 firstSlotSize = GetEnemyPreviewSlotSizeByVisualIndex(0);
        if (firstSlotSize.x <= 0f || firstSlotSize.y <= 0f)
            firstSlotSize = GetLocalActionSlotSize(0);

        bubbleRect.anchorMin = enemyRow.anchorMin;
        bubbleRect.anchorMax = enemyRow.anchorMax;
        bubbleRect.pivot = enemyRow.pivot;
        float baseBubbleWidth = Mathf.Max(rowSize.x, firstSlotSize.x * Mathf.Max(1, slotCount));
        float leftInset = forceFullWidthBelowPreview ? 0f : Mathf.Clamp(enemyClaimBubbleLeftInset, 0f, Mathf.Max(0f, baseBubbleWidth - 1f));
        float xOffset = forceFullWidthBelowPreview ? 0f : (1f - bubbleRect.pivot.x) * leftInset;
        bubbleRect.anchoredPosition = enemyRow.anchoredPosition + new Vector2(xOffset, -(firstSlotSize.y + claimRowVerticalSpacing));
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseBubbleWidth - leftInset);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, firstSlotSize.y);
        bubbleRect.localScale = Vector3.one;
        bubbleRect.localRotation = Quaternion.identity;

        ConfigureEnemyClaimSlotRootForManualAlignment();
        AlignEnemyClaimSlotsToEnemyPreviewSlots();
        return true;
    }

    private void ConfigureEnemyClaimSlotRootForManualAlignment()
    {
        RectTransform root = enemyBubble != null ? enemyBubble.SlotRoot : null;
        if (root == null)
            return;

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
    }

    private void AlignEnemyClaimSlotsToEnemyPreviewSlots()
    {
        if (enemySlots == null || enemyBubble == null || enemyBubble.SlotRoot == null)
            return;

        RectTransform claimRoot = enemyBubble.SlotRoot;
        for (int visualIndex = 0; visualIndex < enemySlots.Length; visualIndex++)
        {
            ClaimSlot claimSlot = enemySlots[visualIndex];
            if (claimSlot == null)
                continue;

            int logicalIndex = claimSlot.SlotIndex;
            RectTransform enemySlotRect = GetEnemyPreviewSlotRectByLogicalIndex(logicalIndex);
            RectTransform claimRect = claimSlot.GetComponent<RectTransform>();
            if (enemySlotRect == null || claimRect == null)
                continue;

            Vector2 slotSize = GetRectSize(enemySlotRect);
            if (slotSize.x <= 0f || slotSize.y <= 0f)
                slotSize = GetEnemyClaimSlotSize(logicalIndex);

            claimRect.anchorMin = new Vector2(0.5f, 0.5f);
            claimRect.anchorMax = new Vector2(0.5f, 0.5f);
            claimRect.pivot = new Vector2(0.5f, 0.5f);
            claimRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, slotSize.x);
            claimRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, slotSize.y);
            claimRect.localScale = Vector3.one;
            claimRect.localRotation = Quaternion.identity;

            Vector3 enemyWorldCenter = enemySlotRect.TransformPoint(enemySlotRect.rect.center);
            Vector3 localCenter = claimRoot.InverseTransformPoint(enemyWorldCenter);
            claimRect.anchoredPosition = new Vector2(localCenter.x, 0f);

            LayoutElement layout = claimRect.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
                layout.preferredWidth = slotSize.x;
                layout.preferredHeight = slotSize.y;
                layout.minWidth = slotSize.x;
                layout.minHeight = slotSize.y;
            }

            claimSlot.SetVisualSize(slotSize);
        }
    }

    private void OnClaimVisibilityToggleChanged(bool visible)
    {
        if (suppressToggleCallback)
            return;
        if(visible) AudioManager.Instance.PlayBluff();

        SetLocalClaimVisible(visible, true);
    }

    private void SetClaimToggleValueWithoutNotify(bool visible)
    {
        if (claimVisibilityToggle == null)
            return;

        suppressToggleCallback = true;
        claimVisibilityToggle.SetIsOnWithoutNotify(visible);
        suppressToggleCallback = false;
    }

    private void SetClaimToggleInteractable(bool value)
    {
        if (claimVisibilityToggle != null)
            claimVisibilityToggle.interactable = value;
    }

    public int GetSlotCostForClaim(ActionData actionData, ActionType actionType)
    {
        if (owner != null)
            return owner.GetSlotCostForAction(actionData, actionType);

        if (actionData != null)
            return actionData.GetSlotCost();

        if (actionType == ActionType.HeavyAttack ||
            actionType == ActionType.Rift ||
            actionType == ActionType.Shift ||
            actionType == ActionType.Fade)
            return 2;

        return 1;
    }

    public Sprite GetSpriteForClaim(ActionData actionData, ActionType actionType, bool enemySide)
    {
        if (actionData != null)
        {
            if (actionData.iconSprite != null)
                return actionData.iconSprite;

            Sprite fromPrefab = GetPreviewSpriteFromPrefab(actionData.sourcePrefab);
            if (fromPrefab != null)
                return fromPrefab;
        }

        return owner != null ? owner.GetClaimPreviewSprite(actionType, enemySide) : emptySprite;
    }

    public void NotifyLocalClaimChanged(ClaimSlot primarySlot, params ClaimSlot[] relatedSlots)
    {
        if (suppressLocalPublish || !initialized || owner == null || !CanEditLocalClaims)
            return;

        localRevision++;
        owner.PublishLocalClaimSnapshot(
            planningRound,
            localRevision,
            BuildStatePayload(localSlots),
            BuildActionPayload(localSlots),
            BuildSourcePayload(localSlots)
        );
    }

    public void ApplyRemoteSnapshot(object payload, int senderActorNumber)
    {
        if (!TryReadSnapshotPayload(payload, out int ownerActorNumber, out int round, out int revision, out int[] states, out int[] actions, out int[] sources))
            return;

        if (owner != null && ownerActorNumber == owner.GetLocalActorNumberForClaims())
            return;

        if (ownerActorNumber != senderActorNumber)
        {
            Debug.LogWarning($"[Claim] Ignored snapshot. Reason=SenderMismatch Sender={senderActorNumber} Owner={ownerActorNumber}");
            return;
        }

        if (round != planningRound || revision <= remoteRevision)
        {
            if (round == planningRound && revision <= remoteRevision)
                Debug.Log($"[Claim] Ignored stale snapshot. Received={revision} Current={remoteRevision}");

            return;
        }

        if (!ValidateSnapshotRows(states, actions, sources))
            return;

        remoteRevision = revision;
        Debug.Log($"[Claim] Received row. Owner={ownerActorNumber} Round={round} Revision={revision}");
        ApplyEnemyPayload(states, actions, sources);
    }

    public void RevealEnemyClaimResult(int logicalSlotIndex, ActionType actualAction, int actualSlotCost)
    {
        if (enemySlots == null || logicalSlotIndex < 0 || logicalSlotIndex >= slotCount)
            return;

        int sourceLogicalIndex = FindEnemyClaimSourceLogicalIndex(logicalSlotIndex);
        if (sourceLogicalIndex < 0)
            return;

        ClaimSlot sourceSlot = GetEnemySlotByLogicalIndex(sourceLogicalIndex);
        if (sourceSlot == null || sourceSlot.State != ClaimSlotState.Action)
            return;

        ActionType claimedAction = sourceSlot.ClaimActionType;
        int claimedCost = Mathf.Max(1, GetSlotCostForClaim(null, claimedAction));
        bool truth = claimedAction == actualAction && claimedCost == Mathf.Max(1, actualSlotCost);
        ClaimResultState result = truth ? ClaimResultState.Truth : ClaimResultState.Lie;

        for (int offset = 0; offset < claimedCost; offset++)
        {
            int targetLogicalIndex = sourceLogicalIndex + offset;
            if (targetLogicalIndex < 0 || targetLogicalIndex >= slotCount)
                break;

            ClaimSlot targetSlot = GetEnemySlotByLogicalIndex(targetLogicalIndex);
            if (targetSlot != null)
                targetSlot.SetResult(result);
        }
    }

    private void ApplyEnemyPayload(int[] states, int[] actions, int[] sources)
    {
        if (enemySlots == null)
            return;

        bool hasAnyClaim = false;

        for (int logicalIndex = 0; logicalIndex < slotCount; logicalIndex++)
        {
            ClaimSlot slot = GetEnemySlotByLogicalIndex(logicalIndex);
            if (slot == null)
                continue;

            ClaimSlotState state = ReadState(states, logicalIndex);
            ActionType action = ReadAction(actions, logicalIndex);
            int sourceIndex = ReadSource(sources, logicalIndex);
            Sprite sprite = GetSpriteForClaim(null, action, true);
            slot.SetRemoteState(state, action, sourceIndex, sprite);
            slot.SetResult(ClaimResultState.Unknown);

            if (state == ClaimSlotState.Action || state == ClaimSlotState.LockedContinuation)
                hasAnyClaim = true;
        }

        enemyClaimHasAnyClaim = hasAnyClaim;

        if (claimActionDisplayLayoutActive)
            RefreshEnemyClaimLayout();

        if (enemyBubble != null)
            enemyBubble.SetVisible(hasAnyClaim, !claimActionDisplayLayoutActive);
    }

    private int FindEnemyClaimSourceLogicalIndex(int logicalSlotIndex)
    {
        ClaimSlot slot = GetEnemySlotByLogicalIndex(logicalSlotIndex);
        if (slot == null)
            return -1;

        if (slot.State == ClaimSlotState.Action)
            return logicalSlotIndex;

        if (slot.State == ClaimSlotState.LockedContinuation && slot.SourceSlotIndex >= 0)
            return slot.SourceSlotIndex;

        return -1;
    }

    private ClaimSlot GetEnemySlotByLogicalIndex(int logicalIndex)
    {
        if (enemySlots == null || logicalIndex < 0 || logicalIndex >= slotCount)
            return null;

        int visualIndex = LogicalToVisualSlotIndex(logicalIndex, slotCount, mirrorEnemyOrder);
        if (visualIndex < 0 || visualIndex >= enemySlots.Length)
            return null;

        return enemySlots[visualIndex];
    }

    private void RebuildSlots(ClaimSpeechBubbleUI bubble, ref ClaimSlot[] slots, bool local)
    {
        if (bubble == null || bubble.SlotRoot == null || slotCount <= 0)
            return;

        RectTransform root = bubble.SlotRoot;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);

        slots = new ClaimSlot[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObject = new GameObject((local ? "LocalClaimSlot_" : "EnemyClaimSlot_") + i, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(ClaimSlot));
            RectTransform rect = slotObject.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            ClaimSlot slot = slotObject.GetComponent<ClaimSlot>();
            int logicalIndex = local ? i : VisualToLogicalSlotIndex(i, slotCount, mirrorEnemyOrder);
            Vector2 slotSize = local ? GetLocalClaimSlotSize(logicalIndex) : GetEnemyClaimSlotSize(logicalIndex);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, slotSize.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, slotSize.y);

            LayoutElement layout = slotObject.AddComponent<LayoutElement>();
            layout.preferredWidth = slotSize.x;
            layout.preferredHeight = slotSize.y;
            layout.minWidth = slotSize.x;
            layout.minHeight = slotSize.y;

            slot.Initialize(this, logicalIndex, local);
            slot.SetVisualSize(slotSize);
            slot.SetInteractable(local && CanEditLocalClaims);
            slots[i] = slot;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetNextSlot(i + 1 < slots.Length ? slots[i + 1] : null);
        }
    }

    private void ClearSlots(ClaimSlot[] slots)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].Clear();
        }
    }

    private int[] BuildStatePayload(ClaimSlot[] slots)
    {
        int[] payload = new int[slotCount];
        for (int i = 0; i < payload.Length; i++)
        {
            ClaimSlot slot = GetLocalSlotByLogicalIndex(i);
            payload[i] = slot != null ? (int)slot.State : (int)ClaimSlotState.Empty;
        }
        return payload;
    }

    private int[] BuildActionPayload(ClaimSlot[] slots)
    {
        int[] payload = new int[slotCount];
        for (int i = 0; i < payload.Length; i++)
        {
            ClaimSlot slot = GetLocalSlotByLogicalIndex(i);
            payload[i] = slot != null ? (int)slot.ClaimActionType : (int)ActionType.None;
        }
        return payload;
    }

    private int[] BuildSourcePayload(ClaimSlot[] slots)
    {
        int[] payload = new int[slotCount];
        for (int i = 0; i < payload.Length; i++)
        {
            ClaimSlot slot = GetLocalSlotByLogicalIndex(i);
            payload[i] = slot != null ? slot.SourceSlotIndex : -1;
        }
        return payload;
    }

    private ClaimSlot GetLocalSlotByLogicalIndex(int logicalIndex)
    {
        if (localSlots == null || logicalIndex < 0 || logicalIndex >= localSlots.Length)
            return null;

        return localSlots[logicalIndex];
    }

    private RectTransform GetLocalAnchor(ActionSlot[] planningSlots)
    {
        if (planningSlots == null || planningSlots.Length == 0 || planningSlots[0] == null)
            return null;

        Transform parent = planningSlots[0].transform.parent;
        return parent as RectTransform ?? planningSlots[0].GetComponent<RectTransform>();
    }

    private void ResolveStaticUiReferences()
    {
        RectTransform parent = GetClaimUiParent();

        if (localBubble == null)
            localBubble = FindStaticBubble(parent, "LocalClaimBubble");

        if (enemyBubble == null)
            enemyBubble = FindStaticBubble(parent, "EnemyClaimBubble");

        if (claimVisibilityToggle == null)
        {
            Transform toggleTransform = parent != null ? parent.Find("ClaimVisibilityToggle") : null;
            if (toggleTransform != null)
                claimVisibilityToggle = toggleTransform.GetComponent<Toggle>();
        }

        if (localBubble != null)
            localBubble.ResolveReferences();

        if (enemyBubble != null)
            enemyBubble.ResolveReferences();

        WarnIfStaticUiMissing(parent);
    }

    private ClaimSpeechBubbleUI FindStaticBubble(RectTransform parent, string objectName)
    {
        Transform found = parent != null ? parent.Find(objectName) : null;
        return found != null ? found.GetComponent<ClaimSpeechBubbleUI>() : null;
    }

    private void WarnIfStaticUiMissing(RectTransform parent)
    {
        if (warnedMissingStaticUi)
            return;

        if (parent == null || localBubble == null || enemyBubble == null || claimVisibilityToggle == null)
        {
            warnedMissingStaticUi = true;
            Debug.LogWarning("[Claim] Static Canvas UI is incomplete. Expected Canvas children: LocalClaimBubble, EnemyClaimBubble, ClaimVisibilityToggle. Runtime UI creation is disabled.");
        }
    }

    private RectTransform GetClaimUiParent()
    {
        if (localAnchor != null && localAnchor.parent is RectTransform anchorParent)
            return anchorParent;

        Canvas canvas = localAnchor != null ? localAnchor.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
    }

    private RectTransform ResolveSlotRoot(ref RectTransform cached, params string[] names)
    {
        if (cached != null)
            return cached;

        RectTransform parent = GetClaimUiParent();
        cached = FindChildRectTransform(parent, names);
        if (cached != null)
            return cached;

        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect != null && MatchesAnyName(rect.name, names))
            {
                cached = rect;
                return cached;
            }
        }

        return null;
    }

    private RectTransform FindChildRectTransform(RectTransform parent, params string[] names)
    {
        if (parent == null)
            return null;

        RectTransform[] rects = parent.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect != null && MatchesAnyName(rect.name, names))
                return rect;
        }

        return null;
    }

    private bool MatchesAnyName(string value, params string[] names)
    {
        string normalized = NormalizeName(value);
        for (int i = 0; i < names.Length; i++)
        {
            if (normalized == NormalizeName(names[i]))
                return true;
        }

        return false;
    }

    private string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private Vector2 GetLocalActionSlotSize(int logicalIndex)
    {
        RectTransform rect = null;
        if (localPlanningSlots != null && logicalIndex >= 0 && logicalIndex < localPlanningSlots.Length && localPlanningSlots[logicalIndex] != null)
            rect = localPlanningSlots[logicalIndex].GetComponent<RectTransform>();

        Vector2 size = GetRectSize(rect);
        if (size.x > 0f && size.y > 0f)
            return size;

        return new Vector2(100f, 100f);
    }

    private Vector2 GetLocalClaimSlotSize(int logicalIndex)
    {
        if (matchLocalClaimSlotSizeToActionSlots)
            return GetLocalActionSlotSize(logicalIndex);

        return GetSafeSlotSize(localClaimSlotSize, new Vector2(100f, 100f));
    }

    private Vector2 GetEnemyClaimSlotSize(int logicalIndex)
    {
        Vector2 enemySlotSize = GetRectSize(GetEnemyPreviewSlotRectByLogicalIndex(logicalIndex));
        if (enemySlotSize.x > 0f && enemySlotSize.y > 0f)
            return enemySlotSize;

        return GetLocalClaimSlotSize(logicalIndex);
    }

    private Vector2 GetSafeSlotSize(Vector2 value, Vector2 fallback)
    {
        if (value.x <= 0f || value.y <= 0f)
            return fallback;

        return value;
    }

    private Vector2 GetRectSize(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Vector2 size = rect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = rect.sizeDelta;

        return size;
    }

    private RectTransform GetEnemyPreviewSlotRectByLogicalIndex(int logicalIndex)
    {
        if (enemyAnchor == null || slotCount <= 0)
            return null;

        int visualIndex = LogicalToVisualSlotIndex(logicalIndex, slotCount, mirrorEnemyOrder);
        return GetEnemyPreviewSlotRectByVisualIndex(visualIndex);
    }

    private RectTransform GetEnemyPreviewSlotRectByVisualIndex(int visualIndex)
    {
        if (enemyAnchor == null || visualIndex < 0 || visualIndex >= enemyAnchor.childCount)
            return null;

        return enemyAnchor.GetChild(visualIndex) as RectTransform;
    }

    private Vector2 GetEnemyPreviewSlotSizeByVisualIndex(int visualIndex)
    {
        return GetRectSize(GetEnemyPreviewSlotRectByVisualIndex(visualIndex));
    }

    private Vector2 GetEnemyPreviewRowSize()
    {
        Vector2 rowSize = GetRectSize(enemyAnchor);
        if (rowSize.x > 0f && rowSize.y > 0f)
            return rowSize;

        if (enemyAnchor == null || enemyAnchor.childCount == 0)
            return Vector2.zero;

        bool hasBounds = false;
        float minX = 0f;
        float maxX = 0f;
        float maxHeight = 0f;

        for (int i = 0; i < enemyAnchor.childCount; i++)
        {
            RectTransform child = enemyAnchor.GetChild(i) as RectTransform;
            if (child == null)
                continue;

            Vector2 size = GetRectSize(child);
            float centerX = child.anchoredPosition.x;
            float left = centerX - size.x * 0.5f;
            float right = centerX + size.x * 0.5f;

            if (!hasBounds)
            {
                minX = left;
                maxX = right;
                hasBounds = true;
            }
            else
            {
                minX = Mathf.Min(minX, left);
                maxX = Mathf.Max(maxX, right);
            }

            maxHeight = Mathf.Max(maxHeight, size.y);
        }

        return hasBounds ? new Vector2(maxX - minX, maxHeight) : Vector2.zero;
    }

    private Sprite GetPreviewSpriteFromPrefab(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return null;

        Image image = sourcePrefab.GetComponent<Image>();
        if (image == null || image.sprite == null)
            image = sourcePrefab.GetComponentInChildren<Image>(true);

        return image != null ? image.sprite : null;
    }

    private bool TryReadSnapshotPayload(object payload, out int ownerActorNumber, out int round, out int revision, out int[] states, out int[] actions, out int[] sources)
    {
        ownerActorNumber = -1;
        round = -1;
        revision = -1;
        states = null;
        actions = null;
        sources = null;

        if (!(payload is object[] data) || data.Length < 6)
            return false;

        try
        {
            ownerActorNumber = Convert.ToInt32(data[0]);
            round = Convert.ToInt32(data[1]);
            revision = Convert.ToInt32(data[2]);
            states = ConvertIntArray(data[3]);
            actions = ConvertIntArray(data[4]);
            sources = ConvertIntArray(data[5]);
            return states != null && actions != null && sources != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool ValidateSnapshotRows(int[] states, int[] actions, int[] sources)
    {
        if (slotCount <= 0)
            return false;

        if (states == null || actions == null || sources == null)
            return false;

        if (states.Length != slotCount || actions.Length != slotCount || sources.Length != slotCount)
        {
            Debug.LogWarning($"[Claim] Ignored snapshot. Reason=SlotCountMismatch Expected={slotCount} States={states.Length} Actions={actions.Length} Sources={sources.Length}");
            return false;
        }

        for (int i = 0; i < slotCount; i++)
        {
            if (!Enum.IsDefined(typeof(ClaimSlotState), states[i]))
            {
                Debug.LogWarning($"[Claim] Ignored snapshot. Reason=InvalidState Slot={i} State={states[i]}");
                return false;
            }

            ClaimSlotState state = (ClaimSlotState)states[i];
            if (!Enum.IsDefined(typeof(ActionType), actions[i]))
            {
                Debug.LogWarning($"[Claim] Ignored snapshot. Reason=InvalidAction Slot={i} Action={actions[i]}");
                return false;
            }

            ActionType action = (ActionType)actions[i];
            int source = sources[i];

            if (state == ClaimSlotState.Action && action == ActionType.None)
            {
                Debug.LogWarning($"[Claim] Ignored snapshot. Reason=ActionSlotHasNone Slot={i}");
                return false;
            }

            if (state == ClaimSlotState.LockedContinuation)
            {
                if (action == ActionType.None || source < 0 || source >= slotCount || source >= i)
                {
                    Debug.LogWarning($"[Claim] Ignored snapshot. Reason=InvalidLockedContinuation Slot={i} Source={source} Action={action}");
                    return false;
                }

                if ((ClaimSlotState)states[source] != ClaimSlotState.Action || (ActionType)actions[source] != action)
                {
                    Debug.LogWarning($"[Claim] Ignored snapshot. Reason=LockedContinuationSourceMismatch Slot={i} Source={source} Action={action}");
                    return false;
                }
            }
        }

        return true;
    }

    private int[] ConvertIntArray(object value)
    {
        if (value is int[] intArray)
            return intArray;

        if (value is object[] objectArray)
        {
            int[] result = new int[objectArray.Length];
            for (int i = 0; i < objectArray.Length; i++)
                result[i] = Convert.ToInt32(objectArray[i]);
            return result;
        }

        return null;
    }

    private ClaimSlotState ReadState(int[] states, int index)
    {
        if (states == null || index < 0 || index >= states.Length)
            return ClaimSlotState.Hidden;

        int value = states[index];
        return Enum.IsDefined(typeof(ClaimSlotState), value)
            ? (ClaimSlotState)value
            : ClaimSlotState.Hidden;
    }

    private ActionType ReadAction(int[] actions, int index)
    {
        if (actions == null || index < 0 || index >= actions.Length)
            return ActionType.None;

        int value = actions[index];
        return Enum.IsDefined(typeof(ActionType), value)
            ? (ActionType)value
            : ActionType.None;
    }

    private int ReadSource(int[] sources, int index)
    {
        if (sources == null || index < 0 || index >= sources.Length)
            return -1;

        return sources[index];
    }

    private int LogicalToVisualSlotIndex(int logicalIndex, int count, bool mirrored)
    {
        if (count <= 0 || logicalIndex < 0 || logicalIndex >= count)
            return -1;

        return mirrored ? count - 1 - logicalIndex : logicalIndex;
    }

    private int VisualToLogicalSlotIndex(int visualIndex, int count, bool mirrored)
    {
        if (count <= 0 || visualIndex < 0 || visualIndex >= count)
            return -1;

        return mirrored ? count - 1 - visualIndex : visualIndex;
    }
}
