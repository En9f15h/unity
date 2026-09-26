using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapSelectionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image previewImage;
    [SerializeField] private TMP_Text mapNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private GameObject selectedMarker;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button button;
    [SerializeField] private Animator animator;
    [SerializeField] private Color normalColor = new Color(0.12f, 0.13f, 0.18f, 0.95f);
    [SerializeField] private Color hoverColor = new Color(0.18f, 0.23f, 0.30f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.30f, 0.25f, 0.12f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.06f, 0.06f, 0.07f, 0.95f);

    private MapSelectionController controller;
    private MapSelectionDefinition definition;
    private bool interactable;
    private bool selected;
    private bool buttonWired;

    public MapSelectionDefinition Definition => definition;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Initialize(MapSelectionDefinition mapDefinition, MapSelectionController owner)
    {
        ResolveReferences();
        definition = mapDefinition;
        controller = owner;
        ApplyDefinition();
        RefreshVisual();
    }

    public void SetSelected(bool value)
    {
        bool changedToSelected = value && !selected;
        selected = value;
        RefreshVisual();

        if (changedToSelected)
            PlayTrigger("MapCardSelected");
    }

    public void SetInteractable(bool value)
    {
        interactable = value && definition != null && definition.isUnlocked;
        if (button != null)
            button.interactable = interactable;

        RefreshVisual();
    }

    private void OnDestroy()
    {
        if (button != null && buttonWired)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (!interactable || controller == null || definition == null)
            return;

        controller.SelectMapFromCard(definition);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage != null && interactable && !selected)
            backgroundImage.color = hoverColor;

        if (interactable)
            PlayTrigger("MapCardHoverIn");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RefreshVisual();

        if (interactable)
            PlayTrigger("MapCardHoverOut");
    }

    private void ApplyDefinition()
    {
        if (definition == null)
            return;

        if (previewImage != null)
        {
            previewImage.sprite = definition.previewSprite;
            StageAtmosphereController.ForImage(previewImage);
        }

        if (mapNameText != null)
            mapNameText.text = definition.displayName;

        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
            descriptionText.gameObject.SetActive(false);
        }
    }

    private void RefreshVisual()
    {
        if (selectedMarker != null)
            selectedMarker.SetActive(selected);

        bool locked = definition != null && !definition.isUnlocked;
        if (lockedOverlay != null)
            lockedOverlay.SetActive(locked);

        if (backgroundImage != null)
            backgroundImage.color = locked ? lockedColor : selected ? selectedColor : normalColor;
    }

    private void ResolveReferences()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            button = gameObject.AddComponent<Button>();

        button.targetGraphic = backgroundImage;
        button.transition = Selectable.Transition.None;
        button.interactable = interactable;

        if (!buttonWired)
        {
            button.onClick.AddListener(HandleClick);
            buttonWired = true;
        }

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName) || !HasTrigger(triggerName))
            return;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    private bool HasTrigger(string triggerName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                return true;
        }

        return false;
    }
}
