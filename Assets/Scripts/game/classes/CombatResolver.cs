using UnityEngine;

public struct AttackResolutionResult
{
    public bool hit;
    public bool parried;
    public bool blocked;
    public bool evaded;
    public bool outOfRange;
    public int damage;
}

public static class CombatResolver
{
    public static AttackResolutionResult ResolveAttackDamage(
        AttackActionData attackData,
        ActionType defenderAction,
        bool defenderJumpingNow,
        int currentDistance)
    {
        AttackResolutionResult result = new AttackResolutionResult();

        if (attackData == null)
            return result;

        if (currentDistance > attackData.range)
        {
            result.outOfRange = true;
            return result;
        }

        if (defenderAction == ActionType.Parry && attackData.canBeParried)
        {
            result.parried = true;
            return result;
        }

        if (defenderAction == ActionType.Defense && attackData.canBeDefended)
        {
            result.blocked = true;
            return result;
        }

        if (defenderJumpingNow)
        {
            switch (attackData.jumpInteraction)
            {
                case JumpInteractionType.Evade:
                    result.evaded = true;
                    return result;

                case JumpInteractionType.HalfDamage:
                    result.hit = true;
                    result.damage = Mathf.Max(1, attackData.damage / 2);
                    return result;
            }
        }

        result.hit = true;
        result.damage = attackData.damage;
        return result;
    }
}
