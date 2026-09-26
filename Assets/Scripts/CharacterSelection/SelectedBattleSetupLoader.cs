using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[DefaultExecutionOrder(-10000)]
public class SelectedBattleSetupLoader : MonoBehaviour
{
    [Header("Selection Data")]
    [SerializeField] private CharacterSelectionDefinition[] characters;
    [SerializeField] private MapSelectionDefinition[] maps;

    [Header("Map Spawn")]
    [SerializeField] private Transform mapRoot;
    [SerializeField] private bool instantiateMapOnAwake = true;

    private readonly Dictionary<string, CharacterSelectionDefinition> characterById = new Dictionary<string, CharacterSelectionDefinition>();
    private readonly Dictionary<string, MapSelectionDefinition> mapById = new Dictionary<string, MapSelectionDefinition>();
    private GameObject spawnedMap;

    private void Awake()
    {
        BuildLookupTables();

        if (!PhotonNetwork.IsConnected || PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogWarning("SelectedBattleSetupLoader: Photon room is missing; using local defaults only.");
            return;
        }

        ResolveAndWriteLegacySelection(PhotonNetwork.LocalPlayer, "Local");

        Player remote = GetRemotePlayer();
        if (remote != null)
            LogResolvedSelection(remote, "Remote");

        if (instantiateMapOnAwake)
            InstantiateSelectedMap();
    }

    public void InstantiateSelectedMap()
    {
        if (spawnedMap != null)
            Destroy(spawnedMap);

        MapSelectionDefinition map = ResolveSelectedMap();
        if (map == null || map.mapPrefab == null)
        {
            Debug.LogWarning("SelectedBattleSetupLoader: selected map prefab is missing; static scene background remains active.");
            return;
        }

        Transform parent = mapRoot != null ? mapRoot : transform;
        spawnedMap = Instantiate(map.mapPrefab, parent);
        spawnedMap.transform.localPosition = Vector3.zero;
        spawnedMap.transform.localRotation = Quaternion.identity;
        spawnedMap.transform.localScale = Vector3.one;

        Debug.Log("SelectedBattleSetupLoader: instantiated map = " + map.mapId);
    }

    private void ResolveAndWriteLegacySelection(Player player, string label)
    {
        if (player == null)
            return;

        CharacterSelectionDefinition definition = ResolvePlayerCharacter(player);
        if (definition == null)
            definition = GetFirstValidCharacter();

        int skinIndex = ResolvePlayerSkinIndex(player, definition);
        int legacyClassIndex = definition != null ? GetLegacyClassIndex(definition) : 0;

        if (player == PhotonNetwork.LocalPlayer)
        {
            Hashtable props = new Hashtable
            {
                { CharacterSelectPhotonKeys.LegacyClassIndex, legacyClassIndex },
                { CharacterSelectPhotonKeys.LegacySkinIndex, skinIndex }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        Debug.Log("SelectedBattleSetupLoader: " + label + " characterId=" + (definition != null ? definition.characterId : "none") +
                  ", classIndex=" + legacyClassIndex + ", skinIndex=" + skinIndex);
    }

    private void LogResolvedSelection(Player player, string label)
    {
        CharacterSelectionDefinition definition = ResolvePlayerCharacter(player);
        if (definition == null)
            definition = GetFirstValidCharacter();

        int skinIndex = ResolvePlayerSkinIndex(player, definition);
        Debug.Log("SelectedBattleSetupLoader: " + label + " characterId=" + (definition != null ? definition.characterId : "none") +
                  ", classIndex=" + (definition != null ? GetLegacyClassIndex(definition) : 0) + ", skinIndex=" + skinIndex);
    }

    private MapSelectionDefinition ResolveSelectedMap()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            TryGetString(PhotonNetwork.CurrentRoom.CustomProperties, CharacterSelectPhotonKeys.SelectedMapId, out string mapId) &&
            mapById.TryGetValue(mapId, out MapSelectionDefinition map) &&
            map != null &&
            map.isUnlocked)
        {
            Debug.Log("SelectedBattleSetupLoader: resolved selectedMapId=" + mapId);
            return map;
        }

        MapSelectionDefinition fallback = GetFirstUnlockedMap();
        if (fallback != null)
            Debug.LogWarning("SelectedBattleSetupLoader: selectedMapId missing or invalid; using " + fallback.mapId);

        return fallback;
    }

    private CharacterSelectionDefinition ResolvePlayerCharacter(Player player)
    {
        if (player == null || player.CustomProperties == null)
            return null;

        if (TryGetString(player.CustomProperties, CharacterSelectPhotonKeys.SelectedCharacterId, out string id) &&
            characterById.TryGetValue(id, out CharacterSelectionDefinition definition))
        {
            return definition;
        }

        return null;
    }

    private int ResolvePlayerSkinIndex(Player player, CharacterSelectionDefinition definition)
    {
        int skinIndex = 0;
        if (player != null && player.CustomProperties != null)
            TryGetInt(player.CustomProperties, CharacterSelectPhotonKeys.SelectedSkinIndex, out skinIndex);

        return definition != null ? definition.GetValidSkinIndex(skinIndex) : Mathf.Max(0, skinIndex);
    }

    private Player GetRemotePlayer()
    {
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i] != PhotonNetwork.LocalPlayer)
                return players[i];
        }

        return null;
    }

    private void BuildLookupTables()
    {
        characterById.Clear();
        if (characters != null)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterSelectionDefinition definition = characters[i];
                if (definition == null || string.IsNullOrEmpty(definition.characterId))
                    continue;

                characterById[definition.characterId] = definition;
                if (definition.legacyClassIndex < 0)
                    definition.legacyClassIndex = i;
            }
        }

        mapById.Clear();
        if (maps != null)
        {
            for (int i = 0; i < maps.Length; i++)
            {
                MapSelectionDefinition definition = maps[i];
                if (definition == null || string.IsNullOrEmpty(definition.mapId))
                    continue;

                mapById[definition.mapId] = definition;
            }
        }
    }

    private CharacterSelectionDefinition GetFirstValidCharacter()
    {
        if (characters == null)
            return null;

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null && !string.IsNullOrEmpty(characters[i].characterId))
                return characters[i];
        }

        return null;
    }

    private MapSelectionDefinition GetFirstUnlockedMap()
    {
        if (maps == null)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].isUnlocked && !string.IsNullOrEmpty(maps[i].mapId))
                return maps[i];
        }

        return null;
    }

    private int GetLegacyClassIndex(CharacterSelectionDefinition definition)
    {
        if (definition == null)
            return 0;

        if (definition.legacyClassIndex >= 0)
            return definition.legacyClassIndex;

        if (characters != null)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] == definition)
                    return i;
            }
        }

        return 0;
    }

    private bool TryGetString(Hashtable table, string key, out string value)
    {
        value = string.Empty;
        if (table == null || !table.TryGetValue(key, out object raw) || raw == null)
            return false;

        value = raw as string;
        return !string.IsNullOrEmpty(value);
    }

    private bool TryGetInt(Hashtable table, string key, out int value)
    {
        value = 0;
        if (table == null || !table.TryGetValue(key, out object raw) || raw == null)
            return false;

        try
        {
            value = Convert.ToInt32(raw);
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning("SelectedBattleSetupLoader: invalid int property for key " + key);
            return false;
        }
    }
}
