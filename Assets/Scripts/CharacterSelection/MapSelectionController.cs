using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MapSelectionController : MonoBehaviour
{
    [SerializeField] private Transform cardRoot;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private TMP_Text selectedMapText;
    [SerializeField] private TMP_Text permissionText;

    private readonly List<MapSelectionCard> cards = new List<MapSelectionCard>();
    private readonly Dictionary<string, MapSelectionDefinition> mapById = new Dictionary<string, MapSelectionDefinition>();
    private MapSelectionDefinition[] maps;
    private string selectedMapId;
    private bool cardsInteractable;

    public event Action<MapSelectionDefinition> MapSelected;

    public void SetMaps(MapSelectionDefinition[] definitions)
    {
        maps = definitions;
        mapById.Clear();

        if (maps != null)
        {
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] == null || string.IsNullOrEmpty(maps[i].mapId))
                    continue;

                mapById[maps[i].mapId] = maps[i];
            }
        }

        RebuildCards();
    }

    public void SelectMapFromCard(MapSelectionDefinition definition)
    {
        if (!cardsInteractable || definition == null || !definition.isUnlocked)
            return;

        MapSelected?.Invoke(definition);
    }

    public void SetSelectedMapId(string mapId)
    {
        selectedMapId = mapId;
        MapSelectionDefinition definition = ResolveMap(mapId);
        if (selectedMapText != null)
            selectedMapText.text = definition != null ? definition.displayName : "No map";

        RefreshCards();
    }

    public void SetCardsInteractable(bool interactable, bool isMasterClient)
    {
        cardsInteractable = interactable;

        if (cardRoot != null)
            cardRoot.gameObject.SetActive(interactable && isMasterClient);

        if (permissionText != null)
            permissionText.text = interactable ? "SELECT" : string.Empty;

        RefreshCards();
    }

    public MapSelectionDefinition ResolveMap(string mapId)
    {
        if (!string.IsNullOrEmpty(mapId) && mapById.TryGetValue(mapId, out MapSelectionDefinition definition))
            return definition;

        return null;
    }

    private void RebuildCards()
    {
        if (cardRoot == null)
            cardRoot = transform;

        for (int i = cards.Count - 1; i >= 0; i--)
        {
            if (cards[i] != null)
                Destroy(cards[i].gameObject);
        }

        cards.Clear();

        if (cardPrefab == null || maps == null)
            return;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null)
                continue;

            GameObject cardObject = Instantiate(cardPrefab, cardRoot);
            MapSelectionCard card = cardObject.GetComponent<MapSelectionCard>();
            if (card == null)
            {
                Debug.LogError("MapSelectionController: map card prefab is missing MapSelectionCard.", cardObject);
                Destroy(cardObject);
                continue;
            }

            card.Initialize(maps[i], this);
            cards.Add(card);
        }

        RefreshCards();
    }

    private void RefreshCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[i].Definition == null)
                continue;

            cards[i].SetSelected(cards[i].Definition.mapId == selectedMapId);
            cards[i].SetInteractable(cardsInteractable);
        }
    }
}
