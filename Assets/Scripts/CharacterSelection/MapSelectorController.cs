using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSelectorController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private List<MapSelectionDefinition> maps = new List<MapSelectionDefinition>();

    [Header("UI")]
    [SerializeField] private Button previousMapButton;
    [SerializeField] private Button nextMapButton;
    [SerializeField] private Image mapPreviewImage;
    [SerializeField] private TMP_Text currentMapNameText;
    [SerializeField] private GameObject hostOnlyLockImage;

    [Header("Optional Animation")]
    [SerializeField] private CharacterSelectionAnimationController animationController;

    private readonly Dictionary<string, MapSelectionDefinition> mapById = new Dictionary<string, MapSelectionDefinition>();
    private int currentMapIndex;
    private bool interactable;
    private bool buttonsWired;
    private bool suppressEvent;

    public event Action<MapSelectionDefinition> MapSelected;

    public void SetMaps(MapSelectionDefinition[] definitions)
    {
        maps.Clear();
        mapById.Clear();
        if (definitions != null)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null)
                    maps.Add(definitions[i]);
            }
        }

        BuildLookup();
        currentMapIndex = ClampIndex(currentMapIndex);
        RefreshCurrentMap();
    }

    public void SelectPreviousMap()
    {
        if (!interactable || maps.Count == 0)
            return;

        SelectMapAt(FindNextUnlockedIndex(currentMapIndex, -1), true, "MapPrevious");
    }

    public void SelectNextMap()
    {
        if (!interactable || maps.Count == 0)
            return;

        SelectMapAt(FindNextUnlockedIndex(currentMapIndex, 1), true, "MapNext");
    }

    public void SetSelectedMapId(string mapId)
    {
        SetSelectionWithoutNotify(mapId);
    }

    public void SetSelectionWithoutNotify(string mapId)
    {
        suppressEvent = true;

        if (!string.IsNullOrEmpty(mapId) && mapById.TryGetValue(mapId, out MapSelectionDefinition definition))
            currentMapIndex = maps.IndexOf(definition);
        else
            currentMapIndex = FindNextUnlockedIndex(-1, 1);

        RefreshCurrentMap();
        suppressEvent = false;
    }

    public string GetCurrentMapId()
    {
        MapSelectionDefinition map = GetCurrentMap();
        return map != null ? map.mapId : string.Empty;
    }

    public int GetCurrentMapIndex()
    {
        return currentMapIndex;
    }

    public MapSelectionDefinition GetCurrentMap()
    {
        if (maps.Count == 0)
            return null;

        int index = ClampIndex(currentMapIndex);
        return index >= 0 ? maps[index] : null;
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        RefreshButtonState();
    }

    private void Awake()
    {
        ResolveReferences();
        BuildLookup();
        WireButtons();
        RefreshCurrentMap();
    }

    private void OnDestroy()
    {
        if (!buttonsWired)
            return;

        if (previousMapButton != null)
            previousMapButton.onClick.RemoveListener(SelectPreviousMap);
        if (nextMapButton != null)
            nextMapButton.onClick.RemoveListener(SelectNextMap);
    }

    private void ResolveReferences()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterSelectionAnimationController>();
    }

    private void WireButtons()
    {
        if (buttonsWired)
            return;

        if (previousMapButton != null)
            previousMapButton.onClick.AddListener(SelectPreviousMap);
        if (nextMapButton != null)
            nextMapButton.onClick.AddListener(SelectNextMap);

        buttonsWired = true;
    }

    private void BuildLookup()
    {
        mapById.Clear();
        List<MapSelectionDefinition> validMaps = new List<MapSelectionDefinition>();
        for (int i = 0; i < maps.Count; i++)
            AddMapIfValid(maps[i], validMaps);

        maps = validMaps;
    }

    private void AddMapIfValid(MapSelectionDefinition definition, List<MapSelectionDefinition> destination)
    {
        if (definition == null)
            return;

        if (string.IsNullOrEmpty(definition.mapId))
        {
            Debug.LogWarning("MapSelectorController: skipped a map with an empty mapId.", definition);
            return;
        }

        if (mapById.ContainsKey(definition.mapId))
        {
            Debug.LogWarning("MapSelectorController: skipped duplicate mapId " + definition.mapId + ".", definition);
            return;
        }

        mapById[definition.mapId] = definition;
        destination.Add(definition);
    }

    private void SelectMapAt(int index, bool notify, string triggerName)
    {
        if (maps.Count == 0)
        {
            currentMapIndex = 0;
            RefreshCurrentMap();
            return;
        }

        currentMapIndex = ClampIndex(index);
        RefreshCurrentMap();

        if (!string.IsNullOrEmpty(triggerName))
            animationController?.PlayTrigger(triggerName);

        if (notify && !suppressEvent)
            MapSelected?.Invoke(GetCurrentMap());
    }

    private void RefreshCurrentMap()
    {
        MapSelectionDefinition map = GetCurrentMap();

        if (mapPreviewImage != null)
        {
            mapPreviewImage.sprite = map != null ? map.previewSprite : null;
            mapPreviewImage.enabled = map != null && map.previewSprite != null;
            StageAtmosphereController.ForImage(mapPreviewImage);
        }

        if (currentMapNameText != null)
            currentMapNameText.text = map != null ? map.displayName : string.Empty;

        RefreshButtonState();
    }

    private void RefreshButtonState()
    {
        bool canStep = interactable && CountUnlockedMaps() > 1;
        SetButton(previousMapButton, canStep);
        SetButton(nextMapButton, canStep);

        if (hostOnlyLockImage != null)
            hostOnlyLockImage.SetActive(false);
    }

    private int FindNextUnlockedIndex(int startIndex, int direction)
    {
        if (maps.Count == 0)
            return 0;

        int step = direction < 0 ? -1 : 1;
        int index = startIndex;
        for (int i = 0; i < maps.Count; i++)
        {
            index += step;
            if (index < 0)
                index = maps.Count - 1;
            if (index >= maps.Count)
                index = 0;

            if (maps[index] != null && maps[index].isUnlocked)
                return index;
        }

        return ClampIndex(startIndex);
    }

    private int CountUnlockedMaps()
    {
        int count = 0;
        for (int i = 0; i < maps.Count; i++)
        {
            if (maps[i] != null && maps[i].isUnlocked)
                count++;
        }

        return count;
    }

    private void SetButton(Button button, bool enabled)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(enabled);
        button.interactable = enabled;
    }

    private int ClampIndex(int index)
    {
        if (maps.Count == 0)
            return 0;

        return Mathf.Clamp(index, 0, maps.Count - 1);
    }
}
