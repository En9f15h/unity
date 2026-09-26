using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public partial class TurnPlanningManager
{
    private bool IsMoveAction(ActionType action)
    {
        return action == ActionType.MoveForward ||
               action == ActionType.MoveBackward;
    }

    private void ApplyMovement(ref int currentDistance, ActionType action, bool isMine)
    {
        switch (action)
        {
            case ActionType.MoveForward:
                Debug.Log(isMine ? "Mine moved forward (+X)." : "Enemy moved forward (+X).");
                break;

            case ActionType.MoveBackward:
                Debug.Log(isMine ? "Mine moved backward (-X)." : "Enemy moved backward (-X).");
                break;

            case ActionType.Jump:
                if (isMine)
                    myJumping = true;
                else
                    enemyJumping = true;
                Debug.Log(isMine ? "Mine jumped." : "Enemy jumped.");
                break;

        }
    }

    private void ApplyOracleShift(bool oracleIsMine)
    {
        if (oracleIsMine && myShiftInterruptedThisStep)
        {
            MarkShiftUsed(oracleIsMine);
            Debug.Log("My Shift was interrupted before movement.");
            return;
        }

        if (!oracleIsMine && enemyShiftInterruptedThisStep)
        {
            MarkShiftUsed(oracleIsMine);
            Debug.Log("Enemy Shift was interrupted before movement.");
            return;
        }

        CharacterUnit oracleUnit = oracleIsMine ? myUnit : enemyUnit;
        CharacterUnit targetUnit = oracleIsMine ? enemyUnit : myUnit;

        if (oracleUnit == null || targetUnit == null)
            return;

        int oracleCell = OracleShiftResolver.WorldToCell(oracleUnit.transform.position.x, boardOriginX, WorldUnitsPerTile);
        int targetCell = OracleShiftResolver.WorldToCell(targetUnit.transform.position.x, boardOriginX, WorldUnitsPerTile);

        OracleShiftResult result = OracleShiftResolver.Resolve(
            oracleCell,
            targetCell,
            boardMinCell,
            boardMaxCell,
            blockedBoardCells,
            oracleShiftMaxFinalDistance
        );

        if (!result.valid)
        {
            Debug.Log("Oracle Shift failed because no valid position was available.");
            return;
        }

        Vector3 oraclePosition = oracleUnit.transform.position;
        Vector3 targetPosition = targetUnit.transform.position;

        oraclePosition.x = OracleShiftResolver.CellToWorldX(result.oracleCell, boardOriginX, WorldUnitsPerTile);
        targetPosition.x = OracleShiftResolver.CellToWorldX(result.enemyCell, boardOriginX, WorldUnitsPerTile);

        if (!AreOracleSpecialMovementPositionsInsideWall(oraclePosition.x, targetPosition.x))
        {
            MarkShiftUsed(oracleIsMine);
            Debug.Log((oracleIsMine ? "My" : "Enemy") + " Shift stayed in place because resolved x is outside wall range. oracleX=" + oraclePosition.x + ", targetX=" + targetPosition.x);
            return;
        }

        oracleUnit.transform.position = oraclePosition;
        targetUnit.transform.position = targetPosition;
        RefreshCharacterFacings();

        MarkShiftUsed(oracleIsMine);

        distance = Mathf.Max(1, Mathf.Abs(result.oracleCell - result.enemyCell));
        Debug.Log((oracleIsMine ? "My" : "Enemy") + " Shift resolved. swapFallback=" + result.usedSwapFallback);
    }

    private void ApplySimultaneousOracleShift()
    {
        if (myShiftInterruptedThisStep)
        {
            MarkShiftUsed(true);
            Debug.Log("My Shift was interrupted before simultaneous Shift exchange.");
        }

        if (enemyShiftInterruptedThisStep)
        {
            MarkShiftUsed(false);
            Debug.Log("Enemy Shift was interrupted before simultaneous Shift exchange.");
        }

        if (myShiftInterruptedThisStep && enemyShiftInterruptedThisStep)
            return;

        if (myShiftInterruptedThisStep)
        {
            ApplyOracleShift(false);
            return;
        }

        if (enemyShiftInterruptedThisStep)
        {
            ApplyOracleShift(true);
            return;
        }

        if (myUnit == null || enemyUnit == null)
            return;

        int myCell = OracleShiftResolver.WorldToCell(myUnit.transform.position.x, boardOriginX, WorldUnitsPerTile);
        int enemyCell = OracleShiftResolver.WorldToCell(enemyUnit.transform.position.x, boardOriginX, WorldUnitsPerTile);

        if (myCell == enemyCell)
        {
            Debug.Log("Simultaneous Shift failed because both characters are already on the same cell.");
            return;
        }

        if (!OracleShiftResolver.IsCellValid(myCell, boardMinCell, boardMaxCell, blockedBoardCells) ||
            !OracleShiftResolver.IsCellValid(enemyCell, boardMinCell, boardMaxCell, blockedBoardCells))
        {
            Debug.Log("Simultaneous Shift failed because one current cell is invalid.");
            return;
        }

        int finalDistance = Mathf.Abs(enemyCell - myCell);
        if (finalDistance > Mathf.Max(1, oracleShiftMaxFinalDistance))
        {
            Debug.Log("Simultaneous Shift failed because final distance would exceed " + oracleShiftMaxFinalDistance + ".");
            return;
        }

        Vector3 myPosition = myUnit.transform.position;
        Vector3 enemyPosition = enemyUnit.transform.position;
        float myX = myPosition.x;

        myPosition.x = enemyPosition.x;
        enemyPosition.x = myX;

        if (!AreOracleSpecialMovementPositionsInsideWall(myPosition.x, enemyPosition.x))
        {
            MarkShiftUsed(true);
            MarkShiftUsed(false);
            Debug.Log("Simultaneous Shift stayed in place because swapped x is outside wall range.");
            return;
        }

        myUnit.transform.position = myPosition;
        enemyUnit.transform.position = enemyPosition;
        RefreshCharacterFacings();

        MarkShiftUsed(true);
        MarkShiftUsed(false);

        distance = Mathf.Max(1, Mathf.Abs(enemyCell - myCell));
        Debug.Log("Simultaneous Shift resolved by exchanging both positions.");
    }

    private IEnumerator PlayMovementStepTimed(ActionType myAction, ActionType enemyAction, float duration)
    {
        Transform myTransform = myUnit != null ? myUnit.transform : null;
        Transform enemyTransform = enemyUnit != null ? enemyUnit.transform : null;

        if (myTransform == null && enemyTransform == null)
            yield break;

        Vector3 myStart = myTransform != null ? myTransform.position : Vector3.zero;
        Vector3 enemyStart = enemyTransform != null ? enemyTransform.position : Vector3.zero;

        Vector3 myTarget = myStart + GetMoveOffset(myUnit, enemyUnit, myAction);
        Vector3 enemyTarget = enemyStart + GetMoveOffset(enemyUnit, myUnit, enemyAction);

        if (ShouldResolveNoCrossTargets(myAction, enemyAction))
            ResolveNoCrossTargets(ref myTarget, ref enemyTarget, myStart, enemyStart);

        float timer = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            if (myTransform != null)
                myTransform.position = Vector3.Lerp(myStart, myTarget, t);

            if (enemyTransform != null)
                enemyTransform.position = Vector3.Lerp(enemyStart, enemyTarget, t);

            yield return null;
        }

        if (myTransform != null)
            myTransform.position = myTarget;

        if (enemyTransform != null)
            enemyTransform.position = enemyTarget;
    }

    private Vector3 GetMoveOffset(CharacterUnit selfUnit, CharacterUnit otherUnit, ActionType action)
    {
        if (!IsMoveAction(action) || selfUnit == null)
            return Vector3.zero;

        float movementDirection = GetMovementWorldDirection(action);
        if (Mathf.Abs(movementDirection) > 0.001f)
            return Vector3.right * (movementDirection * TilesToWorld(1f));

        if (otherUnit == null)
            return Vector3.zero;

        switch (action)
        {
            default:
                return Vector3.zero;
        }
    }

    private float GetMovementWorldDirection(ActionType action)
    {
        switch (action)
        {
            case ActionType.MoveForward:
                return 1f;

            case ActionType.MoveBackward:
                return -1f;

            default:
                return 0f;
        }
    }

    private bool ShouldResolveNoCrossTargets(ActionType myAction, ActionType enemyAction)
    {
        return IsMoveAction(myAction) || IsMoveAction(enemyAction);
    }

    private Vector3 GetDirectionToOpponent(CharacterUnit selfUnit, CharacterUnit otherUnit)
    {
        if (selfUnit == null || otherUnit == null)
            return Vector3.right;

        return otherUnit.transform.position.x >= selfUnit.transform.position.x
            ? Vector3.right
            : Vector3.left;
    }

    private void ResolveNoCrossTargets(ref Vector3 myTarget, ref Vector3 enemyTarget, Vector3 myStart, Vector3 enemyStart)
    {
        bool myIsLeft = myStart.x <= enemyStart.x;

        Vector3 leftStart = myIsLeft ? myStart : enemyStart;
        Vector3 rightStart = myIsLeft ? enemyStart : myStart;

        Vector3 leftTarget = myIsLeft ? myTarget : enemyTarget;
        Vector3 rightTarget = myIsLeft ? enemyTarget : myTarget;

        Transform leftTransform = myIsLeft ? (myUnit != null ? myUnit.transform : null)
                                           : (enemyUnit != null ? enemyUnit.transform : null);

        Transform rightTransform = myIsLeft ? (enemyUnit != null ? enemyUnit.transform : null)
                                            : (myUnit != null ? myUnit.transform : null);

        float leftHalfWidth = GetCharacterHalfWidth(leftTransform);
        float rightHalfWidth = GetCharacterHalfWidth(rightTransform);

        float requiredDistance = leftHalfWidth + rightHalfWidth + minCharacterGap;

        float leftRawX = leftTarget.x;
        float rightRawX = rightTarget.x;
        float leftFinalX = leftRawX;
        float rightFinalX = rightRawX;

        if (leftRawX > rightRawX - requiredDistance)
        {
            float overlap = (leftRawX + requiredDistance) - rightRawX;

            bool leftMoved = Mathf.Abs(leftRawX - leftStart.x) > 0.001f;
            bool rightMoved = Mathf.Abs(rightRawX - rightStart.x) > 0.001f;

            if (leftMoved && rightMoved)
            {
                leftFinalX -= overlap * 0.5f;
                rightFinalX += overlap * 0.5f;
            }
            else if (leftMoved)
            {
                leftFinalX -= overlap;
            }
            else if (rightMoved)
            {
                rightFinalX += overlap;
            }

            LogMovementCollisionCorrection(
                leftTransform,
                rightTransform,
                leftRawX,
                rightRawX,
                leftFinalX,
                rightFinalX,
                leftHalfWidth,
                rightHalfWidth,
                requiredDistance,
                leftMoved,
                rightMoved);
        }

        leftTarget.x = leftFinalX;
        rightTarget.x = rightFinalX;

        if (myIsLeft)
        {
            myTarget = leftTarget;
            enemyTarget = rightTarget;
        }
        else
        {
            myTarget = rightTarget;
            enemyTarget = leftTarget;
        }
    }

    private float GetCharacterHalfWidth(Transform target)
    {
        if (target == null)
            return 0.5f;

        Collider2D col = target.GetComponentInChildren<Collider2D>();
        if (col != null)
            return col.bounds.extents.x;

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
        if (renderers != null && renderers.Length > 0)
        {
            LogMissingMovementCollider(target, "SpriteRenderer bounds");

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            return combined.extents.x;
        }

        LogMissingMovementCollider(target, "default half width");
        return 0.5f;
    }

    private bool AreOracleSpecialMovementPositionsInsideWall(float firstX, float secondX)
    {
        return IsOracleSpecialMovementXInsideWall(firstX) &&
               IsOracleSpecialMovementXInsideWall(secondX);
    }

    private bool IsOracleSpecialMovementXInsideWall(float x)
    {
        if (!constrainOracleSpecialMovementToWall)
            return true;

        float minX = Mathf.Min(oracleMovementWallMinX, oracleMovementWallMaxX);
        float maxX = Mathf.Max(oracleMovementWallMinX, oracleMovementWallMaxX);
        const float tolerance = 0.001f;

        return x >= minX - tolerance && x <= maxX + tolerance;
    }

    private void LogMovementCollisionCorrection(
        Transform leftTransform,
        Transform rightTransform,
        float leftRawX,
        float rightRawX,
        float leftFinalX,
        float rightFinalX,
        float leftHalfWidth,
        float rightHalfWidth,
        float requiredDistance,
        bool leftMoved,
        bool rightMoved)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!verboseMovementCollisionLogs)
            return;

        string leftName = leftTransform != null ? leftTransform.name : "Left";
        string rightName = rightTransform != null ? rightTransform.name : "Right";
        Debug.Log(
            "[MovementCollision] Corrected " +
            leftName + "/" + rightName +
            " raw=(" + leftRawX.ToString("0.###") + ", " + rightRawX.ToString("0.###") + ")" +
            " final=(" + leftFinalX.ToString("0.###") + ", " + rightFinalX.ToString("0.###") + ")" +
            " halfWidths=(" + leftHalfWidth.ToString("0.###") + ", " + rightHalfWidth.ToString("0.###") + ")" +
            " minCharacterGap=" + minCharacterGap.ToString("0.###") +
            " requiredDistance=" + requiredDistance.ToString("0.###") +
            " moved=(" + leftMoved + ", " + rightMoved + ")",
            this);
