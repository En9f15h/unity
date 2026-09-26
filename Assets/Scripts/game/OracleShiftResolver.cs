using System;
using UnityEngine;

public struct OracleShiftResult
{
    public bool valid;
    public bool usedSwapFallback;
    public int oracleCell;
    public int enemyCell;
}

public static class OracleShiftResolver
{
    public static OracleShiftResult Resolve(
        int oracleCell,
        int enemyCell,
        int minCell,
        int maxCell,
        int[] blockedCells,
        int maxFinalDistance = int.MaxValue)
    {
        OracleShiftResult result = new OracleShiftResult
        {
            valid = false,
            usedSwapFallback = false,
            oracleCell = oracleCell,
            enemyCell = enemyCell
        };

        if (oracleCell == enemyCell)
            return result;

        int directionFromOracleToEnemy = enemyCell > oracleCell ? 1 : -1;
        int behindEnemyCell = enemyCell + directionFromOracleToEnemy;
        maxFinalDistance = Mathf.Max(1, maxFinalDistance);

        if (IsCellValid(behindEnemyCell, minCell, maxCell, blockedCells) &&
            behindEnemyCell != enemyCell &&
            IsFinalDistanceAllowed(behindEnemyCell, enemyCell, maxFinalDistance))
        {
            result.valid = true;
            result.oracleCell = behindEnemyCell;
            result.enemyCell = enemyCell;
            return result;
        }

        if (!IsCellValid(oracleCell, minCell, maxCell, blockedCells) ||
            !IsCellValid(enemyCell, minCell, maxCell, blockedCells))
        {
            return result;
        }

        if (IsFinalDistanceAllowed(enemyCell, oracleCell, maxFinalDistance))
        {
            result.valid = true;
            result.usedSwapFallback = true;
            result.oracleCell = enemyCell;
            result.enemyCell = oracleCell;
            return result;
        }

        if (TryFindFarthestValidOracleCell(
                enemyCell,
                directionFromOracleToEnemy,
                minCell,
                maxCell,
                blockedCells,
                maxFinalDistance,
                out int clampedOracleCell))
        {
            result.valid = true;
            result.oracleCell = clampedOracleCell;
            result.enemyCell = enemyCell;
            return result;
        }

        return result;
    }

    private static bool TryFindFarthestValidOracleCell(
        int enemyCell,
        int preferredDirection,
        int minCell,
        int maxCell,
        int[] blockedCells,
        int maxFinalDistance,
        out int oracleCell)
    {
        oracleCell = enemyCell;
        preferredDirection = preferredDirection >= 0 ? 1 : -1;

        for (int distance = maxFinalDistance; distance >= 1; distance--)
        {
            int preferredCell = enemyCell + preferredDirection * distance;
            if (IsCellValid(preferredCell, minCell, maxCell, blockedCells) && preferredCell != enemyCell)
            {
                oracleCell = preferredCell;
                return true;
            }

            int oppositeCell = enemyCell - preferredDirection * distance;
            if (IsCellValid(oppositeCell, minCell, maxCell, blockedCells) && oppositeCell != enemyCell)
            {
                oracleCell = oppositeCell;
                return true;
            }
        }

        return false;
    }

    private static bool IsFinalDistanceAllowed(int oracleCell, int enemyCell, int maxFinalDistance)
    {
        return Mathf.Abs(oracleCell - enemyCell) <= Mathf.Max(1, maxFinalDistance);
    }

    public static bool IsCellValid(int cell, int minCell, int maxCell, int[] blockedCells)
    {
        if (minCell > maxCell)
        {
            int oldMin = minCell;
            minCell = maxCell;
            maxCell = oldMin;
        }

        if (cell < minCell || cell > maxCell)
            return false;

        if (blockedCells == null)
            return true;

        return Array.IndexOf(blockedCells, cell) < 0;
    }

    public static int WorldToCell(float worldX, float originX, float moveStep)
    {
        moveStep = Mathf.Max(0.0001f, moveStep);
        return Mathf.RoundToInt((worldX - originX) / moveStep);
    }

    public static float CellToWorldX(int cell, float originX, float moveStep)
    {
        return originX + cell * Mathf.Max(0.0001f, moveStep);
    }
}
