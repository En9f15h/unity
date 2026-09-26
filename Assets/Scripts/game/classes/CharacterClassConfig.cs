using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterClassConfig", menuName = "Game/Character Class Config")]
public class CharacterClassConfig : ScriptableObject
{
    [Header("Class")]
    public string className;
    public string photonResourceFolder;
    public int maxHP = 30;
    public int slotCount = 5;

    [Header("Skins")]
    public string[] skinNames;
    public GameObject[] skinPrefabs;

    [Header("Attacks")]
    public AttackActionData lightAttack;
    public AttackActionData heavyAttack;
    public AttackActionData lowAttack;

    [Header("Defense")]
    public ActionData parry;
    public ActionData defense;

    [Header("Special Actions")]
    public DanceActionData dance;
    public UltimateActionData ultimate;

    [Header("Class Energy Bar")]
    public GameObject energyBarPrefab;

    [Header("Class Gameplay UI")]
    public GameObject gameplayUiPrefab;

    public virtual ActionData[] GetClassActions()
    {
        return new ActionData[]
        {
            lightAttack,
            heavyAttack,
            lowAttack,
            parry,
            defense,
            dance,
            ultimate
        };
    }

    public virtual ActionData GetActionData(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.LightAttack:
                return lightAttack;

            case ActionType.HeavyAttack:
                return heavyAttack;

            case ActionType.LowAttack:
                return lowAttack;

            case ActionType.Parry:
                return parry;

            case ActionType.Defense:
                return defense;

            case ActionType.Dance:
                return dance;

            case ActionType.Ultimate:
                return ultimate;

            default:
                return null;
        }
    }
}
