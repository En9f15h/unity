using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterClassData", menuName = "Game/Character Class Data")]
public class CharacterClassData : ScriptableObject
{
    public string className;
    public int maxHP = 30;
    public int slotCount = 5;

    [Header("Skill Names")]
    public List<string> skills = new List<string>();

    [Header("Available Skins For This Class")]
    public List<CharacterAppearanceData> appearances = new List<CharacterAppearanceData>();
}
