using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterClassData", menuName = "Game/Character Class Data")]
public class CharacterClassData : ScriptableObject
{
    public string className;
    public int maxHP = 30;
    public int slotCount = 5;

    [Header("技能名稱")]
    public List<string> skills = new List<string>();

    [Header("此職業可用造型")]
    public List<CharacterAppearanceData> appearances = new List<CharacterAppearanceData>();
}