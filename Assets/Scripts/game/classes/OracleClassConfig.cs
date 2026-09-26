using UnityEngine;

[CreateAssetMenu(fileName = "OracleConfig", menuName = "Game/Oracle Class Config")]
public class OracleClassConfig : CharacterClassConfig
{
    [Header("Oracle Actions")]
    public AttackActionData bolt;
    public AttackActionData rift;
    public ActionData shift;
    public DefenseActionData ward;
    public DefenseActionData fade;
    public UltimateActionData sight;

    [Header("Oracle UI Sprites")]
    public Sprite sightSelectedSlotFrame;
    public Sprite sightUsedOverlay;
    public Sprite shiftCooldownOverlay;
    public Sprite riftSecondarySlotLockedIcon;

    public override ActionData[] GetClassActions()
    {
        return new ActionData[]
        {
            bolt,
            rift,
            shift,
            ward,
            fade,
            sight,
            dance
        };
    }

    public override ActionData GetActionData(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Bolt:
                return bolt;

            case ActionType.Rift:
                return rift;

            case ActionType.Shift:
                return shift;

            case ActionType.Ward:
                return ward;

            case ActionType.Fade:
            case ActionType.LowAttack:
                return fade;

            case ActionType.Sight:
                return sight;

            case ActionType.Ultimate:
                return sight;

            case ActionType.Dance:
                return dance;

            case ActionType.Parry:
                return ward;

            case ActionType.Defense:
                return shift;

            default:
                return base.GetActionData(actionType);
        }
    }
}
