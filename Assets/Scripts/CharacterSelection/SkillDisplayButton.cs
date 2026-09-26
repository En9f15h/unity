using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillDisplayButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    private CharacterSkillPopup popup;
    private SkillDisplayData data;

    public void Initialize(SkillDisplayData skillData, CharacterSkillPopup owner)
    {
        data = skillData;
        popup = owner;

        if (iconImage != null)
            iconImage.sprite = data != null ? data.icon : null;

        if (nameText != null)
        {
            nameText.text = string.Empty;
            nameText.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (popup != null && data != null)
            popup.ShowSkill(data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (popup != null && data != null)
            popup.ShowSkill(data);
    }
}
