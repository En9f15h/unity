using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSelectionDefinition", menuName = "Game/Character Selection Definition")]
public class CharacterSelectionDefinition : ScriptableObject
{
    [Header("Stable Identity")]
    public string characterId;
    public CharacterClassConfig combatConfig;
    public int legacyClassIndex = -1;

    [Header("Display")]
    public string displayName;
    public string englishName;
    public string roleName;
    public string shortRole;
    [TextArea] public string description;

    [Header("Preview Assets")]
    public Sprite cardSprite;
    public Sprite portraitSprite;
    public GameObject previewPrefab;

    [Header("Selection Scene Images")]
    public Sprite selectionPanelBackgroundSprite;
    public Sprite characterPreviewBackgroundSprite;
    public Sprite characterInfoBackgroundSprite;
    public Sprite radarBackgroundSprite;

    [Header("Six-Axis UI Ratings")]
    [Range(0, 5)] public int vitality;
    [Range(0, 5)] public int attack;
    [Range(0, 5)] public int defense;
    [Range(0, 5)] public int mobility;
    [Range(0, 5)] public int range;
    [Range(0, 5)] public int prediction;

    [Header("Legacy Six-Axis UI Ratings")]
    [Range(0, 5)] public int vitalityRating;
    [Range(0, 5)] public int attackRating;
    [Range(0, 5)] public int defenseRating;
    [Range(0, 5)] public int mobilityRating;
    [Range(0, 5)] public int rangeRating;
    [Range(0, 5)] public int predictionRating;
    [Range(0, 5)] public int difficultyRating;

    [Header("Skins")]
    public string[] skinNames;
    public Sprite[] skinPortraits;
    public GameObject[] skinPreviewPrefabs;

    [Header("Skill Popup")]
    public SkillDisplayData[] skills;

    public string GetDisplayNameFallback()
    {
        if (!string.IsNullOrEmpty(displayName))
            return displayName;

        if (!string.IsNullOrEmpty(englishName))
            return englishName;

        return characterId;
    }

    public string GetClassLocalizationKey()
    {
        string key = ResolveClassLocalizationKey(characterId);
        if (!string.IsNullOrEmpty(key))
            return key;

        key = ResolveClassLocalizationKey(displayName);
        if (!string.IsNullOrEmpty(key))
            return key;

        return ResolveClassLocalizationKey(englishName);
    }

    public int GetValidSkinIndex(int requestedIndex)
    {
        int count = GetSkinCount();
        if (count <= 0)
            return 0;

        return Mathf.Clamp(requestedIndex, 0, count - 1);
    }

    public int GetSkinCount()
    {
        if (skinNames != null && skinNames.Length > 0)
            return skinNames.Length;

        if (combatConfig != null && combatConfig.skinNames != null && combatConfig.skinNames.Length > 0)
            return combatConfig.skinNames.Length;

        if (combatConfig != null && combatConfig.skinPrefabs != null && combatConfig.skinPrefabs.Length > 0)
            return combatConfig.skinPrefabs.Length;

        return 1;
    }

    public string GetSkinName(int skinIndex)
    {
        int validIndex = GetValidSkinIndex(skinIndex);
        if (skinNames != null && validIndex >= 0 && validIndex < skinNames.Length && !string.IsNullOrEmpty(skinNames[validIndex]))
            return skinNames[validIndex];

        if (combatConfig != null && combatConfig.skinNames != null && validIndex >= 0 && validIndex < combatConfig.skinNames.Length)
            return combatConfig.skinNames[validIndex];

        return "Default";
    }

    public string GetDisplayRole()
    {
        if (!string.IsNullOrEmpty(shortRole))
            return shortRole;

        return roleName;
    }

    public Sprite GetSkinPortrait(int skinIndex)
    {
        int validIndex = GetValidSkinIndex(skinIndex);
        if (skinPortraits != null && validIndex >= 0 && validIndex < skinPortraits.Length && skinPortraits[validIndex] != null)
            return skinPortraits[validIndex];

        return portraitSprite != null ? portraitSprite : cardSprite;
    }

    public GameObject GetSkinPreviewPrefab(int skinIndex)
    {
        int validIndex = GetValidSkinIndex(skinIndex);
        if (skinPreviewPrefabs != null && validIndex >= 0 && validIndex < skinPreviewPrefabs.Length && skinPreviewPrefabs[validIndex] != null)
            return skinPreviewPrefabs[validIndex];

        if (combatConfig != null && combatConfig.skinPrefabs != null && validIndex >= 0 && validIndex < combatConfig.skinPrefabs.Length)
            return combatConfig.skinPrefabs[validIndex];

        return previewPrefab;
    }

    public float[] GetRadarValues()
    {
        return new float[]
        {
            ResolveRating(vitality, vitalityRating),
            ResolveRating(attack, attackRating),
            ResolveRating(defense, defenseRating),
            ResolveRating(mobility, mobilityRating),
            ResolveRating(range, rangeRating),
            ResolveRating(prediction, predictionRating)
        };
    }

    private int ResolveRating(int currentValue, int legacyValue)
    {
        return currentValue > 0 ? currentValue : legacyValue;
    }

    private static string ResolveClassLocalizationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);

        if (string.Equals(normalized, "knight", System.StringComparison.OrdinalIgnoreCase))
            return "Knight";

        if (string.Equals(normalized, "oracle", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "fortuneteller", System.StringComparison.OrdinalIgnoreCase))
            return "Oracle";

        return string.Empty;
    }
}
