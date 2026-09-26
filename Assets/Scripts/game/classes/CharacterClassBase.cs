using UnityEngine;

public abstract class CharacterClassBase
{
    public abstract string ClassName { get; }
    public abstract int MaxHP { get; }
    public abstract int SlotCount { get; }
    public abstract string[] SkinNames { get; }

    public abstract AttackActionData LightAttack { get; }
    public abstract AttackActionData HeavyAttack { get; }
    public abstract AttackActionData LowAttack { get; }

    public abstract ActionData Parry { get; }
    public abstract ActionData Defense { get; }
    public abstract DanceActionData Dance { get; }
    public abstract UltimateActionData Ultimate { get; }
    public virtual ActionData[] GetAllActions()
    {
        return new ActionData[]
        {
            LightAttack,
            HeavyAttack,
            LowAttack,
            Parry,
            Defense,
            Dance,
            Ultimate
        };
    }
}
