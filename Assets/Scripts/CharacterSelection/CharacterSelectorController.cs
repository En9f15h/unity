using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectorController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private List<CharacterSelectionDefinition> characters = new List<CharacterSelectionDefinition>();

    [Header("Target UI")]
    [SerializeField] private CharacterSelectionPlayerPanel targetPanel;
    [SerializeField] private CharacterSkillPopup skillPopup;

    [Header("Buttons")]
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private Button previousSkinButton;
    [SerializeField] private Button nextSkinButton;
    [SerializeField] private Button skillButton;

    [Header("Optional Animation")]
    [SerializeField] private CharacterSelectionAnimationController animationController;

    private readonly Dictionary<string, CharacterSelectionDefinition> characterById = new Dictionary<string, CharacterSelectionDefinition>();
    private int currentCharacterIndex;
    private int currentSkinIndex;
    private bool interactable = true;
    private bool buttonsWired;
    private bool suppressEvent;

    public event Action<CharacterSelectionDefinition, int> SelectionChanged;
    public event Action<CharacterSelectionDefinition, int> PreviewChanged;

    public void SetCharacters(CharacterSelectionDefinition[] definitions)
    {
        characters.Clear();
        characterById.Clear();
        if (definitions != null)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null)
                    characters.Add(definitions[i]);
            }
        }

        BuildLookup();
        currentCharacterIndex = ClampCharacterIndex(currentCharacterIndex);
        currentSkinIndex = GetCurrentDefinition() != null ? GetCurrentDefinition().GetValidSkinIndex(currentSkinIndex) : 0;
        RefreshCurrentCharacter();
    }

    public void SelectPreviousCharacter()
    {
        if (!interactable || characters.Count == 0)
            return;

        SelectCharacterAt(WrapIndex(currentCharacterIndex - 1), true, true, "CharacterPrevious");
    }

    public void SelectNextCharacter()
    {
        if (!interactable || characters.Count == 0)
            return;

        SelectCharacterAt(WrapIndex(currentCharacterIndex + 1), true, true, "CharacterNext");
    }

    public void SetCharacterById(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || !characterById.TryGetValue(characterId, out CharacterSelectionDefinition definition))
        {
            SelectCharacterAt(GetFirstValidIndex(), true, !suppressEvent, string.Empty);
            return;
        }

        int index = characters.IndexOf(definition);
        SelectCharacterAt(index, true, !suppressEvent, string.Empty);
    }

    public void SetSelectionWithoutNotify(string characterId, int skinIndex)
    {
        suppressEvent = true;
        SetCharacterById(characterId);
        SetCurrentSkinIndex(skinIndex);
        suppressEvent = false;
        RefreshCurrentCharacter();
    }

    public void RefreshCurrentCharacter()
    {
        CharacterSelectionDefinition definition = GetCurrentDefinition();
        if (definition != null)
        {
            currentSkinIndex = definition.GetValidSkinIndex(currentSkinIndex);
            targetPanel?.SetCharacter(definition, currentSkinIndex);
            skillPopup?.SetAccessible(true);
            skillPopup?.BindSkills(definition.skills);
        }
        else
        {
            currentSkinIndex = 0;
            targetPanel?.ClearCharacter();
            skillPopup?.BindSkills(null);
        }

        RefreshButtonState();
        PreviewChanged?.Invoke(definition, currentSkinIndex);
    }

    public string GetCurrentCharacterId()
    {
        CharacterSelectionDefinition definition = GetCurrentDefinition();
        return definition != null ? definition.characterId : string.Empty;
    }

    public int GetCurrentCharacterIndex()
    {
        return currentCharacterIndex;
    }

    public CharacterSelectionDefinition GetCurrentDefinition()
    {
        if (characters.Count == 0)
            return null;

        int index = ClampCharacterIndex(currentCharacterIndex);
        return index >= 0 ? characters[index] : null;
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        RefreshButtonState();
    }

    public void SelectPreviousSkin()
    {
        if (!interactable)
            return;

        ChangeSkin(-1);
    }

    public void SelectNextSkin()
    {
        if (!interactable)
            return;

        ChangeSkin(1);
    }

    public int GetCurrentSkinIndex()
    {
        return currentSkinIndex;
    }

    public void SetCurrentSkinIndex(int skinIndex)
    {
        CharacterSelectionDefinition definition = GetCurrentDefinition();
        currentSkinIndex = definition != null ? definition.GetValidSkinIndex(skinIndex) : Mathf.Max(0, skinIndex);
        RefreshCurrentCharacter();

        if (!suppressEvent)
            SelectionChanged?.Invoke(definition, currentSkinIndex);
    }

    private void Awake()
    {
        ResolveReferences();
        BuildLookup();
        WireButtons();
        RefreshCurrentCharacter();
    }

    private void OnDestroy()
    {
        if (!buttonsWired)
            return;

        if (previousCharacterButton != null)
            previousCharacterButton.onClick.RemoveListener(SelectPreviousCharacter);
        if (nextCharacterButton != null)
            nextCharacterButton.onClick.RemoveListener(SelectNextCharacter);
        if (previousSkinButton != null)
            previousSkinButton.onClick.RemoveListener(SelectPreviousSkin);
        if (nextSkinButton != null)
            nextSkinButton.onClick.RemoveListener(SelectNextSkin);
    }

    private void ResolveReferences()
    {
        if (targetPanel == null)
            targetPanel = GetComponentInParent<CharacterSelectionPlayerPanel>();

        if (animationController == null)
        {
            CharacterSelectionAnimationController candidate = GetComponent<CharacterSelectionAnimationController>();
            if (candidate != null && IsSafeAnimationTarget(candidate))
                animationController = candidate;
        }
    }

    private void WireButtons()
    {
        if (buttonsWired)
            return;

        if (previousCharacterButton != null)
            previousCharacterButton.onClick.AddListener(SelectPreviousCharacter);
        if (nextCharacterButton != null)
            nextCharacterButton.onClick.AddListener(SelectNextCharacter);
        if (previousSkinButton != null)
            previousSkinButton.onClick.AddListener(SelectPreviousSkin);
        if (nextSkinButton != null)
            nextSkinButton.onClick.AddListener(SelectNextSkin);

        buttonsWired = true;
    }

    private void BuildLookup()
    {
        characterById.Clear();

        List<CharacterSelectionDefinition> validCharacters = new List<CharacterSelectionDefinition>();
        for (int i = 0; i < characters.Count; i++)
            AddDefinitionIfValid(characters[i], validCharacters);

        characters = validCharacters;
    }

    private void AddDefinitionIfValid(CharacterSelectionDefinition definition, List<CharacterSelectionDefinition> destination)
    {
        if (definition == null)
            return;

        if (string.IsNullOrEmpty(definition.characterId))
        {
            Debug.LogWarning("CharacterSelectorController: skipped a character with an empty characterId.", definition);
            return;
        }

        if (characterById.ContainsKey(definition.characterId))
        {
            Debug.LogWarning("CharacterSelectorController: skipped duplicate characterId " + definition.characterId + ".", definition);
            return;
        }

        characterById[definition.characterId] = definition;
        destination.Add(definition);
    }

    private void SelectCharacterAt(int index, bool resetSkin, bool notify, string triggerName)
    {
        if (characters.Count == 0)
        {
            currentCharacterIndex = 0;
            currentSkinIndex = 0;
            RefreshCurrentCharacter();
            return;
        }

        currentCharacterIndex = ClampCharacterIndex(index);
        if (resetSkin)
            currentSkinIndex = 0;

        RefreshCurrentCharacter();
        if (!string.IsNullOrEmpty(triggerName) && IsSafeAnimationTarget(animationController))
            animationController?.PlayTrigger(triggerName);

        if (notify && !suppressEvent)
            SelectionChanged?.Invoke(GetCurrentDefinition(), currentSkinIndex);
    }

    private void ChangeSkin(int delta)
    {
        CharacterSelectionDefinition definition = GetCurrentDefinition();
        if (definition == null)
            return;

        int count = definition.GetSkinCount();
        if (count <= 1)
            return;

        currentSkinIndex += delta;
        if (currentSkinIndex < 0)
            currentSkinIndex = count - 1;
        if (currentSkinIndex >= count)
            currentSkinIndex = 0;

        RefreshCurrentCharacter();

        if (!suppressEvent)
            SelectionChanged?.Invoke(definition, currentSkinIndex);
    }

    private void RefreshButtonState()
    {
        CharacterSelectionDefinition definition = GetCurrentDefinition();
        bool hasCharacters = characters.Count > 0;
        bool hasMultipleCharacters = characters.Count > 1;
        bool hasMultipleSkins = definition != null && definition.GetSkinCount() > 1;
        bool hasSkills = definition != null && definition.skills != null && definition.skills.Length > 0;

        SetButton(previousCharacterButton, interactable && hasMultipleCharacters);
        SetButton(nextCharacterButton, interactable && hasMultipleCharacters);
        SetButton(previousSkinButton, interactable && hasMultipleSkins);
        SetButton(nextSkinButton, interactable && hasMultipleSkins);
        SetButton(skillButton, hasCharacters && hasSkills);
    }

    private void SetButton(Button button, bool enabled)
    {
        if (button != null)
            button.interactable = enabled;
    }

    private int WrapIndex(int index)
    {
        if (characters.Count == 0)
            return 0;

        if (index < 0)
            return characters.Count - 1;
        if (index >= characters.Count)
            return 0;

        return index;
    }

    private int ClampCharacterIndex(int index)
    {
        if (characters.Count == 0)
            return 0;

        return Mathf.Clamp(index, 0, characters.Count - 1);
    }

    private int GetFirstValidIndex()
    {
        return characters.Count > 0 ? 0 : 0;
    }

    private bool IsSafeAnimationTarget(CharacterSelectionAnimationController candidate)
    {
        if (candidate == null)
            return false;

        // Selection feedback clips animate RectTransform.anchoredPosition.x.
        // Do not play them on the player panel root or it will override the layout controller.
        return targetPanel == null || candidate.transform != targetPanel.transform;
    }
}
