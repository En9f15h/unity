using UnityEngine;

[CreateAssetMenu(fileName = "MapSelectionDefinition", menuName = "Game/Map Selection Definition")]
public class MapSelectionDefinition : ScriptableObject
{
    public string mapId;
    public string displayName;
    [TextArea] public string description;
    public Sprite previewSprite;
    public GameObject mapPrefab;
    public bool isUnlocked = true;

    [Header("Existing Stage Compatibility")]
    public int legacyStageIndex = -1;
}
