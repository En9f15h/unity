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
}
