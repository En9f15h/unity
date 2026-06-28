using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterClassConfig", menuName = "Game/Character Class Config")]
public class CharacterClassConfig : ScriptableObject
{
    [Header("基本資料")]
    public string className;
    public int maxHP = 30;
    public int slotCount = 5;

    [Header("造型")]
    public string[] skinNames;
    public GameObject[] skinPrefabs;

    [Header("攻擊")]
    public AttackActionData lightAttack;
    public AttackActionData heavyAttack;
    public AttackActionData lowAttack;

    [Header("防禦")]
    public ActionData parry;
    public ActionData defense;

    [Header("特殊行動")]
    public DanceActionData dance;
    public UltimateActionData ultimate;

    [Header("職業專屬能量條")]
    public GameObject energyBarPrefab;
}