#endif
    }

    private void LogMissingMovementCollider(Transform target, string fallback)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (target == null)
            return;

        int id = target.GetInstanceID();
        if (!warnedMissingMovementColliderIds.Add(id))
            return;

        Debug.LogWarning(
            "[MovementCollision] " + target.name +
            " has no Collider2D for movement separation; using " + fallback + ".",
            target);
#endif
    }

    private int GetCurrentGridDistance()
    {
        if (myUnit == null || enemyUnit == null)
            return distance;

        float worldDistance = Mathf.Abs(enemyUnit.transform.position.x - myUnit.transform.position.x);

        // Convert world distance back into grid steps using the authoritative tile size.
        int gridDistance = Mathf.RoundToInt(worldDistance / WorldUnitsPerTile);

        // Treat less than one step apart as adjacent distance 0.
        if (gridDistance < 1)
            gridDistance = 1;

        return gridDistance;
    }

    private void RefreshDistance()
    {
        distance = GetCurrentGridDistance();
        RefreshCharacterFacings();
        Debug.Log("Current grid distance = " + distance);
    }

    private void RefreshCharacterFacings()
    {
        RefreshCharacterFacing(myUnit, enemyUnit);
        RefreshCharacterFacing(enemyUnit, myUnit);
    }

    private void RefreshCharacterFacing(CharacterUnit unit, CharacterUnit opponent)
    {
        if (unit == null || opponent == null)
            return;

        CharacterFacing facing = unit.GetComponent<CharacterFacing>();
        if (facing != null)
            facing.FaceTarget(opponent.transform);
    }

}
