using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public partial class TurnPlanningManager
{
    private int usageClaimTurn = -1;
    private int[] usageActualSlots, usageClaimSlots;
    private readonly System.Collections.Generic.HashSet<int> recordedClaimTurns = new System.Collections.Generic.HashSet<int>();

    private void CaptureUsageClaims(int turn, int[] submittedActions)
    {
        EnsureClaimControllerForCurrentUi();
        usageClaimTurn = turn;
        usageActualSlots = (int[])submittedActions.Clone();
        usageClaimSlots = claimController != null ? claimController.CaptureLocalClaimActions() : null;
        for (int i = 0; planningSlots != null && i < planningSlots.Length && i < usageActualSlots.Length; i++)
        {
            var slot = planningSlots[i];
            if (slot != null && slot.IsOccupiedByHeavyExtension && slot.GetExtensionActionData() != null)
                usageActualSlots[i] = (int)slot.GetExtensionActionData().actionType;
        }
    }
    private void RecordConfirmedUsageClaims(int turn)
    {
        if (!PhotonNetwork.InRoom || usageClaimTurn != turn || usageActualSlots == null || usageClaimSlots == null || !recordedClaimTurns.Add(turn)) return;
        LocalActionUsage.RecordClaims(LocalActionUsage.CharacterId(PhotonNetwork.LocalPlayer), usageActualSlots, usageClaimSlots);
    }
    private void EnsureClaimControllerForCurrentUi()
    {
        if (claimController == null)
            claimController = GetComponent<ClaimController>();

        if (claimController == null)
            claimController = gameObject.AddComponent<ClaimController>();

        claimController.Initialize(
            this,
            planningSlots,
            currentEnemyPreviewRoot,
            emptyPreviewSprite,
            mirrorEnemyPreviewSlotOrder
        );
    }

    private void ResetClaimsForPlanning(int planningRound)
    {
        EnsureClaimControllerForCurrentUi();

        if (claimController != null)
            claimController.ResetForPlanning(planningRound);
    }

    private void ApplyClaimActionDisplayLayout()
    {
        EnsureClaimControllerForCurrentUi();

        if (claimController != null)
            claimController.EnterActionDisplayLayout(currentEnemyPreviewRoot);
    }

    private void RestoreClaimActionDisplayLayout()
    {
        if (claimController != null)
            claimController.ExitActionDisplayLayout();
    }

    private void SetClaimInteractionEnabled(bool value)
    {
        EnsureClaimControllerForCurrentUi();

        if (claimController != null)
            claimController.SetLocalInteractable(value);
    }

    private void HandleClaimSnapshotEvent(EventData photonEvent)
    {
        EnsureClaimControllerForCurrentUi();

        if (claimController != null)
            claimController.ApplyRemoteSnapshot(photonEvent.CustomData, photonEvent.Sender);
    }

    public void PublishLocalClaimSnapshot(int planningRound, int revision, int[] states, int[] actions, int[] sources)
    {
        if (PhotonNetwork.LocalPlayer == null || !PhotonNetwork.InRoom)
            return;

        object[] payload = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            planningRound,
            revision,
            states ?? new int[0],
            actions ?? new int[0],
            sources ?? new int[0]
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };

        PhotonNetwork.RaiseEvent(EVENT_CLAIM_SNAPSHOT, payload, options, SendOptions.SendReliable);
        Debug.Log($"[Claim] Sent row. Owner={PhotonNetwork.LocalPlayer.ActorNumber} Round={planningRound} Revision={revision}");
    }

    public int GetLocalActorNumberForClaims()
    {
        if (PhotonNetwork.LocalPlayer != null)
            return PhotonNetwork.LocalPlayer.ActorNumber;

        return myActorNumber;
    }

    public Sprite GetClaimPreviewSprite(ActionType action, bool enemySide)
    {
        if (action == ActionType.None)
            return emptyPreviewSprite;

        ActionType displayAction = enemySide ? GetEnemyPreviewDisplayAction(action) : action;
        Sprite sprite = GetPreviewSprite(displayAction);
        if (sprite != null)
            return sprite;

        CharacterClassConfig config = enemySide ? GetEnemyClassConfig() : GetMyClassConfig();
        ActionData actionData = config != null ? config.GetActionData(action) : null;
        if (actionData != null)
        {
            if (actionData.iconSprite != null)
                return actionData.iconSprite;

            if (actionData.sourcePrefab != null)
            {
                Image image = actionData.sourcePrefab.GetComponent<Image>();
                if (image == null || image.sprite == null)
                    image = actionData.sourcePrefab.GetComponentInChildren<Image>(true);

                if (image != null)
                    return image.sprite;
            }
        }

        return emptyPreviewSprite;
    }

    private void RevealEnemyClaimResultForAction(int logicalSlotIndex, ActionType actualAction, int slotCost)
    {
        if (claimController == null)
            return;

        claimController.RevealEnemyClaimResult(logicalSlotIndex, actualAction, slotCost);
    }
}
