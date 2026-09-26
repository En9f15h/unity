using UnityEngine;
using UnityEngine.UI;   
using TMPro;
using UnityEngine.EventSystems;

public class ShowInfo : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Image putImage;
    [SerializeField] private TextMeshProUGUI putText;
    [SerializeField] private Sprite infoImage;
    [SerializeField, TextArea] public string infoText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        putImage.sprite = infoImage;
        putText.text = infoText;
    }
    public void SetInfoText(string newText)
    {
        infoText = newText;
    }
}
