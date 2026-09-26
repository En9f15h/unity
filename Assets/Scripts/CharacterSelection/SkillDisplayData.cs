using System;
using UnityEngine;

[Serializable]
public class SkillDisplayData
{
    public string skillName;
    [TextArea] public string description;
    public Sprite icon;
    public int damage;
    public string range;
    public int actionSlotCost = 1;
    public int energyCost;
    [TextArea] public string counterDescription;
}
